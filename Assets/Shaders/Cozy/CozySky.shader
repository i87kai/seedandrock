// Cozy Stylized Rendering Framework - procedural skybox with soft clouds.
// Soft zenith/horizon gradient, sun disc + glow, sunset band, two layers of
// procedural "volumetric-looking" clouds (lit towards the sun with silver
// lining), cloud drift, stars + moon at night, and a horizon haze that matches
// the atmosphere fog colour. No textures required.
//
// When a CozyAtmosphere component is in the scene it publishes the sky colours,
// sun direction and fog colour as globals and this shader follows them so the
// sky, fog and water reflections stay in sync through the day/night cycle.
// Without the component the material colours below are used with the main
// light as the sun.
Shader "Cozy/Sky"
{
    Properties
    {
        [Header(Sky Colors (fallback when no CozyAtmosphere))]
        _ZenithColor ("Zenith", Color) = (0.24, 0.48, 0.90, 1)
        _HorizonColor ("Horizon", Color) = (0.72, 0.86, 0.98, 1)
        _GroundColor ("Below Horizon", Color) = (0.42, 0.50, 0.58, 1)
        _HorizonFalloff ("Horizon Falloff", Range(0.2, 4)) = 0.85
        _HazeColor ("Horizon Haze", Color) = (0.86, 0.92, 0.98, 1)
        _HazeHeight ("Haze Height", Range(0.01, 0.6)) = 0.16
        _HazeStrength ("Haze Strength", Range(0, 1)) = 0.7

        [Header(Sun)]
        _SunColor ("Sun Color (fallback)", Color) = (1.0, 0.95, 0.85, 1)
        _SunSize ("Sun Disc Size", Range(0.001, 0.2)) = 0.025
        _SunSoftness ("Sun Disc Softness", Range(0.001, 0.2)) = 0.012
        _SunGlow ("Sun Glow", Range(0, 3)) = 0.8
        _SunGlowPower ("Sun Glow Power", Range(1, 64)) = 10
        _SunsetColor ("Sunset Band Color", Color) = (1.0, 0.55, 0.30, 1)
        _SunsetStrength ("Sunset Band Strength", Range(0, 2)) = 1.0

        [Header(Clouds)]
        _CloudColor ("Cloud Lit Color", Color) = (1.0, 0.99, 0.97, 1)
        _CloudShadowColor ("Cloud Shadow Color", Color) = (0.62, 0.70, 0.86, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudDensity ("Cloud Density", Range(0.1, 4)) = 1.6
        _CloudSoftness ("Cloud Edge Softness", Range(0.02, 0.6)) = 0.25
        _CloudScale ("Cloud Scale", Range(0.2, 8)) = 1.6
        _CloudHeight ("Cloud Dome Height", Range(0.05, 2)) = 0.35
        _CloudSpeed ("Cloud Speed", Range(0, 2)) = 0.25
        _CloudDirection ("Cloud Wind Direction (XZ)", Vector) = (1, 0.3, 0, 0)
        _CloudShading ("Cloud Shading Contrast", Range(0, 2)) = 0.8
        _CloudSilverLining ("Silver Lining", Range(0, 3)) = 1.2
        _CloudHorizonFade ("Cloud Horizon Fade", Range(0.01, 0.5)) = 0.08
        [KeywordEnum(Low, Medium, High)] _CloudQuality ("Cloud Quality", Float) = 1

        [Header(Night)]
        _NightSkyColor ("Night Zenith Tint", Color) = (0.03, 0.05, 0.12, 1)
        _StarDensity ("Star Density", Range(0, 1)) = 0.6
        _StarBrightness ("Star Brightness", Range(0, 3)) = 1.2
        _MoonColor ("Moon Color", Color) = (0.86, 0.90, 1.0, 1)
        _MoonSize ("Moon Size", Range(0.005, 0.15)) = 0.035
        _MoonGlow ("Moon Glow", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "CozySky"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozySkyVertex
            #pragma fragment CozySkyFragment
            #pragma shader_feature_local _CLOUDQUALITY_LOW _CLOUDQUALITY_MEDIUM _CLOUDQUALITY_HIGH

            #include "CozyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half  _HorizonFalloff;
                half4 _HazeColor;
                half  _HazeHeight;
                half  _HazeStrength;
                half4 _SunColor;
                half  _SunSize;
                half  _SunSoftness;
                half  _SunGlow;
                half  _SunGlowPower;
                half4 _SunsetColor;
                half  _SunsetStrength;
                half4 _CloudColor;
                half4 _CloudShadowColor;
                half  _CloudCoverage;
                half  _CloudDensity;
                half  _CloudSoftness;
                float _CloudScale;
                float _CloudHeight;
                float _CloudSpeed;
                float4 _CloudDirection;
                half  _CloudShading;
                half  _CloudSilverLining;
                half  _CloudHorizonFade;
                half4 _NightSkyColor;
                half  _StarDensity;
                half  _StarBrightness;
                half4 _MoonColor;
                half  _MoonSize;
                half  _MoonGlow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            Varyings CozySkyVertex(Attributes input)
            {
                Varyings output;
                // Skybox meshes are unit cubes centred on the camera; world direction = object direction.
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDirWS = TransformObjectToWorldDir(input.positionOS.xyz, false);
                return output;
            }

            // ---------------------------------------------------------------
            // Clouds: a 2D noise field mapped onto a curved dome, with a second
            // sample shifted towards the sun to fake self-shadowing.
            // ---------------------------------------------------------------
            float CozyCloudDensity(float2 uv, int octaves)
            {
                float shape = CozyFbm(uv, octaves);
                // Coverage drives the threshold; density steepens the falloff.
                float threshold = 1.0 - _CloudCoverage;
                float d = smoothstep(threshold - _CloudSoftness, threshold + _CloudSoftness * 0.5, shape);
                return saturate(pow(d, 1.0 / max(_CloudDensity, 0.1)) * _CloudDensity * 0.75);
            }

            // Returns rgb = cloud colour, a = coverage for one layer.
            half4 CozyCloudLayer(float3 dir, float3 sunDir, half3 litColor, half3 shadowColor, float scale, float speed, float heightOffset, int octaves, half sunriseMix)
            {
                float height = _CloudHeight + heightOffset;
                // Project the view ray onto a dome: flatter near the horizon.
                float2 planar = dir.xz / max(dir.y + 0.12, 0.02) * height;
                float2 windDir = normalize(_CloudDirection.xy + float2(1e-4, 0));
                float2 uv = planar * scale + windDir * (_Time.y * speed * 0.05);

                float d = CozyCloudDensity(uv, octaves);
                if (d <= 0.001) return half4(0, 0, 0, 0);

                // Shift towards the sun in dome-space: brighter on the sun-facing side.
                float2 sunOffset = normalize(sunDir.xz + float2(1e-4, 0)) * 0.06 * saturate(sunDir.y + 0.4);
                float dTowardsSun = CozyCloudDensity(uv + sunOffset, octaves);
                float shade = saturate(0.5 + (d - dTowardsSun) * 2.5 * _CloudShading);

                half3 color = lerp(litColor, shadowColor, shade * saturate(d * 1.4));
                // Silver lining: thin parts near the sun glow.
                half sunDot = saturate(dot(dir, sunDir));
                half lining = pow(sunDot, 6.0h) * (1.0h - saturate(d * 1.2h)) * _CloudSilverLining;
                color += litColor * lining * sunriseMix;

                half horizonFade = smoothstep(0.0, _CloudHorizonFade, dir.y);
                return half4(color, d * horizonFade);
            }

            // Stars: jittered point in each cell of a cube-mapped grid.
            half CozyStars(float3 dir, half density)
            {
                float3 p = dir * 90.0;
                float3 cell = floor(p);
                float3 f = frac(p);
                float h = CozyHash31(cell);
                if (h > density * 0.35) return 0.0h;
                float3 starPos = float3(CozyHash31(cell + 7.1), CozyHash31(cell + 13.7), CozyHash31(cell + 31.3));
                float dist = length(f - starPos);
                half star = saturate(1.0h - dist * 5.0h);
                star = pow(star, 3.0h);
                // Gentle twinkle.
                star *= 0.6h + 0.4h * sin(_Time.y * (1.5 + h * 4.0) + h * 40.0);
                return star;
            }

            half4 CozySkyFragment(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDirWS);
                bool live = CozyAtmosphereReady();

                float3 sunDir = CozySunDirection();
                half3 sunColor = live ? _CozySunColor.rgb : _SunColor.rgb;
                half3 zenith  = live ? _CozySkyZenithColor.rgb  : _ZenithColor.rgb;
                half3 horizon = live ? _CozySkyHorizonColor.rgb : _HorizonColor.rgb;
                half3 ground  = live ? _CozySkyGroundColor.rgb  : _GroundColor.rgb;
                half3 haze    = live ? _CozyFogColor.rgb        : _HazeColor.rgb;

                half sunHeight = sunDir.y;                                   // -1..1
                half day = live ? _CozyAtmosphereParams.y : smoothstep(-0.12h, 0.18h, sunHeight);
                half night = 1.0h - day;
                half sunset = live ? _CozyAtmosphereParams.z : (1.0h - smoothstep(0.0h, 0.35h, abs(sunHeight))) ;

                // --- Base gradient -----------------------------------------------
                half3 sky = CozySkyGradient(dir, zenith, horizon, ground, _HorizonFalloff);

                // Sunset band: warm glow around the horizon, strongest towards the sun.
                half sunDot = dot(dir, sunDir);
                half towardsSun = saturate(sunDot * 0.5h + 0.5h);
                half band = (1.0h - saturate(abs(dir.y) * 3.5h)) * pow(towardsSun, 2.5h);
                sky = lerp(sky, _SunsetColor.rgb, band * sunset * _SunsetStrength * 0.8h);

                // Sun glow.
                half glow = pow(saturate(sunDot), _SunGlowPower) * _SunGlow;
                sky += sunColor * glow * (0.35h + 0.65h * day);

                // --- Clouds --------------------------------------------------------
                half3 cloudLit = _CloudColor.rgb * lerp(half3(0.25h, 0.28h, 0.40h), half3(1, 1, 1), day);
                cloudLit = lerp(cloudLit, cloudLit * _SunsetColor.rgb * 1.3h, sunset * 0.7h);
                half3 cloudShadow = _CloudShadowColor.rgb * lerp(half3(0.15h, 0.17h, 0.28h), half3(1, 1, 1), day);
                cloudShadow = lerp(cloudShadow, cloudShadow * lerp(half3(1, 1, 1), _SunsetColor.rgb, 0.5h), sunset);

            #if defined(_CLOUDQUALITY_LOW)
                const int octaves = 3;
                const bool twoLayers = false;
            #elif defined(_CLOUDQUALITY_HIGH)
                const int octaves = 5;
                const bool twoLayers = true;
            #else
                const int octaves = 4;
                const bool twoLayers = true;
            #endif

                half4 clouds = 0;
                if (dir.y > -0.02)
                {
                    clouds = CozyCloudLayer(dir, sunDir, cloudLit, cloudShadow, _CloudScale, _CloudSpeed, 0.0, octaves, 0.5h + 0.5h * day);
                    if (twoLayers)
                    {
                        // Higher, thinner, faster layer for parallax and variety.
                        half4 upper = CozyCloudLayer(dir, sunDir, cloudLit * 1.05h, cloudShadow, _CloudScale * 1.9, _CloudSpeed * 1.7, 0.45, max(octaves - 1, 2), 0.5h + 0.5h * day);
                        upper.a *= 0.55h;
                        clouds.rgb = lerp(clouds.rgb, upper.rgb, upper.a * (1.0h - clouds.a));
                        clouds.a = saturate(clouds.a + upper.a * (1.0h - clouds.a));
                    }
                }

                // --- Night: stars + moon (behind clouds) ----------------------------
                half3 nightSky = 0;
                if (night > 0.001h)
                {
                    half stars = CozyStars(dir, _StarDensity) * _StarBrightness * saturate(dir.y * 4.0h + 0.2h);
                    float3 moonDir = -sunDir;
                    half moonDot = dot(dir, moonDir);
                    half moonDisc = smoothstep(1.0h - _MoonSize - 0.002h, 1.0h - _MoonSize + 0.003h, moonDot);
                    half moonGlow = pow(saturate(moonDot), 48.0h) * _MoonGlow;
                    nightSky = stars.xxx * half3(0.9h, 0.95h, 1.0h) + _MoonColor.rgb * (moonDisc + moonGlow * 0.5h);
                    sky = lerp(sky, sky + _NightSkyColor.rgb, night * 0.5h);
                    sky += nightSky * night;
                }

                // Sun disc (above the clouds is fine: clouds occlude via the blend below).
                half disc = smoothstep(1.0h - _SunSize - _SunSoftness, 1.0h - _SunSize + _SunSoftness * 0.25h, sunDot);
                sky += sunColor * disc * 3.0h * (0.2h + 0.8h * day);

                // Composite clouds.
                sky = lerp(sky, clouds.rgb, clouds.a);

                // --- Horizon haze, matching the fog colour --------------------------
                half hazeMask = (1.0h - smoothstep(0.0h, _HazeHeight, saturate(dir.y))) * _HazeStrength;
                hazeMask *= saturate(1.0h + dir.y * 4.0h); // fade out far below the horizon
                half3 hazeColor = lerp(haze, sunColor * 0.7h + haze * 0.3h, pow(towardsSun, 4.0h) * sunset * 0.6h);
                sky = lerp(sky, hazeColor, hazeMask);

                // Submerged camera: the sky is only seen through the water body.
                sky = lerp(sky, _CozyUnderwaterColor.rgb, _CozyUnderwaterParams.x * 0.9h);

                return half4(max(sky, 0.0h), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
