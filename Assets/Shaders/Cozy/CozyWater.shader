// Cozy Stylized Rendering Framework - stylized water.
// Depth-based shallow/deep gradient, animated procedural normals (no textures),
// fresnel sky reflection (atmosphere-driven, no probe needed), sun specular,
// shore foam + ripple bands, optional lightweight screen-space refraction,
// vertex swells, correct underwater appearance for swimming, URP shadows & fog.
//
// Requires the URP asset to have Depth Texture enabled (and Opaque Texture for
// refraction) - both are already on in Assets/Settings/PC_RPAsset.asset.
Shader "Cozy/Water"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor ("Shallow Color (A = opacity)", Color) = (0.30, 0.80, 0.78, 0.45)
        _DeepColor ("Deep Color (A = opacity)", Color) = (0.05, 0.30, 0.55, 0.92)
        _DepthDistance ("Depth Gradient Distance", Range(0.1, 30)) = 5
        _DistantColor ("Distant Water Color", Color) = (0.12, 0.42, 0.66, 1)
        _DistanceFade ("Distance Fade (m)", Range(20, 600)) = 220
        _UnderwaterColor ("Underwater Tint (seen from below)", Color) = (0.10, 0.42, 0.55, 0.75)
        _Saturation ("Saturation", Range(0, 2)) = 1.1

        [Header(Waves)]
        _WaveAmplitude ("Swell Amplitude", Range(0, 1)) = 0.12
        _WaveLength ("Swell Length", Range(1, 60)) = 14
        _WaveSpeed ("Swell Speed", Range(0, 4)) = 0.8
        _NormalScale ("Ripple Scale", Range(0.02, 2)) = 0.35
        _NormalStrength ("Ripple Strength", Range(0, 2)) = 0.55
        _NormalSpeed ("Ripple Speed", Range(0, 3)) = 0.6
        [Toggle(_COZY_WATER_DETAIL_HIGH)] _DetailHigh ("High Detail Ripples (2 layers + sparkle)", Float) = 1

        [Header(Reflection and Specular)]
        _ReflectionStrength ("Sky Reflection Strength", Range(0, 1)) = 0.5
        _ReflectionMax ("Max Reflection At Grazing Angles", Range(0, 1)) = 0.55
        _ReflectionSkyLift ("Reflect Higher Sky (bluer, less haze)", Range(0, 1)) = 0.45
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4
        _FresnelBias ("Fresnel Bias", Range(0, 0.5)) = 0.04
        _SunSpecularPower ("Sun Specular Power", Range(8, 1024)) = 260
        _SunSpecularStrength ("Sun Specular Strength", Range(0, 4)) = 1.0
        _SunGlow ("Sun Glow In Reflection", Range(0, 1)) = 0.25
        _SparkleStrength ("Sparkle", Range(0, 2)) = 0.5

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (0.95, 1.0, 0.97, 1)
        _FoamDistance ("Shore Foam Distance", Range(0.05, 6)) = 1.6
        _FoamScale ("Foam Noise Scale", Range(0.05, 4)) = 0.9
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.45
        _FoamSoftness ("Foam Softness", Range(0.01, 0.5)) = 0.18
        _FoamBands ("Shore Ripple Bands", Range(0, 1)) = 0.5
        _CrestFoam ("Swell Crest Foam", Range(0, 1)) = 0.25

        [Header(Refraction)]
        [Toggle(_COZY_REFRACTION)] _Refraction ("Refraction (uses Opaque Texture)", Float) = 1
        _RefractionStrength ("Refraction Strength", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 300

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyWaterVertex
            #pragma fragment CozyWaterFragment

            #pragma shader_feature_local _COZY_REFRACTION
            #pragma shader_feature_local _COZY_WATER_DETAIL_HIGH
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "CozyCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #if defined(_COZY_REFRACTION)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                half4 _DistantColor;
                float _DistanceFade;
                half4 _UnderwaterColor;
                half  _Saturation;
                float _WaveAmplitude;
                float _WaveLength;
                float _WaveSpeed;
                float _NormalScale;
                half  _NormalStrength;
                float _NormalSpeed;
                half  _ReflectionStrength;
                half  _ReflectionMax;
                half  _ReflectionSkyLift;
                half  _FresnelPower;
                half  _FresnelBias;
                half  _SunSpecularPower;
                half  _SunSpecularStrength;
                half  _SunGlow;
                half  _SparkleStrength;
                half4 _FoamColor;
                float _FoamDistance;
                float _FoamScale;
                half  _FoamCutoff;
                half  _FoamSoftness;
                half  _FoamBands;
                half  _CrestFoam;
                half  _RefractionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                // x: swell height (-1..1), y: fog factor
                half2  data       : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            // Three summed directional sines: broad swells. Returns height in [-1,1].
            float CozySwell(float2 xz, float t)
            {
                float k = COZY_TWO_PI / max(_WaveLength, 1.0);
                float a = sin(dot(xz, float2(0.86, 0.50)) * k + t * 1.00);
                float b = sin(dot(xz, float2(-0.34, 0.94)) * k * 1.37 + t * 1.23 + 1.7);
                float c = sin(dot(xz, float2(0.62, -0.78)) * k * 2.10 + t * 0.87 + 3.9);
                return a * 0.5 + b * 0.32 + c * 0.18;
            }

            // Procedural ripple height field; sum of scrolling noise layers.
            float CozyRippleHeight(float2 xz, float t)
            {
                float2 p = xz * _NormalScale;
                float h = CozyValueNoise(p + float2(t * 0.9, t * 0.4));
                h += CozyValueNoise(p * 2.13 - float2(t * 0.7, -t * 0.55)) * 0.5;
            #if defined(_COZY_WATER_DETAIL_HIGH)
                h += CozyValueNoise(p * 4.7 + float2(-t * 1.1, t * 0.9)) * 0.25;
                h += CozyValueNoise((p * 0.47 + float2(t * 0.25, -t * 0.2)).yx) * 0.8;
            #endif
                return h;
            }

            half3 CozyRippleNormal(float2 xz, float t, half strength)
            {
                const float e = 0.08;
                float hC = CozyRippleHeight(xz, t);
                float hX = CozyRippleHeight(xz + float2(e, 0.0), t);
                float hZ = CozyRippleHeight(xz + float2(0.0, e), t);
                float2 grad = float2(hX - hC, hZ - hC) / e;
                return normalize(half3(-grad.x * strength, 1.0h, -grad.y * strength));
            }

            Varyings CozyWaterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float swell = CozySwell(positionWS.xz, _Time.y * _WaveSpeed);
                positionWS.y += swell * _WaveAmplitude;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.data = half2(swell, ComputeFogFactor(output.positionCS.z));
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                return output;
            }

            half4 CozyWaterFragment(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                float2 screenUV = CozyScreenUV(input.positionCS);
                float waterEyeDepth = input.positionCS.w;
                float t = _Time.y * _NormalSpeed;

                bool cameraBelow = _WorldSpaceCameraPos.y < positionWS.y - 0.02;

                // 0 near the camera .. 1 far away. Far water must read as one calm, stable colour:
                // ripples, refraction and foam all fade out with it.
                half distance01 = saturate(waterEyeDepth / max(_DistanceFade, 1.0));
                half rippleStrength = _NormalStrength * lerp(1.0h, 0.15h, distance01);
                half3 normalWS = CozyRippleNormal(positionWS.xz, t, rippleStrength);
                if (cameraBelow) normalWS = -normalWS;
                half3 viewDirWS = half3(normalize(_WorldSpaceCameraPos - positionWS));
                half NdotV = saturate(dot(normalWS, viewDirWS));

                // --- Scene depth behind the surface -------------------------------
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthDiff = max(sceneEyeDepth - waterEyeDepth, 0.0);

            #if defined(_COZY_REFRACTION)
                // Distort the opaque texture by the ripple normal; reject samples
                // that would pull objects standing in front of the water surface.
                float2 refrUV = screenUV + normalWS.xz * _RefractionStrength * saturate(depthDiff * 0.5 + 0.2) / (1.0 + waterEyeDepth * 0.05);
                float refrEyeDepth = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                if (refrEyeDepth < waterEyeDepth) refrUV = screenUV; else { sceneEyeDepth = refrEyeDepth; depthDiff = max(refrEyeDepth - waterEyeDepth, 0.0); }
                half3 sceneColor = half3(SampleSceneColor(refrUV));
                // Beyond the far plane the opaque texture only contains the (bright) sky: never let
                // that leak through as "white water". Treat anything past the far plane as deep.
                if (sceneEyeDepth > _ProjectionParams.z * 0.98) depthDiff = 1e4;
            #endif

                half depth01 = 1.0h - exp(-depthDiff / max(_DepthDistance, 0.05) * 2.5);

                // --- Lighting inputs ----------------------------------------------
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 sunDir = CozySunDirection();
                half3 sunColor = CozyAtmosphereReady() ? _CozySunColor.rgb : mainLight.color;
                half shadow = mainLight.shadowAttenuation;

                // --- Water body colour --------------------------------------------
                half3 bodyColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                half bodyAlpha = lerp(_ShallowColor.a, _DeepColor.a, depth01);
                // Distant water: converge on a single readable colour and become opaque.
                bodyColor = lerp(bodyColor, _DistantColor.rgb, distance01);
                bodyAlpha = lerp(bodyAlpha, 1.0h, distance01);
                if (cameraBelow)
                {
                    bodyColor = _UnderwaterColor.rgb;
                    bodyAlpha = _UnderwaterColor.a;
                }
                // Ambient from the sky probe so night water is dark.
                half3 ambient = SampleSH(half3(0, 1, 0));
                bodyColor *= saturate(ambient * 0.6h + sunColor * 0.4h * lerp(0.6h, 1.0h, shadow) + 0.05h);

                // --- Fresnel sky reflection ---------------------------------------
                half fresnel = _FresnelBias + (1.0h - _FresnelBias) * pow(1.0h - NdotV, _FresnelPower);
                half3 reflDir = reflect(-viewDirWS, normalWS);
                // At grazing angles the mirrored direction skims the horizon, which is the hazy,
                // near-white part of the sky gradient. Real water does that; stylized water should
                // stay blue, so lift the sampled direction toward the zenith instead.
                reflDir.y = max(abs(reflDir.y), _ReflectionSkyLift);
                half3 skyColor = CozySampleSkyApprox(reflDir);
                // Sun glow inside the reflection: tight and energy-limited (uses the normalised sun
                // colour, not the HDR light intensity, so it can never blow the surface out to white).
                half sunGlow = pow(saturate(dot(reflDir, sunDir)), 160.0h) * _SunGlow;
                skyColor += saturate(sunColor) * sunGlow;
                half reflectionAmount = min(fresnel * _ReflectionStrength, _ReflectionMax) * (cameraBelow ? 0.3h : 1.0h);

                // --- Specular + sparkle ------------------------------------------
                half3 halfDir = SafeNormalize(sunDir + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), _SunSpecularPower);
                spec = smoothstep(0.05h, 0.35h, spec) * _SunSpecularStrength * shadow;
            #if defined(_COZY_WATER_DETAIL_HIGH)
                float glitter = CozyValueNoise(positionWS.xz * 9.0 + float2(t * 3.0, -t * 2.2));
                glitter = pow(saturate(glitter), 14.0) * 20.0;
                spec += glitter * _SparkleStrength * pow(saturate(dot(normalWS, halfDir)), 32.0h) * shadow * (1.0h - distance01);
            #endif
                spec = min(spec, 1.5h);

                // --- Foam ------------------------------------------------------------
                half shoreMask = 1.0h - saturate(depthDiff / max(_FoamDistance, 0.01));
                float foamNoise = CozyFbm(positionWS.xz * _FoamScale + float2(t * 0.6, t * 0.35), 3);
                half bands = saturate(sin((depthDiff / max(_FoamDistance, 0.01)) * COZY_TWO_PI * 1.5 - _Time.y * 1.4) * 0.5 + 0.5);
                bands = pow(bands, 3.0h) * _FoamBands;
                half crest = saturate(input.data.x - 0.35h) * _CrestFoam * saturate(foamNoise * 2.0h - 0.6h) * 2.0h;
                half foamValue = shoreMask * (0.55h + 0.45h * foamNoise) + shoreMask * bands + crest;
                half foam = smoothstep(_FoamCutoff, _FoamCutoff + _FoamSoftness, foamValue) * (1.0h - distance01 * 0.85h);
                if (cameraBelow) foam *= 0.25h;
                half3 foamColor = _FoamColor.rgb * (ambient + sunColor * lerp(0.45h, 1.0h, shadow));

                // --- Compose -------------------------------------------------------
                half3 color;
                half alpha;
            #if defined(_COZY_REFRACTION)
                // Tint the refracted scene by the shallow colour before it fades to the body colour.
                half3 tintedScene = sceneColor * lerp(half3(1, 1, 1), bodyColor * 1.4h, saturate(depth01 * 0.8h + 0.15h));
                color = lerp(tintedScene, bodyColor, bodyAlpha);
                alpha = 1.0h;
            #else
                color = bodyColor;
                alpha = bodyAlpha;
            #endif
                color = lerp(color, skyColor, reflectionAmount);
                color += sunColor * spec;
                color = lerp(color, foamColor, foam);
                alpha = saturate(alpha + reflectionAmount + foam + spec * 0.5h);

                color = CozySaturation(color, _Saturation);
                color = CozyApplyFog(color, positionWS, input.data.y);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
