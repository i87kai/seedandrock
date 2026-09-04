Shader "SeedAndRock/Stylized Terrain"
{
    Properties
    {
        _RockColor ("Rock Color", Color) = (0.36, 0.35, 0.32, 1)
        _RockShadowColor ("Rock Crevice Color", Color) = (0.22, 0.21, 0.20, 1)
        _SnowColor ("Snow Color", Color) = (0.90, 0.94, 0.97, 1)
        _SandColor ("Sand Color", Color) = (0.78, 0.68, 0.46, 1)
        _WetDarkening ("Wet Bank Darkening", Range(0, 1)) = 0.42
        _SlopeBlend ("Slope Rock Start", Range(0, 1)) = 0.52
        _SlopeSharpness ("Slope Rock Sharpness", Range(1, 12)) = 4.5
        _MacroScale ("Macro Variation Scale", Range(0.001, 0.2)) = 0.012
        _MacroStrength ("Macro Variation Strength", Range(0, 0.5)) = 0.16
        _DetailScale ("Detail Variation Scale", Range(0.05, 4)) = 0.55
        _DetailStrength ("Detail Variation Strength", Range(0, 0.5)) = 0.10
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.28
        _WetSpecular ("Wet Specular", Range(0, 1)) = 0.25
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
                half4 _RockColor;
                half4 _RockShadowColor;
                half4 _SnowColor;
                half4 _SandColor;
                half _WetDarkening;
                half _SlopeBlend;
                half _SlopeSharpness;
                float _MacroScale;
                half _MacroStrength;
                float _DetailScale;
                half _DetailStrength;
                half _LightWrap;
                half _WetSpecular;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float4 surface : TEXCOORD1; // x wetness, y snow, z sand, w rockiness
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half4 surface : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.surface = input.surface;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float2 worldXZ = input.positionWS.xz;

                // Large and small scale value-noise variation keeps wide plains from reading as flat colour.
                half macro = SR_Fbm(worldXZ * _MacroScale, 3);
                half detail = SR_ValueNoise(worldXZ * _DetailScale);
                half elevation = saturate(input.color.a + (macro - 0.5h) * _MacroStrength);

                half3 biomeColor = input.color.rgb;
                half3 albedo = lerp(biomeColor * 0.74h, biomeColor * 1.16h, elevation);
                albedo *= 1.0h + (detail - 0.5h) * _DetailStrength;

                // Sand near warm shores / deserts, driven by the generator.
                albedo = lerp(albedo, _SandColor.rgb * (0.9h + macro * 0.2h), input.surface.z);

                // Slope-based rock with crevice darkening; steeper faces and rocky biomes go grey.
                half slope = 1.0h - normalWS.y;
                half rockMask = saturate((slope - _SlopeBlend * 0.5h) * _SlopeSharpness);
                rockMask = saturate(rockMask + input.surface.w * 0.75h);
                half3 rock = lerp(_RockShadowColor.rgb, _RockColor.rgb, saturate(macro * 0.6h + detail * 0.6h));
                albedo = lerp(albedo, rock, rockMask);

                // Snow settles on flatter ground; steep rock keeps peeking through.
                half snowMask = saturate(input.surface.y * saturate(normalWS.y * 1.6h - 0.25h) + input.surface.y * 0.25h);
                albedo = lerp(albedo, _SnowColor.rgb * (0.94h + detail * 0.06h), snowMask);

                // Wet river and lake banks are darker and slightly glossy.
                half wet = input.surface.x * (1.0h - snowMask * 0.7h);
                albedo *= 1.0h - wet * _WetDarkening;

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
                lit += SR_AdditionalLights(albedo, normalWS, input.positionWS);

                half3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half specular = pow(saturate(dot(normalWS, halfDir)), 48.0h) * wet * _WetSpecular;
                lit += mainLight.color * specular * mainLight.shadowAttenuation;

                return half4(MixFog(lit, input.fogFactor), 1.0h);
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
