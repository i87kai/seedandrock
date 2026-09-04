#ifndef COZY_TERRAIN_DEPTH_INCLUDED
#define COZY_TERRAIN_DEPTH_INCLUDED

// Depth/shadow pass glue for Cozy/Terrain: applies Unity Terrain draw-instancing
// before the transform and clips terrain holes. Expects the Cozy/Terrain
// HLSLINCLUDE block to be declared first.

#define COZY_DEPTH_PRE_TRANSFORM(a) CozyTerrainInstancing(a.positionOS, a.normalOS, a.uv0);
#define COZY_DEPTH_FRAGMENT_CLIP(v)  CozyTerrainClipHoles(v.uv0);
#include "CozyDepthPasses.hlsl"

float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS)
{
    return positionWS;
}

#endif // COZY_TERRAIN_DEPTH_INCLUDED
