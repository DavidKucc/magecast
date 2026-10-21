// Hills and mountains far beyond a map's walls: a silhouette, not a place. No texture -- a colour from the
// foot up to the peaks, snow above a line, the sun's side a little lighter, and the whole of it faded
// towards the colour of the sky at the horizon, so it sits back in the haze behind everything near.
Shader "MageCast/Distant"
{
    Properties
    {
        _Base ("Foot", Color) = (0.30, 0.36, 0.30, 1)
        _Top ("Upper slopes", Color) = (0.42, 0.46, 0.44, 1)
        _Snow ("Snow", Color) = (0.92, 0.94, 1.0, 1)
        _SnowLine ("Snow from (m)", Float) = 10000
        _Height ("Height of the peaks (m)", Float) = 60
        _Haze ("Haze", Color) = (0.70, 0.76, 0.86, 1)
        _HazeAmount ("Haze amount", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Base, _Top, _Snow, _Haze;
            float _SnowLine, _Height, _HazeAmount;

            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float h = saturate(i.world.y / _Height);
                fixed3 colour = lerp(_Base.rgb, _Top.rgb, h);
                // snow, its edge broken up so it lies in streaks down the gullies
                float streak = noise(i.world.xz * 0.08) * 6 + noise(i.world.xz * 0.3) * 2;
                float snow = smoothstep(_SnowLine - 2, _SnowLine + 3, i.world.y + streak - 4 * (1 - saturate(n.y)));
                colour = lerp(colour, _Snow.rgb, snow);
                float lit = dot(n, normalize(_WorldSpaceLightPos0.xyz)) * 0.5 + 0.72;
                colour *= lit;
                return fixed4(lerp(colour, _Haze.rgb, _HazeAmount), 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
