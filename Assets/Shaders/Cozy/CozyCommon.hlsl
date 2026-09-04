#ifndef COZY_COMMON_INCLUDED
#define COZY_COMMON_INCLUDED

// ---------------------------------------------------------------------------
// Cozy Stylized Rendering Framework - shared utilities
//
// Everything here is pipeline-agnostic helper code plus the *global* uniforms
// that the C# side (CozyAtmosphere.cs / CozyWind.cs) publishes with
// Shader.SetGlobal*. Every global has a "ready" flag so the shaders degrade to
// sensible defaults when the scripts are not present in a scene.
// ---------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ----------------------------------------------------------------------------
// Global atmosphere state (see CozyAtmosphere.cs)
// ----------------------------------------------------------------------------
// x: ready flag (1 when CozyAtmosphere is driving the globals)
// y: day factor    (0 = full night, 1 = full day)
// z: sunset factor (0..1, peaks while the sun crosses the horizon)
// w: night factor  (1 - day, cached for convenience)
float4 _CozyAtmosphereParams;
// xyz: normalized direction *towards* the sun, w: sun elevation (-1..1)
float4 _CozySunDirection;
half4  _CozySunColor;           // rgb: direct sun colour (already includes intensity)
half4  _CozySkyZenithColor;
half4  _CozySkyHorizonColor;
half4  _CozySkyGroundColor;
half4  _CozyFogColor;           // base fog / atmosphere colour
half4  _CozyFogSunColor;        // colour fog takes on when looking towards the sun
// x: height fog density, y: height fog falloff, z: height fog base height (world Y),
// w: sun in-scatter strength for fog
float4 _CozyFogParams;

// ----------------------------------------------------------------------------
// Global wind state (see CozyWind.cs)
// ----------------------------------------------------------------------------
// x,y: normalized wind direction (world XZ), z: strength (metres of sway), w: speed
float4 _CozyWindParams;
// x: gustiness, y: turbulence scale, z: leaf flutter multiplier, w: ready flag
float4 _CozyWindParams2;

// ----------------------------------------------------------------------------
// Underwater state (see CozyCameraSetup.cs). x: 0..1 submerged blend,
// y: absorption density (1/m), z: water surface height, w: unused.
// ----------------------------------------------------------------------------
float4 _CozyUnderwaterParams;
half4  _CozyUnderwaterColor;

// ----------------------------------------------------------------------------
// Small maths helpers
// ----------------------------------------------------------------------------
#define COZY_PI 3.14159265359
#define COZY_TWO_PI 6.28318530718

half CozyLuminance(half3 c)
{
    return dot(c, half3(0.2126h, 0.7152h, 0.0722h));
}

// saturation = 1 leaves the colour untouched, >1 boosts, <1 mutes.
half3 CozySaturation(half3 c, half saturation)
{
    half l = CozyLuminance(c);
    return max(lerp(l.xxx, c, saturation), 0.0h);
}

// Symmetric soft threshold: 0 below (edge - softness), 1 above (edge + softness).
half CozySoftStep(half edge, half softness, half x)
{
    softness = max(softness, 1e-4h);
    return smoothstep(edge - softness, edge + softness, x);
}

float CozyRemap(float value, float inMin, float inMax, float outMin, float outMax)
{
    return outMin + (value - inMin) * (outMax - outMin) / max(inMax - inMin, 1e-5);
}

// ----------------------------------------------------------------------------
// Hash & value noise (cheap, deterministic, no textures)
// ----------------------------------------------------------------------------
float CozyHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float CozyHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 CozyHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float CozyHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// Smooth value noise in [0,1].
float CozyValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = CozyHash21(i);
    float b = CozyHash21(i + float2(1.0, 0.0));
    float c = CozyHash21(i + float2(0.0, 1.0));
    float d = CozyHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Fractal value noise, result roughly in [0,1]. octaves is clamped to [1,6].
float CozyFbm(float2 p, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    float total = 0.0;
    float2x2 rotate = float2x2(0.8, -0.6, 0.6, 0.8);
    octaves = clamp(octaves, 1, 6);
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * CozyValueNoise(p);
        total += amplitude;
        p = mul(rotate, p) * 2.03 + 17.1;
        amplitude *= 0.5;
    }
    return value / max(total, 1e-4);
}

