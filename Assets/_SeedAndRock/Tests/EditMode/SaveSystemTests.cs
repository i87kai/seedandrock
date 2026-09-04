using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using SeedAndRock.Saves;

namespace SeedAndRock.Tests
{
    /// <summary>Tiny line-based serializer so the repository tests do not depend on an engine JSON implementation.</summary>
    internal sealed class TestSerializer : ISaveSerializer
    {
        public string Serialize(SavedWorldCollection collection)
        {
            StringBuilder builder = new StringBuilder();
            foreach (SavedWorld world in collection.worlds)
            {
                builder.Append(string.Join("\t", new[]
                {
                    world.version.ToString(CultureInfo.InvariantCulture), world.id, world.worldName, world.seed.ToString(CultureInfo.InvariantCulture),
                    world.difficulty, world.createdUtc, world.lastPlayedUtc, world.hasVisited ? "1" : "0",
                    F(world.playerX), F(world.playerY), F(world.playerZ), F(world.playerYaw), F(world.playerPitch)
                }));
                builder.Append('\n');
            }

            return builder.ToString();
        }

        public SavedWorldCollection Deserialize(string text)
        {
            if (text.StartsWith("CORRUPT", StringComparison.Ordinal)) throw new FormatException("corrupt");
            SavedWorldCollection collection = new SavedWorldCollection();
            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                string[] parts = line.Split('\t');
                collection.worlds.Add(new SavedWorld
                {
                    version = int.Parse(parts[0], CultureInfo.InvariantCulture), id = parts[1], worldName = parts[2],
                    seed = int.Parse(parts[3], CultureInfo.InvariantCulture), difficulty = parts[4], createdUtc = parts[5], lastPlayedUtc = parts[6],
                    hasVisited = parts[7] == "1", playerX = P(parts[8]), playerY = P(parts[9]), playerZ = P(parts[10]), playerYaw = P(parts[11]), playerPitch = P(parts[12])
                });
            }

            return collection;
        }

        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static float P(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }

    public sealed class WorldValidationTests
    {
        [TestCase("Home", true)]
        [TestCase("   Rolling   Hills  ", true)]
        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("Bad/Name", false)]
        [TestCase("Quote\"d", false)]
        [TestCase("CON", false)]
        [TestCase("Trailing.", false)]
        public void NameValidation(string name, bool expected)
        {
            Assert.That(WorldValidation.ValidateName(name, out string error), Is.EqualTo(expected));
            Assert.That(string.IsNullOrEmpty(error), Is.EqualTo(expected));
        }

        [Test]
        public void NameLongerThanLimitIsRejected()
        {
            Assert.That(WorldValidation.ValidateName(new string('a', WorldValidation.MaxNameLength + 1), out _), Is.False);
            Assert.That(WorldValidation.ValidateName(new string('a', WorldValidation.MaxNameLength), out _), Is.True);
        }

        [Test]
        public void NormalizeCollapsesWhitespace()
        {
            Assert.That(WorldValidation.NormalizeName("  Misty \t\n Vale  "), Is.EqualTo("Misty Vale"));
        }

