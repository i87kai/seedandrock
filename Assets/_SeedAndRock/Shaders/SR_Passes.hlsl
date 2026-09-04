#ifndef SEEDANDROCK_PASSES_INCLUDED
#define SEEDANDROCK_PASSES_INCLUDED

// Self-contained ShadowCaster / DepthOnly / DepthNormals programs for the procedural SeedAndRock
// meshes. They avoid URP's texture-based pass includes so no _BaseMap/_Cutoff plumbing is needed.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

struct SR_PassAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct SR_PassVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
};

float3 _LightDirection;
float3 _LightPosition;

SR_PassVaryings SR_ShadowVertex(SR_PassAttributes input)
{
    SR_PassVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS = positionCS;
    output.normalWS = normalWS;
    return output;
}

half4 SR_ShadowFragment(SR_PassVaryings input) : SV_TARGET
{
    return 0;
}

SR_PassVaryings SR_DepthVertex(SR_PassAttributes input)
{
    SR_PassVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half SR_DepthOnlyFragment(SR_PassVaryings input) : SV_TARGET
{
    return input.positionCS.z;
}

half4 SR_DepthNormalsFragment(SR_PassVaryings input) : SV_TARGET
{
    return half4(normalize(input.normalWS), 0.0h);
}

#endif
