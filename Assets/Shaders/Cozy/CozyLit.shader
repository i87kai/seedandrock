// Cozy Stylized Rendering Framework - general purpose surface shader.
// Soft toon ramp, warm shadows, gentle rim, soft specular; supports URP shadows,
// Forward+/additional lights, SSAO, fog, optional texture & alpha clip, vertex
// colours and optional tree-trunk wind bending (shares the canopy wind maths).
Shader "Cozy/Lit"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap ("Base Map (optional)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.8, 0.8, 0.8, 1)
        _VertexColorStrength ("Vertex Color Influence", Range(0, 1)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1.1
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Soft Toon Lighting)]
        _RampOffset ("Light Ramp Offset", Range(-1, 1)) = 0.25
        _RampSoftness ("Light Ramp Softness", Range(0.02, 1)) = 0.45
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.3
        _ShadowTint ("Shadow Tint", Color) = (0.72, 0.58, 0.62, 1)
        _ShadowTintStrength ("Shadow Tint Strength", Range(0, 1)) = 0.6

        [Header(Specular)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.25
        _SpecularSoftness ("Specular Softness", Range(0.01, 0.5)) = 0.25

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1.0, 0.92, 0.8, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.35
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimLightMask ("Rim Only On Lit Side", Range(0, 1)) = 0.6

        [Header(Wind)]
        [KeywordEnum(None, Object, Vertex)] _WindSource ("Wind Bending (None / Object pivot / Vertex data)", Float) = 0
        _WindInfluence ("Wind Influence", Range(0, 2)) = 1

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        #include "CozyCommon.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half  _VertexColorStrength;
            half  _Saturation;
            half  _Cutoff;
            half  _RampOffset;
            half  _RampSoftness;
            half  _LightWrap;
            half4 _ShadowTint;
            half  _ShadowTintStrength;
            half  _Smoothness;
            half  _SpecularStrength;
            half  _SpecularSoftness;
            half4 _RimColor;
            half  _RimStrength;
            half  _RimPower;
            half  _RimLightMask;
            half  _WindInfluence;
            half4 _EmissionColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyLitVertex
            #pragma fragment CozyLitFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WINDSOURCE_NONE _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "CozyLighting.hlsl"
            #include "CozyWind.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half4  color      : COLOR;
                half   fogFactor  : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CozyLitVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if defined(_WINDSOURCE_OBJECT) || defined(_WINDSOURCE_VERTEX)
                float3 pivotWS; float heightAboveBase; float random;
                CozyResolveWindInputs(input.positionOS.xyz, input.uv0, input.uv1, pivotWS, heightAboveBase, random);
                positionWS = CozyTreeBend(positionWS, pivotWS, heightAboveBase, random, _WindInfluence);
            #endif

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.shadowCoord = CozyGetShadowCoord(positionWS, output.positionCS);
                return output;
            }

            half4 CozyLitFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = tex * _BaseColor;
                baseColor.rgb *= lerp(half3(1, 1, 1), input.color.rgb, _VertexColorStrength);
            #if defined(_ALPHATEST_ON)
                clip(baseColor.a - _Cutoff);
            #endif

                CozySurface s = CozyInitSurface();
                s.albedo = baseColor.rgb;
                s.alpha = baseColor.a;
                s.normalWS = normalize(input.normalWS);
                s.positionWS = input.positionWS;
                s.viewDirWS = half3(normalize(GetCameraPositionWS() - input.positionWS));
                s.shadowCoord = input.shadowCoord;
                s.screenUV = CozyScreenUV(input.positionCS);
                s.occlusion = 1.0h;
                s.emission = _EmissionColor.rgb;

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
                k.translucency = 0.0h;
                k.translucencyColor = half3(1, 1, 1);
                k.saturation = _Saturation;

                half3 color = CozyShade(s, k);
                color = CozyApplyFog(color, input.positionWS, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Depth / shadow passes share CozyDepthPasses.hlsl. The displacement
        // hook applies the same trunk bending so shadows follow the wind.
        // ------------------------------------------------------------------
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WINDSOURCE_NONE _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "CozyLitDepth.hlsl"
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WINDSOURCE_NONE _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma multi_compile_instancing
            #include "CozyLitDepth.hlsl"
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WINDSOURCE_NONE _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "CozyLitDepth.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