        [Test]
        public void SeedParsingHandlesNumbersTextAndEmpty()
        {
            Assert.That(WorldValidation.TryParseSeed("  ", out _), Is.EqualTo(SeedParseStatus.Empty));
            Assert.That(WorldValidation.TryParseSeed("240613", out int numeric), Is.EqualTo(SeedParseStatus.Numeric));
            Assert.That(numeric, Is.EqualTo(240613));
            Assert.That(WorldValidation.TryParseSeed("-17", out int negative), Is.EqualTo(SeedParseStatus.Numeric));
            Assert.That(negative, Is.EqualTo(-17));
            Assert.That(WorldValidation.TryParseSeed("99999999999", out _), Is.EqualTo(SeedParseStatus.Invalid));
            Assert.That(WorldValidation.TryParseSeed("misty vale", out int textA), Is.EqualTo(SeedParseStatus.Text));
            WorldValidation.TryParseSeed("misty vale", out int textB);
            Assert.That(textB, Is.EqualTo(textA), "text seeds must hash deterministically");
            Assert.That(textA, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void DifficultyCycleWrapsAround()
        {
            Assert.That(WorldValidation.NextDifficulty("Normal"), Is.EqualTo("Hard"));
            Assert.That(WorldValidation.NextDifficulty("Hard"), Is.EqualTo("Peaceful"));
            Assert.That(WorldValidation.NextDifficulty("unknown"), Is.EqualTo("Peaceful"));
        }

        [Test]
        public void SanitizeRejectsUnsafeRecordsAndRepairsMinorIssues()
        {
            DateTime now = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(WorldValidation.SanitizeRecord(null, now), Is.False);
            Assert.That(WorldValidation.SanitizeRecord(new SavedWorld { id = "../etc", worldName = "x" }, now), Is.False);
            Assert.That(WorldValidation.SanitizeRecord(new SavedWorld { id = "abc123", worldName = "bad/name" }, now), Is.False);

            SavedWorld repairable = new SavedWorld { id = "abc123", worldName = " Home ", difficulty = "Impossible", playerX = float.NaN, hasVisited = true };
            Assert.That(WorldValidation.SanitizeRecord(repairable, now), Is.True);
            Assert.That(repairable.worldName, Is.EqualTo("Home"));
            Assert.That(repairable.difficulty, Is.EqualTo(WorldValidation.DefaultDifficulty));
            Assert.That(repairable.hasVisited, Is.False);
            Assert.That(repairable.CreatedUtc, Is.EqualTo(now));
        }
    }

    public sealed class WorldSaveRepositoryTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "seedandrock-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private WorldSaveRepository CreateRepository() => new WorldSaveRepository(directory, new TestSerializer(), clock: () => new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc));

        [Test]
        public void UpsertWritesAtomicallyAndLeavesNoTemporaryFile()
        {
            WorldSaveRepository repository = CreateRepository();
            SavedWorld world = repository.CreateRecord("Home", 240613, "Normal");
            repository.Upsert(world);

            Assert.That(File.Exists(repository.FilePath), Is.True);
            Assert.That(File.Exists(repository.TemporaryPath), Is.False);
            List<SavedWorld> loaded = repository.LoadAll();
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].worldName, Is.EqualTo("Home"));
            Assert.That(loaded[0].seed, Is.EqualTo(240613));
        }

        [Test]
        public void SecondWriteKeepsBackupAndBackupIsUsedWhenMainFileIsCorrupt()
        {
            WorldSaveRepository repository = CreateRepository();
            SavedWorld first = repository.CreateRecord("First", 1, "Easy");
            repository.Upsert(first);
            SavedWorld second = repository.CreateRecord("Second", 2, "Hard");
            repository.Upsert(second);

            Assert.That(File.Exists(repository.BackupPath), Is.True, "previous version should be retained as backup");
            Assert.That(repository.LoadAll().Count, Is.EqualTo(2));

            File.WriteAllText(repository.FilePath, "CORRUPT");
            List<string> warnings = new List<string>();
            repository.Warning += warnings.Add;
            List<SavedWorld> recovered = repository.LoadAll();
            Assert.That(recovered.Count, Is.EqualTo(1), "backup holds the state before the last write");
            Assert.That(recovered[0].worldName, Is.EqualTo("First"));
            Assert.That(warnings.Count, Is.GreaterThan(0));
        }

        [Test]
        public void PlayerStateRoundTrips()
        {
            WorldSaveRepository repository = CreateRepository();
            SavedWorld world = repository.CreateRecord("Journey", 77, "Normal");
            world.SetPlayerState(new PlayerStateData(12.5f, 8.25f, -40.125f, 271.5f, -12.75f));
            repository.Upsert(world);

            SavedWorld loaded = repository.Find(world.id);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.hasVisited, Is.True);
            PlayerStateData state = loaded.GetPlayerState();
            Assert.That(state.x, Is.EqualTo(12.5f));
            Assert.That(state.y, Is.EqualTo(8.25f));
            Assert.That(state.z, Is.EqualTo(-40.125f));
            Assert.That(state.yaw, Is.EqualTo(271.5f));
            Assert.That(state.pitch, Is.EqualTo(-12.75f));
        }

        [Test]
        public void DeleteRemovesOnlyTheRequestedWorld()
        {
            WorldSaveRepository repository = CreateRepository();
            SavedWorld a = repository.CreateRecord("A", 1, "Normal");
            SavedWorld b = repository.CreateRecord("B", 2, "Normal");
            repository.Upsert(a);
            repository.Upsert(b);
            Assert.That(repository.Delete(a.id), Is.True);
            Assert.That(repository.Delete("does-not-exist"), Is.False);
            List<SavedWorld> remaining = repository.LoadAll();
            Assert.That(remaining.Count, Is.EqualTo(1));
            Assert.That(remaining[0].id, Is.EqualTo(b.id));
        }

        [Test]
        public void SeedUniquenessAndInvalidRecordsAreEnforced()
        {
            WorldSaveRepository repository = CreateRepository();
            repository.Upsert(repository.CreateRecord("A", 5, "Normal"));
            Assert.That(repository.ContainsSeed(5), Is.True);
            Assert.That(repository.ContainsSeed(6), Is.False);

            int calls = 0;
            int unique = repository.GenerateUniqueSeed(() => calls++ == 0 ? 5 : 9000);
            Assert.That(unique, Is.EqualTo(9000));

            Assert.Throws<InvalidDataException>(() => repository.Upsert(new SavedWorld { id = "not safe!", worldName = "X" }));
        }

        [Test]
        public void InvalidEntriesInTheFileAreSkippedOnLoad()
        {
            WorldSaveRepository repository = CreateRepository();
            repository.Upsert(repository.CreateRecord("Good", 1, "Normal"));
            string text = File.ReadAllText(repository.FilePath);
            text += "2\tbad id!\tBroken\t3\tNormal\t\t\t0\t0\t0\t0\t0\t0\n";
            File.WriteAllText(repository.FilePath, text);
            List<SavedWorld> loaded = repository.LoadAll();
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].worldName, Is.EqualTo("Good"));
        }
    }
}
