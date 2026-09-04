using UnityEngine;

namespace SeedAndRock.Survival
{
    public enum SurvivalWarning
    {
        None,
        Cold,
        Hot
    }

    public enum SurvivalDamageType
    {
        Generic,
        EnvironmentCold,
        EnvironmentHeat,
        Starvation,
        Dehydration
    }

    public struct SurvivalVitals
    {
        public float Health;
        public float Hunger;
        public float Thirst;
        public float BodyTemperatureCelsius;

        public static SurvivalVitals CreateFull(in SurvivalSettingsData settings)
        {
            return new SurvivalVitals
            {
                Health = settings.maxHealth,
                Hunger = settings.maxHunger,
                Thirst = settings.maxThirst,
                BodyTemperatureCelsius = settings.restingBodyTemperature
            };
        }
    }

    /// <summary>Pure survival tick math so clothing, weather, and tests can share one path.</summary>
    public static class SurvivalSimulation
    {
        public static float DifficultyScale(string difficulty)
        {
            switch (difficulty)
            {
                case "Peaceful": return 0f;
                case "Easy": return 0.7f;
                case "Hard": return 1.35f;
                default: return 1f;
            }
        }

        public static float ResolveBodyTemperatureTarget(float feltCelsius, in SurvivalSettingsData settings)
        {
            float resting = settings.restingBodyTemperature;
            if (feltCelsius >= settings.comfortMinCelsius && feltCelsius <= settings.comfortMaxCelsius)
                return resting;

            float target;
            if (feltCelsius < settings.comfortMinCelsius)
                target = resting - (settings.comfortMinCelsius - feltCelsius) * settings.temperaturePullFactor;
            else
                target = resting + (feltCelsius - settings.comfortMaxCelsius) * settings.temperaturePullFactor;

            return Mathf.Clamp(target, settings.minBodyTemperature, settings.maxBodyTemperature);
        }

        public static SurvivalWarning EvaluateWarning(float bodyTemperatureCelsius, in SurvivalSettingsData settings)
        {
            if (bodyTemperatureCelsius <= settings.hypothermiaCelsius)
                return SurvivalWarning.Cold;
            if (bodyTemperatureCelsius >= settings.hyperthermiaCelsius)
                return SurvivalWarning.Hot;
            return SurvivalWarning.None;
        }

        public static void Tick(
            ref SurvivalVitals vitals,
            in SurvivalTickContext context,
            in SurvivalSettingsData settings,
            float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            vitals.Hunger = Mathf.Clamp(
                vitals.Hunger - settings.hungerDrainPerSecond * context.HungerDrainScale * deltaTime + context.HungerRestore * deltaTime,
                0f,
                settings.maxHunger);
            vitals.Thirst = Mathf.Clamp(
                vitals.Thirst - settings.thirstDrainPerSecond * context.ThirstDrainScale * deltaTime + context.ThirstRestore * deltaTime,
                0f,
                settings.maxThirst);

            float target = ResolveBodyTemperatureTarget(context.FeltCelsius, settings);
            float ease = settings.bodyTemperatureEasePerSecond * context.TemperatureChangeScale;
            ease *= Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(context.Insulation));
            vitals.BodyTemperatureCelsius = Mathf.Clamp(
                Mathf.MoveTowards(vitals.BodyTemperatureCelsius, target, ease * deltaTime),
                settings.minBodyTemperature,
                settings.maxBodyTemperature);

            float damagePerSecond = 0f;
            if (vitals.BodyTemperatureCelsius <= settings.hypothermiaCelsius ||
                vitals.BodyTemperatureCelsius >= settings.hyperthermiaCelsius)
            {
                damagePerSecond += settings.environmentDamagePerSecond * context.EnvironmentalDamageScale;
            }

            if (vitals.Hunger <= 0.001f)
                damagePerSecond += settings.starvationDamagePerSecond * context.EnvironmentalDamageScale;
            if (vitals.Thirst <= 0.001f)
                damagePerSecond += settings.dehydrationDamagePerSecond * context.EnvironmentalDamageScale;

            if (damagePerSecond > 0f)
                vitals.Health = Mathf.Max(0f, vitals.Health - damagePerSecond * deltaTime);

            if (context.HealthRestore > 0f)
                vitals.Health = Mathf.Min(settings.maxHealth, vitals.Health + context.HealthRestore * deltaTime);
        }
    }
}
