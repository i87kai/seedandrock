// Cozy Stylized Rendering Framework - grass.
// Opaque, double-sided. World-space travelling wind waves + gusts with the root
// pinned, root-to-tip gradient, backlight translucency and soft lighting.
//
// Shape modes:  Procedural (tapered blade carved from a quad, no texture),
//               Texture (alpha-clipped grass card), Solid (mesh as-is).
// Wind source:  Object pivot (default, prefab/MapMagic grass meshes: root at
//               object origin, tip = object-space Y / Blade Height) or Vertex
//               data (UV0.y = 0 root..1 tip, UV1.x = per-blade random).
Shader "Cozy/Grass"
{
    Properties
    {
        [Header(Texture (optional))]
        [MainTexture] _BaseMap ("Grass Texture (Texture mode, A = alpha)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Colors)]
        _BaseColor ("Root Color", Color) = (0.20, 0.42, 0.14, 1)
        _TipColor ("Tip Color", Color) = (0.72, 0.88, 0.36, 1)
        _TipPower ("Gradient Power", Range(0.2, 4)) = 1.4
        _VertexColorStrength ("Biome Vertex Color Influence", Range(0, 1)) = 0.7
        _RootDarkening ("Root Ambient Occlusion", Range(0, 1)) = 0.45
        _BendBrightening ("Wind Bend Brightening", Range(0, 1)) = 0.25
        _Saturation ("Saturation", Range(0, 2)) = 1.15

        [Header(Shape)]
        [KeywordEnum(Procedural, Texture, Solid)] _Shape ("Blade Shape", Float) = 0
        _BladeHeight ("Blade Height (Object wind: metres to tip)", Range(0.05, 3)) = 0.8
        _BladeTaper ("Blade Taper", Range(0.5, 4)) = 1.6
        _BladeCurve ("Blade Curve", Range(0, 1)) = 0.35
        _NormalUpBlend ("Normal Up Blend", Range(0, 1)) = 0.7

        [Header(Wind)]
        [KeywordEnum(Object, Vertex)] _WindSource ("Wind Source (Object pivot / Vertex data)", Float) = 0
        _WindInfluence ("Wind Influence", Range(0, 3)) = 1

        [Header(Soft Toon Lighting)]
        _RampOffset ("Light Ramp Offset", Range(-1, 1)) = 0.1
        _RampSoftness ("Light Ramp Softness", Range(0.02, 1)) = 0.55
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.5
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.58, 0.72, 1)
        _ShadowTintStrength ("Shadow Tint Strength", Range(0, 1)) = 0.55
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.2
        _SpecularSoftness ("Specular Softness", Range(0.01, 0.5)) = 0.3
        _Translucency ("Backlight Strength", Range(0, 3)) = 1.2
        _TranslucencyColor ("Backlight Color", Color) = (1.0, 0.96, 0.55, 1)
        _RimColor ("Rim Color", Color) = (1.0, 0.97, 0.85, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "IgnoreProjector"="True" }
        LOD 200

        HLSLINCLUDE
        #include "CozyCommon.hlsl"
        #include "CozyWind.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half  _Cutoff;
            half  _BladeHeight;
            half4 _BaseColor;
            half4 _TipColor;
            half  _TipPower;
            half  _VertexColorStrength;
            half  _RootDarkening;
            half  _BendBrightening;
            half  _Saturation;
            half  _BladeTaper;
            half  _BladeCurve;
            half  _NormalUpBlend;
            half  _WindInfluence;
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
        CBUFFER_END

        // Tapered blade silhouette carved out of the quad. Returns the signed
        // distance to the blade edge (positive inside).
        half CozyBladeShape(float2 uv)
        {
            half tip = saturate(uv.y);
            half halfWidth = 0.5h * (1.0h - pow(tip, _BladeTaper));
            // Slight sideways curve so blades read as leaves rather than triangles.
            half curve = (tip * tip) * _BladeCurve * 0.25h;
            return halfWidth - abs(uv.x - 0.5h - curve);
        }

        // Fragment clip for the selected shape mode (shared by all passes).
        void CozyGrassClip(float2 uv)
        {
        #if defined(_SHAPE_PROCEDURAL)
            clip(CozyBladeShape(uv));
        #elif defined(_SHAPE_TEXTURE)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap)).a - _Cutoff);
        #endif
        }

        // 0 at the root, 1 at the tip, and a per-blade random.
        void CozyGrassTip(float3 positionOS, float2 uv0, float2 uv1, out float tip, out float random)
        {
        #if defined(_WINDSOURCE_VERTEX)
            tip = saturate(uv0.y);
            random = uv1.x;
        #else
            float scaleY = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
            tip = saturate(positionOS.y * scaleY / _BladeHeight);
            random = CozyObjectRandom();
        #endif
        }

        float3 CozyGrassDisplace(float3 positionOS, float3 positionWS, float2 uv0, float2 uv1, out float bend)
        {
            float tip, random;
            CozyGrassTip(positionOS, uv0, uv1, tip, random);
            return CozyGrassWind(positionWS, tip, random, _WindInfluence, bend);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyGrassVertex
            #pragma fragment CozyGrassFragment

            #pragma shader_feature_local _SHAPE_PROCEDURAL _SHAPE_TEXTURE _SHAPE_SOLID
            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
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
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                // x: bend amount, y: fog factor, z: per-blade random, w: root..tip
                half4  data        : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half4  color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CozyGrassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float tip, random;
                CozyGrassTip(input.positionOS.xyz, input.uv0, input.uv1, tip, random);
                float bend;
                positionWS = CozyGrassWind(positionWS, tip, random, _WindInfluence, bend);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.uv = input.uv0;
                output.data = half4(bend, ComputeFogFactor(output.positionCS.z), random, tip);
                output.shadowCoord = CozyGetShadowCoord(positionWS, output.positionCS);
                output.color = input.color;
                return output;
            }

            half4 CozyGrassFragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                CozyGrassClip(input.uv);

                half tip = input.data.w;
                half3 normalWS = normalize(input.normalWS);
                normalWS = IS_FRONT_VFACE(isFrontFace, normalWS, -normalWS);
                // Blend towards "up" so the thin double-sided quads shade like a soft carpet.
                normalWS = normalize(lerp(normalWS, half3(0, 1, 0), _NormalUpBlend));

                half3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, pow(tip, _TipPower));
            #if defined(_SHAPE_TEXTURE)
                albedo *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(input.uv, _BaseMap)).rgb;
            #endif
                half3 biomeTint = input.color.rgb / max(CozyLuminance(input.color.rgb), 0.05h);
                albedo *= lerp(half3(1, 1, 1), biomeTint, _VertexColorStrength);
                // Subtle per-blade brightness variation.
                albedo *= 0.9h + input.data.z * 0.2h;
                // Bent blades catch more light.
                albedo *= 1.0h + input.data.x * _BendBrightening * tip;

                CozySurface s = CozyInitSurface();
                s.albedo = albedo;
                s.normalWS = normalWS;
                s.positionWS = input.positionWS;
                s.viewDirWS = half3(normalize(GetCameraPositionWS() - input.positionWS));
                s.shadowCoord = input.shadowCoord;
                s.screenUV = CozyScreenUV(input.positionCS);
                s.occlusion = lerp(1.0h - _RootDarkening, 1.0h, tip);
                s.thickness = tip;

                CozyStyle k;
                k.rampOffset = _RampOffset;
                k.rampSoftness = _RampSoftness;
                k.lightWrap = _LightWrap;
                k.shadowTint = _ShadowTint.rgb;
                k.shadowTintStrength = _ShadowTintStrength;
                k.smoothness = _Smoothness;
                k.specularStrength = _SpecularStrength * tip;
                k.specularSoftness = _SpecularSoftness;
                k.rimColor = _RimColor.rgb;
                k.rimStrength = _RimStrength * tip;
                k.rimPower = 3.0h;
                k.rimLightMask = 0.8h;
                k.translucency = _Translucency;
                k.translucencyColor = _TranslucencyColor.rgb;
                k.saturation = _Saturation;

                half3 color = CozyShade(s, k);
                color = CozyApplyFog(color, input.positionWS, input.data.y);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Grass shadows are optional for performance: turn "Cast Shadows" off on
        // dense grass renderers/detail prototypes. The pass is here for hero grass.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyShadowVertex
            #pragma fragment CozyShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _SHAPE_PROCEDURAL _SHAPE_TEXTURE _SHAPE_SOLID
            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma multi_compile_instancing
            #include "CozyGrassDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthOnlyVertex
            #pragma fragment CozyDepthOnlyFragment
            #pragma shader_feature_local _SHAPE_PROCEDURAL _SHAPE_TEXTURE _SHAPE_SOLID
            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma multi_compile_instancing
            #include "CozyGrassDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthNormalsVertex
            #pragma fragment CozyDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _SHAPE_PROCEDURAL _SHAPE_TEXTURE _SHAPE_SOLID
            #pragma shader_feature_local _WINDSOURCE_OBJECT _WINDSOURCE_VERTEX
            #pragma multi_compile_instancing
            #include "CozyGrassDepth.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
