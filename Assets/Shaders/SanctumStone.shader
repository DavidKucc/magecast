// Stone for the Sanctum (the first map), built out of scaled cubes, in the painted look of the spell
// effects: WorldStone's triplanar pattern, plus what a painter puts on a block -- its edges caught
// lighter, as if worn round and lit, broken up by the pattern so they read as chipped rather than
// drawn; a second, cooler colour wandering over the sides in big patches; tops that take moss or
// dust; and the foot darker where it meets the floor.
//
// The edges are found from the box itself: every block is a unit cube scaled by its transform, so how
// far a point is from the nearest edge, in metres, is (0.5 - |object position|) times the scale on each
// axis -- the two smallest of those on a face. That needs the object's own matrix, so these blocks must
// not be batched (DisableBatching, and the builder leaves them out of static batching).
Shader "MageCast/SanctumStone"
{
    Properties
    {
        _MainTex ("Pattern (grey)", 2D) = "white" {}
        _Color ("Side colour", Color) = (0.6, 0.55, 0.5, 1)
        _Tint2 ("Second side colour (patches)", Color) = (0.55, 0.52, 0.56, 1)
        _TopColor ("Top colour", Color) = (0.6, 0.6, 0.5, 1)
        _EdgeColor ("Edge colour", Color) = (0.95, 0.88, 0.76, 1)
        _EdgeWidth ("Edge width (m), 0 = none", Float) = 0.1
        _Scale ("Metres per texture", Float) = 4
        _PatchScale ("Patch size (m)", Float) = 7
        _Grime ("Darker towards the floor", Range(0, 1)) = 0.3
        _Glossiness ("Smoothness", Range(0, 1)) = 0.1
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "DisableBatching" = "True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _Tint2, _TopColor, _EdgeColor;
        float _EdgeWidth, _Scale, _PatchScale, _Grime;
        half _Glossiness;
        fixed4 _EmissionColor;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float3 objPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
        }

        // cheap smooth value noise, for the big colour patches
        float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.worldNormal);
            float3 w = pow(abs(n), 4);
            w /= (w.x + w.y + w.z);
            float3 p = IN.worldPos / _Scale;
            float pattern = tex2D(_MainTex, p.zy).r * w.x
                          + tex2D(_MainTex, p.xz).r * w.y
                          + tex2D(_MainTex, p.xy).r * w.z;

            // big soft patches of a second colour over the sides
            float3 q = IN.worldPos / _PatchScale;
            float patch = noise(q.xz + q.y * 0.7) * w.y + noise(q.zy) * w.x + noise(q.xy) * w.z;
            fixed3 side = lerp(_Color.rgb, _Tint2.rgb, smoothstep(0.35, 0.75, patch));

            float up = saturate(n.y);
            fixed3 colour = lerp(side, _TopColor.rgb, up * up);

            // worn, lit edges
            if (_EdgeWidth > 0.001)
            {
                float3 s = float3(length(unity_ObjectToWorld._m00_m10_m20),
                                  length(unity_ObjectToWorld._m01_m11_m21),
                                  length(unity_ObjectToWorld._m02_m12_m22));
                float3 e = (0.5 - abs(IN.objPos)) * s;
                float lo = min(e.x, min(e.y, e.z)), hi = max(e.x, max(e.y, e.z));
                float edge = e.x + e.y + e.z - lo - hi;           // the second smallest: the nearest edge on this face
                float width = _EdgeWidth * (0.55 + 0.9 * pattern);
                float worn = 1 - smoothstep(width * 0.35, width, edge);
                colour = lerp(colour, _EdgeColor.rgb * (0.85 + 0.3 * pattern), worn * 0.85);
            }

            float grime = lerp(1 - _Grime, 1, saturate((IN.worldPos.y + 0.6) / 3.2));
            o.Albedo = colour * pattern * grime;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
