Shader "SeedAndRock/Stylized Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.30, 0.66, 0.62, 0.55)
        _DeepColor ("Deep Color", Color) = (0.04, 0.20, 0.34, 0.92)
        _FoamColor ("Foam Color", Color) = (0.86, 0.95, 0.92, 1)
        _SkyTint ("Reflection Tint", Color) = (0.72, 0.84, 0.92, 1)
        _DepthDistance ("Depth Fade Distance", Range(0.1, 20)) = 4.5
        _FoamDistance ("Shore Foam Distance", Range(0.02, 3)) = 0.55
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.55
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.5)) = 0.05
        _WaveFrequency ("Wave Frequency", Range(0.01, 2)) = 0.35
        _WaveSpeed ("Wave Speed", Range(0, 4)) = 0.55
        _RippleScale ("Ripple Scale", Range(0.02, 2)) = 0.28
        _RippleStrength ("Ripple Normal Strength", Range(0, 1)) = 0.35
        _Refraction ("Refraction Strength", Range(0, 0.2)) = 0.035
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.45
        _Specular ("Sun Specular", Range(0, 2)) = 0.6
        _Smoothness ("Sun Smoothness", Range(8, 512)) = 180
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "SR_Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _SkyTint;
                half _DepthDistance;
                half _FoamDistance;
                half _FoamStrength;
                half _WaveAmplitude;
                half _WaveFrequency;
                half _WaveSpeed;
                half _RippleScale;
                half _RippleStrength;
                half _Refraction;
                half _ReflectionStrength;
                half _Specular;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;      // world xz
                float4 info : TEXCOORD1;    // x depth at vertex, y river flow strength
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half2 info : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _WaveSpeed;
                float phaseA = dot(positionWS.xz, float2(1.0, 0.73)) * _WaveFrequency + time;
                float phaseB = dot(positionWS.xz, float2(-0.44, 1.0)) * (_WaveFrequency * 1.7) + time * 1.31;
                // Rivers stay flat so their surface never pops above carved banks; lakes and sea get gentle swell.
                float swell = (1.0 - saturate(input.info.y * 1.5)) * saturate(input.info.x * 0.6);
                positionWS.y += (sin(phaseA) + cos(phaseB) * 0.55) * _WaveAmplitude * swell;

                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.info = half2(input.info.x, input.info.y);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half3 RippleNormal(float2 p, half flow)
            {
                float t = _Time.y * (0.35 + flow * 0.9);
                float2 flowDir = float2(0.6, 0.8) * flow * t * 0.6;
                float2 q = p * _RippleScale;
                float e = 0.12;
                float h0 = SR_Fbm(q + float2(t * 0.11, t * 0.07) - flowDir, 2) + SR_Fbm(q * 1.9 - float2(t * 0.09, -t * 0.12), 2) * 0.5;
                float hx = SR_Fbm(q + float2(e, 0) + float2(t * 0.11, t * 0.07) - flowDir, 2) + SR_Fbm((q + float2(e, 0)) * 1.9 - float2(t * 0.09, -t * 0.12), 2) * 0.5;
                float hz = SR_Fbm(q + float2(0, e) + float2(t * 0.11, t * 0.07) - flowDir, 2) + SR_Fbm((q + float2(0, e)) * 1.9 - float2(t * 0.09, -t * 0.12), 2) * 0.5;
                half2 slope = half2(hx - h0, hz - h0) / e;
                return normalize(half3(-slope.x * _RippleStrength, 1.0h, -slope.y * _RippleStrength));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = input.screenPos.w;
                float viewDepth = max(sceneDepth - surfaceDepth, 0.0);

                half3 normalWS = RippleNormal(input.positionWS.xz, input.info.y);
                half3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                // Refraction: offset the opaque colour lookup by the ripple normal, scaled by depth so shores stay crisp.
                float2 refractedUV = screenUV + normalWS.xz * _Refraction * saturate(viewDepth * 0.5);
                float refractedDepth = LinearEyeDepth(SampleSceneDepth(refractedUV), _ZBufferParams);
                if (refractedDepth < surfaceDepth) refractedUV = screenUV; // never refract objects in front of the water
                half3 background = SampleSceneColor(refractedUV);

                // Depth colouring blends camera-relative depth with the vertex depth hint so oblique views still read well.
                half depth01 = 1.0h - exp(-viewDepth / max(_DepthDistance, 0.01h));
                half vertexDepth01 = 1.0h - exp(-input.info.x / max(_DepthDistance, 0.01h));
                depth01 = max(depth01, vertexDepth01 * 0.6h);
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                half absorption = lerp(_ShallowColor.a, _DeepColor.a, depth01);
                half3 color = lerp(background, waterColor, absorption);

                // Reflection cue from the environment probe / sky with a Fresnel falloff.
                half3 reflectDir = reflect(-viewDir, normalWS);
                half3 reflection = GlossyEnvironmentReflection(reflectDir, 0.15h, 1.0h) * _SkyTint.rgb;
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDir)), 4.0h);
                color = lerp(color, reflection, saturate(fresnel * _ReflectionStrength + 0.04h));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half specular = pow(saturate(dot(normalWS, halfDir)), _Smoothness) * _Specular;
                color += mainLight.color * specular * mainLight.shadowAttenuation;
                color *= lerp(0.72h, 1.0h, mainLight.shadowAttenuation);

                // Restrained foam: shoreline contact plus streaks where rivers run.
                float foamNoise = SR_Fbm(input.positionWS.xz * 0.9 + float2(_Time.y * 0.18, -_Time.y * 0.11), 2);
                half shoreFoam = saturate(1.0h - viewDepth / max(_FoamDistance, 0.01h)) * saturate(foamNoise * 1.6h - 0.25h);
                half flowFoam = saturate(input.info.y * 1.4h - 0.55h) * saturate(SR_ValueNoise(input.positionWS.xz * 1.6 + float2(0, -_Time.y * 1.4)) * 1.3h - 0.65h);
                half foam = saturate(shoreFoam + flowFoam) * _FoamStrength;
                color = lerp(color, _FoamColor.rgb, foam);

                half alpha = saturate(max(absorption, 0.35h) + foam * 0.5h + fresnel * 0.2h);
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
