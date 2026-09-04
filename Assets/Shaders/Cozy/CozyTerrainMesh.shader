// Cozy Stylized Rendering Framework - MESH terrain (MeshRenderer based, e.g. the legacy
// SeedAndRock procedural mesh). For Unity Terrain / MapMagic use Cozy/Terrain instead.
// Consumes the vertex data written by WorldMeshBuilder.BuildTerrain:
//   COLOR.rgb = biome tint, COLOR.a = broad elevation 0..1
// Adds slope rock, macro/detail noise variation (no tiling textures), wet
// shoreline darkening, high-altitude tinting and the shared Cozy soft lighting.
Shader "Cozy/Terrain Mesh"
{
    Properties
    {
        [Header(Biome Colors)]
        _LowColor ("Lowland Tint", Color) = (0.86, 0.92, 0.72, 1)
        _HighColor ("Highland Tint", Color) = (1.06, 1.02, 0.94, 1)
        _VertexColorStrength ("Biome Vertex Color Influence", Range(0, 1)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1.15

        [Header(Slope Rock)]
        _RockColor ("Rock Color", Color) = (0.46, 0.42, 0.40, 1)
        _RockColorDark ("Rock Shadow Color", Color) = (0.30, 0.27, 0.29, 1)
        _SlopeStart ("Rock Slope Start", Range(0, 1)) = 0.45
        _SlopeSoftness ("Rock Slope Softness", Range(0.01, 0.6)) = 0.2

        [Header(Snow Caps)]
        _SnowColor ("Snow Color", Color) = (0.94, 0.96, 1.0, 1)
        _SnowHeight ("Snow Height (world Y)", Float) = 26
        _SnowSoftness ("Snow Blend Height", Range(0.1, 30)) = 6

        [Header(Shoreline)]
        _WaterLevel ("Water Level (world Y)", Float) = 3.5
        _WetHeight ("Wet Band Height", Range(0, 6)) = 1.4
        _WetDarkening ("Wet Darkening", Range(0, 1)) = 0.35
        _SandColor ("Sand Color", Color) = (0.80, 0.72, 0.52, 1)
        _SandHeight ("Sand Band Height", Range(0, 8)) = 1.1

        [Header(Procedural Variation)]
        _MacroScale ("Macro Noise Scale", Range(0.001, 0.2)) = 0.02
        _MacroStrength ("Macro Variation", Range(0, 0.6)) = 0.16
        _DetailScale ("Detail Noise Scale", Range(0.05, 4)) = 0.6
        _DetailStrength ("Detail Variation", Range(0, 0.5)) = 0.08

        [Header(Soft Toon Lighting)]
        _RampOffset ("Light Ramp Offset", Range(-1, 1)) = 0.2
        _RampSoftness ("Light Ramp Softness", Range(0.02, 1)) = 0.5
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
        _ShadowTint ("Shadow Tint", Color) = (0.70, 0.60, 0.66, 1)
        _ShadowTintStrength ("Shadow Tint Strength", Range(0, 1)) = 0.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.08
        _SpecularSoftness ("Specular Softness", Range(0.01, 0.5)) = 0.3
        _RimColor ("Rim Color", Color) = (1.0, 0.95, 0.85, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.12
        _RimPower ("Rim Power", Range(0.5, 8)) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        #include "CozyCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _LowColor;
            half4 _HighColor;
            half  _VertexColorStrength;
            half  _Saturation;
            half4 _RockColor;
            half4 _RockColorDark;
            half  _SlopeStart;
            half  _SlopeSoftness;
            half4 _SnowColor;
            float _SnowHeight;
            float _SnowSoftness;
            float _WaterLevel;
            float _WetHeight;
            half  _WetDarkening;
            half4 _SandColor;
            float _SandHeight;
            float _MacroScale;
            half  _MacroStrength;
            float _DetailScale;
            half  _DetailStrength;
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
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyTerrainVertex
            #pragma fragment CozyTerrainFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "CozyLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  color      : COLOR;
                half   fogFactor  : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings CozyTerrainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.shadowCoord = CozyGetShadowCoord(positionWS, output.positionCS);
                return output;
            }

            half4 CozyTerrainFragment(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float3 positionWS = input.positionWS;

                // --- Base albedo from biome vertex colour + elevation tint -------
                half3 biome = lerp(half3(0.5h, 0.6h, 0.35h), input.color.rgb, _VertexColorStrength);
                half elevation = input.color.a;
                half3 albedo = biome * lerp(_LowColor.rgb, _HighColor.rgb, elevation);

                // --- Procedural variation (breaks up flat colour, no textures) ---
                float macro = CozyFbm(positionWS.xz * _MacroScale, 3);
                float detail = CozyNoise2(positionWS.xz * _DetailScale);
                albedo *= 1.0h + (macro - 0.5h) * _MacroStrength * 2.0h;
                albedo *= 1.0h + (detail - 0.5h) * _DetailStrength * 2.0h;

                // --- Slope rock ---------------------------------------------------
                half slope = 1.0h - saturate(normalWS.y);
                half rockMask = CozySoftStep(_SlopeStart, _SlopeSoftness, slope + (macro - 0.5h) * 0.15h);
                half3 rock = lerp(_RockColorDark.rgb, _RockColor.rgb, saturate(macro * 0.6h + detail * 0.4h + 0.15h));
                albedo = lerp(albedo, rock, rockMask);

                // --- Shoreline: sand band then wet darkening near the water -------
                float aboveWater = positionWS.y - _WaterLevel;
                half sandMask = (1.0h - smoothstep(0.0, _SandHeight, aboveWater + (detail - 0.5) * 0.6)) * (1.0h - rockMask);
                albedo = lerp(albedo, _SandColor.rgb, sandMask * 0.85h);
                half wetMask = 1.0h - smoothstep(-0.5, _WetHeight, aboveWater);
                albedo *= 1.0h - wetMask * _WetDarkening;

                // --- Snow caps on high, flat-ish ground ---------------------------
                half snowMask = smoothstep(_SnowHeight - _SnowSoftness, _SnowHeight + _SnowSoftness, positionWS.y + (macro - 0.5) * _SnowSoftness);
                snowMask *= saturate(normalWS.y * 1.6h - 0.3h);
                albedo = lerp(albedo, _SnowColor.rgb, snowMask);

                // --- Cozy lighting ------------------------------------------------
                CozySurface s = CozyInitSurface();
                s.albedo = albedo;
                s.normalWS = normalWS;
                s.positionWS = positionWS;
                s.viewDirWS = half3(normalize(GetCameraPositionWS() - positionWS));
                s.shadowCoord = input.shadowCoord;
                s.screenUV = CozyScreenUV(input.positionCS);

                CozyStyle k;
                k.rampOffset = _RampOffset;
                k.rampSoftness = _RampSoftness;
                k.lightWrap = _LightWrap;
                k.shadowTint = _ShadowTint.rgb;
                k.shadowTintStrength = _ShadowTintStrength;
                k.smoothness = lerp(_Smoothness, 0.7h, wetMask);
                k.specularStrength = _SpecularStrength * (1.0h + wetMask * 3.0h + snowMask);
                k.specularSoftness = _SpecularSoftness;
                k.rimColor = _RimColor.rgb;
                k.rimStrength = _RimStrength * (1.0h + snowMask * 2.0h);
                k.rimPower = _RimPower;
                k.rimLightMask = 0.7h;
                k.translucency = 0.0h;
                k.translucencyColor = half3(1, 1, 1);
                k.saturation = _Saturation;

                half3 color = CozyShade(s, k);
                color = CozyApplyFog(color, positionWS, input.fogFactor);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyShadowVertex
            #pragma fragment CozyShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "CozyDepthPasses.hlsl"
            float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS) { return positionWS; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthOnlyVertex
            #pragma fragment CozyDepthOnlyFragment
            #include "CozyDepthPasses.hlsl"
            float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS) { return positionWS; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthNormalsVertex
            #pragma fragment CozyDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "CozyDepthPasses.hlsl"
            float3 CozyDepthDisplace(CozyDepthAttributes input, float3 positionWS, float3 normalWS) { return positionWS; }
            ENDHLSL
        }
    }

    FallBack Off
}
