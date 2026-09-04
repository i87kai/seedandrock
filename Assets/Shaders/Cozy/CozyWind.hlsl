#ifndef COZY_WIND_INCLUDED
#define COZY_WIND_INCLUDED

// ---------------------------------------------------------------------------
// Cozy Stylized Rendering Framework - world-space wind
//
// All motion is evaluated in WORLD space so batched meshes (the procedural
// world merges thousands of trees/grass blades into a few meshes) still move
// coherently and independently of object transforms.
//
// Two ways to feed the wind functions:
//
//  1. OBJECT PIVOT (default, "_WINDSOURCE_OBJECT"): one transform per plant, which
//     is what MapMagic / Unity Terrain trees / prefab scatter produce. The pivot is
//     the object origin, height = object-space Y, random = hash of the world position.
//     Works with GPU instancing. Nothing to author.
//
//  2. VERTEX DATA ("_WINDSOURCE_VERTEX"): for batched meshes that merge many plants
//     into one renderer. The mesh must carry the contract below:
//   Trees (trunk + canopy share this so they stay attached):
//     UV0.x = height above the tree base in metres
//     UV0.y = per-tree random 0..1
//     UV1   = tree pivot (base) position in OBJECT space XZ
//     UV2.x = 0..1 normalized height inside the canopy, UV2.y = canopy height in metres
//   Grass blades:
//     UV0   = blade quad UV (x across, y = 0 at the root, 1 at the tip)
//     UV1.x = per-blade random 0..1, UV1.y = blade height in metres
//
// If a mesh lacks that data Unity feeds zeros, which gracefully disables
// bending (height 0 => no offset) while leaf flutter (world-position based)
// still works.
// ---------------------------------------------------------------------------

#include "CozyCommon.hlsl"

struct CozyWindSettings
{
    float2 direction;   // normalized world XZ
    float  strength;    // metres of maximum sway at the reference height
    float  speed;       // animation speed multiplier
    float  gustiness;   // 0 = steady breeze, 1 = strong gust contrast
    float  turbulence;  // spatial scale of gust cells (smaller = larger cells)
    float  flutter;     // multiplier for high-frequency leaf motion
};

CozyWindSettings CozyGetGlobalWind()
{
    CozyWindSettings w;
    if (_CozyWindParams2.w > 0.5)
    {
        w.direction  = _CozyWindParams.xy;
        w.strength   = _CozyWindParams.z;
        w.speed      = _CozyWindParams.w;
        w.gustiness  = _CozyWindParams2.x;
        w.turbulence = _CozyWindParams2.y;
        w.flutter    = _CozyWindParams2.z;
    }
    else
    {
        // Sensible defaults when no CozyWind component is present.
        w.direction  = normalize(float2(0.8, 0.35));
        w.strength   = 0.35;
        w.speed      = 1.0;
        w.gustiness  = 0.6;
        w.turbulence = 0.03;
        w.flutter    = 1.0;
    }
    return w;
}

// ---------------------------------------------------------------------------
// Object-pivot helpers (per-instance transform = one plant).
// ---------------------------------------------------------------------------
float3 CozyObjectPivotWS()
{
    return float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
}

float CozyObjectRandom()
{
    return CozyHash31(CozyObjectPivotWS() * 0.731);
}

// Resolves (pivotWS, heightAboveBase, random) for the selected wind source.
// Height uses object-space Y scaled by the object's Y scale so prefabs of any
// size bend consistently.
void CozyResolveWindInputs(float3 positionOS, float2 uv0, float2 uv1, out float3 pivotWS, out float heightAboveBase, out float random)
{
#if defined(_WINDSOURCE_VERTEX)
    pivotWS = TransformObjectToWorld(float3(uv1.x, 0.0, uv1.y));
    heightAboveBase = uv0.x;
    random = uv0.y;
#else
    pivotWS = CozyObjectPivotWS();
    float scaleY = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
    heightAboveBase = max(positionOS.y, 0.0) * scaleY;
    random = CozyObjectRandom();
#endif
}

// Slowly drifting gust field in [0,1]; large soft cells travel along the wind.
float CozyGustField(float2 worldXZ, CozyWindSettings w)
{
    float t = _Time.y * w.speed;
    float2 p = worldXZ * w.turbulence - w.direction * t * 0.18;
    float gust = CozyNoise2(p);
    // gustiness controls contrast: 0 => constant 0.75, 1 => full 0..1 range
    return lerp(0.75, gust, w.gustiness);
}

