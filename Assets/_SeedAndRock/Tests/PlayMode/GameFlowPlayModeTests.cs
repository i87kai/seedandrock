using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SeedAndRock.Tests
{
    /// <summary>Exercises the shipped runtime flow rather than recreating it with test-only services.</summary>
    public sealed class GameFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator CreateGeneratePauseSaveAndReturnToMenu()
        {
            SceneManager.LoadScene("World", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Type flowType = RuntimeType("SeedAndRock.UI.SeedAndRockGameFlow");
            object flow = FindRuntimeObject(flowType);
            Assert.That(flow, Is.Not.Null, "Runtime bootstrap did not create SeedAndRockGameFlow.");
            Assert.That(State(flow), Is.EqualTo("MainMenu"));

            string worldName = "Integration " + Guid.NewGuid().ToString("N").Substring(0, 8);
            int testSeed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            Invoke(flow, "ShowWorldBrowser");
            Invoke(flow, "ShowCreateWorld");
            Invoke(flow, "CreateWorld", worldName, testSeed.ToString(), "Balanced");
            Assert.That(State(flow), Is.EqualTo("Loading"));

            float deadline = Time.realtimeSinceStartup + 90f;
            while (State(flow) == "Loading" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(State(flow), Is.EqualTo("Playing"), "World generation did not enter gameplay before the timeout.");
            object generator = FindRuntimeObject(RuntimeType("SeedAndRock.World.WorldGenerator"));
            Assert.That(generator.GetType().GetProperty("LastResult").GetValue(generator), Is.Not.Null);
            Assert.That(GameObject.Find("SeedAndRock_Player"), Is.Not.Null);

            object world = flowType.GetProperty("CurrentWorld").GetValue(flow);
            string id = (string)world.GetType().GetField("id").GetValue(world);
            Invoke(flow, "PauseGame");
            Assert.That(State(flow), Is.EqualTo("Paused"));
            Invoke(flow, "SaveFromPause");
            Invoke(flow, "SaveAndReturnToMainMenu");
            Assert.That(State(flow), Is.EqualTo("MainMenu"));

            Type repositoryType = RuntimeType("SeedAndRock.Saves.WorldSaveService");
            object repository = Activator.CreateInstance(repositoryType);
            Assert.That((bool)repositoryType.GetMethod("Delete").Invoke(repository, new object[] { id }), Is.True, "Integration test cleanup could not remove its saved world.");
            Time.timeScale = 1f;
        }

        private static Type RuntimeType(string name)
        {
            Type type = Type.GetType(name + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, name + " was not compiled into Assembly-CSharp.");
            return type;
        }

        private static object FindRuntimeObject(Type type)
        {
            UnityEngine.Object[] instances = Resources.FindObjectsOfTypeAll(type);
            Assert.That(instances.Length, Is.GreaterThan(0), "No instance found for " + type.FullName);
            return instances[0];
        }

        private static string State(object flow) => flow.GetType().GetProperty("State").GetValue(flow).ToString();

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing public runtime method: " + name);
            method.Invoke(target, arguments);
        }
    }
}
