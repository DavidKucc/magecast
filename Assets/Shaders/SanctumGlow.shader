// Light painted onto the Sanctum: rune lines on the floor and walls, halos under them, the glow round a
// crystal. Additive, no texture: a line (LineRenderer, its width across uv.y) is brightest down its
// middle and falls off softly to its sides; a quad in radial mode (_Radial 1) is a round soft halo.
// Colour times vertex colour, so one material serves every element's colour, and _Boost lets the
// runes be lit up (RuneGlow) beyond their resting brightness.
Shader "MageCast/SanctumGlow"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.3, 1.4, 1.3, 1)
        _Radial ("Round halo (1) or line (0)", Float) = 0
        _Softness ("Falloff power", Range(0.5, 8)) = 2
        _Core ("Bright core", Range(0, 2)) = 0.6
        _Boost ("Boost", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Radial, _Softness, _Core, _Boost;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 colour : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 colour : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.colour = v.colour;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = _Radial > 0.5 ? saturate(length(i.uv * 2 - 1)) : abs(i.uv.y * 2 - 1);
                float soft = pow(saturate(1 - r), _Softness);
                float core = pow(saturate(1 - r), _Softness * 4) * _Core;
                return fixed4(_Color.rgb * i.colour.rgb * i.colour.a * (soft + core) * _Boost, 1);
            }
            ENDCG
        }
    }
}
