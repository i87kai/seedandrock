#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cozy.Rendering.Editor
{
    public static class CozyBatchSetup
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_SeedAndRock/Scenes/World.unity", OpenSceneMode.Single);
            CozySceneSetup.SetupScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cozy] Batch scene setup saved World.unity.");
        }
    }
}
#endif
