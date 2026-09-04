Shader "SeedAndRock/Stylized Environment"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.35, 0.55, 0.22, 1)
        _AccentColor ("Accent Color", Color) = (0.55, 0.72, 0.30, 1)
        _AccentStrength ("Accent Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AccentColor;
                half _AccentStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; half fogFactor : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 albedo = lerp(_BaseColor.rgb, _AccentColor.rgb, input.color.g * _AccentStrength);
                Light lightData = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lit = albedo * (SampleSH(normalWS) + lightData.color * (0.18h + saturate(dot(normalWS, lightData.direction)) * 0.82h) * lightData.shadowAttenuation);
                return half4(MixFog(lit, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
}