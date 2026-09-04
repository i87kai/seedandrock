Shader "SeedAndRock/Stylized Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.10, 0.70, 0.67, 0.72)
        _DeepColor ("Deep Color", Color) = (0.03, 0.20, 0.40, 0.84)
        _FoamColor ("Foam Color", Color) = (0.85, 0.98, 0.88, 0.8)
        _DepthDistance ("Depth Distance", Range(0.1, 20)) = 5
        _WaveAmplitude ("Wave Amplitude", Range(0, 1)) = 0.22
        _WaveFrequency ("Wave Frequency", Range(0.01, 2)) = 0.22
        _WaveSpeed ("Wave Speed", Range(0, 4)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half _DepthDistance;
                half _WaveAmplitude;
                half _WaveFrequency;
                half _WaveSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float phaseA = dot(positionOS.xz, float2(1.0, 0.73)) * _WaveFrequency + _Time.y * _WaveSpeed;
                float phaseB = dot(positionOS.xz, float2(-0.44, 1.0)) * (_WaveFrequency * 1.7) + _Time.y * (_WaveSpeed * 1.31);
                positionOS.y += (sin(phaseA) + cos(phaseB) * 0.55) * _WaveAmplitude;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = input.screenPos.w;
                half depth01 = saturate((sceneDepth - surfaceDepth) / max(_DepthDistance, 0.01h));
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                half foam = saturate((1.0h - depth01) * 2.4h);
                waterColor = lerp(waterColor, _FoamColor.rgb, foam * 0.75h);
                Light mainLight = GetMainLight();
                half sparkle = pow(saturate(dot(normalize(_WorldSpaceCameraPos - input.positionWS), mainLight.direction)), 22.0h) * 0.2h;
                waterColor += mainLight.color * sparkle;
                return half4(MixFog(waterColor, input.fogFactor), lerp(_ShallowColor.a, _DeepColor.a, depth01));
            }
            ENDHLSL
        }
    }
}