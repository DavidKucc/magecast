// Stone for level geometry built out of scaled cubes.
//
// A scaled cube stretches its own UVs over every face, so a 22 m wall and a 1 m post would wear the same
// texture at wildly different sizes. This one projects the pattern from world space along the three axes
// instead (triplanar), so every surface shows the same number of tiles per metre whatever its size --
// which is what lets the map be generated from boxes and still read as built from stone.
//
// Faces that look up take a second colour (moss, dust), and everything darkens a little towards the
// floor, which grounds the blocks without any baked lighting.
Shader "MageCast/WorldStone"
{
    Properties
    {
        _MainTex ("Pattern (grey)", 2D) = "white" {}
        _Color ("Side colour", Color) = (0.5, 0.5, 0.5, 1)
        _TopColor ("Top colour", Color) = (0.5, 0.5, 0.5, 1)
        _Scale ("Metres per texture", Float) = 4
        _Grime ("Darker towards the floor", Range(0, 1)) = 0.3
        _Glossiness ("Smoothness", Range(0, 1)) = 0.1
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _TopColor;
        float _Scale;
        float _Grime;
        half _Glossiness;
        fixed4 _EmissionColor;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 w = pow(abs(IN.worldNormal), 4);
            w /= (w.x + w.y + w.z);
            float3 p = IN.worldPos / _Scale;
            float pattern = tex2D(_MainTex, p.zy).r * w.x
                          + tex2D(_MainTex, p.xz).r * w.y
                          + tex2D(_MainTex, p.xy).r * w.z;

            float up = saturate(IN.worldNormal.y);
            fixed3 colour = lerp(_Color.rgb, _TopColor.rgb, up * up);
            float grime = lerp(1 - _Grime, 1, saturate((IN.worldPos.y + 1.2) / 4));

            o.Albedo = colour * pattern * grime;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
