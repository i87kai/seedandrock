#if UNITY_EDITOR
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;

namespace SeedAndRock.World.Editor
{
    /// <summary>Ensures the runtime title screens always have TMP fonts without opening a modal importer.</summary>
    [InitializeOnLoad]
    internal static class SeedAndRockTmpResources
    {
        static SeedAndRockTmpResources()
        {
            EditorApplication.delayCall += EnsureResources;
        }

        private static void EnsureResources()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
                TMP_PackageResourceImporter.ImportResources(true, false, false);
        }
    }
}
#endif
