// The Sanctum's sky: a painted dusk in the colours of the spell effects -- deep violet overhead, through
// rose to a warm amber band at the horizon, a soft sun low in it, a few stars coming out up high, and
// long thin bands of cloud catching the light. Everything is worked out from the view direction, so it
// costs one pass and no textures.
Shader "MageCast/SanctumSky"
{
    Properties
    {
        _Zenith ("Overhead", Color) = (0.13, 0.11, 0.30, 1)
        _Middle ("Middle", Color) = (0.46, 0.30, 0.56, 1)
        _Horizon ("Horizon", Color) = (1.0, 0.62, 0.42, 1)
        _Below ("Below the horizon", Color) = (0.20, 0.16, 0.28, 1)
        _SunColour ("Sun", Color) = (1.0, 0.78, 0.55, 1)
        _SunDirection ("Sun direction", Vector) = (-0.5, 0.25, 0.8, 0)
        _SunSize ("Sun size (1 - cos of its radius)", Range(0.0001, 0.02)) = 0.0012
        _Stars ("Stars", Range(0, 2)) = 0.8
        _CloudColour ("Cloud", Color) = (1.0, 0.70, 0.62, 1)
        _Clouds ("Clouds", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith, _Middle, _Horizon, _Below, _SunColour, _CloudColour;
            float4 _SunDirection;
            float _SunSize, _Stars, _Clouds;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                float a = hash(float3(i, 0)), b = hash(float3(i + float2(1, 0), 0));
                float c = hash(float3(i + float2(0, 1), 0)), d = hash(float3(i + float2(1, 1), 0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                fixed3 sky;
                if (h >= 0)
                {
                    float t = pow(saturate(h), 0.55);
                    sky = lerp(_Horizon.rgb, _Middle.rgb, smoothstep(0.0, 0.45, t));
                    sky = lerp(sky, _Zenith.rgb, smoothstep(0.4, 1.0, t));
                }
                else sky = lerp(_Horizon.rgb * 0.7, _Below.rgb, smoothstep(0.0, 0.25, -h));

                // the sun: a soft disc and a wide glow round it, warming the sky on its side
                float3 sd = normalize(_SunDirection.xyz);
                float c = dot(d, sd);
                float glow = pow(saturate(c), 6) * 0.28 + pow(saturate(c), 90) * 0.45;
                float disc = smoothstep(1 - _SunSize, 1 - _SunSize * 0.5, c);
                sky += _SunColour.rgb * (glow * saturate(1 - h * 1.2) + disc * 0.95);

                // thin bands of cloud near the horizon, lit on the sun's side
                if (h > -0.02)
                {
                    float2 cp = d.xz / max(0.08, h + 0.12);
                    float band = noise(cp * float2(0.6, 2.4) + 3.1) * 0.6 + noise(cp * float2(1.4, 5.0)) * 0.4;
                    float cloud = smoothstep(0.55, 0.85, band) * smoothstep(0.55, 0.06, h) * smoothstep(-0.02, 0.05, h);
                    fixed3 lit = lerp(_Middle.rgb * 0.9, _CloudColour.rgb, saturate(c * 0.5 + 0.5));
                    sky = lerp(sky, lit, cloud * _Clouds);
                }

                // stars, only where the sky is dark
                if (h > 0.2)
                {
                    float3 cell = floor(d * 160);
                    float s = hash(cell);
                    float star = step(0.9965, s) * smoothstep(0.2, 0.75, h) * (0.6 + 0.4 * hash(cell + 7));
                    sky += star * _Stars;
                }
                return fixed4(sky, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
