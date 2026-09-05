// Cozy Stylized Rendering Framework - tree canopy / bush foliage.
// World-space wind bending (shared with Cozy/Lit trunks so trees stay whole),
// leaf flutter, rounded "puffy" normals, height gradient, per-tree colour
// variation and soft translucency / backlighting.
//
// Wind source (material dropdown):
//   Object pivot (default) - one transform per tree (MapMagic / Terrain trees / prefabs).
//   Vertex data            - batched meshes carrying the CozyWind.hlsl UV contract.
// Optional _BaseMap with alpha clip for textured leaf cards.
Shader "Cozy/Foliage"
{
    Properties
    {
        [Header(Texture (optional))]
        [MainTexture] _BaseMap ("Leaf Texture (optional, A = alpha)", 2D) = "white" {}
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Colors)]
        _BaseColor ("Canopy Bottom Color", Color) = (0.22, 0.46, 0.20, 1)
        _TopColor ("Canopy Top Color", Color) = (0.62, 0.84, 0.34, 1)
        _GradientPower ("Gradient Power", Range(0.2, 4)) = 1.3
        _VertexColorStrength ("Biome Vertex Color Influence", Range(0, 1)) = 0.55
        _VariationColor ("Per-Tree Variation Color", Color) = (0.86, 0.72, 0.30, 1)
        _ColorVariation ("Per-Tree Variation", Range(0, 1)) = 0.3
        _Saturation ("Saturation", Range(0, 2)) = 1.15
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Shape)]
        _NormalSmoothing ("Rounded Normal Blend", Range(0, 1)) = 0.6
        _CanopyCenterHeight ("Canopy Center Height (0..1 of canopy)", Range(0, 1)) = 0.4

        [Header(Wind)]
        [KeywordEnum(Object, Vertex)] _WindSource ("Wind Source (Object pivot / Vertex data)", Float) = 0
        _WindInfluence ("Bend Influence (keep = trunk)", Range(0, 2)) = 1
        _FlutterStrength ("Leaf Flutter", Range(0, 1)) = 0.15

        [Header(Soft Toon Lighting)]
        _RampOffset ("Light Ramp Offset", Range(-1, 1)) = 0.15
        _RampSoftness ("Light Ramp Softness", Range(0.02, 1)) = 0.5
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.4
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.55, 0.70, 1)
        _ShadowTintStrength ("Shadow Tint Strength", Range(0, 1)) = 0.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.15
        _SpecularSoftness ("Specular Softness", Range(0.01, 0.5)) = 0.3

        [Header(Translucency and Rim)]
        _Translucency ("Backlight Strength", Range(0, 3)) = 1.0
        _TranslucencyColor ("Backlight Color", Color) = (1.0, 0.95, 0.55, 1)
        _RimColor ("Rim Color", Color) = (1.0, 0.96, 0.80, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.4
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimLightMask ("Rim Only On Lit Side", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        #include "CozyCommon.hlsl"
        #include "CozyWind.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half  _Cutoff;
            half4 _BaseColor;
            half4 _TopColor;
            half  _GradientPower;
            half  _VertexColorStrength;
            half4 _VariationColor;
            half  _ColorVariation;
            half  _Saturation;
            half  _NormalSmoothing;
            half  _CanopyCenterHeight;
            half  _WindInfluence;
            half  _FlutterStrength;
            half  _RampOffset;
            half  _RampSoftness;
            half  _LightWrap;
            half4 _ShadowTint;
            half  _ShadowTintStrength;
            half  _Smoothness;
            half  _SpecularStrength;
            half  _SpecularSoftness;
            half  _Translucency;
            half4 _TranslucencyColor;
            half4 _RimColor;
            half  _RimStrength;
            half  _RimPower;
            half  _RimLightMask;
        CBUFFER_END

        // Shared by the lit pass and the depth passes so shadows match.
        // Canopy 0..1 height: from vertex data, or from object-space Y relative to the mesh
        // bounds proxy (_CanopyBase/_CanopyTop are derived per object below).
        float CozyCanopyT(float3 positionOS, float2 uv2)
        {
        #if defined(_WINDSOURCE_VERTEX)
            return uv2.x;
        #else
            // Object mode: assume the canopy spans the object's positive Y range; scale so a
            // ~4 m prefab maps to 0..1. Artists can shift with _CanopyCenterHeight.
            return saturate(positionOS.y * 0.3);
        #endif
        }

        float3 CozyFoliageDisplace(float3 positionOS, float3 positionWS, float3 normalWS, float2 uv0, float2 uv1, float2 uv2)
        {
            float3 pivotWS; float heightAboveBase; float random;
            CozyResolveWindInputs(positionOS, uv0, uv1, pivotWS, heightAboveBase, random);
            positionWS = CozyTreeBend(positionWS, pivotWS, heightAboveBase, random, _WindInfluence);
            // Flutter fades in from the bottom of the canopy so it never tears away from the trunk.
            float flutterMask = saturate(CozyCanopyT(positionOS, uv2) * 2.0 + 0.2);
            positionWS = CozyLeafFlutter(positionWS, normalWS, flutterMask, _FlutterStrength);
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyFoliageVertex
            #pragma fragment CozyFoliageFragment

            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "CozyLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                half3  roundNormalWS : TEXCOORD2;
                // x: canopy 0..1, y: per-tree random, z: fog factor
                half3  data        : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float2 uv          : TEXCOORD5;
                half4  color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CozyFoliageVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float3 pivotWS; float heightAboveBase; float random;
                CozyResolveWindInputs(input.positionOS.xyz, input.uv0, input.uv1, pivotWS, heightAboveBase, random);
                float canopyT = CozyCanopyT(input.positionOS.xyz, input.uv2);

                // Puffy normal: from the canopy centre to the vertex; gives soft,
                // rounded shading regardless of the low-poly geometry.
            #if defined(_WINDSOURCE_VERTEX)
                float canopyHeight = max(input.uv2.y, 0.01);
            #else
                float canopyHeight = max(heightAboveBase / max(canopyT, 0.05), 0.5);
            #endif
                float canopyBottom = heightAboveBase - canopyT * canopyHeight;
                float3 canopyCenter = pivotWS + float3(0.0, canopyBottom + _CanopyCenterHeight * canopyHeight, 0.0);
                half3 roundNormal = half3(normalize(positionWS - canopyCenter + half3(0, 1e-3, 0)));

                positionWS = CozyFoliageDisplace(input.positionOS.xyz, positionWS, normalWS, input.uv0, input.uv1, input.uv2);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.roundNormalWS = roundNormal;
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                output.data = half3(canopyT, random, ComputeFogFactor(output.positionCS.z));
                output.shadowCoord = CozyGetShadowCoord(positionWS, output.positionCS);
                output.color = input.color;
                return output;
            }

            half4 CozyFoliageFragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            #if defined(_ALPHATEST_ON)
                clip(tex.a - _Cutoff);
            #endif
                half canopyT = input.data.x;
                half random = input.data.y;

                half3 normalWS = normalize(lerp(input.normalWS, input.roundNormalWS, _NormalSmoothing));
                normalWS = IS_FRONT_VFACE(isFrontFace, normalWS, -normalWS);

                // --- Colour: vertical gradient, biome tint, per-tree variation -----
                half3 albedo = lerp(_BaseColor.rgb, _TopColor.rgb, pow(saturate(canopyT), _GradientPower)) * tex.rgb;
                half3 biomeTint = input.color.rgb / max(CozyLuminance(input.color.rgb), 0.05h);
                albedo *= lerp(half3(1, 1, 1), biomeTint, _VertexColorStrength);
                half variation = (random - 0.5h) * 2.0h * _ColorVariation;
                albedo = lerp(albedo, albedo * _VariationColor.rgb * 1.6h, saturate(variation));
                albedo = lerp(albedo, albedo * half3(0.85h, 0.95h, 1.1h), saturate(-variation));

                CozySurface s = CozyInitSurface();
                s.albedo = albedo;
                s.normalWS = normalWS;
                s.positionWS = input.positionWS;
                s.viewDirWS = half3(normalize(GetCameraPositionWS() - input.positionWS));
                s.shadowCoord = input.shadowCoord;
                s.screenUV = CozyScreenUV(input.positionCS);
                // Cheap canopy self-occlusion: darker towards the bottom/centre.
                s.occlusion = lerp(0.55h, 1.0h, canopyT);
                s.thickness = lerp(0.35h, 1.0h, canopyT);

                CozyStyle k;
                k.rampOffset = _RampOffset;
                k.rampSoftness = _RampSoftness;
                k.lightWrap = _LightWrap;
                k.shadowTint = _ShadowTint.rgb;
                k.shadowTintStrength = _ShadowTintStrength;
                k.smoothness = _Smoothness;
                k.specularStrength = _SpecularStrength;
                k.specularSoftness = _SpecularSoftness;
                k.rimColor = _RimColor.rgb;
                k.rimStrength = _RimStrength;
                k.rimPower = _RimPower;
                k.rimLightMask = _RimLightMask;
                k.translucency = _Translucency;
                k.translucencyColor = _TranslucencyColor.rgb;
                k.saturation = _Saturation;

                half3 color = CozyShade(s, k);
                color = CozyApplyFog(color, input.positionWS, input.data.z);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyShadowVertex
            #pragma fragment CozyShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "CozyFoliageDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthOnlyVertex
            #pragma fragment CozyDepthOnlyFragment
            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "CozyFoliageDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthNormalsVertex
            #pragma fragment CozyDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "CozyFoliageDepth.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
