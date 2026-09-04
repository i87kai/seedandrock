#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SeedAndRock.World.Editor
{
    [CustomEditor(typeof(WorldGenerator))]
    public sealed class WorldGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(10f);
            WorldGenerator generator = (WorldGenerator)target;

            using (new EditorGUI.DisabledScope(generator.Settings == null))
            {
                if (GUILayout.Button("Generate / Regenerate World", GUILayout.Height(30f)))
                    generator.GenerateWorld();
                if (GUILayout.Button("Clear Generated World"))
                    generator.ClearGeneratedWorld();
            }
        }
    }
}
#endif