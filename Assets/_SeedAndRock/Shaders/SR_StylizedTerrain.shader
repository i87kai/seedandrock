Shader "SeedAndRock/Stylized Terrain"
{
    Properties
    {
        _LowColor ("Lowland Color", Color) = (0.20, 0.38, 0.13, 1)
        _HighColor ("Sunlit Grass Color", Color) = (0.50, 0.68, 0.24, 1)
        _RockColor ("Rock Color", Color) = (0.34, 0.33, 0.30, 1)
        _SlopeBlend ("Slope Rock Blend", Range(0, 1)) = 0.58
        _MacroScale ("Macro Variation Scale", Range(0.005, 0.2)) = 0.035
        _MacroStrength ("Macro Variation Strength", Range(0, 0.5)) = 0.18
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
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LowColor;
                half4 _HighColor;
                half4 _RockColor;
                half _SlopeBlend;
                half _MacroScale;
                half _MacroStrength;
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
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float variation = sin(input.positionWS.x * _MacroScale + input.positionWS.z * (_MacroScale * 0.72)) * 0.5 + 0.5;
                half heightBlend = saturate(input.color.a + (variation - 0.5) * _MacroStrength);
                // Terrain vertices supply a local biome tint; interpolated vertex colour keeps
                // biome borders soft rather than producing disconnected hard-edged patches.
                half3 biomeColor = input.color.rgb;
                half3 albedo = lerp(biomeColor * 0.72h, biomeColor * 1.18h, heightBlend);
                half slope = saturate((1.0h - normalWS.y - _SlopeBlend) * 3.0h);
                albedo = lerp(albedo, _RockColor.rgb, slope);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 lit = albedo * (ambient + mainLight.color * (0.18h + ndotl * 0.82h) * mainLight.shadowAttenuation);
                return half4(MixFog(lit, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