// Primary sway oscillation in [-1,1], phase-shifted per instance so a forest
// never moves in lock-step.
float CozySway(float2 worldXZ, float random, CozyWindSettings w)
{
    float t = _Time.y * w.speed;
    float phase = dot(worldXZ, w.direction) * 0.12 + random * COZY_TWO_PI;
    return sin(t * 1.1 + phase) * 0.7 + sin(t * 1.9 + phase * 1.7 + 1.3) * 0.3;
}

// ---------------------------------------------------------------------------
// Tree bending. Offsets the vertex in world space; the offset grows with the
// square of the height above the base so trunks stay planted.
//   heightAboveBase : metres above the pivot (UV0.x)
//   random          : per-tree random (UV0.y)
//   pivotWS         : world position of the tree base
//   influence       : material multiplier (keep 1 on trunk & canopy so they stay attached)
// ---------------------------------------------------------------------------
float3 CozyTreeBend(float3 positionWS, float3 pivotWS, float heightAboveBase, float random, float influence)
{
    CozyWindSettings w = CozyGetGlobalWind();
    float gust = CozyGustField(pivotWS.xz, w);
    float sway = CozySway(pivotWS.xz, random, w);

    // Lean permanently with the wind and oscillate around that lean.
    float lean = (0.55 + 0.45 * sway) * gust;
    // Slight side-to-side wobble perpendicular to the wind direction.
    float2 side = float2(-w.direction.y, w.direction.x);
    float wobble = sin(_Time.y * w.speed * 0.8 + random * COZY_TWO_PI + heightAboveBase * 0.35) * 0.25 * gust;

    float h = max(heightAboveBase, 0.0);
    // Grows faster than linear so the base stays planted; ~1 at a 6 m tall tree.
    float bendMask = pow(h * 0.18, 1.8);
    float2 offsetXZ = (w.direction * lean + side * wobble) * w.strength * influence * bendMask;

    positionWS.xz += offsetXZ;
    // Very cheap length preservation so bent tops do not stretch upward.
    positionWS.y -= dot(offsetXZ, offsetXZ) * 0.15;
    return positionWS;
}

// ---------------------------------------------------------------------------
// Leaf flutter: high-frequency, low-amplitude motion along the normal so a
// canopy silhouette "breathes". Zero at the base of the canopy via mask.
// ---------------------------------------------------------------------------
float3 CozyLeafFlutter(float3 positionWS, float3 normalWS, float mask, float amplitude)
{
    CozyWindSettings w = CozyGetGlobalWind();
    float t = _Time.y * w.speed;
    float gust = CozyGustField(positionWS.xz, w);
    float phase = dot(positionWS, float3(3.1, 2.3, 2.7));
    float flutter = sin(t * 5.3 + phase) * 0.6 + sin(t * 8.9 + phase * 1.31) * 0.4;
    positionWS += normalWS * flutter * amplitude * w.flutter * mask * (0.3 + 0.7 * gust) * w.strength;
    return positionWS;
}

// ---------------------------------------------------------------------------
// Grass. Travelling waves along the wind direction plus per-blade jitter.
//   tipFactor : 0 at the root, 1 at the tip (UV0.y)
//   random    : per-blade random (UV1.x)
// Returns the displaced position and writes the bend amount (0..1) so the
// fragment shader can brighten bent blades.
// ---------------------------------------------------------------------------
float3 CozyGrassWind(float3 positionWS, float tipFactor, float random, float influence, out float bendAmount)
{
    CozyWindSettings w = CozyGetGlobalWind();
    float t = _Time.y * w.speed;

    float along = dot(positionWS.xz, w.direction);
    // Large travelling wave, then a smaller faster ripple, then a gust field.
    float wave = sin(along * 0.35 - t * 1.6) * 0.5 + 0.5;
    float ripple = sin(along * 1.7 - t * 3.1 + random * COZY_TWO_PI) * 0.5 + 0.5;
    float gust = CozyGustField(positionWS.xz, w);

    float amount = (wave * 0.55 + ripple * 0.2 + 0.25) * gust;
    // Random per-blade sideways jitter so blades don't form a rigid sheet.
    float2 side = float2(-w.direction.y, w.direction.x);
    float jitter = sin(t * 2.3 + random * COZY_TWO_PI) * 0.3;

    float mask = tipFactor * tipFactor; // root stays fixed
    float2 offsetXZ = (w.direction * amount + side * jitter * amount) * w.strength * influence * mask;

    positionWS.xz += offsetXZ;
    positionWS.y -= dot(offsetXZ, offsetXZ) * 0.5;
    bendAmount = saturate(amount * influence);
    return positionWS;
}

#endif // COZY_WIND_INCLUDED
