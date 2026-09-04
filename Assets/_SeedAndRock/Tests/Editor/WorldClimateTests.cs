using NUnit.Framework;
using SeedAndRock.World;
using UnityEngine;

namespace SeedAndRock.Tests.Editor
{
    public sealed class WorldClimateTests
    {
        [Test]
        public void MapsBiomeThresholdsToPlannedCelsius()
        {
            Assert.That(WorldClimate.Temperature01ToCelsius(0f), Is.EqualTo(-15f).Within(0.01f));
            Assert.That(WorldClimate.Temperature01ToCelsius(WorldClimate.SnowThreshold), Is.EqualTo(0f).Within(0.01f));
            Assert.That(WorldClimate.Temperature01ToCelsius(WorldClimate.TemperateMid), Is.EqualTo(16f).Within(0.01f));
            Assert.That(WorldClimate.Temperature01ToCelsius(WorldClimate.DesertThreshold), Is.EqualTo(32f).Within(0.01f));
            Assert.That(WorldClimate.Temperature01ToCelsius(1f), Is.EqualTo(46f).Within(0.01f));
        }

        [Test]
        public void CelsiusIncreasesWithClimateTemperature()
        {
            float previous = WorldClimate.Temperature01ToCelsius(0f);
            for (int i = 1; i <= 20; i++)
            {
                float current = WorldClimate.Temperature01ToCelsius(i / 20f);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous));
                previous = current;
            }
        }

        [Test]
        public void WaterCoolsAmbientTemperature()
        {
            WorldGenerationSettings settings = ScriptableObject.CreateInstance<WorldGenerationSettings>();
            try
            {
                float dry = settings.ToAmbientCelsius(WorldClimate.TemperateMid, false);
                float wet = settings.ToAmbientCelsius(WorldClimate.TemperateMid, true);
                Assert.That(dry, Is.EqualTo(16f).Within(0.01f));
                Assert.That(wet, Is.EqualTo(dry - settings.waterCoolingCelsius).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CustomKeypointsAreHonored()
        {
            float celsius = WorldClimate.Temperature01ToCelsius(0.5f, -20f, -5f, 10f, 30f, 50f);
            Assert.That(celsius, Is.EqualTo(10f).Within(0.01f));
        }
    }
}
