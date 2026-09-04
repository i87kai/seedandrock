using System;
using System.Collections.Generic;
using System.IO;

namespace SeedAndRock.Saves
{
    /// <summary>Serialization boundary so the repository stays engine-independent (Unity supplies JsonUtility).</summary>
    public interface ISaveSerializer
    {
        string Serialize(SavedWorldCollection collection);
        SavedWorldCollection Deserialize(string text);
    }

    /// <summary>
    /// File-backed registry of saved worlds with atomic writes: content is written to a temporary file,
    /// then swapped into place while the previous file is kept as a backup that is used if the main
    /// file is ever unreadable.
    /// </summary>
    public sealed class WorldSaveRepository
    {
        public const string DefaultFileName = "seed-and-rock-worlds.json";

        private readonly ISaveSerializer serializer;
        private readonly Func<DateTime> clock;

        public string Directory { get; }
        public string FilePath { get; }
        public string BackupPath => FilePath + ".bak";
        public string TemporaryPath => FilePath + ".tmp";

        /// <summary>Raised with a human-readable message whenever a read problem was recovered from.</summary>
        public event Action<string> Warning;

        public WorldSaveRepository(string directory, ISaveSerializer serializer, string fileName = DefaultFileName, Func<DateTime> clock = null)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            Directory = directory;
            FilePath = Path.Combine(directory, fileName);
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        public List<SavedWorld> LoadAll()
        {
            List<SavedWorld> worlds = TryRead(FilePath, out bool corrupt);
            if (worlds == null)
            {
                if (corrupt) Warning?.Invoke("Save file was unreadable; using the last good backup.");
                worlds = TryRead(BackupPath, out _) ?? new List<SavedWorld>();
            }

            DateTime now = clock();
            HashSet<string> seen = new HashSet<string>();
            List<SavedWorld> valid = new List<SavedWorld>(worlds.Count);
            foreach (SavedWorld record in worlds)
            {
                if (!WorldValidation.SanitizeRecord(record, now))
                {
                    Warning?.Invoke("Skipped an invalid saved world entry.");
                    continue;
                }

                if (!seen.Add(record.id)) continue;
                valid.Add(record);
            }

            return valid;
        }

        public SavedWorld Find(string id)
        {
            foreach (SavedWorld world in LoadAll())
                if (world.id == id) return world;
            return null;
        }

        public void Upsert(SavedWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!WorldValidation.SanitizeRecord(world, clock()))
                throw new InvalidDataException("Refusing to save an invalid world record.");

            List<SavedWorld> worlds = LoadAll();
            int index = worlds.FindIndex(candidate => candidate.id == world.id);
            if (index >= 0) worlds[index] = world; else worlds.Add(world);
            WriteAtomic(worlds);
        }

        public bool Delete(string id)
        {
            List<SavedWorld> worlds = LoadAll();
            int removed = worlds.RemoveAll(candidate => candidate.id == id);
            if (removed == 0) return false;
            WriteAtomic(worlds);
            return true;
        }

        public bool ContainsSeed(int seed, string excludingId = null)
        {
            foreach (SavedWorld world in LoadAll())
                if (world.seed == seed && world.id != excludingId) return true;
            return false;
        }

        /// <summary>Draws seeds from <paramref name="randomSource"/> until one is unused and non-zero.</summary>
        public int GenerateUniqueSeed(Func<int> randomSource)
        {
            if (randomSource == null) throw new ArgumentNullException(nameof(randomSource));
            List<SavedWorld> worlds = LoadAll();
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                int seed = randomSource() & int.MaxValue;
                if (seed == 0) continue;
                bool taken = false;
                foreach (SavedWorld world in worlds)
                    if (world.seed == seed) { taken = true; break; }
                if (!taken) return seed;
            }

            throw new InvalidOperationException("Could not find an unused seed.");
        }

        public static string NewId() => Guid.NewGuid().ToString("N");

        public SavedWorld CreateRecord(string name, int seed, string difficulty)
        {
            string now = SavedWorld.FormatUtc(clock());
            return new SavedWorld
            {
                id = NewId(),
                worldName = WorldValidation.NormalizeName(name),
                seed = seed,
                difficulty = WorldValidation.IsValidDifficulty(difficulty) ? difficulty : WorldValidation.DefaultDifficulty,
                createdUtc = now,
                lastPlayedUtc = now,
                version = SavedWorld.CurrentVersion
            };
        }

        private List<SavedWorld> TryRead(string path, out bool corrupt)
        {
            corrupt = false;
            try
            {
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) { corrupt = true; return null; }
                SavedWorldCollection collection = serializer.Deserialize(text);
                if (collection == null || collection.worlds == null) { corrupt = true; return null; }
                return collection.worlds;
            }
            catch (Exception)
            {
                corrupt = true;
                return null;
            }
        }

        private void WriteAtomic(List<SavedWorld> worlds)
        {
            System.IO.Directory.CreateDirectory(Directory);
            string json = serializer.Serialize(new SavedWorldCollection { worlds = worlds });
            File.WriteAllText(TemporaryPath, json);

            if (File.Exists(FilePath))
            {
                try
                {
                    File.Replace(TemporaryPath, FilePath, BackupPath, true);
                    return;
                }
                catch (PlatformNotSupportedException) { }
                catch (IOException) { }

                // Fallback for file systems without an atomic replace: keep the previous file as backup first.
                File.Copy(FilePath, BackupPath, true);
                File.Delete(FilePath);
            }

            File.Move(TemporaryPath, FilePath);
        }
    }
}
