#ifndef COZY_FOLIAGE_DEPTH_INCLUDED
#define COZY_FOLIAGE_DEPTH_INCLUDED

// Depth/shadow pass glue for Cozy/Foliage: same wind displacement as the lit
// pass (so shadows sway) plus optional alpha clip. Expects the Cozy/Foliage
// HLSLINCLUDE block (cbuffer, _BaseMap, CozyFoliageDisplace) to be declared.

#if defined(_ALPHATEST_ON)
    #define COZY_DEPTH_FRAGMENT_CLIP(v) \
        clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(v.uv0, _BaseMap)).a - _Cutoff);
#endif

#include "CozyDepthPasses.hlsl"

float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS)
{
    return CozyFoliageDisplace(input.positionOS.xyz, positionWS, normalWS, input.uv0, input.uv1, input.uv2);
}

#endif // COZY_FOLIAGE_DEPTH_INCLUDED