// Two-octave noise; the workhorse for vertex-shader gusts because it is cheap.
float CozyNoise2(float2 p)
{
    return CozyValueNoise(p) * 0.66 + CozyValueNoise(p * 2.07 + 5.3) * 0.34;
}

// ----------------------------------------------------------------------------
// Atmosphere helpers
// ----------------------------------------------------------------------------
bool CozyAtmosphereReady()
{
    return _CozyAtmosphereParams.x > 0.5;
}

float3 CozySunDirection()
{
    // Falls back to URP's main light when the atmosphere script is absent.
    return CozyAtmosphereReady() ? _CozySunDirection.xyz : normalize(_MainLightPosition.xyz + float3(0, 1e-4, 0));
}

half3 CozySkyGradient(float3 dirWS, half3 zenith, half3 horizon, half3 ground, half falloff)
{
    half up = saturate(dirWS.y);
    half3 sky = lerp(horizon, zenith, pow(up, falloff));
    half below = saturate(-dirWS.y * 6.0h);
    return lerp(sky, ground, below);
}

// Cheap "reflection of the sky" without a probe - used by water.
half3 CozySampleSkyApprox(float3 dirWS)
{
    if (CozyAtmosphereReady())
        return CozySkyGradient(dirWS, _CozySkyZenithColor.rgb, _CozySkyHorizonColor.rgb, _CozySkyGroundColor.rgb, 0.6h);
    // Fallback: sample the ambient probe which is derived from the skybox.
    return SampleSH(half3(dirWS));
}

// Combines URP's per-camera fog (already in fogFactor) with an optional
// height-fog + sun in-scatter layer driven by CozyAtmosphere.
// Camera-submerged absorption: the whole frame tints/fades toward the water
// colour with distance. Applied by every Cozy surface and the sky so the view
// from under the surface reads as being inside the water volume.
half3 CozyApplyUnderwater(half3 color, float3 positionWS)
{
    half submerged = _CozyUnderwaterParams.x;
    if (submerged <= 0.001h)
        return color;
    float dist = length(_WorldSpaceCameraPos - positionWS);
    half absorb = 1.0h - exp(-dist * _CozyUnderwaterParams.y);
    return lerp(color, _CozyUnderwaterColor.rgb, absorb * submerged);
}

half3 CozyApplyFog(half3 color, float3 positionWS, half fogFactor)
{
    if (_CozyUnderwaterParams.x > 0.001h)
        return CozyApplyUnderwater(color, positionWS);

    if (!CozyAtmosphereReady())
        return MixFog(color, fogFactor);

    float3 toCamera = _WorldSpaceCameraPos - positionWS;
    float dist = length(toCamera);
    float3 viewDir = toCamera / max(dist, 1e-4);

    // Sun in-scatter: fog turns golden when looking towards the sun.
    half sunAmount = pow(saturate(dot(-viewDir, _CozySunDirection.xyz)), 6.0h);
    half3 fogColor = lerp(_CozyFogColor.rgb, _CozyFogSunColor.rgb, sunAmount * _CozyFogParams.w);

    // Distance fog from URP (respects RenderSettings.fog mode/density) but tinted
    // with the atmosphere colour instead of the flat RenderSettings colour.
    color = MixFogColor(color, fogColor, fogFactor);

    // Height fog: denser below the base height, faded by distance.
    float heightFalloff = max(_CozyFogParams.y, 1e-3);
    float heightDensity = _CozyFogParams.x;
    if (heightDensity > 0.0)
    {
        // Analytic integral of exponential height fog along the view ray.
        float camHeight = _WorldSpaceCameraPos.y - _CozyFogParams.z;
        float dy = positionWS.y - _WorldSpaceCameraPos.y;
        float fogIntegral = exp(-camHeight * heightFalloff);
        float slope = dy * heightFalloff;
        if (abs(slope) > 1e-3)
            fogIntegral *= (1.0 - exp(-slope)) / slope;
        float heightFog = 1.0 - exp(-heightDensity * dist * fogIntegral);
        color = lerp(color, fogColor, saturate(heightFog));
    }
    return color;
}

// ----------------------------------------------------------------------------
// Screen helpers
// ----------------------------------------------------------------------------
float2 CozyScreenUV(float4 positionCS)
{
    return GetNormalizedScreenSpaceUV(positionCS);
}

#endif // COZY_COMMON_INCLUDED
