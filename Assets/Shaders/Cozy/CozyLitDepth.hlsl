#ifndef COZY_LIT_DEPTH_INCLUDED
#define COZY_LIT_DEPTH_INCLUDED

// Depth/shadow pass glue for Cozy/Lit: optional trunk bending + alpha clip.
// Expects the Cozy/Lit UnityPerMaterial cbuffer and _BaseMap to be declared
// (they live in the shader's HLSLINCLUDE block).

#include "CozyWind.hlsl"

#if defined(_ALPHATEST_ON)
    #define COZY_DEPTH_FRAGMENT_CLIP(v) \
        clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(v.uv0, _BaseMap)).a * _BaseColor.a - _Cutoff);
#endif

#include "CozyDepthPasses.hlsl"

float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS)
{
#if defined(_WINDSOURCE_OBJECT) || defined(_WINDSOURCE_VERTEX)
    float3 pivotWS; float heightAboveBase; float random;
    CozyResolveWindInputs(input.positionOS.xyz, input.uv0, input.uv1, pivotWS, heightAboveBase, random);
    positionWS = CozyTreeBend(positionWS, pivotWS, heightAboveBase, random, _WindInfluence);
#endif
    return positionWS;
}

#endif // COZY_LIT_DEPTH_INCLUDED
