Shader "SeedAndRock/Stylized Environment"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _VariationStrength ("Position Variation", Range(0, 0.5)) = 0.12
        _VariationScale ("Position Variation Scale", Range(0.001, 0.5)) = 0.04
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.32
        _SkyLight ("Sky Light", Range(0, 1)) = 0.18
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "SR_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _VariationStrength;
                float _VariationScale;
                half _LightWrap;
                half _SkyLight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
            };

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
                // Vertex colours are authored per face by the mesh builder; a slow world-space variation
                // keeps clusters of identical props from reading as copies.
                half variation = SR_ValueNoise(input.positionWS.xz * _VariationScale) - 0.5h;
                half3 albedo = input.color.rgb * _BaseColor.rgb * (1.0h + variation * _VariationStrength);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half occlusion = 1.0h;
#if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionHCS));
                occlusion = ao.indirectAmbientOcclusion;
                mainLight.shadowAttenuation *= ao.directAmbientOcclusion;
#endif

                half3 lit = SR_Ambient(albedo, normalWS) * occlusion;
                lit += SR_Diffuse(albedo, normalWS, mainLight, _LightWrap);
                lit += albedo * saturate(normalWS.y) * _SkyLight * occlusion;
                lit += SR_AdditionalLights(albedo, normalWS, input.positionWS);
                return half4(MixFog(lit, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex SR_ShadowVertex
            #pragma fragment SR_ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "SR_Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex SR_DepthVertex
            #pragma fragment SR_DepthOnlyFragment
            #include "SR_Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex SR_DepthVertex
            #pragma fragment SR_DepthNormalsFragment
            #include "SR_Passes.hlsl"
            ENDHLSL
        }
    }
}
