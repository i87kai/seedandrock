// Cozy Stylized Rendering Framework - Unity Terrain shader (MapMagic 2 output).
// Drop-in replacement for "Universal Render Pipeline/Terrain/Lit" on a Terrain
// component: reads the terrain control (splat) map and the first four Terrain
// Layers, blends them softly, then adds the Cozy look on top:
//   * per-layer colour tint + saturation (paint the mood without new textures)
//   * distance macro variation and detail noise to break tiling
//   * slope-driven rock tint, snow caps, wet shoreline darkening (world-space Y)
//   * shared Cozy soft toon lighting, warm shadows, rim, URP shadows/fog/SSAO
// Supports terrain instancing (draw-instanced), holes and per-pixel normals.
// Limitation: terrains with more than 4 layers only render the first 4 (no
// add-pass); keep MapMagic layer count <= 4 per terrain or split by tint.
Shader "Cozy/Terrain"
{
    Properties
    {
        [Header(Layer Tints (multiplies each Terrain Layer))]
        _LayerTint0 ("Layer 0 Tint", Color) = (1, 1, 1, 1)
        _LayerTint1 ("Layer 1 Tint", Color) = (1, 1, 1, 1)
        _LayerTint2 ("Layer 2 Tint", Color) = (1, 1, 1, 1)
        _LayerTint3 ("Layer 3 Tint", Color) = (1, 1, 1, 1)
        _LayerSaturation ("Layer Saturation", Range(0, 2)) = 1.15
        _TextureInfluence ("Texture Detail Influence", Range(0, 1)) = 0.85
        _BlendSharpness ("Splat Blend Sharpness", Range(1, 8)) = 2.5

        [Header(Procedural Variation)]
        _MacroScale ("Macro Variation Scale (m)", Range(5, 400)) = 90
        _MacroStrength ("Macro Variation Strength", Range(0, 1)) = 0.22
        _MacroColorA ("Macro Tint A", Color) = (0.92, 1.0, 0.86, 1)
        _MacroColorB ("Macro Tint B", Color) = (1.0, 0.94, 0.82, 1)
        _DetailScale ("Detail Noise Scale (m)", Range(0.5, 20)) = 3
        _DetailStrength ("Detail Noise Strength", Range(0, 0.5)) = 0.08

        [Header(Slope Rock)]
        _SlopeColor ("Slope Rock Color", Color) = (0.56, 0.52, 0.50, 1)
        _SlopeStart ("Slope Start (0 flat .. 1 vertical)", Range(0, 1)) = 0.45
        _SlopeSharpness ("Slope Sharpness", Range(0.02, 1)) = 0.2
        _SlopeStrength ("Slope Rock Strength", Range(0, 1)) = 0.85

        [Header(Snow and Shoreline (world Y))]
        _SnowColor ("Snow Color", Color) = (0.96, 0.98, 1.0, 1)
        _SnowHeight ("Snow Start Height (m)", Float) = 120
        _SnowBlend ("Snow Blend (m)", Range(0.5, 80)) = 20
        _SnowStrength ("Snow Strength", Range(0, 1)) = 0
        _WaterLevel ("Water Level (m)", Float) = 0
        _WetHeight ("Wet Shoreline Height (m)", Range(0, 10)) = 1.5
        _WetDarkening ("Wet Darkening", Range(0, 1)) = 0.35

        [Header(Soft Toon Lighting)]
        _RampOffset ("Light Ramp Offset", Range(-1, 1)) = 0.05
        _RampSoftness ("Light Ramp Softness", Range(0.02, 1)) = 0.6
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.25
        _ShadowTint ("Shadow Tint", Color) = (0.60, 0.56, 0.72, 1)
        _ShadowTintStrength ("Shadow Tint Strength", Range(0, 1)) = 0.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.15
        _SpecularSoftness ("Specular Softness", Range(0.01, 0.5)) = 0.3
        _RimColor ("Rim Color", Color) = (1.0, 0.95, 0.85, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.12
        _RimPower ("Rim Power", Range(0.5, 8)) = 4

        // --- set by the terrain engine ---
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat3 ("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Splat0 ("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _TerrainHolesTexture ("Holes Map (RGB)", 2D) = "white" {}
        [HideInInspector] _NumLayersCount ("Total Layer Count", Float) = 1.0
        [ToggleUI] _EnableInstancedPerPixelNormal ("Enable Instanced per-pixel normal", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="False" "TerrainCompatible"="True" }
        LOD 200

        HLSLINCLUDE
        #include "CozyCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _LayerTint0, _LayerTint1, _LayerTint2, _LayerTint3;
            half  _LayerSaturation;
            half  _TextureInfluence;
            half  _BlendSharpness;
            half  _MacroScale;
            half  _MacroStrength;
            half4 _MacroColorA;
            half4 _MacroColorB;
            half  _DetailScale;
            half  _DetailStrength;
            half4 _SlopeColor;
            half  _SlopeStart;
            half  _SlopeSharpness;
            half  _SlopeStrength;
            half4 _SnowColor;
            float _SnowHeight;
            half  _SnowBlend;
            half  _SnowStrength;
            float _WaterLevel;
            half  _WetHeight;
            half  _WetDarkening;
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
            // Terrain engine
            float4 _Control_ST;
            float4 _Control_TexelSize;
            half4  _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
            half   _NumLayersCount;
        #ifdef UNITY_INSTANCING_ENABLED
            float4 _TerrainHeightmapRecipSize;
        #endif
            float4 _TerrainHeightmapScale;
        CBUFFER_END

        TEXTURE2D(_Control); SAMPLER(sampler_Control);
        TEXTURE2D(_Splat0);  SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);
        TEXTURE2D(_Splat2);
        TEXTURE2D(_Splat3);

        #if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
            #define COZY_TERRAIN_PERPIXEL_NORMAL
        #endif

        #ifdef UNITY_INSTANCING_ENABLED
            TYPED_TEXTURE2D(float4, _TerrainHeightmapTexture);
            TEXTURE2D(_TerrainNormalmapTexture);
            SAMPLER(sampler_TerrainNormalmapTexture);
        #endif

        UNITY_INSTANCING_BUFFER_START(Terrain)
            UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData) // xBase, yBase, skipScale
        UNITY_INSTANCING_BUFFER_END(Terrain)

        #ifdef _ALPHATEST_ON
            TEXTURE2D(_TerrainHolesTexture); SAMPLER(sampler_TerrainHolesTexture);
            void CozyTerrainClipHoles(float2 uv)
            {
                clip(SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r == 0.0 ? -1 : 1);
            }
        #else
            void CozyTerrainClipHoles(float2 uv) {}
        #endif

        // Unity Terrain draw-instancing: rebuild position/normal/uv from the heightmap.
        void CozyTerrainInstancing(inout float4 positionOS, inout float3 normalOS, inout float2 uv)
        {
        #ifdef UNITY_INSTANCING_ENABLED
            float2 patchVertex = positionOS.xy;
            float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
            float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z;
            float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));
            positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
            positionOS.y = height * _TerrainHeightmapScale.y;
        #ifdef COZY_TERRAIN_PERPIXEL_NORMAL
            normalOS = float3(0, 1, 0);
        #else
            normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
        #endif
            uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
        #endif
        }

        half3 CozyTerrainNormalWS(float2 uv, half3 vertexNormalWS)
        {
        #ifdef COZY_TERRAIN_PERPIXEL_NORMAL
            float2 sampleCoords = (uv / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
            half3 n = half3(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1);
            return normalize(TransformObjectToWorldNormal(n));
        #else
            return normalize(vertexNormalWS);
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

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
            #pragma multi_compile_fragment __ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL

            #include "CozyLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2; // terrain 0..1
                float4 uvSplat01   : TEXCOORD3;
                float4 uvSplat23   : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                half   fogFactor   : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CozyTerrainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                CozyTerrainInstancing(input.positionOS, input.normalOS, input.uv);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.uvSplat01.xy = TRANSFORM_TEX(input.uv, _Splat0);
                output.uvSplat01.zw = TRANSFORM_TEX(input.uv, _Splat1);
                output.uvSplat23.xy = TRANSFORM_TEX(input.uv, _Splat2);
                output.uvSplat23.zw = TRANSFORM_TEX(input.uv, _Splat3);
                output.shadowCoord = CozyGetShadowCoord(positionWS, output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 CozyTerrainFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                CozyTerrainClipHoles(input.uv);

                // --- splat weights (sample at texel centres like URP does) ---
                float2 splatUV = (input.uv * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);
                control = pow(max(control, 1e-3h), _BlendSharpness);
                half weightSum = dot(control, half4(1, 1, 1, 1));
                control /= max(weightSum, 1e-3h);

                half3 l0 = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, input.uvSplat01.xy).rgb * _LayerTint0.rgb;
                half3 l1 = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, input.uvSplat01.zw).rgb * _LayerTint1.rgb;
                half3 l2 = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, input.uvSplat23.xy).rgb * _LayerTint2.rgb;
                half3 l3 = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, input.uvSplat23.zw).rgb * _LayerTint3.rgb;
                half3 albedo = l0 * control.r + l1 * control.g + l2 * control.b + l3 * control.a;

                // Flatten texture detail toward the flat layer tints so the look reads as painted, not photographic.
                half3 tintOnly = (_LayerTint0.rgb * control.r + _LayerTint1.rgb * control.g +
                                  _LayerTint2.rgb * control.b + _LayerTint3.rgb * control.a) * 0.5h;
                albedo = lerp(tintOnly, albedo, _TextureInfluence);
                albedo = CozySaturation(albedo, _LayerSaturation);

                // --- procedural variation ---
                float2 wxz = input.positionWS.xz;
                half macro = CozyFbm(wxz / _MacroScale, 3);
                half3 macroTint = lerp(_MacroColorA.rgb, _MacroColorB.rgb, macro);
                albedo *= lerp(half3(1, 1, 1), macroTint, _MacroStrength);
                half detail = CozyValueNoise(wxz / _DetailScale) - 0.5h;
                albedo *= 1.0h + detail * _DetailStrength * 2.0h;

                // --- slope rock, snow, wet shoreline ---
                half3 normalWS = CozyTerrainNormalWS(input.uv, input.normalWS);
                half slope = 1.0h - saturate(normalWS.y);
                half rock = CozySoftStep(_SlopeStart, _SlopeSharpness, slope) * _SlopeStrength;
                albedo = lerp(albedo, _SlopeColor.rgb * (0.85h + detail * 0.6h), rock);

                half snow = saturate((input.positionWS.y - _SnowHeight) / _SnowBlend) * saturate(normalWS.y * 1.4h - 0.3h) * _SnowStrength;
                albedo = lerp(albedo, _SnowColor.rgb, snow);

                half wet = 1.0h - saturate((input.positionWS.y - _WaterLevel) / max(_WetHeight, 0.01h));
                albedo *= 1.0h - wet * _WetDarkening;

                // --- lighting ---
                CozySurface s = CozyInitSurface();
                s.albedo = albedo;
                s.normalWS = normalWS;
                s.positionWS = input.positionWS;
                s.viewDirWS = half3(normalize(GetCameraPositionWS() - input.positionWS));
                s.shadowCoord = input.shadowCoord;
                s.screenUV = CozyScreenUV(input.positionCS);

                CozyStyle k;
                k.rampOffset = _RampOffset;
                k.rampSoftness = _RampSoftness;
                k.lightWrap = _LightWrap;
                k.shadowTint = _ShadowTint.rgb;
                k.shadowTintStrength = _ShadowTintStrength;
                k.smoothness = lerp(_Smoothness, 0.6h, wet * 0.5h);
                k.specularStrength = _SpecularStrength * (1.0h + wet + snow);
                k.specularSoftness = _SpecularSoftness;
                k.rimColor = _RimColor.rgb;
                k.rimStrength = _RimStrength;
                k.rimPower = _RimPower;
                k.rimLightMask = 0.6h;
                k.translucency = 0.0h;
                k.translucencyColor = half3(0, 0, 0);
                k.saturation = 1.0h;

                half3 color = CozyShade(s, k);
                color = CozyApplyFog(color, input.positionWS, input.fogFactor);
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

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyShadowVertex
            #pragma fragment CozyShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_fragment __ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #include "CozyTerrainDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthOnlyVertex
            #pragma fragment CozyDepthOnlyFragment
            #pragma multi_compile_fragment __ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #include "CozyTerrainDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CozyDepthNormalsVertex
            #pragma fragment CozyDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment __ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
            #include "CozyTerrainDepth.hlsl"
            ENDHLSL
        }

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
        UsePass "Universal Render Pipeline/Terrain/Lit/SceneSelectionPass"
    }

    Fallback "Universal Render Pipeline/Terrain/Lit"
}
