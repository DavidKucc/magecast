using System.Collections;
using System.Collections.Generic;
using MageCast.Combat;
using MageCast.Gestures;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// The network side of one player. Who decides what:
    ///
    ///   movement, aim, drawing       the player themselves -- anything else is lag on every input
    ///   whether a spell hit, damage  the server, once -- so a hit counts exactly once
    ///   being pushed, slowed, thrown the player's own machine, told by the server -- it owns the body
    ///
    /// Everybody else's copy of a player is a puppet: its motor and caster are switched off, it is
    /// moved by the network, and this class plays out what the real one did -- the glyph forming over
    /// its head, the spell leaving its hands.
    /// </summary>
    public class PlayerNet : NetworkBehaviour
    {
        /// <summary>This machine's own player, once spawned.</summary>
        public static PlayerNet Local { get; private set; }

        /// <summary>Every player currently in the game, on this machine.</summary>
        public static readonly List<PlayerNet> All = new List<PlayerNet>();

        public readonly NetworkVariable<bool> Drawing = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public readonly NetworkVariable<bool> Grounded = new NetworkVariable<bool>(true,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>A spell in hand -- the puppet walks instead of jogging, like its owner.</summary>
        public readonly NetworkVariable<bool> Holding = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Pulling up onto a ledge, for everyone else's animator.</summary>
        public readonly NetworkVariable<bool> Climbing = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>What the player is looking at, so a puppet's head turns the same way.</summary>
        public readonly NetworkVariable<Vector3> AimPoint = new NetworkVariable<Vector3>(Vector3.zero,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public readonly NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public readonly NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Deaths = new NetworkVariable<int>(0);

        [SerializeField] float respawnSeconds = 3f;

        /// <summary>How often the forming glyph is sent, in seconds. 20 a second reads as live.</summary>
        const float StrokeSendInterval = 0.05f;

        GestureCaster caster;
        PlayerMotor motor;
        Health health;
        CharacterController body;
        OwnerNetworkTransform netTransform;
        GlyphDisplay glyph;

        readonly List<Vector2> pendingPoints = new List<Vector2>();
        float nextStrokeSend;

        /// <summary>Set when this copy is somebody else's player, played back from the network.</summary>
        public bool IsRemote { get { return IsSpawned && !IsOwner; } }

        public string DisplayName
        {
            get
            {
                string n = PlayerName.Value.ToString();
                return string.IsNullOrEmpty(n) ? "Player " + (OwnerClientId + 1) : n;
            }
        }

        /// <summary>When this machine last saw the player go down -- for the respawn countdown.</summary>
        public float DiedAt { get; private set; }
        public float RespawnSeconds { get { return respawnSeconds; } }

        void Awake()
        {
            caster = GetComponent<GestureCaster>();
            motor = GetComponent<PlayerMotor>();
            health = GetComponent<Health>();
            body = GetComponent<CharacterController>();
            netTransform = GetComponent<OwnerNetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);

            if (IsOwner)
            {
                Local = this;
                PlayerName.Value = new FixedString32Bytes(NetSession.SafeName(NetSession.PlayerName));

                // Netcode creates the body and only then puts it at the spawn point. A CharacterController
                // keeps its own idea of where it is, and its first Move put the guest back at the origin --
                // inside the central massif, from where it fell out of the world. Measured on the first
                // online test: the host spawned the guest at SpawnB_1, the guest's copy stood at y -13.8.
                if (motor != null) motor.TeleportTo(transform.position, transform.eulerAngles.y);

                ThirdPersonCamera cam = FindObjectOfType<ThirdPersonCamera>();
                if (cam != null) cam.SetTarget(transform, transform.eulerAngles.y);
            }
            else
            {
                // A puppet. Its own input must never run -- two copies of GestureCaster reading one
                // mouse would draw the same glyph twice, and a second motor would fight the network
                // for the position.
                SetEnabled<PlayerMotor>(false);
                SetEnabled<GestureCaster>(false);
                SetEnabled<GestureLegend>(false);
                SetEnabled<AimSurfaceMarker>(false);
                SetEnabled<PlayerHud>(false);

                // somebody else's health is read off the bar over their head, your own off the HUD
                if (GetComponent<HealthBar>() == null) gameObject.AddComponent<HealthBar>();
                glyph = gameObject.AddComponent<GlyphDisplay>();
            }

            health.Changed += OnHealthChanged;
            health.Died += OnDied;
            ApplyAlive(!health.IsDead);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            health.Changed -= OnHealthChanged;
            health.Died -= OnDied;

            if (Local == this)
            {
                Local = null;
                GameInput.LocalDead = false;
                ThirdPersonCamera cam = FindObjectOfType<ThirdPersonCamera>();
                if (cam != null) cam.SetTarget(null, 0f);
            }
        }

        void SetEnabled<T>(bool on) where T : Behaviour
        {
            T c = GetComponent<T>();
            if (c != null) c.enabled = on;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            // NetworkVariables only send when the value actually changes, so writing every frame is free
            if (motor != null) Grounded.Value = motor.IsGrounded;
            if (motor != null) Climbing.Value = motor.IsClimbing;
            if (caster != null) Holding.Value = caster.HeldSpell != null;

            if (pendingPoints.Count > 0 && Time.time >= nextStrokeSend) FlushStroke();
        }

        // ---------------------------------------------------------------- the forming glyph

        /// <summary>Owner: a draw has started.</summary>
        public void OwnerStrokeBegin()
        {
            if (!IsSpawned) return;
            Drawing.Value = true;
            pendingPoints.Clear();
            if (IsServer) StrokeBeginClientRpc(); else StrokeBeginServerRpc();
        }

        /// <summary>
        /// Owner: one more point of the stroke, in units of screen height from the screen centre -- the
        /// drawing cursor starts in the centre, so the glyph comes out the same whatever the resolution.
        /// </summary>
        public void OwnerStrokePoint(Vector2 point)
        {
            if (!IsSpawned) return;
            pendingPoints.Add(point);
        }

        /// <summary>Owner: the draw is over, one way or another.</summary>
        public void OwnerStrokeEnd(StrokeOutcome outcome, byte spell, byte tier = 0)
        {
            if (!IsSpawned) return;
            if (pendingPoints.Count > 0) FlushStroke();
            Drawing.Value = false;
            if (IsServer) StrokeEndClientRpc(outcome, spell, tier); else StrokeEndServerRpc(outcome, spell, tier);
        }

        void FlushStroke()
        {
            Vector2[] batch = pendingPoints.ToArray();
            pendingPoints.Clear();
            nextStrokeSend = Time.time + StrokeSendInterval;
            if (IsServer) StrokePointsClientRpc(batch); else StrokePointsServerRpc(batch);
        }

        [ServerRpc]
        void StrokeBeginServerRpc() { StrokeBeginClientRpc(); }

        [ServerRpc]
        void StrokePointsServerRpc(Vector2[] points) { StrokePointsClientRpc(points); }

        [ServerRpc]
        void StrokeEndServerRpc(StrokeOutcome outcome, byte spell, byte tier) { StrokeEndClientRpc(outcome, spell, tier); }

        [ClientRpc]
        void StrokeBeginClientRpc()
        {
            if (IsOwner || glyph == null) return;
            glyph.Begin();
        }

        [ClientRpc]
        void StrokePointsClientRpc(Vector2[] points)
        {
            if (IsOwner || glyph == null) return;
            glyph.Add(points);
        }

        [ClientRpc]
        void StrokeEndClientRpc(StrokeOutcome outcome, byte spell, byte tier)
        {
            if (IsOwner) return;
            if (glyph != null) glyph.End(outcome, caster.SpellColour(spell));

            // the outcomes that produce no spell still deserve a word -- a fizzle is the moment to push
            switch (outcome)
            {
                case StrokeOutcome.Fizzle: WorldPopups.Word(transform, "FIZZLE", GestureCaster.FailColour); break;
                case StrokeOutcome.Interrupted: WorldPopups.Word(transform, "INTERRUPTED", GestureCaster.FailColour); break;
                case StrokeOutcome.Lost: WorldPopups.Word(transform, "DROPPED", GestureCaster.FailColour); break;
                // the spell exists from here on -- its name and tier go up now, not when it is sent (and
                // again if a tier III falls to II in the hand)
                case StrokeOutcome.Held: caster.AnnounceCreated(spell, tier); break;
                case StrokeOutcome.Sent: if (spell == GestureCaster.MisfireId) caster.AnnounceCreated(spell, 1); break;
            }
        }

        // ---------------------------------------------------------------- casting

        /// <summary>
        /// Owner: a spell leaves the hand. It appears at once on this machine, so the caster never feels
        /// the network; the server makes its own copy, which is the one that actually hits anybody, and
        /// everyone else gets a copy to watch.
        /// </summary>
        public void RequestCast(CastData data)
        {
            caster.SpawnCast(data, IsServer);
            if (IsServer) CastClientRpc(data); else CastServerRpc(data);
        }

        [ServerRpc]
        void CastServerRpc(CastData data)
        {
            caster.SpawnCast(data, true);
            CastClientRpc(data);
        }

        [ClientRpc]
        void CastClientRpc(CastData data)
        {
            // the owner already made theirs, and the server's is the real one
            if (IsOwner || IsServer) return;
            caster.SpawnCast(data, false);
        }

        /// <summary>Server: a patch landed. Everyone else gets one to see and to be slid or thrown by.</summary>
        public void BroadcastZone(Vector3 at, GroundEffect effect, float radius, float lifetime, float strength,
                                  Color colour)
        {
            if (!IsServer) return;
            ZoneClientRpc(at, effect, radius, lifetime, strength, colour);
        }

        [ClientRpc]
        void ZoneClientRpc(Vector3 at, GroundEffect effect, float radius, float lifetime, float strength,
                           Color colour)
        {
            if (IsServer) return;
            // owned by this player on every machine, so the two-patch limit takes away the same one everywhere
            SpellZone.Spawn(at, effect, radius, lifetime, strength, colour, false, Health.NoAttacker, OwnerClientId);
        }

        /// <summary>
        /// Server: a patch turned into another -- fire and ice into water, a fire fanned by air. Everyone
        /// takes their own copy of the old one away and puts down the new one, owned by the same player.
        /// </summary>
        public void BroadcastReplace(Vector3 oldCentre, GroundEffect oldKind, Vector3 at, GroundEffect kind,
                                     float radius, float lifetime, float strength, Color colour, ulong zoneOwner,
                                     bool fanned)
        {
            if (!IsServer) return;
            ReplaceClientRpc(oldCentre, oldKind, at, kind, radius, lifetime, strength, colour, zoneOwner, fanned);
        }

        [ClientRpc]
        void ReplaceClientRpc(Vector3 oldCentre, GroundEffect oldKind, Vector3 at, GroundEffect kind,
                              float radius, float lifetime, float strength, Color colour, ulong zoneOwner, bool fanned)
        {
            if (IsServer) return;
            SpellZone old = SpellZone.Find(oldCentre, oldKind);
            if (old != null) old.Remove();
            if (kind == GroundEffect.None) return;
            SpellZone z = SpellZone.Spawn(at, kind, radius, lifetime, strength, colour, false, Health.NoAttacker, zoneOwner);
            if (fanned) z.MarkFanned();
        }

        /// <summary>
        /// Server: air blew one of the fire patches away as a wave. Everyone takes the patch away and runs
        /// their own copy of the wave -- to look at; only the server's burns and shoves.
        /// </summary>
        public void BroadcastFireWave(Vector3 fireCentre, Vector3 from, Vector3 wind, float burnPerSecond)
        {
            if (!IsServer) return;
            FireWaveClientRpc(fireCentre, from, wind, burnPerSecond);
        }

        [ClientRpc]
        void FireWaveClientRpc(Vector3 fireCentre, Vector3 from, Vector3 wind, float burnPerSecond)
        {
            if (IsServer) return;
            SpellZone fire = SpellZone.Find(fireCentre, GroundEffect.Burn);
            if (fire != null) fire.Remove();
            FireWave.Spawn(from, wind, burnPerSecond, Health.NoAttacker, false);
        }

        /// <summary>Server: this player's lightning ran through an ice patch. Everyone sees it happen.</summary>
        public void BroadcastCharge(Vector3 iceCentre, Vector3 struck, Vector3[] victims)
        {
            if (!IsServer) return;
            ChargeClientRpc(iceCentre, struck, victims);
        }

        [ClientRpc]
        void ChargeClientRpc(Vector3 iceCentre, Vector3 struck, Vector3[] victims)
        {
            if (IsServer) return;
            SpellZone ice = SpellZone.ConductorAt(iceCentre);
            if (ice != null) ice.ShowDischarge(struck, new List<Vector3>(victims));
            else foreach (Vector3 v in victims) ChargeArc.Spawn(struck, v);
        }

        /// <summary>Server: this player's barrier took a hit; this much of it is left.</summary>
        public void BroadcastBarrierWear(float fraction)
        {
            if (!IsServer) return;
            BarrierWearClientRpc(fraction);
        }

        [ClientRpc]
        void BarrierWearClientRpc(float fraction)
        {
            if (IsServer) return;
            CastShield shield = CastShield.Of(OwnerClientId);
            if (shield != null) shield.SetWear(fraction);
        }

        // ---------------------------------------------------------------- server -> the body's owner

        ClientRpcParams ToOwner()
        {
            return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
        }

        public void SendImpulse(Vector3 impulse) { if (IsServer) ImpulseClientRpc(impulse, ToOwner()); }
        public void SendSlow(float multiplier, float duration) { if (IsServer) SlowClientRpc(multiplier, duration, ToOwner()); }
        public void SendLaunch(float upSpeed) { if (IsServer) LaunchClientRpc(upSpeed, ToOwner()); }
        public void SendInterrupt() { if (IsServer) InterruptClientRpc(ToOwner()); }
        public void SendStagger() { if (IsServer) StaggerClientRpc(ToOwner()); }
        public void SendToss(Vector3 along, float upSpeed) { if (IsServer) TossClientRpc(along, upSpeed, ToOwner()); }
        public void SendSlide(Vector3 push) { if (IsServer) SlideClientRpc(push, ToOwner()); }

        [ClientRpc]
        void SlideClientRpc(Vector3 push, ClientRpcParams p = default) { if (IsOwner) motor.Slide(push); }

        [ClientRpc]
        void ImpulseClientRpc(Vector3 impulse, ClientRpcParams p = default) { if (IsOwner) motor.AddImpulse(impulse); }

        [ClientRpc]
        void SlowClientRpc(float multiplier, float duration, ClientRpcParams p = default) { if (IsOwner) motor.ApplySlow(multiplier, duration); }

        [ClientRpc]
        void LaunchClientRpc(float upSpeed, ClientRpcParams p = default) { if (IsOwner) motor.Launch(upSpeed); }

        [ClientRpc]
        void InterruptClientRpc(ClientRpcParams p = default) { if (IsOwner) caster.Interrupt(); }

        [ClientRpc]
        void StaggerClientRpc(ClientRpcParams p = default) { if (IsOwner) caster.Staggered(); }

        [ClientRpc]
        void TossClientRpc(Vector3 along, float upSpeed, ClientRpcParams p = default) { if (IsOwner) motor.Toss(along, upSpeed); }

        // ---------------------------------------------------------------- dying

        void OnHealthChanged(float current, float max)
        {
            ApplyAlive(current > 0f);
        }

        void OnDied()
        {
            DiedAt = Time.time;
            WorldPopups.Word(transform, "DOWN", new Color(1f, 0.4f, 0.35f));

            if (!IsServer) return;

            Deaths.Value++;
            PlayerNet killer = Find(health.LastAttacker);
            if (killer != null && killer != this)
            {
                killer.Kills.Value++;
                MatchDirector.Scored(killer);
            }
            KillFeedClientRpc(killer != null && killer != this ? killer.DisplayName : "", DisplayName);
            StartCoroutine(RespawnLater());
        }

        [ClientRpc]
        void KillFeedClientRpc(string killer, string victim)
        {
            ArenaHud.ReportKill(killer, victim);
        }

        IEnumerator RespawnLater()
        {
            yield return new WaitForSeconds(respawnSeconds);
            if (!IsSpawned) yield break;

            ServerRespawnAt(MatchDirector.PickSpawn(this));
        }

        /// <summary>Server: back to full health at <paramref name="spawn"/> (or where they are, if none).</summary>
        public void ServerRespawnAt(Transform spawn)
        {
            if (!IsServer || !IsSpawned) return;
            Vector3 at = spawn != null ? spawn.position : transform.position;
            float yaw = spawn != null ? spawn.eulerAngles.y : transform.eulerAngles.y;

            health.ResetToFull();
            RespawnClientRpc(at, yaw, ToOwner());
        }

        /// <summary>Somebody reached the kill limit: the result on every screen, hands off until the next match.</summary>
        [ClientRpc]
        public void MatchWonClientRpc(string winner, string score)
        {
            ArenaHud.ShowWinner(winner, score, MatchDirector.IntermissionSeconds);
            GameInput.MatchOverUntil = Time.time + MatchDirector.IntermissionSeconds;
        }

        [ClientRpc]
        void RespawnClientRpc(Vector3 at, float yaw, ClientRpcParams p = default)
        {
            if (!IsOwner) return;
            motor.TeleportTo(at, yaw);
            if (netTransform != null) netTransform.Teleport(at, Quaternion.Euler(0f, yaw, 0f), transform.localScale);

            ThirdPersonCamera cam = FindObjectOfType<ThirdPersonCamera>();
            if (cam != null) cam.SetTarget(transform, yaw);
        }

        /// <summary>
        /// A dead player falls where they stood (PlayerAnimation plays the death) and lies there until
        /// the respawn -- but without a collider, so the body neither soaks up shots nor blocks a doorway.
        /// </summary>
        void ApplyAlive(bool alive)
        {
            if (body != null) body.enabled = alive;
            if (IsOwner) GameInput.LocalDead = !alive;
        }

        public static PlayerNet Find(ulong clientId)
        {
            if (clientId == Health.NoAttacker) return null;
            foreach (PlayerNet p in All) if (p.OwnerClientId == clientId) return p;
            return null;
        }
    }
}
