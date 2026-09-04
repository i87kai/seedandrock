#ifndef SEEDANDROCK_COMMON_INCLUDED
#define SEEDANDROCK_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---------------------------------------------------------------------------------------------
// Cheap procedural noise shared by the SeedAndRock surface shaders. Everything is world-space
// driven so it never tiles with UVs and stays stable across mesh chunks.
// ---------------------------------------------------------------------------------------------

float SR_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SR_ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = SR_Hash21(i);
    float b = SR_Hash21(i + float2(1.0, 0.0));
    float c = SR_Hash21(i + float2(0.0, 1.0));
    float d = SR_Hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float SR_Fbm(float2 p, int octaves)
{
    float sum = 0.0;
    float amplitude = 0.5;
    float normalizer = 0.0;
    for (int i = 0; i < octaves; i++)
    {
        sum += SR_ValueNoise(p) * amplitude;
        normalizer += amplitude;
        p = p * 2.03 + float2(17.1, 9.7);
        amplitude *= 0.5;
    }
    return sum / max(normalizer, 1e-4);
}

// Soft wrapped diffuse: keeps shadowed sides readable in a cozy, low-contrast style.
half3 SR_Diffuse(half3 albedo, half3 normalWS, Light light, half wrap)
{
    half ndotl = dot(normalWS, light.direction);
    half diffuse = saturate((ndotl + wrap) / (1.0h + wrap));
    return albedo * light.color * diffuse * light.shadowAttenuation * light.distanceAttenuation;
}

half3 SR_Ambient(half3 albedo, half3 normalWS)
{
    return albedo * SampleSH(normalWS);
}

// Additional (point/spot) lights, when the renderer has them enabled.
half3 SR_AdditionalLights(half3 albedo, half3 normalWS, float3 positionWS)
{
    half3 result = 0;
#if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
    for (uint i = 0u; i < count; ++i)
    {
        Light light = GetAdditionalLight(i, positionWS);
        result += SR_Diffuse(albedo, normalWS, light, 0.15h);
    }
#endif
    return result;
}

#endif
