using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
            Invoke(flow, "CreateWorld", worldName, testSeed.ToString(), "Normal");
            Assert.That(State(flow), Is.EqualTo("Loading"));

            float deadline = Time.realtimeSinceStartup + 90f;
            while (State(flow) == "Loading" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(State(flow), Is.EqualTo("Playing"), "World generation did not enter gameplay before the timeout.");
            object generator = FindRuntimeObject(RuntimeType("SeedAndRock.World.WorldGenerator"));
            Assert.That(generator.GetType().GetProperty("LastResult").GetValue(generator), Is.Not.Null);
            Assert.That(GameObject.Find("SeedAndRock_Player"), Is.Not.Null);

            GameObject player = GameObject.Find("SeedAndRock_Player");
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position += new Vector3(1.25f, 0f, 1.25f);
            Vector3 savedPosition = player.transform.position;
            body.enabled = true;
            Physics.SyncTransforms();

            object world = flowType.GetProperty("CurrentWorld").GetValue(flow);
            string id = (string)world.GetType().GetField("id").GetValue(world);
            Invoke(flow, "PauseGame");
            Assert.That(State(flow), Is.EqualTo("Paused"));
            Invoke(flow, "SaveFromPause");
            Invoke(flow, "SaveAndReturnToMainMenu");
            Assert.That(State(flow), Is.EqualTo("MainMenu"));

            // Exercise the actual saved-world card callback, including its captured SavedWorld record.
            Invoke(flow, "ShowWorldBrowser");
            Button playButton = FindWorldPlayButton(id);
            Assert.That(playButton, Is.Not.Null, "Saved world browser did not create a PLAY button for the saved record.");
            playButton.onClick.Invoke();
            Assert.That(State(flow), Is.EqualTo("Loading"));
            deadline = Time.realtimeSinceStartup + 90f;
            while (State(flow) == "Loading" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(State(flow), Is.EqualTo("Playing"), "Saved-world PLAY callback did not enter gameplay.");
            object loadedWorld = flowType.GetProperty("CurrentWorld").GetValue(flow);
            Assert.That((int)loadedWorld.GetType().GetField("seed").GetValue(loadedWorld), Is.EqualTo(testSeed));
            GameObject restoredPlayer = GameObject.Find("SeedAndRock_Player");
            Assert.That(restoredPlayer, Is.Not.Null);
            Assert.That(Vector2.Distance(new Vector2(restoredPlayer.transform.position.x, restoredPlayer.transform.position.z), new Vector2(savedPosition.x, savedPosition.z)), Is.LessThan(0.1f), "Saved player position was not restored.");

            Invoke(flow, "PauseGame");
            Invoke(flow, "SaveAndReturnToMainMenu");
            Assert.That(State(flow), Is.EqualTo("MainMenu"));

            Type repositoryType = RuntimeType("SeedAndRock.Saves.WorldSaveService");
            object repository = Activator.CreateInstance(repositoryType);
            Assert.That((bool)repositoryType.GetMethod("Delete").Invoke(repository, new object[] { id }), Is.True, "Integration test cleanup could not remove its saved world.");
            Time.timeScale = 1f;
        }

        private static Button FindWorldPlayButton(string worldId)
        {
            string cardName = "World_" + worldId;
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button == null || button.name != "PlayButton" || !button.gameObject.activeInHierarchy) continue;
                Transform current = button.transform;
                while (current != null)
                {
                    if (current.name == cardName) return button;
                    current = current.parent;
                }
            }

            return null;
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
