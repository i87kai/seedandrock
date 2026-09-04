#ifndef COZY_DEPTH_PASSES_INCLUDED
#define COZY_DEPTH_PASSES_INCLUDED

// ---------------------------------------------------------------------------
// Cozy Stylized Rendering Framework - shared ShadowCaster / DepthOnly /
// DepthNormals passes.
//
// The including shader MUST define, before including this file:
//
//   float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS)
//       Returns the wind-displaced world position (return positionWS for none).
//
// and MAY define:
//
//   #define COZY_DEPTH_FRAGMENT_CLIP(varyings)  <code that calls clip()>
//       Used for alpha testing / procedural shapes (grass blade taper).
//
//   #define COZY_DEPTH_PRE_TRANSFORM(attributes) <code modifying attributes in place>
//       Runs before the object->world transform (Unity Terrain instancing).
//
// Then use these entry points in the passes:
//   ShadowCaster : CozyShadowVertex / CozyShadowFragment
//   DepthOnly    : CozyDepthOnlyVertex / CozyDepthOnlyFragment
//   DepthNormals : CozyDepthNormalsVertex / CozyDepthNormalsFragment
// ---------------------------------------------------------------------------

#include "CozyCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#ifndef COZY_DEPTH_FRAGMENT_CLIP
#define COZY_DEPTH_FRAGMENT_CLIP(varyings)
#endif
#ifndef COZY_DEPTH_PRE_TRANSFORM
#define COZY_DEPTH_PRE_TRANSFORM(attributes)
#endif

struct CozyDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv0        : TEXCOORD0;
    float2 uv1        : TEXCOORD1;
    float2 uv2        : TEXCOORD2;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CozyDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS   : TEXCOORD0;
    float2 uv0        : TEXCOORD1;
    float2 uv1        : TEXCOORD2;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Forward declaration - provided by the including shader.
float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS);

// Shadow casting light parameters (set by URP's ShadowUtils).
float3 _LightDirection;
float3 _LightPosition;

CozyDepthVaryings CozyDepthCommonVertex(CozyDepthAttributes input, out float3 positionWS, out float3 normalWS)
{
    CozyDepthVaryings output = (CozyDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    COZY_DEPTH_PRE_TRANSFORM(input)
    positionWS = TransformObjectToWorld(input.positionOS.xyz);
    normalWS = TransformObjectToWorldNormal(input.normalOS);
    positionWS = CozyDepthDisplace(input, positionWS, normalWS);

    output.normalWS = normalWS;
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    output.color = input.color;
    return output;
}

// --------------------------------------------------------------------------
// ShadowCaster
// --------------------------------------------------------------------------
CozyDepthVaryings CozyShadowVertex(CozyDepthAttributes input)
{
    float3 positionWS, normalWS;
    CozyDepthVaryings output = CozyDepthCommonVertex(input, positionWS, normalWS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(positionCS);
    return output;
}

half4 CozyShadowFragment(CozyDepthVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    COZY_DEPTH_FRAGMENT_CLIP(input)
    return 0;
}

// --------------------------------------------------------------------------
// DepthOnly
// --------------------------------------------------------------------------
CozyDepthVaryings CozyDepthOnlyVertex(CozyDepthAttributes input)
{
    float3 positionWS, normalWS;
    CozyDepthVaryings output = CozyDepthCommonVertex(input, positionWS, normalWS);
    output.positionCS = TransformWorldToHClip(positionWS);
    return output;
}

half CozyDepthOnlyFragment(CozyDepthVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    COZY_DEPTH_FRAGMENT_CLIP(input)
    return input.positionCS.z;
}

// --------------------------------------------------------------------------
// DepthNormals (needed for SSAO "Depth Normals" source and decals)
// --------------------------------------------------------------------------
CozyDepthVaryings CozyDepthNormalsVertex(CozyDepthAttributes input)
{
    return CozyDepthOnlyVertex(input);
}

void CozyDepthNormalsFragment(CozyDepthVaryings input, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    COZY_DEPTH_FRAGMENT_CLIP(input)

    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
    outNormalWS = half4(packedNormalWS, 0.0);
#else
    outNormalWS = half4(normalWS, 0.0);
#endif

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif // COZY_DEPTH_PASSES_INCLUDED
