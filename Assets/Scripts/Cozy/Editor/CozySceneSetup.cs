#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cozy.Rendering.Editor
{
    /// <summary>
    /// One-click integration of the Cozy Stylized Rendering Framework into any
    /// scene (including MapMagic 2 scenes). Nothing here generates world content;
    /// it only wires rendering: atmosphere, wind, skybox, volume, camera, and
    /// swaps terrain / selected materials to Cozy shaders.
    /// </summary>
    public static class CozySceneSetup
    {
        private const string MenuRoot = "Tools/Cozy Rendering/";

        private const string SkyMaterialPath = "Assets/Materials/Cozy/CozySky.mat";
        private const string TerrainMaterialPath = "Assets/Materials/Cozy/CozyTerrain.mat";
        private const string VolumeProfilePath = "Assets/Settings/Cozy/CozyVolumeProfile.asset";
        private const string UnderwaterProfilePath = "Assets/Settings/Cozy/CozyUnderwaterProfile.asset";

        [MenuItem(MenuRoot + "Setup Cozy Rendering In Scene", false, 0)]
        public static void SetupScene()
        {
            Undo.SetCurrentGroupName("Setup Cozy Rendering");
            int group = Undo.GetCurrentGroup();

            // --- Atmosphere + wind -------------------------------------------------
            var root = GameObject.Find("Cozy Rendering");
            if (root == null)
            {
                root = new GameObject("Cozy Rendering");
                Undo.RegisterCreatedObjectUndo(root, "Cozy Rendering");
            }
            var atmosphere = GetOrAdd<CozyAtmosphere>(root);
            GetOrAdd<CozyWind>(root);

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (skyMat != null)
            {
                atmosphere.skyboxMaterial = skyMat;
                RenderSettings.skybox = skyMat;
            }

            // --- Global volume ----------------------------------------------------
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            Volume global = null;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.isGlobal) { global = v; break; }
            if (global == null)
            {
                var go = new GameObject("Cozy Global Volume");
                Undo.RegisterCreatedObjectUndo(go, "Cozy Global Volume");
                go.transform.SetParent(root.transform);
                global = go.AddComponent<Volume>();
                global.isGlobal = true;
            }
            if (profile != null) global.sharedProfile = profile;

            // Underwater volume: weight is driven by CozyCameraSetup.
            var underwaterProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UnderwaterProfilePath);
            Volume underwater = null;
            var uwT = root.transform.Find("Cozy Underwater Volume");
            if (uwT == null)
            {
                var go = new GameObject("Cozy Underwater Volume");
                Undo.RegisterCreatedObjectUndo(go, "Cozy Underwater Volume");
                go.transform.SetParent(root.transform);
                underwater = go.AddComponent<Volume>();
                underwater.isGlobal = true;
                underwater.priority = 10;
                underwater.weight = 0f;
            }
            else underwater = uwT.GetComponent<Volume>();
            if (underwaterProfile != null) underwater.sharedProfile = underwaterProfile;

            // --- Cameras ----------------------------------------------------------
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.cameraType != CameraType.Game) continue;
                var setup = GetOrAdd<CozyCameraSetup>(cam.gameObject);
                setup.underwaterVolume = underwater;
                EditorUtility.SetDirty(setup);
            }

            // --- URP asset requirements -----------------------------------------
            EnableUrpTextures();

            // --- Terrains ---------------------------------------------------------
            ApplyTerrainMaterial();

            EditorUtility.SetDirty(atmosphere);
            Undo.CollapseUndoOperations(group);
            Debug.Log("[Cozy] Scene setup complete. Tune 'Cozy Rendering' (atmosphere/wind), the global volume, and the Cozy materials.");
        }

        [MenuItem(MenuRoot + "Apply Cozy Terrain Material To All Terrains", false, 20)]
        public static void ApplyTerrainMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (mat == null)
            {
                Debug.LogWarning("[Cozy] " + TerrainMaterialPath + " not found.");
                return;
            }
            int count = 0;
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(terrain, "Cozy Terrain Material");
                terrain.materialTemplate = mat;
                EditorUtility.SetDirty(terrain);
                count++;
            }
            Debug.Log("[Cozy] Applied Cozy/Terrain to " + count + " terrain(s). MapMagic: also set this material in the MapMagic terrain settings so streamed tiles use it.");
        }

        [MenuItem(MenuRoot + "Enable Depth And Opaque Textures On Active URP Asset", false, 21)]
        public static void EnableUrpTextures()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                Debug.LogWarning("[Cozy] Active render pipeline is not URP.");
                return;
            }
            Undo.RecordObject(asset, "Cozy URP textures");
            asset.supportsCameraDepthTexture = true;
            asset.supportsCameraOpaqueTexture = true;
            asset.supportsHDR = true;
            EditorUtility.SetDirty(asset);
        }

        // ----------------------------------------------------------------------
        // Material conversion helpers (used for tree / rock / grass prefabs that
        // MapMagic scatters - the shaders are prefab-friendly by default).
        // ----------------------------------------------------------------------
        [MenuItem(MenuRoot + "Convert Selected Materials/To Cozy Lit", false, 40)]
        public static void ConvertSelectedToLit() => ConvertSelected("Cozy/Lit", null);

        [MenuItem(MenuRoot + "Convert Selected Materials/To Cozy Lit (bark, wind bending)", false, 41)]
        public static void ConvertSelectedToLitWind() => ConvertSelected("Cozy/Lit", m =>
        {
            m.SetFloat("_WindSource", 1f);
            m.EnableKeyword("_WINDSOURCE_OBJECT");
            m.DisableKeyword("_WINDSOURCE_NONE");
        });

        [MenuItem(MenuRoot + "Convert Selected Materials/To Cozy Foliage", false, 42)]
        public static void ConvertSelectedToFoliage() => ConvertSelected("Cozy/Foliage", null);

        [MenuItem(MenuRoot + "Convert Selected Materials/To Cozy Grass (texture card)", false, 43)]
        public static void ConvertSelectedToGrass() => ConvertSelected("Cozy/Grass", m =>
        {
            m.SetFloat("_Shape", 1f);
            m.EnableKeyword("_SHAPE_TEXTURE");
            m.DisableKeyword("_SHAPE_PROCEDURAL");
        });

        private static void ConvertSelected(string shaderName, System.Action<Material> configure)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("[Cozy] Shader not found: " + shaderName); return; }
            var mats = new List<Material>();
            foreach (var o in Selection.objects)
            {
                if (o is Material m) mats.Add(m);
                else if (o is GameObject go)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var sm in r.sharedMaterials) if (sm != null && !mats.Contains(sm)) mats.Add(sm);
            }
            foreach (var m in mats)
            {
                Undo.RecordObject(m, "Convert to " + shaderName);
                Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
                Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                bool alphaClip = m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f);
                m.shader = shader;
                if (baseMap != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseMap);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
                if (alphaClip && m.HasProperty("_AlphaClip"))
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.EnableKeyword("_ALPHATEST_ON");
                }
                configure?.Invoke(m);
                EditorUtility.SetDirty(m);
            }
            Debug.Log("[Cozy] Converted " + mats.Count + " material(s) to " + shaderName + ".");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }
    }
}
#endif
