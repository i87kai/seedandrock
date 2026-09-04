Shader "SeedAndRock/Stylized Grass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.22, 0.52, 0.12, 1)
        _TipColor ("Tip Color", Color) = (0.62, 0.82, 0.25, 1)
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.22
        _WindFrequency ("Wind Frequency", Range(0.05, 5)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half _WindStrength;
                half _WindFrequency;
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
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float phase = dot(positionOS.xz, float2(0.17, 0.11)) + _Time.y * _WindFrequency;
                positionOS.xz += float2(sin(phase), cos(phase * 1.37)) * (_WindStrength * input.uv.y * input.uv.y);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                Light lightData = GetMainLight();
                half facing = saturate(dot(normalWS, lightData.direction) * 0.5h + 0.5h);
                half3 albedo = lerp(_BaseColor.rgb * input.color.rgb, _TipColor.rgb, input.uv.y);
                half3 color = albedo * (SampleSH(normalWS) + lightData.color * (0.35h + facing * 0.65h));
                return half4(MixFog(color, input.fogFactor), saturate(input.uv.y * 1.15h));
            }
            ENDHLSL
        }
    }
}