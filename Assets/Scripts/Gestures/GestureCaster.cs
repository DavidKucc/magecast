using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// How well the shape was drawn. Precision is the payoff curve of the whole game: a hopeless
    /// stroke costs you the cast entirely, a perfect one crits.
    /// </summary>
    public enum CastQuality { Fizzle, Misfire, Weak, Clean, Perfect }

    public enum SpellKind
    {
        Projectile,   // flies straight until it hits something
        Ballistic,    // arcs under its own gravity and leaves an area where it lands
        Barrier       // placed rather than aimed
    }

    /// <summary>What a spell leaves on the floor when it lands there instead of in somebody.</summary>
    public enum GroundEffect
    {
        None,
        Burn,      // damage per second to anyone standing in it
        Ice,       // takes your grip away: you slide, and changing direction takes most of a second
        Updraft,   // throws whoever walks in upwards, once

        // Appended, never inserted: Unity stores these as numbers in every serialized spell, and a new
        // value slipped in ahead of Updraft turned every air spell into water.
        Water      // fire and ice together: harmless to walk in, but it carries lightning like ice does
    }

    [System.Serializable]
    public class Spell
    {
        public string gesture;

        /// <summary>What the HUD calls it. ASCII on purpose -- Unity's built-in OnGUI font renders
        /// Czech diacritics as empty boxes, so "OHEN" would look like a bug rather than a word.</summary>
        public string displayName = "SPELL";

        public SpellKind kind = SpellKind.Projectile;
        public Color colour = Color.white;

        /// <summary>
        /// Damage before the precision multiplier. Placeholder values -- how often a spell lands at all
        /// is still being measured, and until that settles these numbers cannot be balanced against
        /// anything. They exist so a hit is visible, not because they are right.
        /// </summary>
        public float damage = 20f;

        [Header("Projectile / Ballistic")]
        public float speed = 20f;
        public float radius = 0.3f;
        public float lifetime = 4f;
        public float gravity = 0f;          // Ballistic only; per-spell, not the world's
        public float areaRadius = 0f;       // Ballistic only; the area it leaves
        public float areaLifetime = 0f;
        public float knockback = 0f;        // metres per second shoved into whatever it hits

        [Header("Surfaces -- what it does when it misses a person")]
        // One gesture, several effects, decided by what the spell actually lands on. The point is that
        // there is nothing new to learn: fire burns what it touches, ice slows, lightning grounds out,
        // air lifts. Deliberately not every spell does something on every surface -- twelve effects
        // would be a soup, and a spell with nothing to do on a miss is paying for being hard to miss.

        /// <summary>Walls it bounces off before it stops. Each bounce keeps this share of the damage,
        /// so a direct shot always stays better than a clever one.</summary>
        public int wallBounces = 0;
        public float bounceDamageKeep = 0.8f;

        /// <summary>Anyone within this radius of a wall impact is blown away from the wall. 0 = off.</summary>
        public float wallBurstRadius = 0f;

        public GroundEffect groundEffect = GroundEffect.None;
        public float zoneRadius = 0f;
        public float zoneLifetime = 0f;

        /// <summary>Burn: damage per second. Slow: speed multiplier, 0.5 = half. Updraft: launch speed.</summary>
        public float zoneStrength = 0f;

        [Header("On a direct hit")]
        public float hitSlow = 1f;              // speed multiplier; 1 = none
        public float hitSlowDuration = 0f;

        /// <summary>Knocks a glyph out of the target's hand if they are drawing one.</summary>
        public bool interruptsDrawing = false;

        [Header("Combinations")]
        /// <summary>Lightning into ice: damage to everyone touching the patch, before the power multiplier. 0 = none.</summary>
        public float chargeDamage = 0f;

        [Header("Against a barrier")]
        /// <summary>A hit takes damage x this off a barrier -- fire is hard on barriers, lightning is not.</summary>
        public float barrierWear = 1f;

        /// <summary>...and never less than this, so a spell without damage still counts for something.</summary>
        public float barrierWearFlat = 0f;

        [Header("Barrier")]
        public float barrierWidth = 2.8f;
        public float barrierHeight = 2.2f;
        public float barrierDistance = 2.4f;
    }

    /// <summary>
    /// The core mechanic, in two phases.
    ///
    /// 1. DRAW -- hold RMB. The camera freezes and the mouse draws instead of looking. WASD still
    ///    works at reduced speed with sprint and jump locked; that is what makes a long gesture a real
    ///    risk rather than a free action.
    /// 2. AIM AND SEND -- release RMB and the spell is held ready. The camera comes back, and you have
    ///    under a second to point and fire with LMB.
    ///
    /// The split exists because the old design -- lock the direction at the moment you START drawing --
    /// made moving targets unhittable. A target running at 6 m/s moves about 8 m during a one-second
    /// draw plus flight, which at 10 m range is roughly 40 degrees of lead, chosen before you began.
    /// Worse, it was self-defeating: the forming glyph is supposed to warn the opponent, and a warned
    /// opponent changes direction, so the better the telegraph the more certainly the prediction was
    /// wrong. Aiming after the draw leaves the vulnerable window intact and cuts the lead to flight
    /// time alone.
    ///
    /// The window is deliberately under a second. Give it two to five and the best play becomes "draw
    /// safely behind cover, peek, fire" -- cover is never more than about three metres away in this
    /// arena -- and the vulnerable window, which the entire map is designed around, becomes optional.
    ///
    /// Networked through PlayerNet: the forming glyph is streamed to everyone else as it is drawn, and a
    /// finished cast goes out as a CastData that every machine turns into the same spell. Only the
    /// server's copy of a spell hits anybody. Offline -- no PlayerNet, or not spawned -- it all happens
    /// here exactly as it always did.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class GestureCaster : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] int drawButton = 1;                 // RMB
        [SerializeField] int sendButton = 0;                 // LMB
        [SerializeField] float drawSensitivity = 14f;        // pixels per unit of mouse delta -- NOT aim sensitivity
        [SerializeField] float minPointSpacing = 4f;         // pixels; stops a stationary mouse flooding the stroke
        [SerializeField] float minStrokeLength = 60f;        // pixels; below this it was a twitch, not a gesture

        [Header("While drawing")]
        [SerializeField, Range(0.1f, 1f)] float castMoveSpeed = 0.6f;

        [Header("Aim window")]
        // Under a second on purpose -- see the class comment. Sprint stays locked through it, otherwise
        // sprint-peek-fire hands back the safety the short window was there to deny.
        [SerializeField] float aimWindow = 0.9f;

        [Header("Recognition")]
        // Runes by their lines and angles (RuneSegments) rather than as a $P point cloud. F7 in
        // training switches between the two, so they can be compared on the same hand.
        [SerializeField] bool segmentRecognition = true;
        [SerializeField] float minMargin = 0.001f;
        [SerializeField] float tiltTolerance = 25f;

        [Header("Quality tiers (excess, in units of the shape's own spread)")]
        // Dimensionless: 1.0 means "as sloppy as the calibration stroke" for EVERY shape, so these four
        // numbers cannot silently favour whichever shape they were tuned on. With the current two
        // shapes that works out to almost the same raw distances as before -- see GestureTemplate.Spread
        // for why the mechanism is kept anyway.
        //
        // Loosened when the vocabulary became runes. Spread is calibrated so that a stroke drawn with
        // 0.10 of hand shake scores an excess of about 1.0 -- so the old misfire gate at 1.15 meant
        // literally "shake more than a tenth and you fire blind", which for a rune drawn with a mouse
        // during a fight is not a skill test, it is a tax.
        //
        // What makes moving them safe is measured separately: every shape in the set sits at least 2.1
        // excess units away from its nearest rival, so a stroke can be badly drawn and still be
        // unmistakably the rune it was meant to be. The margin test in Grade is what stops a scribble
        // casting, not this gate -- and that has not been touched.
        [SerializeField] float perfectDistance = 0.45f;     // roughly a steady hand
        [SerializeField] float cleanDistance = 1.00f;
        [SerializeField] float weakDistance = 2.20f;        // still casts, just badly
        [SerializeField] float misfireDistance = 3.20f;     // past this it is not the shape at all

        [Header("Quality payoff")]
        // Power is a continuous lerp between these two from the precision axes; there is no separate
        // "clean" number any more, because the tier is now read off precision rather than the other
        // way round and a third constant would only be able to disagree with it.
        //
        // The floor is deliberately low. The trade being made is "you almost always get the spell you
        // drew, and a scrappy one is nearly worthless" instead of "a scrappy one gives you nothing at
        // all" -- firing blanks reads as the game refusing you, a feeble hit reads as your own fault.
        // A sevenfold gap between worst and best is a strong enough reason to draw well.
        [SerializeField] float weakPower = 0.25f;
        [SerializeField] float perfectPower = 1.8f;

        [Header("Precision axes -> spell properties")]
        [SerializeField, Range(0f, 1f)] float damageFromSteadiness = 0.5f;
        [SerializeField] float sizeReferencePixels = 250f;
        [SerializeField] float sizeSpreadMin = 0.7f;
        [SerializeField] float sizeSpreadMax = 1.4f;

        [Header("Spells -- circle")]
        // The only closed shape, and the only defensive one. Easy to remember for that reason alone.
        [SerializeField] Spell barrier = new Spell {
            gesture = GestureTemplates.Uruz, displayName = "BARRIER", kind = SpellKind.Barrier,
            colour = new Color(1f, 0.85f, 0.4f), lifetime = 6f, damage = 0f };

        [Header("Spells -- the four runes")]
        // Every area effect below obeys one rule: standing in it for its whole life costs LESS than a
        // direct hit. Fire's patch does 4/s for 5 s = 20 against a direct 22, at every quality. Aiming
        // at the person has to pay better than aiming next to them, or nobody would aim.
        //
        // Patches last 5-6 s and each player may have two down; the third takes the oldest away.

        // Kenaz. Bounces once off a wall -- the answer to "how do I hit someone behind cover", round a
        // corner rather than over it. On the floor it leaves a burning patch.
        [SerializeField] Spell fire = new Spell {
            gesture = GestureTemplates.Kenaz, displayName = "FIRE", kind = SpellKind.Projectile,
            colour = new Color(1f, 0.45f, 0.15f), speed = 26f, radius = 0.38f, lifetime = 4f,
            damage = 22f,
            wallBounces = 1, bounceDamageKeep = 0.8f,
            barrierWear = 1.5f,
            groundEffect = GroundEffect.Burn, zoneRadius = 2.5f, zoneLifetime = 5f, zoneStrength = 4f };

        // Laguz. Slower and fatter than fire, so it hits harder but is far easier to sidestep at range.
        // Its patch is slippery rather than slow: you keep your speed and lose your grip -- 12% of the
        // usual -- so whoever is on it slides on in whatever direction they were going and cannot
        // sidestep. In a game about dodging, that is control. It is also what lightning is waiting for.
        [SerializeField] Spell ice = new Spell {
            gesture = GestureTemplates.Laguz, displayName = "ICE", kind = SpellKind.Projectile,
            colour = new Color(0.55f, 0.85f, 1f), speed = 17f, radius = 0.55f, lifetime = 4f,
            damage = 30f,
            hitSlow = 0.5f, hitSlowDuration = 1.2f,
            barrierWear = 1f,
            groundEffect = GroundEffect.Ice, zoneRadius = 3f, zoneLifetime = 6f, zoneStrength = 0.12f };

        // Sowulo. Nearly hitscan at arena ranges: 44 m/s crosses a 12 m fight in a quarter second, which
        // is inside human reaction time. On bare floor or a wall it grounds out and does nothing -- being
        // almost impossible to dodge is its strength, and the price is that it has to hit a person.
        //
        // Except on ice. Into an ice patch, or into somebody standing on one, it charges the whole patch
        // and hits everyone touching it for 40 x power -- 52 for a clean cast. The strongest combination
        // in the game, because it costs two casts and the target had seconds to step off the ice.
        [SerializeField] Spell lightning = new Spell {
            gesture = GestureTemplates.Sowulo, displayName = "LIGHTNING", kind = SpellKind.Projectile,
            colour = new Color(0.75f, 0.7f, 1f), speed = 44f, radius = 0.18f, lifetime = 2.5f,
            damage = 12f,
            chargeDamage = 40f,
            barrierWear = 0.5f };

        // Ehwaz. The answer to a dug-in opponent. Into a wall it bursts and blows whoever is leaning on
        // it out into the open; on the floor it leaves an updraft that throws the next person up about
        // 2 m -- onto a predictable arc, which is where a lightning bolt finds them.
        //
        // It does NO damage, ever. If it did, it would be the spell for everything and get spammed; as it
        // is, it never wins a fight on its own and is in every good moment of one -- it is how you
        // deliver somebody into your fire. It still knocks a glyph out of the hand of anyone drawing.
        [SerializeField] Spell air = new Spell {
            gesture = GestureTemplates.Ehwaz, displayName = "AIR", kind = SpellKind.Projectile,
            colour = new Color(0.8f, 0.95f, 0.9f), speed = 30f, radius = 0.7f, lifetime = 3f,
            knockback = 12f, damage = 0f,
            interruptsDrawing = true,
            wallBurstRadius = 3f,
            barrierWear = 0f, barrierWearFlat = 3f,
            // 9.4 m/s against the -22 gravity the motor uses peaks at 2.0 m
            groundEffect = GroundEffect.Updraft, zoneRadius = 2f, zoneLifetime = 5f, zoneStrength = 9.4f };

        [Header("Misfire (retired)")]
        // Kept only as the fallback for an unknown spell id on the wire. A botched gesture used to fire
        // this off in a random direction; it now simply fizzles -- see EndDraw.
        [SerializeField] Spell misfire = new Spell {
            gesture = "misfire", displayName = "MISFIRE", colour = new Color(0.55f, 0.55f, 0.6f),
            speed = 11f, radius = 0.3f, lifetime = 2.5f, damage = 5f };

        [Header("Slots")]
        // Bank a drawn spell instead of sending it, then pull it out later. Using a slot empties it and
        // starts the cooldown, so you cannot walk around permanently loaded -- which was the whole
        // reason to put a timer on slots rather than on spells.
        [SerializeField] float slotCooldown = 30f;

        // A slot cast must not be instant. At arena range a projectile crosses the gap inside human
        // reaction time, so without a visible wind-up there is nothing for the target to react TO and
        // the slot becomes an undodgeable delete button. Short enough to still be worth the slot.
        [SerializeField] float slotWindUp = 0.3f;

        [Header("Calibration")]
        [SerializeField] bool logCasts = true;

        ThirdPersonCamera cam;
        PlayerMotor motor;
        GestureTrail trail;
        Crosshair crosshair;
        PlayerAnimation anim;      // optional: the game ran on a capsule long before there was a model
        PlayerNet net;             // optional: null or unspawned means offline

        readonly List<Vector2> stroke = new List<Vector2>();
        Vector2 cursor;
        float strokeLength;

        bool drawing;
        float castStartedAt;
        bool movedWhileCasting;
        string practiceTarget = "?";

        /// <summary>
        /// One banked spell. Holds the grading too, not just which spell it was -- a spell you drew
        /// perfectly should still land as a crit an hour later, otherwise banking quietly costs you
        /// the thing you earned by drawing well.
        /// </summary>
        class SpellSlot
        {
            public Spell spell;            // null = empty
            public float precision;
            public CastQuality quality;
            public StrokeMetrics metrics;
            public float readyAt;          // cooldown expiry
        }

        readonly SpellSlot[] slots = { new SpellSlot(), new SpellSlot() };

        /// <summary>
        /// Per-slot debounce. Input.inputString REPEATS while a key is held -- GetKeyDown does not --
        /// so one slightly long press delivered the character again a frame or two later. The second
        /// delivery found the spell already banked and pulled it straight back out, starting the
        /// cooldown and letting the aim window lapse: one keypress, spell gone, slot on cooldown.
        /// </summary>
        readonly float[] slotBlockedUntil = new float[2];
        const float SlotKeyDebounce = 0.35f;

        /// <summary>
        /// Chosen DURING the draw: hold a slot key while drawing and the spell goes straight into that
        /// slot on release, with no aim window at all.
        ///
        /// Deciding in advance is the point, not a limitation. If you could bank after seeing the
        /// grade, the best play would be to draw in safety over and over and bank only the crits, and
        /// every fight would open with two of them. Committing first makes a slot a bet rather than a
        /// pick of the best takes.
        /// </summary>
        int bankTarget = -1;

        /// <summary>Which slot is armed and waiting for the mouse. -1 = none.</summary>
        int selectedSlot = -1;

        int windUpFrom = -1;
        float windUpEndsAt;

        /// <summary>
        /// Precision a banked spell is capped at -- just under the Perfect tier, so a slot can never
        /// crit. A spell drawn in safety would otherwise be strictly better than one drawn under fire,
        /// and drawing under fire is the entire game. A banked spell is stabilised, not sharpened.
        /// </summary>
        const float BankedPrecisionCap = 0.84f;

        // held between the draw and the send
        bool holding;
        Spell heldSpell;
        CastQuality heldQuality;
        float heldPrecision;
        StrokeMetrics heldMetrics;

        /// <summary>The last stroke's lines, errors and precision, for the diagnostics line.</summary>
        string lastSegments = "";
        float heldUntil;

        string lastResult = "";
        float lastResultTime = -99f;

        string headline = "";
        Color headlineColour = Color.white;
        float headlineUntil = -99f;
        GUIStyle bigStyle;
        GUIStyle smallStyle;
        GUIStyle slotStyle;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            cam = FindObjectOfType<ThirdPersonCamera>();
            crosshair = FindObjectOfType<Crosshair>();
            anim = GetComponent<PlayerAnimation>();
            net = GetComponent<PlayerNet>();
        }

        // In Start rather than Awake: somebody else's copy of a player is switched off as it spawns,
        // before Start ever runs, and must not put a full-screen drawing canvas on this machine.
        void Start()
        {
            trail = GestureTrail.Create(null);
        }

        void OnDestroy()
        {
            if (trail != null) Destroy(trail.canvas.gameObject);
        }

        bool Networked { get { return net != null && net.IsSpawned; } }

        void Update()
        {
            // A menu or death takes the hands off the glyph -- carrying on would draw with a mouse that
            // is busy clicking buttons.
            if (GameInput.Blocked)
            {
                if (drawing) Interrupt();
                return;
            }

            ReadPracticeTarget();

            // Committed: the wind-up is the tell that makes a slot cast dodgeable at all, so nothing
            // may interrupt it and nothing else may start during it.
            if (windUpFrom >= 0)
            {
                if (Time.time >= windUpEndsAt) FireFromSlot();
                return;
            }

            HandleSlotKeys();

            if (holding)
            {
                if (Input.GetMouseButtonDown(sendButton)) { Send(); return; }
                if (Time.time >= heldUntil) LoseHeld();
            }
            else if (selectedSlot >= 0 && Input.GetMouseButtonDown(sendButton))
            {
                BeginWindUp();
                return;
            }

            // Changed your mind mid-glyph: the other button throws it away. Nothing is cast and nothing
            // is spent; the right button has to be pressed again to start over.
            if (drawing && Input.GetMouseButtonDown(sendButton))
            {
                AbortDraw("CANCELLED", StrokeOutcome.Cancelled);
                return;
            }

            if (!drawing && !holding && Input.GetMouseButtonDown(drawButton)) BeginDraw();
            else if (drawing && Input.GetMouseButton(drawButton)) ContinueDraw();
            else if (drawing && Input.GetMouseButtonUp(drawButton)) EndDraw();
        }

        // Moved to the function row: the number keys belong to the slots now, and a calibration aid
        // must never sit on top of something you press mid-fight.
        void ReadPracticeTarget()
        {
            if (Input.GetKeyDown(KeyCode.F1)) practiceTarget = GestureTemplates.Uruz;
            if (Input.GetKeyDown(KeyCode.F7) && !drawing)
            {
                segmentRecognition = !segmentRecognition;
                Headline("RECOGNIZER: " + (segmentRecognition ? "LINES + ANGLES" : "$P SHAPE"), Color.white, 1.5f);
            }
            RuneSegments.Enabled = segmentRecognition;
            if (Input.GetKeyDown(KeyCode.F2)) practiceTarget = GestureTemplates.Kenaz;
            if (Input.GetKeyDown(KeyCode.F3)) practiceTarget = GestureTemplates.Laguz;
            if (Input.GetKeyDown(KeyCode.F4)) practiceTarget = GestureTemplates.Sowulo;
            if (Input.GetKeyDown(KeyCode.F5)) practiceTarget = GestureTemplates.Ehwaz;
        }

        // ---------------------------------------------------------------- slots

        /// <summary>
        /// One key, three meanings, chosen by what you are already doing -- and the three contexts
        /// never overlap, so there is nothing to remember:
        ///
        ///   while drawing      pick where this spell will be banked
        ///   holding a spell    bank it now (the opportunistic route; the draw-time one has no timer)
        ///   empty handed       select the slot, which then fires on the mouse
        /// </summary>
        void HandleSlotKeys()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (Time.time < slotBlockedUntil[i]) continue;
                if (!SlotPressed(i)) continue;

                slotBlockedUntil[i] = Time.time + SlotKeyDebounce;

                if (drawing) ChooseBankTarget(i);
                else if (holding) StoreHeld(i);
                else SelectSlot(i);
            }
        }

        void ChooseBankTarget(int index)
        {
            SpellSlot slot = slots[index];
            int shown = index + 1;

            if (Time.time < slot.readyAt || slot.spell != null)
            {
                bankTarget = -1;
                Headline("SLOT " + shown + (slot.spell != null ? " FULL" : " COOLING"), FailColour, 1f);
                return;
            }

            bankTarget = bankTarget == index ? -1 : index;   // press again to change your mind
            if (bankTarget >= 0) Announce("this one goes to slot " + shown);
            else Announce("banking cancelled - this one comes to hand");
        }

        void SelectSlot(int index)
        {
            SpellSlot slot = slots[index];
            int shown = index + 1;

            if (slot.spell == null)
            {
                selectedSlot = -1;
                if (Time.time < slot.readyAt)
                    Headline("SLOT " + shown + "  " + Mathf.CeilToInt(slot.readyAt - Time.time) + "s", FailColour, 1f);
                return;
            }

            // Selecting costs nothing and is reversible. What costs is FIRING, which empties the slot
            // and starts the cooldown -- so there is no reason to punish someone for changing their mind.
            selectedSlot = selectedSlot == index ? -1 : index;

            if (selectedSlot >= 0)
            {
                Headline(slot.spell.displayName + "  SELECTED", slot.spell.colour, 1f);
                Announce("LMB to send from slot " + shown);
                if (crosshair != null) crosshair.SetTint(slot.spell.colour);
            }
            else
            {
                Announce("slot " + shown + " deselected");
                if (crosshair != null) crosshair.SetTint(null);
            }
        }

        void BeginWindUp()
        {
            SpellSlot slot = slots[selectedSlot];
            if (slot.spell == null) { selectedSlot = -1; return; }

            windUpFrom = selectedSlot;
            windUpEndsAt = Time.time + slotWindUp;
            motor.SprintLocked = true;

            // the wind-up only works as a tell if the other side can see it
            if (Networked) net.OwnerStrokeEnd(StrokeOutcome.WindUp, IndexOf(slot.spell));
            Headline(slot.spell.displayName, slot.spell.colour, slotWindUp + 0.6f);
        }

        void FireFromSlot()
        {
            SpellSlot slot = slots[windUpFrom];
            int shown = windUpFrom + 1;

            windUpFrom = -1;
            selectedSlot = -1;
            motor.SprintLocked = false;
            if (crosshair != null) crosshair.SetTint(null);

            if (slot.spell == null) return;

            Spell spell = slot.spell;
            Cast(spell, ResolveAim(), slot.precision, slot.quality, slot.metrics);

            // Firing is what "using" a slot means, so this is where the cooldown starts -- selecting is
            // free, and charging for a decision you can undo would only teach people not to touch it.
            slot.spell = null;
            slot.readyAt = Time.time + slotCooldown;

            Headline(spell.displayName + "  SENT", spell.colour, 1.2f);
            Announce("from slot " + shown + " at " + slot.quality);
        }

        /// <summary>
        /// Reads the physical key AND the character it produced.
        ///
        /// On a Czech layout the keys in the 1 and 2 positions type '+' and 'e-caron', and there is no
        /// KeyCode for the latter at all -- so a KeyCode-only check works or fails depending on the
        /// layout the player happens to have active, which is exactly the kind of bug that looks like
        /// broken input. Checking the typed character as well covers both without asking anyone to
        /// switch layouts to play.
        /// </summary>
        bool SlotPressed(int index)
        {
            if (index == 0)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return true;
                return Typed('1') || Typed('+');
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return true;
            // written as an escape so the source stays pure ASCII and cannot be broken by a tool or
            // editor guessing the file's encoding wrong
            return Typed('2') || Typed('\u011B');   // e-caron: the Czech key in the 2 position
        }

        static bool Typed(char c)
        {
            string s = Input.inputString;
            for (int i = 0; i < s.Length; i++) if (s[i] == c) return true;
            return false;
        }

        void StoreHeld(int index)
        {
            SpellSlot slot = slots[index];
            int shown = index + 1;

            if (Time.time < slot.readyAt)
            {
                Headline("SLOT " + shown + " COOLING", FailColour, 1f);
                return;
            }
            if (slot.spell != null)
            {
                Headline("SLOT " + shown + " FULL", FailColour, 1f);
                return;
            }

            Bank(index, heldSpell, heldPrecision, heldMetrics);
            ClearHeld();
        }

        /// <summary>Puts a spell in a slot, capped so a slot can never hold a crit.</summary>
        void Bank(int index, Spell spell, float precision, StrokeMetrics metrics)
        {
            SpellSlot slot = slots[index];

            float capped = Mathf.Min(precision, BankedPrecisionCap);
            slot.spell = spell;
            slot.precision = capped;
            slot.quality = PrecisionTier(capped);
            slot.metrics = metrics;

            Headline(spell.displayName + "  ->  SLOT " + (index + 1), spell.colour, 1.2f);
            AnnounceCreated(IndexOf(spell), false);
            if (Networked) net.OwnerStrokeEnd(StrokeOutcome.Banked, IndexOf(spell), false);
            Announce(precision > BankedPrecisionCap
                     ? "banked at " + slot.quality + " - a slot never holds a crit, only a live cast does"
                     : "banked at " + slot.quality);
        }

        // ---------------------------------------------------------------- drawing

        void BeginDraw()
        {
            if (cam == null) return;

            drawing = true;
            stroke.Clear();
            strokeLength = 0f;

            cam.LookEnabled = false;
            motor.SpeedMultiplier = castMoveSpeed;
            motor.SprintLocked = true;
            motor.JumpLocked = true;

            castStartedAt = Time.time;
            movedWhileCasting = false;
            bankTarget = -1;              // decided during this draw, never carried over from the last

            if (anim != null) anim.SetDrawing(true);

            cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            stroke.Add(cursor);
            trail.Begin();
            trail.Push(cursor);

            if (Networked)
            {
                net.OwnerStrokeBegin();
                net.OwnerStrokePoint(Shared(cursor));
            }
        }

        /// <summary>
        /// A stroke point as others see it: from the screen centre, in screen heights. The cursor
        /// always starts in the centre, so this is the same glyph on any screen.
        /// </summary>
        static Vector2 Shared(Vector2 screenPoint)
        {
            float h = Mathf.Max(1f, Screen.height);
            return new Vector2((screenPoint.x - Screen.width * 0.5f) / h, (screenPoint.y - Screen.height * 0.5f) / h);
        }

        void ContinueDraw()
        {
            // drawing while running is the realistic case, and the one the thresholds have to survive
            if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
                movedWhileCasting = true;

            // The hardware cursor is locked for mouselook, so there is no mouse position to read -- the
            // drawing cursor is integrated from raw deltas instead. That also gives drawing its own
            // sensitivity, which a value tuned for flicking the camera would ruin.
            Vector2 delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * drawSensitivity;
            cursor += delta;
            cursor.x = Mathf.Clamp(cursor.x, 8f, Screen.width - 8f);
            cursor.y = Mathf.Clamp(cursor.y, 8f, Screen.height - 8f);

            if (stroke.Count == 0 || Vector2.Distance(stroke[stroke.Count - 1], cursor) >= minPointSpacing)
            {
                if (stroke.Count > 0) strokeLength += Vector2.Distance(stroke[stroke.Count - 1], cursor);
                stroke.Add(cursor);
                trail.Push(cursor);
                if (Networked) net.OwnerStrokePoint(Shared(cursor));
                UpdatePreview();
            }
        }

        void UpdatePreview()
        {
            if (stroke.Count < 8 || strokeLength < minStrokeLength) { trail.SetPreview(null); return; }

            if (segmentRecognition)
            {
                RuneSegments.Match m = RuneSegments.Recognise(stroke);
                trail.SetPreview(m.Name == null ? (Color?)null : PreviewColour(m.Name, PrecisionTier(m.Precision)));
                return;
            }

            RecognitionResult r = Recognise();
            CastQuality q = Grade(r);
            string id = ResolveGestureId(r);

            trail.SetPreview(q == CastQuality.Fizzle || id == null
                             ? (Color?)null
                             : PreviewColour(id, q));
        }

        void EndDraw()
        {
            drawing = false;
            cam.LookEnabled = true;
            motor.SpeedMultiplier = 1f;
            motor.JumpLocked = false;
            trail.Clear();
            if (anim != null) anim.SetDrawing(false);

            if (strokeLength < minStrokeLength)
            {
                motor.SprintLocked = false;
                Headline("CANCELLED", FailColour, 1f);    // a flick is a cancel, not a failed cast
                Announce("too short - cancelled");
                if (Networked) net.OwnerStrokeEnd(StrokeOutcome.Cancelled, 0);
                return;
            }

            RecognitionResult r;
            CastQuality quality;
            StrokeMetrics metrics;
            float precision;
            string id;

            if (segmentRecognition)
            {
                // Lines and angles: the rune is whichever one the lines follow, and how closely they
                // follow it is the precision. See RuneSegments.
                RuneSegments.Match m = RuneSegments.Recognise(stroke);
                id = m.Name;
                metrics = StrokeMetrics.Compute(stroke, TemplateFor(id));
                precision = m.Precision;
                quality = id == null ? CastQuality.Fizzle : PrecisionTier(precision);
                // in the log's terms: "excess" is the mean angle error, "margin" the gap to the next rune
                r = new RecognitionResult { Name = id, Excess = m.MeanError, Distance = m.MeanError,
                                            Runner = m.RunnerUp != null ? m.RunnerUpError : 999f, Accepted = id != null };
                LogCast(r, quality, metrics);
                lastSegments = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0} lines, angle err {1:F0} (worst {2:F0}), straight {3:F2}, lengths {4:F2}, precision {5:F2}{6}",
                    m.Lines, m.MeanError > 900f ? 0f : m.MeanError, m.WorstError > 900f ? 0f : m.WorstError,
                    m.Straightness, m.LengthError, m.Precision,
                    m.RunnerUp != null ? "   next: " + m.RunnerUp + " " + m.RunnerUpError.ToString("F0") : "");
            }
            else
            {
                r = Recognise();
                quality = Grade(r);
                metrics = StrokeMetrics.Compute(stroke, TemplateFor(r.Name));
                precision = Precision(metrics);
                id = ResolveGestureId(r);

                // A line drawn on the diagonal is not "probably up". It is a stroke whose author did not
                // commit, and guessing would occasionally cast fire when they wanted air.
                if (id == null && quality != CastQuality.Fizzle) quality = CastQuality.Misfire;

                if (quality != CastQuality.Fizzle && quality != CastQuality.Misfire)
                    quality = PrecisionTier(precision);

                // logged as graded, so the calibration still sees which strokes were nearly something
                LogCast(r, quality, metrics);
                lastSegments = "";
            }

            // No more misfire. A stroke that is not clearly one of the shapes simply does not cast: the
            // tiers of the ones that do are the whole of the reward, and a wild shot flying off in a
            // random direction was noise on top of it.
            if (quality == CastQuality.Misfire) quality = CastQuality.Fizzle;

            if (quality == CastQuality.Fizzle)
            {
                motor.SprintLocked = false;
                Headline("FIZZLE", FailColour);
                WorldPopups.Word(transform, "FIZZLE", FailColour);
                if (Networked) net.OwnerStrokeEnd(StrokeOutcome.Fizzle, 0);
                Announce(segmentRecognition
                         ? "nothing cast - " + lastSegments
                         : string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                         "nothing cast (best {0}, excess {1:F2})", r.Name ?? "-", r.Excess));
                return;
            }

            Spell cast = SpellFor(id);

            // Banked straight from the draw: the destination was chosen before the grade was known, so
            // no aim window ever starts and there is nothing to race against.
            if (bankTarget >= 0)
            {
                motor.SprintLocked = false;
                Bank(bankTarget, cast, precision, metrics);
                bankTarget = -1;
                return;
            }

            heldSpell = cast;
            heldQuality = quality;
            heldPrecision = precision;
            heldMetrics = metrics;
            heldUntil = Time.time + aimWindow;
            holding = true;
            motor.SprintLocked = true;    // no sprint-peek-fire

            if (crosshair != null) crosshair.SetTint(heldSpell.colour);
            bool crit = quality == CastQuality.Perfect;
            AnnounceCreated(IndexOf(heldSpell), crit);
            if (Networked) net.OwnerStrokeEnd(StrokeOutcome.Held, IndexOf(heldSpell), crit);

            if (segmentRecognition)
                Announce(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                       "{0}  {1}   dmg {2:F0}   {3}", quality, id,
                                       heldSpell.damage * Mathf.Lerp(weakPower, perfectPower, precision), lastSegments));
            else Announce(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                   "{0}  {1}   excess {2:F2}   power {3:F2}   dmg {4:F0}{5}   " +
                                   "steady {6:F2} def {7:F2}{8}",
                                   quality, id, r.Excess,
                                   Mathf.Lerp(weakPower, perfectPower, precision),
                                   heldSpell.damage * Mathf.Lerp(weakPower, perfectPower, precision),
                                   quality == CastQuality.Perfect ? "  CRIT" : "",
                                   metrics.Steadiness, metrics.Definition,
                                   metrics.Weakest == "-" ? "" : "   weakest: " + metrics.Weakest));
        }

        // ---------------------------------------------------------------- sending

        void Send()
        {
            Spell spell = heldSpell;
            ClearHeld();
            if (Networked) net.OwnerStrokeEnd(StrokeOutcome.Sent, IndexOf(spell));
            Cast(spell, ResolveAim(), heldPrecision, heldQuality, heldMetrics);
            Headline(spell.displayName + "  SENT", spell.colour, 1.2f);
        }

        void LoseHeld()
        {
            Spell lost = heldSpell;
            ClearHeld();
            // Letting it lapse is the cancel: draw something you did not want and simply do not send
            // it. It has to cost the cast, or the right play would be to keep one drawn at all times.
            Headline("LOST", FailColour, 1.2f);
            Announce("aim window expired on " + (lost != null ? lost.displayName : "-"));
            if (Networked) net.OwnerStrokeEnd(StrokeOutcome.Lost, lost != null ? IndexOf(lost) : (byte)0);
        }

        void ClearHeld()
        {
            holding = false;
            heldSpell = null;
            motor.SprintLocked = false;
            if (crosshair != null) crosshair.SetTint(null);
        }

        /// <summary>A spell drawn and waiting to be sent, or null. Read by the aim marker.</summary>
        public Spell HeldSpell { get { return holding ? heldSpell : null; } }

        /// <summary>
        /// How much the held spell's areas are scaled by the size it was drawn at. Exposed so the aim
        /// marker shows the patch that will ACTUALLY appear -- the drawn size moves it by up to 40%, and
        /// a marker drawn at the base size would be lying about exactly the thing it exists to show.
        /// </summary>
        public float HeldSizeScale { get { return holding ? SizeScale(heldMetrics) : 1f; } }

        /// <summary>Where a cast leaves from. Public so the aim marker traces the same line.</summary>
        public Vector3 MuzzlePoint { get { return Muzzle(); } }

        float SizeScale(StrokeMetrics m)
        {
            return Mathf.Clamp(m.SizePixels / Mathf.Max(1f, sizeReferencePixels), sizeSpreadMin, sizeSpreadMax);
        }

        /// <summary>
        /// Knocks a glyph out of the hand mid-draw: nothing is cast, nothing is spent.
        ///
        /// Only a draw in progress is affected. A spell already drawn and held is left alone -- the risk
        /// in this game is in the drawing, and punishing the hold as well would charge twice for the
        /// same thing while rewarding an opponent for simply waiting.
        /// </summary>
        public void Interrupt()
        {
            // somebody else's player: their own machine has to let go of the glyph
            if (Networked && !net.IsOwner) { net.SendInterrupt(); return; }
            AbortDraw("INTERRUPTED", StrokeOutcome.Interrupted);
        }

        /// <summary>
        /// Drops a glyph in progress with nothing cast. The right button has to be let go and pressed
        /// again to draw -- until then the mouse is back to aiming, even with the button still held.
        /// </summary>
        void AbortDraw(string reason, StrokeOutcome outcome)
        {
            if (!drawing) return;

            // the undo of BeginDraw, minus any grading: there is no stroke left to grade
            drawing = false;
            stroke.Clear();
            strokeLength = 0f;
            bankTarget = -1;

            cam.LookEnabled = true;
            motor.SpeedMultiplier = 1f;
            motor.SprintLocked = false;
            motor.JumpLocked = false;
            if (trail != null) trail.Clear();
            if (anim != null) anim.SetDrawing(false);

            Headline(reason, FailColour, 1f);
            WorldPopups.Word(transform, reason, FailColour);
            if (Networked) net.OwnerStrokeEnd(outcome, 0);
        }

        // ---------------------------------------------------------------- recognition

        RecognitionResult Recognise()
        {
            return PDollarRecognizer.Recognise(stroke, GestureTemplates.All,
                                               misfireDistance, minMargin, tiltTolerance);
        }

        /// <summary>
        /// Turns "circle" or "line" into the gesture that actually names a spell. A line only becomes
        /// one once the direction it travelled is known, and $P cannot supply that -- see
        /// StrokeDirections. Returns null when the direction is too diagonal to call.
        /// </summary>
        string ResolveGestureId(RecognitionResult r)
        {
            // Every shape in the vocabulary now names its own spell. The lines needed a second step
            // because $P cannot tell left-to-right from right-to-left, so four spells shared one
            // template; runes are told apart by shape, so there is nothing left to resolve.
            if (GestureTemplates.IsRune(r.Name)) return r.Name;
            return null;
        }

        CastQuality Grade(RecognitionResult r)
        {
            return Grade(r, perfectDistance, cleanDistance, weakDistance, misfireDistance, minMargin);
        }

        /// <summary>Static so the editor tests grade with exactly this logic instead of a copy that drifts.</summary>
        // Defaults kept equal to the serialized fields above. The editor tests call this without
        // arguments precisely so they grade the way the game does -- which only holds while these match.
        public static CastQuality Grade(RecognitionResult r, float perfect = 0.45f, float clean = 1.00f,
                                        float weak = 2.20f, float misfire = 3.20f, float minMargin = 0.001f)
        {
            // Grades EXCESS over each shape's own floor, not raw distance. Raw distance is not
            // comparable between shapes, so grading it would make one shape crit almost always and
            // another almost never, regardless of how well the player drew either.
            if (r.Name == null || r.Excess > misfire) return CastQuality.Fizzle;
            if (r.Margin < minMargin) return CastQuality.Misfire;   // two shapes fit equally well
            if (r.Excess > weak) return CastQuality.Misfire;
            if (r.Excess > clean) return CastQuality.Weak;
            if (r.Excess > perfect) return CastQuality.Clean;
            return CastQuality.Perfect;
        }

        /// <summary>How well drawn, as one number, from the axes that measure craft rather than shape.</summary>
        float Precision(StrokeMetrics m)
        {
            float shape = m.Steadiness * damageFromSteadiness +
                          m.Definition * (1f - damageFromSteadiness);

            // Closure counts only where it means something. The recognizer now joins a nearly-closed
            // loop for you, so a sloppy circle still casts the spell you intended -- this is where the
            // sloppiness gets paid for instead. Open strokes skip it: their closure is a constant 1,
            // and scoring it would hand every line a free axis the circle has to work for.
            if (!m.ClosureApplies) return Mathf.Clamp01(shape);
            return Mathf.Clamp01(shape * 0.75f + m.Closure * 0.25f);
        }

        static CastQuality PrecisionTier(float precision)
        {
            if (precision >= 0.85f) return CastQuality.Perfect;
            if (precision >= 0.65f) return CastQuality.Clean;
            return CastQuality.Weak;
        }

        Color PreviewColour(string gestureId, CastQuality q)
        {
            Color baseColour = q == CastQuality.Misfire ? misfire.colour : SpellFor(gestureId).colour;
            switch (q)
            {
                case CastQuality.Perfect: return Color.Lerp(baseColour, Color.white, 0.55f);
                case CastQuality.Clean: return baseColour;
                case CastQuality.Weak: return baseColour * 0.65f;
                default: return new Color(0.5f, 0.5f, 0.52f, 0.7f);
            }
        }

        Spell SpellFor(string gestureId)
        {
            if (gestureId == GestureTemplates.Uruz) return barrier;
            if (gestureId == GestureTemplates.Kenaz) return fire;
            if (gestureId == GestureTemplates.Laguz) return ice;
            if (gestureId == GestureTemplates.Sowulo) return lightning;
            if (gestureId == GestureTemplates.Ehwaz) return air;
            return misfire;
        }

        /// <summary>
        /// The castable vocabulary, for anything that needs to show it -- the on-screen legend, for now.
        /// Read from the same mapping the caster uses, so a legend cannot drift from what actually fires.
        /// </summary>
        public struct VocabularyEntry
        {
            public string Id;
            public string SpellName;
            public Color Colour;
        }

        public List<VocabularyEntry> Vocabulary()
        {
            var list = new List<VocabularyEntry>();
            string[] ids = { GestureTemplates.Kenaz, GestureTemplates.Laguz, GestureTemplates.Sowulo,
                             GestureTemplates.Ehwaz, GestureTemplates.Uruz };
            foreach (string id in ids)
            {
                Spell s = SpellFor(id);
                list.Add(new VocabularyEntry { Id = id, SpellName = s.displayName, Colour = s.colour });
            }
            return list;
        }

        GestureTemplate TemplateFor(string shapeName)
        {
            if (shapeName == null) return null;
            var all = GestureTemplates.All;
            for (int i = 0; i < all.Count; i++) if (all[i].Name == shapeName) return all[i];
            return null;
        }

        // ---------------------------------------------------------------- effects

        // ---------------------------------------------------------------- spell ids, for the wire

        public const byte BarrierId = 0, FireId = 1, IceId = 2, LightningId = 3, AirId = 4, MisfireId = 5;

        public Spell SpellByIndex(byte index)
        {
            switch (index)
            {
                case BarrierId: return barrier;
                case FireId: return fire;
                case IceId: return ice;
                case LightningId: return lightning;
                case AirId: return air;
                default: return misfire;
            }
        }

        public byte IndexOf(Spell spell)
        {
            if (spell == barrier) return BarrierId;
            if (spell == fire) return FireId;
            if (spell == ice) return IceId;
            if (spell == lightning) return LightningId;
            if (spell == air) return AirId;
            return MisfireId;
        }

        public Color SpellColour(byte index)
        {
            return SpellByIndex(index).colour;
        }

        /// <summary>
        /// The spell's name over the caster's head, at the moment it is MADE -- the glyph has just
        /// become a fire in the hand -- not when it is sent. That is the moment the opponent needs to
        /// know about: from here there are 0.9 s to get behind something. Shown on every machine, the
        /// caster's own included; a crit gets an exclamation mark.
        /// </summary>
        public void AnnounceCreated(byte index, bool crit)
        {
            Spell spell = SpellByIndex(index);
            WorldPopups.Spell(transform, crit ? spell.displayName + "!" : spell.displayName,
                              crit ? Color.Lerp(spell.colour, Color.white, 0.35f) : spell.colour, crit);
        }

        /// <summary>
        /// The owner's side of a cast: everything a spell needs is decided here, where the aim is, and
        /// packed up so every machine can produce the same one.
        /// </summary>
        void Cast(Spell spell, Vector3 direction, float precision, CastQuality quality, StrokeMetrics metrics)
        {
            CastData data = new CastData
            {
                Spell = IndexOf(spell),
                Quality = (byte)quality,
                Precision = precision,
                SizeScale = SizeScale(metrics),
                Muzzle = Muzzle(),
                Direction = direction,
                Feet = transform.position,
                SentAt = Networked ? Unity.Netcode.NetworkManager.Singleton.ServerTime.Time : 0.0
            };

            if (Networked) net.RequestCast(data);
            else SpawnCast(data, true);
        }

        /// <summary>
        /// Makes the spell described by <paramref name="data"/> happen on this machine. Runs on every
        /// machine for every cast, including on somebody else's copy of a player.
        ///
        /// Only an <paramref name="authoritative"/> copy -- the server's, or offline -- deals damage,
        /// shoves, slows or leaves patches. Everyone else's copy is there to be seen: it flies, bounces
        /// and flashes the same, and does nothing to anybody.
        ///
        /// Quality scales what the spell has to give: a projectile gets bigger and faster, a lobbed one
        /// leaves a wider patch, a barrier stands wider and longer. Same knob, different meaning.
        /// </summary>
        public void SpawnCast(CastData data, bool authoritative)
        {
            Spell spell = SpellByIndex(data.Spell);
            CastQuality quality = (CastQuality)data.Quality;
            float precision = data.Precision;
            float sizeScale = data.SizeScale;
            Vector3 direction = data.Direction;

            if (anim != null) anim.PlaySend(spell.kind == SpellKind.Barrier);
            bool crit = quality == CastQuality.Perfect;

            // Damage comes from how precisely the shape was drawn. Size comes from how BIG it was
            // drawn, which is not a mistake to avoid but a choice: a wide sweep costs time and screen
            // space while the camera is frozen, and buys a fatter projectile.
            float power = Mathf.Lerp(weakPower, perfectPower, precision);
            float dealt = spell.damage * power;

            Color colour = crit ? Color.Lerp(spell.colour, Color.white, 0.4f) : spell.colour;
            ulong attacker = Networked ? net.OwnerClientId : Combat.Health.NoAttacker;

            if (spell.kind == SpellKind.Barrier)
            {
                Vector3 flat = new Vector3(direction.x, 0f, direction.z).normalized;
                if (flat.sqrMagnitude < 0.01f) flat = transform.forward;

                // Planted on the ground rather than floating at chest height, so it is cover you can
                // crouch behind and not a hovering pane with a gap underneath.
                Vector3 at = data.Feet + flat * spell.barrierDistance;
                RaycastHit hit;
                if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                    at = hit.point;

                // Every machine plants its own, and every one is solid: the server's stops the real
                // shots, the rest stop the copies, so what you see blocked is what was blocked.
                CastShield.Spawn(at, flat,
                                 spell.barrierWidth * sizeScale,
                                 spell.barrierHeight,
                                 spell.lifetime * Mathf.Lerp(0.6f, 1.4f, precision),
                                 colour,
                                 CastShield.BaseDurability * Mathf.Lerp(0.6f, 1.4f, precision),
                                 attacker, Networked ? net : null, authoritative);
                return;
            }

            // the flash in the hands as it leaves, with its sound, for everyone watching
            SpellFx.Entry fx = SpellFx.For(spell);
            if (fx != null && fx.cast != null)
                SpellFx.Play(fx.cast, data.Muzzle, Quaternion.LookRotation(direction) * Quaternion.Euler(fx.castTurn), fx.castScale,
                             null, false);

            bool ballistic = spell.kind == SpellKind.Ballistic;
            Projectile shot = Projectile.Spawn(data.Muzzle, direction,
                             spell.speed,
                             spell.radius * sizeScale,
                             spell.lifetime, colour, transform, power,
                             spell.gravity,
                             ballistic ? spell.areaRadius * sizeScale : 0f,
                             ballistic ? spell.areaLifetime * Mathf.Lerp(0.6f, 1.4f, precision) : 0f,
                             spell.knockback * Mathf.Lerp(0.7f, 1.3f, precision),
                             dealt,
                             spell, sizeScale, authoritative, attacker);

            // A cast that arrived over the network is as old as its trip here: the copy is moved on by
            // that much, so it flies -- and lands, and hits -- where the caster's own shot is, instead of
            // leaving the hand a ping late. The server's copy too, which is the one that decides hits.
            if (shot != null && Networked && data.SentAt > 0.0)
            {
                float late = (float)(Unity.Netcode.NetworkManager.Singleton.ServerTime.Time - data.SentAt);
                if (late > 0.005f) shot.CatchUp(late);
            }
        }

        Vector3 Muzzle()
        {
            return transform.position + Vector3.up * 1.4f + transform.forward * 0.6f;
        }

        /// <summary>
        /// Aim from the muzzle at whatever the camera centre is looking at, not straight down camera
        /// forward. The camera sits half a metre off the shoulder, so those are different lines, and
        /// firing along the second one makes close-range casts visibly miss the crosshair.
        /// </summary>
        Vector3 ResolveAim()
        {
            Vector3 origin = cam.AimOrigin;
            Vector3 forward = cam.AimDirection;

            RaycastHit hit;
            Vector3 target = Physics.Raycast(origin, forward, out hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                             ? hit.point
                             : origin + forward * 200f;

            Vector3 dir = target - Muzzle();
            return dir.sqrMagnitude < 0.01f ? forward : dir.normalized;
        }

        // ---------------------------------------------------------------- reporting

        void Announce(string text)
        {
            lastResult = text;
            lastResultTime = Time.time;
        }

        /// <summary>
        /// The one line the player actually reads. Everything else on screen is diagnostics for us.
        ///
        /// It answers "what did I just make", which is the question the whole draw phase leaves open --
        /// you watched a shape form, and until this appears you do not know whether the game agreed
        /// with you about what it was.
        /// </summary>
        void Headline(string text, Color colour, float seconds = 2f)
        {
            headline = text;
            headlineColour = colour;
            headlineUntil = Time.time + seconds;
        }

        public static readonly Color FailColour = new Color(0.72f, 0.72f, 0.76f);

        void LogCast(RecognitionResult r, CastQuality quality, StrokeMetrics metrics)
        {
            if (!logCasts) return;
            CastLog.Record(practiceTarget, ResolveGestureId(r) ?? r.Name, r.Excess, r.Margin, quality,
                           Time.time - castStartedAt, strokeLength, stroke.Count, movedWhileCasting, metrics);
        }

        void OnGUI()
        {
            if (bigStyle == null)
            {
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                slotStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            }

            // --- top left: what you are holding, or what just happened ---
            if (holding && heldSpell != null)
            {
                float left = Mathf.Max(0f, heldUntil - Time.time);
                bigStyle.normal.textColor = heldSpell.colour;
                GUI.Label(new Rect(14f, 10f, 700f, 40f), heldSpell.displayName + "  READY", bigStyle);

                smallStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(16f, 46f, 700f, 20f),
                          string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                        "LMB to send   {0:F2}s left", left),
                          smallStyle);

                // a bar rather than only a number: you will be looking at the world, not the corner
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(16f, 66f, 220f, 5f), Texture2D.whiteTexture);
                GUI.color = heldSpell.colour;
                // clamped: the fraction can exceed 1 if the window is ever extended at runtime, and an
                // unclamped bar then paints straight across the screen
                float fraction = Mathf.Clamp01(left / Mathf.Max(0.01f, aimWindow));
                GUI.DrawTexture(new Rect(16f, 66f, 220f * fraction, 5f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else if (Time.time < headlineUntil)
            {
                bigStyle.normal.textColor = headlineColour;
                GUI.Label(new Rect(14f, 10f, 700f, 40f), headline, bigStyle);
            }

            // --- below that: diagnostics, for us rather than for the player ---
            smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

            if (Time.time - lastResultTime <= 2.5f)
                GUI.Label(new Rect(16f, 86f, 900f, 20f), lastResult, smallStyle);

            if (logCasts)
                GUI.Label(new Rect(16f, 104f, 900f, 20f),
                          "practice target: " + practiceTarget +
                          "   (F1 uruz/barrier  F2 kenaz/fire  F3 laguz/ice  F4 sowulo/lightning  F5 ehwaz/air" +
                          "   F7 recognizer: " + (segmentRecognition ? "lines+angles" : "$P") + ")",
                          smallStyle);

            DrawSlots();
        }

        /// <summary>
        /// Two boxes, bottom left, above the health bar. Words rather than icons on purpose -- art is
        /// not the question being answered yet, and a placeholder icon would only invite opinions about
        /// the icon instead of about whether banking a spell is any fun.
        /// </summary>
        void DrawSlots()
        {
            const float w = 176f, h = 34f, x = 16f, gap = 6f;
            float bottom = Screen.height - 62f;      // clear of the health bar underneath

            for (int i = 0; i < slots.Length; i++)
            {
                Rect box = new Rect(x, bottom - (slots.Length - i) * (h + gap), w, h);
                SpellSlot slot = slots[i];
                bool selected = selectedSlot == i || windUpFrom == i;
                bool marked = drawing && bankTarget == i;

                // A selected slot has to be obvious at a glance -- it changes what the mouse does, and
                // an input that silently changes meaning is the fastest way to make a game feel broken.
                if (selected || marked)
                {
                    GUI.color = marked ? new Color(1f, 1f, 1f, 0.5f)
                                       : (slot.spell != null ? slot.spell.colour : Color.white);
                    GUI.DrawTexture(new Rect(box.x - 2f, box.y - 2f, box.width + 4f, box.height + 4f),
                                    Texture2D.whiteTexture);
                }

                GUI.color = new Color(0f, 0f, 0f, selected ? 0.8f : 0.55f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = Color.white;

                smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(box.x + 7f, box.y + 2f, 60f, 16f), i == 0 ? "1 / +" : "2 / e", smallStyle);

                string text;
                Color colour;
                float cooling = slot.readyAt - Time.time;

                if (slot.spell != null)
                {
                    text = slot.spell.displayName;
                    colour = slot.spell.colour;
                }
                else if (cooling > 0f)
                {
                    text = Mathf.CeilToInt(cooling) + "s";
                    colour = new Color(0.65f, 0.65f, 0.7f);
                }
                else
                {
                    text = "empty";
                    colour = new Color(0.55f, 0.55f, 0.6f);
                }

                slotStyle.normal.textColor = colour;
                GUI.Label(new Rect(box.x + 7f, box.y + 12f, w - 14f, 22f), text, slotStyle);

                // a drained bar makes the wait readable without staring at the number
                if (slot.spell == null && cooling > 0f)
                {
                    float f = Mathf.Clamp01(1f - cooling / Mathf.Max(0.01f, slotCooldown));
                    GUI.color = new Color(0.5f, 0.5f, 0.55f, 0.8f);
                    GUI.DrawTexture(new Rect(box.x, box.yMax - 3f, w * f, 3f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // the wind-up, filling left to right: the same beat the opponent gets to react to
                if (windUpFrom == i)
                {
                    float f = Mathf.Clamp01(1f - (windUpEndsAt - Time.time) / Mathf.Max(0.01f, slotWindUp));
                    GUI.color = slot.spell != null ? slot.spell.colour : Color.white;
                    GUI.DrawTexture(new Rect(box.x, box.yMax - 4f, w * f, 4f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
        }
    }
}
