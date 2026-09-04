#ifndef COZY_LIGHTING_INCLUDED
#define COZY_LIGHTING_INCLUDED

// ---------------------------------------------------------------------------
// Cozy Stylized Rendering Framework - soft stylized lighting model
//
// Goals: smooth toon-like ramps (no hard bands), warm tinted shadows, gentle
// rim, soft specular blobs, optional foliage translucency, URP shadows,
// Forward+/additional lights, SSAO and fog. Used by every Cozy surface shader.
//
// Usage:
//   CozySurface s = CozyInitSurface();   // fill in fields
//   CozyStyle   k = ...                   // artist controls from the material
//   half3 color = CozyShade(s, k);
// ---------------------------------------------------------------------------

#include "CozyCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(_SCREEN_SPACE_OCCLUSION)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#endif

struct CozySurface
{
    half3  albedo;
    half   alpha;
    half3  normalWS;        // normalized
    float3 positionWS;
    half3  viewDirWS;       // normalized, surface -> camera
    float4 shadowCoord;
    float2 screenUV;        // normalized screen UV (for SSAO / Forward+)
    half   occlusion;       // baked/vertex AO multiplier for ambient (1 = none)
    half   thickness;       // 0..1 translucency mask (foliage: 1 at thin edges)
    half3  emission;
};

struct CozyStyle
{
    half  rampOffset;       // NdotL value where the lit/shadow transition sits (-1..1)
    half  rampSoftness;     // width of the transition (0 = hard toon, 1 = fully soft)
    half3 shadowTint;       // colour multiplied into shadowed ambient (warm for cozy)
    half  shadowTintStrength;
    half  smoothness;       // 0..1, drives specular exponent
    half  specularStrength;
    half  specularSoftness; // widens the soft highlight blob
    half3 rimColor;
    half  rimStrength;
    half  rimPower;
    half  rimLightMask;     // 0 = rim everywhere, 1 = rim only on the lit side
    half  translucency;     // backlight strength (foliage)
    half3 translucencyColor;
    half  saturation;       // 1 = neutral
    half  lightWrap;        // 0..1, wraps diffuse around the terminator for softness
};

CozySurface CozyInitSurface()
{
    CozySurface s;
    s.albedo = half3(1, 1, 1);
    s.alpha = 1;
    s.normalWS = half3(0, 1, 0);
    s.positionWS = 0;
    s.viewDirWS = half3(0, 1, 0);
    s.shadowCoord = 0;
    s.screenUV = 0;
    s.occlusion = 1;
    s.thickness = 0;
    s.emission = 0;
    return s;
}

// Soft toon ramp: wrapped Lambert pushed through a wide smoothstep.
half CozyDiffuseRamp(half NdotL, CozyStyle k)
{
    half wrapped = (NdotL + k.lightWrap) / (1.0h + k.lightWrap);
    half softness = max(k.rampSoftness, 0.02h);
    return smoothstep(k.rampOffset - softness, k.rampOffset + softness, wrapped);
}

half CozySpecular(half3 normalWS, half3 viewDirWS, half3 lightDir, CozyStyle k)
{
    half3 halfDir = SafeNormalize(lightDir + viewDirWS);
    half NdotH = saturate(dot(normalWS, halfDir));
    half exponent = exp2(1.0h + k.smoothness * 9.0h); // 2..1024
    half spec = pow(NdotH, exponent);
    // Turn the highlight into a soft blob instead of a physically sharp dot.
    half edge = 0.35h;
    return smoothstep(edge - k.specularSoftness, edge + k.specularSoftness, spec) * k.specularStrength;
}

half3 CozyShadeLight(CozySurface s, CozyStyle k, Light light, half ambientOcclusionDirect)
{
    half NdotL = dot(s.normalWS, light.direction);
    half ramp = CozyDiffuseRamp(NdotL, k);
    half attenuation = light.distanceAttenuation * light.shadowAttenuation * ambientOcclusionDirect;
    half lit = ramp * attenuation;

    half3 radiance = light.color * lit;
    half3 color = s.albedo * radiance;

    // Soft specular, only where lit.
    color += light.color * CozySpecular(s.normalWS, s.viewDirWS, light.direction, k) * lit;

    // Translucency / backlight: light coming through thin foliage towards the eye.
    if (k.translucency > 0.0h)
    {
        half backlight = pow(saturate(dot(s.viewDirWS, -light.direction)), 3.0h);
        half wrapBack = saturate(-NdotL * 0.5h + 0.5h);
        color += s.albedo * k.translucencyColor * light.color * backlight * wrapBack * s.thickness * k.translucency * attenuation;
    }
    return color;
}

half3 CozyShade(CozySurface s, CozyStyle k)
{
    half4 shadowMask = half4(1, 1, 1, 1);
    Light mainLight = GetMainLight(s.shadowCoord, s.positionWS, shadowMask);

    half aoIndirect = s.occlusion;
    half aoDirect = 1.0h;
#if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(s.screenUV);
    aoIndirect *= ao.indirectAmbientOcclusion;
    aoDirect = ao.directAmbientOcclusion;
#endif

    // --- Ambient with warm tinted shadows -----------------------------------
    half3 ambient = SampleSH(s.normalWS) * aoIndirect;
    half NdotL = dot(s.normalWS, mainLight.direction);
    half mainLit = CozyDiffuseRamp(NdotL, k) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    half3 tint = lerp(half3(1, 1, 1), k.shadowTint, k.shadowTintStrength * (1.0h - mainLit));
    half3 color = s.albedo * ambient * tint;

    // --- Main light ---------------------------------------------------------
    color += CozyShadeLight(s, k, mainLight, aoDirect);

    // --- Additional lights (Forward / Forward+) -----------------------------
#if defined(_ADDITIONAL_LIGHTS)
    // LIGHT_LOOP_BEGIN expects an InputData named `inputData` for the cluster iterator.
    InputData inputData = (InputData)0;
    inputData.positionWS = s.positionWS;
    inputData.normalizedScreenSpaceUV = s.screenUV;
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, s.positionWS, shadowMask);
        color += CozyShadeLight(s, k, light, aoDirect);
    LIGHT_LOOP_END
#endif

    // --- Rim ----------------------------------------------------------------
    if (k.rimStrength > 0.0h)
    {
        half NdotV = saturate(dot(s.normalWS, s.viewDirWS));
        half rim = pow(1.0h - NdotV, max(k.rimPower, 0.1h));
        rim = smoothstep(0.15h, 0.9h, rim);
        half lightSideMask = lerp(1.0h, saturate(NdotL * 0.5h + 0.5h) * mainLight.shadowAttenuation, k.rimLightMask);
        color += k.rimColor * mainLight.color * rim * k.rimStrength * lightSideMask;
    }

    color += s.emission;
    color = CozySaturation(color, k.saturation);
    return color;
}

// Helper: shadow coordinate handling that works for all shadow modes.
float4 CozyGetShadowCoord(float3 positionWS, float4 positionCS)
{
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    return ComputeScreenPos(positionCS);
#else
    return TransformWorldToShadowCoord(positionWS);
#endif
}

#endif // COZY_LIGHTING_INCLUDED
