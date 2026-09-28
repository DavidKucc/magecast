// The barrier: a pane of energy rather than a tinted slab of glass.
//
// Drawn on a quad whose UV runs 0-1 across and up; _Size gives it in metres so the pattern keeps its
// size on a wall of any width. Everything is worked out here -- no textures -- so it needs nothing from
// the bought effect packs and lives in the public repository.
//
//  - a hex lattice, faint, with energy flowing up through it
//  - a brighter frame and a glow where it meets the floor, so its extent reads at a glance
//  - hits: a flash and a ring running out from where each spell struck (CastShield feeds _Hits)
//  - wear: as _Strength falls, holes open with burning rims, and near the end it flickers --
//    the opponent can see that one more fireball will do it
//  - _Rise draws it up out of the floor when it appears
Shader "MageCast/EnergyWall"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.85, 0.4, 1)
        _Strength ("Strength left", Range(0, 1)) = 1
        _Rise ("Risen", Range(0, 1)) = 1
        _Size ("Size in metres (x, y)", Vector) = (2.8, 2.2, 0, 0)
        // 1 for the shards a broken wall flies apart into: particles, faded by their vertex colour
        _UseVertexColor ("Use vertex colour", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        // premultiplied: tints what is behind it a little and glows on top
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Strength;
            float _Rise;
            float4 _Size;
            // up to four recent hits: xy = where (UV), z = when (_Time.y), w = how hard (0-1)
            float4 _Hits[4];
            float _UseVertexColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = lerp(fixed4(1, 1, 1, 1), v.color, _UseVertexColor);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 3; k++) { v += a * noise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            // distance from a point to the nearest edge of its hexagonal cell (cells 1 across)
            float hexEdge(float2 p)
            {
                const float2 s = float2(1.0, 1.7320508);
                float4 hc = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
                float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
                float2 q = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? h.xy : h.zw;
                q = abs(q);
                return 0.5 - max(dot(q, s * 0.5), q.x);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 m = uv * _Size.xy;
                float t = _Time.y;

                // rising out of the floor, with a bright leading edge
                float top = _Rise * 1.05;
                clip(top - uv.y);
                float lead = (1.0 - saturate((top - uv.y) * _Size.y * 6.0)) * step(_Rise, 0.999);

                float lattice = 1.0 - smoothstep(0.0, 0.06, hexEdge(m * 3.0));
                float flow = fbm(float2(m.x * 1.4, m.y * 1.4 - t * 0.9));
                float shimmer = fbm(float2(m.x * 4.0 + t * 0.3, m.y * 4.0 - t * 1.7));

                float2 fromEdge = min(uv, 1.0 - uv) * _Size.xy;
                float frame = 1.0 - smoothstep(0.0, 0.14, min(fromEdge.x, fromEdge.y));
                float ground = 1.0 - smoothstep(0.0, 0.45, m.y);

                // hits: a flash where it struck and a ring running out from it
                float hit = 0.0;
                for (int k = 0; k < 4; k++)
                {
                    float age = t - _Hits[k].z;
                    if (age >= 0.0 && age < 0.7)
                    {
                        float r = distance(m, _Hits[k].xy * _Size.xy);
                        float ring = 1.0 - smoothstep(0.0, 0.14, abs(r - age * 4.5));
                        float core = saturate(1.0 - r / 0.6) * (1.0 - saturate(age * 4.0));
                        hit += (ring * 0.8 + core * 1.6) * (1.0 - age / 0.7) * _Hits[k].w;
                    }
                }

                // wear: holes eaten through it, their rims burning
                float n = fbm(m * 1.2 + 7.3);
                // never so eaten that it stops looking like cover: it blocks until the moment it breaks
                float eaten = (1.0 - _Strength) * 0.42;
                float hole = step(n, eaten);
                float rim = (1.0 - smoothstep(0.0, 0.07, n - eaten)) * step(0.02, eaten) * (1.0 - hole);

                float flicker = _Strength < 0.3 ? 0.7 + 0.3 * sin(t * 41.0) * sin(t * 23.0) : 1.0;

                float a = 0.07 + lattice * 0.28 + flow * 0.16 + shimmer * lattice * 0.25
                        + frame * 0.55 + ground * 0.35 + hit + rim * 0.9 + lead * 1.4;
                a *= (1.0 - hole) * flicker * lerp(0.5, 1.0, _Strength);
                // softly thinner towards the top, so it reads as rising energy and not a board
                a *= lerp(1.0, 0.6, smoothstep(0.55, 1.0, uv.y));
                a = saturate(a);

                a *= i.color.a;
                float3 col = _Color.rgb * i.color.rgb * (1.0 + hit * 1.5 + rim * 1.5 + lead + frame * 0.4);
                return fixed4(col * a, a * 0.6);
            }
            ENDCG
        }
    }
    Fallback Off
}
