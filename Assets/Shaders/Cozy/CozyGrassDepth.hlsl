#ifndef COZY_GRASS_DEPTH_INCLUDED
#define COZY_GRASS_DEPTH_INCLUDED

// Depth/shadow pass glue for Cozy/Grass. Expects the Cozy/Grass HLSLINCLUDE
// block (CozyGrassClip, CozyGrassDisplace) to be declared first.

#define COZY_DEPTH_FRAGMENT_CLIP(v) CozyGrassClip(v.uv0);
#include "CozyDepthPasses.hlsl"

float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS)
{
    float bend;
    return CozyGrassDisplace(input.positionOS.xyz, positionWS, input.uv0, input.uv1, bend);
}

#endif // COZY_GRASS_DEPTH_INCLUDED
