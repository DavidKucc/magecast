// Painted paving for ground built out of scaled cubes (the Ruins).
//
// WorldStone's idea with a colour texture: the picture is laid from world space along the three axes, so
// every slab of ground shows the same stones at the same size whatever the box under it measures, and
// two boxes side by side carry on each other's pattern. Big soft patches lighten and darken it, or a
// 3 m picture repeated over a 50 m square reads as wallpaper.
Shader "MageCast/WorldPaving"
{
    Properties
    {
        _MainTex ("Paving", 2D) = "white" {}
        _Color ("Tint, tops", Color) = (1, 1, 1, 1)
        _SideColor ("Tint, sides", Color) = (0.8, 0.8, 0.8, 1)
        _Scale ("Metres per texture", Float) = 3
        _PatchScale ("Patch size (m)", Float) = 9
        _Patch ("Patch strength", Range(0, 0.5)) = 0.14
        _Glossiness ("Smoothness", Range(0, 1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _SideColor;
        float _Scale, _PatchScale, _Patch;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 w = pow(abs(IN.worldNormal), 4);
            w /= (w.x + w.y + w.z);
            float3 p = IN.worldPos / _Scale;
            fixed3 c = tex2D(_MainTex, p.zy).rgb * w.x
                     + tex2D(_MainTex, p.xz).rgb * w.y
                     + tex2D(_MainTex, p.xy).rgb * w.z;

            float3 q = IN.worldPos / _PatchScale;
            float patch = noise(q.xz + q.y * 0.7) + 0.5 * noise(q.xz * 2.3 + 11.0);
            c *= 1 + (patch / 1.5 - 0.5) * 2 * _Patch;

            float up = saturate(IN.worldNormal.y);
            o.Albedo = c * lerp(_SideColor.rgb, _Color.rgb, up * up);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
