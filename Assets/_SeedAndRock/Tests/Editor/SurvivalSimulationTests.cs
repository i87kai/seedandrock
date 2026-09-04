using NUnit.Framework;
using SeedAndRock.Survival;
using SeedAndRock.World;
using UnityEngine;

namespace SeedAndRock.Tests.Editor
{
    public sealed class SurvivalSimulationTests
    {
        [Test]
        public void ComfortableAmbientHoldsRestingBodyTemperature()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            SurvivalVitals vitals = SurvivalVitals.CreateFull(settings);
            SurvivalTickContext context = ComfortContext(18f);

            SurvivalSimulation.Tick(ref vitals, context, settings, 120f);

            Assert.That(vitals.BodyTemperatureCelsius, Is.EqualTo(settings.restingBodyTemperature).Within(0.05f));
            Assert.That(SurvivalSimulation.EvaluateWarning(vitals.BodyTemperatureCelsius, settings), Is.EqualTo(SurvivalWarning.None));
        }

        [Test]
        public void ExtremeColdLowersBodyTemperatureGradually()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            SurvivalVitals vitals = SurvivalVitals.CreateFull(settings);
            SurvivalTickContext context = ComfortContext(-15f);

            SurvivalSimulation.Tick(ref vitals, context, settings, 20f);

            Assert.That(vitals.BodyTemperatureCelsius, Is.LessThan(settings.restingBodyTemperature - 0.3f));
            Assert.That(vitals.BodyTemperatureCelsius, Is.GreaterThan(settings.restingBodyTemperature - 2f));
        }

        [Test]
        public void ExtremeHeatRaisesBodyTemperatureGradually()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            SurvivalVitals vitals = SurvivalVitals.CreateFull(settings);
            SurvivalTickContext context = ComfortContext(46f);

            SurvivalSimulation.Tick(ref vitals, context, settings, 20f);

            Assert.That(vitals.BodyTemperatureCelsius, Is.GreaterThan(settings.restingBodyTemperature + 0.3f));
            Assert.That(vitals.BodyTemperatureCelsius, Is.LessThan(settings.restingBodyTemperature + 2f));
        }

        [Test]
        public void DangerousBodyTemperatureDamagesHealthSlowly()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            SurvivalVitals vitals = SurvivalVitals.CreateFull(settings);
            vitals.BodyTemperatureCelsius = settings.hypothermiaCelsius;
            SurvivalTickContext context = ComfortContext(-12f);

            SurvivalSimulation.Tick(ref vitals, context, settings, 10f);

            Assert.That(vitals.Health, Is.LessThan(settings.maxHealth));
            Assert.That(vitals.Health, Is.GreaterThan(settings.maxHealth - 10f));
            Assert.That(SurvivalSimulation.EvaluateWarning(vitals.BodyTemperatureCelsius, settings), Is.EqualTo(SurvivalWarning.Cold));
        }

        [Test]
        public void EmptyHungerAndThirstDamageHealth()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            SurvivalVitals vitals = SurvivalVitals.CreateFull(settings);
            vitals.Hunger = 0f;
            vitals.Thirst = 0f;
            SurvivalTickContext context = ComfortContext(18f);

            SurvivalSimulation.Tick(ref vitals, context, settings, 8f);

            Assert.That(vitals.Health, Is.LessThan(settings.maxHealth - 2f));
            Assert.That(vitals.Health, Is.GreaterThan(90f));
        }

        [Test]
        public void HungerAndThirstDrainOverTime()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            SurvivalVitals vitals = SurvivalVitals.CreateFull(settings);
            SurvivalTickContext context = ComfortContext(18f);

            SurvivalSimulation.Tick(ref vitals, context, settings, 30f);

            Assert.That(vitals.Hunger, Is.LessThan(settings.maxHunger));
            Assert.That(vitals.Thirst, Is.LessThan(vitals.Hunger));
        }

        [Test]
        public void PeacefulDifficultyDisablesNeedDrainAndEnvironmentDamage()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            SurvivalVitals vitals = SurvivalVitals.CreateFull(settings);
            vitals.BodyTemperatureCelsius = 31f;
            SurvivalTickContext context = ComfortContext(-15f);
            float scale = SurvivalSimulation.DifficultyScale("Peaceful");
            context.HungerDrainScale = scale;
            context.ThirstDrainScale = scale;
            context.EnvironmentalDamageScale = scale;

            SurvivalSimulation.Tick(ref vitals, context, settings, 60f);

            Assert.That(scale, Is.EqualTo(0f));
            Assert.That(vitals.Hunger, Is.EqualTo(settings.maxHunger).Within(0.01f));
            Assert.That(vitals.Thirst, Is.EqualTo(settings.maxThirst).Within(0.01f));
            Assert.That(vitals.Health, Is.EqualTo(settings.maxHealth).Within(0.01f));
        }

        [Test]
        public void BodyTemperatureTargetStaysRestingInsideComfortBand()
        {
            SurvivalSettingsData settings = SurvivalSettingsData.CreateDefault();
            Assert.That(SurvivalSimulation.ResolveBodyTemperatureTarget(12f, settings), Is.EqualTo(37f).Within(0.01f));
            Assert.That(SurvivalSimulation.ResolveBodyTemperatureTarget(26f, settings), Is.EqualTo(37f).Within(0.01f));
            Assert.That(SurvivalSimulation.ResolveBodyTemperatureTarget(-15f, settings), Is.LessThan(32.5f));
            Assert.That(SurvivalSimulation.ResolveBodyTemperatureTarget(46f, settings), Is.GreaterThan(40.5f));
        }

        private static SurvivalTickContext ComfortContext(float ambientCelsius)
        {
            ClimateSample climate = new ClimateSample(2f, 0.4f, 0.5f, 0.5f, 0.1f, false, 0f, SeedAndRockBiome.Grassland, ambientCelsius);
            SurvivalTickContext context = SurvivalTickContext.CreateDefault(climate, 1f);
            context.FeltCelsius = ambientCelsius;
            return context;
        }
    }
}
