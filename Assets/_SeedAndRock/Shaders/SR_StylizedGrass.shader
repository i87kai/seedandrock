Shader "SeedAndRock/Stylized Grass"
{
    Properties
    {
        _BaseColor ("Root Tint", Color) = (0.86, 0.92, 0.80, 1)
        _TipColor ("Tip Tint", Color) = (1.05, 1.08, 0.92, 1)
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.16
        _WindFrequency ("Wind Frequency", Range(0.05, 5)) = 1.1
        _GustScale ("Gust Scale", Range(0.001, 0.2)) = 0.03
        _FadeStart ("Fade Start Distance", Range(5, 400)) = 110
        _FadeEnd ("Fade End Distance", Range(5, 500)) = 170
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "SR_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half _WindStrength;
                half _WindFrequency;
                float _GustScale;
                half _FadeStart;
                half _FadeEnd;
                half _LightWrap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half fade : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Wind: a slow travelling gust field modulates a faster per-blade flutter; only the tip moves.
                float gust = SR_ValueNoise(positionWS.xz * _GustScale + _Time.y * float2(0.21, 0.13)) * 2.0 - 1.0;
                float phase = dot(positionWS.xz, float2(0.17, 0.11)) + _Time.y * _WindFrequency;
                float bend = input.uv.y * input.uv.y;
                positionWS.xz += (float2(sin(phase), cos(phase * 1.37)) * 0.5 + float2(gust, gust * 0.6)) * _WindStrength * bend;

                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                float distance = length(_WorldSpaceCameraPos - positionWS);
                output.fade = 1.0h - saturate((distance - _FadeStart) / max(_FadeEnd - _FadeStart, 1.0h));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.fade - 0.001h);
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 albedo = input.color.rgb * lerp(_BaseColor.rgb, _TipColor.rgb, input.uv.y);
                // Blades are thin cards: light them like an upward-facing surface with gentle wrap so both sides match.
                half3 lit = SR_Ambient(albedo, normalWS) + SR_Diffuse(albedo, normalWS, mainLight, _LightWrap);
                lit *= lerp(0.55h, 1.0h, input.uv.y); // darker roots read as depth in dense patches
                half alpha = saturate(input.uv.y * 1.6h + 0.15h) * input.fade;
                return half4(MixFog(lit, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
