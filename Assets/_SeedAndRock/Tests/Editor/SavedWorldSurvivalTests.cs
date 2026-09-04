using NUnit.Framework;
using SeedAndRock.UI;
using UnityEngine;

namespace SeedAndRock.Tests.Editor
{
    public sealed class SavedWorldSurvivalTests
    {
        [Test]
        public void JsonRoundTripPreservesSurvivalVitals()
        {
            SavedWorld world = new SavedWorld
            {
                id = "test-world",
                worldName = "Frost",
                seed = 99,
                difficulty = "Hard",
                hasVisited = true,
                playerX = 3f,
                playerY = 4f,
                playerZ = 5f,
                hasSurvivalState = true,
                health = 81.5f,
                hunger = 64f,
                thirst = 22.25f,
                bodyTemperature = 33.4f
            };

            SavedWorld restored = JsonUtility.FromJson<SavedWorld>(JsonUtility.ToJson(world));

            Assert.That(restored.hasSurvivalState, Is.True);
            Assert.That(restored.health, Is.EqualTo(81.5f).Within(0.001f));
            Assert.That(restored.hunger, Is.EqualTo(64f).Within(0.001f));
            Assert.That(restored.thirst, Is.EqualTo(22.25f).Within(0.001f));
            Assert.That(restored.bodyTemperature, Is.EqualTo(33.4f).Within(0.001f));
            Assert.That(restored.difficulty, Is.EqualTo("Hard"));
        }

        [Test]
        public void LegacySavesWithoutSurvivalFieldsDoNotLookDead()
        {
            const string legacy = "{\"id\":\"old\",\"worldName\":\"Old\",\"seed\":1,\"difficulty\":\"Normal\",\"hasVisited\":true,\"playerX\":0,\"playerY\":1,\"playerZ\":0}";
            SavedWorld restored = JsonUtility.FromJson<SavedWorld>(legacy);
            Assert.That(restored.hasSurvivalState, Is.False);
            Assert.That(restored.health, Is.EqualTo(0f));
        }
    }
}
