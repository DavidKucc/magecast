// Trees and bushes in the painted look of the maps: no texture, the colour worked out from where a point
// sits. Shade low in the crown and on its underside, light on top, soft patches of a third colour across
// it, and the light wrapped round the crown so its dark side is a deep colour rather than black. The
// tree's mesh carries in its vertex colour how high a point sits in the crown (red, 0-1) and a random
// shade per tree (green), so the trees survive static batching, which merges them into one mesh.
Shader "MageCast/Foliage"
{
    Properties
    {
        _Shade ("Shade (low, inside)", Color) = (0.10, 0.20, 0.12, 1)
        _Light ("Light (high, outside)", Color) = (0.48, 0.64, 0.26, 1)
        _Patch ("Patches", Color) = (0.30, 0.50, 0.22, 1)
        _PatchScale ("Patch size (m)", Float) = 1.6
        _Wrap ("Light wrapped round", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Wrap fullforwardshadows
        #pragma target 3.0

        fixed4 _Shade, _Light, _Patch;
        float _PatchScale, _Wrap;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float4 color : COLOR;
        };

        half4 LightingWrap(SurfaceOutput s, half3 lightDir, half atten)
        {
            half diff = saturate((dot(s.Normal, lightDir) + _Wrap) / (1 + _Wrap));
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (diff * atten);
            c.a = s.Alpha;
            return c;
        }

        float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            float up = saturate(IN.worldNormal.y * 0.5 + 0.5);
            fixed3 colour = lerp(_Shade.rgb, _Light.rgb, saturate(IN.color.r * 0.55 + up * 0.6 - 0.15));
            float3 q = IN.worldPos / _PatchScale;
            float patch = noise(q.xz + q.y * 0.7) * 0.6 + noise(q.xy * 1.7 + q.z) * 0.4;
            colour = lerp(colour, _Patch.rgb, smoothstep(0.5, 0.8, patch) * 0.65);
            o.Albedo = colour * (0.86 + 0.28 * IN.color.g);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
