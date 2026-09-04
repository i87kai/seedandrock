using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SeedAndRock.Tests
{
    /// <summary>Unity-side guard for references that cannot be validated by the engine-independent tests.</summary>
    public sealed class WorldSceneIntegrationTests
    {
        private const string WorldScenePath = "Assets/_SeedAndRock/Scenes/World.unity";

        [Test]
        public void WorldSceneLoadsWithNoMissingScriptsAndRequiredPresentationObjects()
        {
            Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);

            bool hasWorldGenerator = false;
            bool hasPresentation = false;
            Component volume = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject gameObject = transform.gameObject;
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject), Is.Zero, gameObject.name + " contains a missing script.");
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().FullName;
                        hasWorldGenerator |= typeName == "SeedAndRock.World.WorldGenerator";
                        hasPresentation |= typeName == "SeedAndRock.World.WorldPresentationController";
                        if (typeName == "UnityEngine.Rendering.Volume") volume = component;
                    }
                }
            }

            Assert.That(hasWorldGenerator, Is.True, "World scene has no WorldGenerator.");
            Assert.That(hasPresentation, Is.True, "World scene has no WorldPresentationController.");
            Assert.That(volume, Is.Not.Null, "World scene has no Volume.");
            SerializedProperty profile = new SerializedObject(volume).FindProperty("sharedProfile");
            Assert.That(profile, Is.Not.Null, "World Volume has no profile property.");
            Assert.That(profile.objectReferenceValue, Is.Not.Null, "World Volume has no assigned profile.");
        }

        [TestCase("Assets/_SeedAndRock/Materials/SR_Terrain.mat", "SeedAndRock/Stylized Terrain")]
        [TestCase("Assets/_SeedAndRock/Materials/SR_Water.mat", "SeedAndRock/Stylized Water")]
        [TestCase("Assets/_SeedAndRock/Materials/SR_Grass.mat", "SeedAndRock/Stylized Grass")]
        [TestCase("Assets/_SeedAndRock/Materials/SR_Foliage.mat", "SeedAndRock/Stylized Environment")]
        [TestCase("Assets/_SeedAndRock/Materials/SR_Rock.mat", "SeedAndRock/Stylized Environment")]
        [TestCase("Assets/_SeedAndRock/Materials/SR_Trunk.mat", "SeedAndRock/Stylized Environment")]
        public void PresentationMaterialUsesItsExpectedShader(string materialPath, string shaderName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null, "Missing material: " + materialPath);
            Assert.That(material.shader, Is.Not.Null, materialPath + " has no shader.");
            Assert.That(material.shader.name, Is.EqualTo(shaderName), materialPath + " references an unexpected shader.");
        }
    }
}
