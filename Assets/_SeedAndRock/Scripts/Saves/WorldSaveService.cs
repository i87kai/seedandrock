using System;
using System.Collections.Generic;
using SeedAndRock.Player;
using SeedAndRock.Survival;
using UnityEngine;

namespace SeedAndRock.Saves
{
    /// <summary>JsonUtility adapter for the engine-independent repository.</summary>
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        public string Serialize(SavedWorldCollection collection) => JsonUtility.ToJson(collection, true);
        public SavedWorldCollection Deserialize(string text) => JsonUtility.FromJson<SavedWorldCollection>(text);
    }

    /// <summary>
    /// Scene-facing save service: wraps the repository with Unity's persistent data path, JSON
    /// serialization and player-state capture. All persistence rules live in the core repository.
    /// </summary>
    public sealed class WorldSaveService
    {
        private readonly WorldSaveRepository repository;

        public WorldSaveService() : this(Application.persistentDataPath) { }

        public WorldSaveService(string directory)
        {
            repository = new WorldSaveRepository(directory, new JsonUtilitySaveSerializer());
            repository.Warning += message => Debug.LogWarning("[SeedAndRock] " + message);
        }

        public string FilePath => repository.FilePath;

        public List<SavedWorld> LoadAll()
        {
            try { return repository.LoadAll(); }
            catch (Exception exception)
            {
                Debug.LogWarning("[SeedAndRock] Could not read saved worlds: " + exception.Message);
                return new List<SavedWorld>();
            }
        }

        public bool TrySave(SavedWorld world, out string error)
        {
            try
            {
                repository.Upsert(world);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogError("[SeedAndRock] Saving world failed: " + exception.Message);
                return false;
            }
        }

        public bool Delete(string id)
        {
            try { return repository.Delete(id); }
            catch (Exception exception)
            {
                Debug.LogError("[SeedAndRock] Deleting world failed: " + exception.Message);
                return false;
            }
        }

        public bool ContainsSeed(int seed) => repository.ContainsSeed(seed);

        public int GenerateUniqueSeed() => repository.GenerateUniqueSeed(() => Guid.NewGuid().GetHashCode());

        public SavedWorld CreateRecord(string name, int seed, string difficulty) => repository.CreateRecord(name, seed, difficulty);

        /// <summary>Writes the player's transform into the record and stamps the last-played time.</summary>
        public static void CapturePlayer(SavedWorld world, FirstPersonExplorerController controller)
        {
            if (world == null) return;
            if (controller != null)
            {
                Vector3 position = controller.transform.position;
                world.SetPlayerState(new PlayerStateData(position.x, position.y, position.z, controller.Yaw, controller.Pitch));
            }

            PlayerSurvival survival = controller != null ? controller.GetComponent<PlayerSurvival>() : null;
            if (survival != null)
            {
                world.hasSurvivalState = true;
                world.health = survival.Health;
                world.hunger = survival.Hunger;
                world.thirst = survival.Thirst;
                world.bodyTemperature = survival.BodyTemperatureCelsius;
            }

            world.lastPlayedUtc = SavedWorld.FormatUtc(DateTime.UtcNow);
            if(controller!=null)world.expedition=controller.GetComponent<PlayerExpedition>()?.Capture();
        }
    }
}
