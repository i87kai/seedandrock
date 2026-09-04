using System;
using UnityEngine;

namespace SeedAndRock.Survival
{
    [Serializable]
    public struct SurvivalSettingsData
    {
        public float maxHealth;
        public float maxHunger;
        public float maxThirst;
        public float restingBodyTemperature;
        public float hungerDrainPerSecond;
        public float thirstDrainPerSecond;
        public float comfortMinCelsius;
        public float comfortMaxCelsius;
        public float bodyTemperatureEasePerSecond;
        public float temperaturePullFactor;
        public float hypothermiaCelsius;
        public float hyperthermiaCelsius;
        public float environmentDamagePerSecond;
        public float starvationDamagePerSecond;
        public float dehydrationDamagePerSecond;
        public float minBodyTemperature;
        public float maxBodyTemperature;

        public static SurvivalSettingsData CreateDefault()
        {
            return new SurvivalSettingsData
            {
                maxHealth = 100f,
                maxHunger = 100f,
                maxThirst = 100f,
                restingBodyTemperature = 37f,
                hungerDrainPerSecond = 0.075f,
                thirstDrainPerSecond = 0.11f,
                comfortMinCelsius = 12f,
                comfortMaxCelsius = 26f,
                bodyTemperatureEasePerSecond = 0.028f,
                temperaturePullFactor = 0.25f,
                hypothermiaCelsius = 32f,
                hyperthermiaCelsius = 40.5f,
                environmentDamagePerSecond = 0.28f,
                starvationDamagePerSecond = 0.18f,
                dehydrationDamagePerSecond = 0.22f,
                minBodyTemperature = 28f,
                maxBodyTemperature = 43f
            };
        }
    }

    [CreateAssetMenu(fileName = "SR_SurvivalSettings", menuName = "SeedAndRock/Survival Settings")]
    public sealed class SurvivalSettings : ScriptableObject
    {
        [Header("Vitals")]
        [Min(1f)] public float maxHealth = 100f;
        [Min(1f)] public float maxHunger = 100f;
        [Min(1f)] public float maxThirst = 100f;
        public float restingBodyTemperature = 37f;

        [Header("Need drain")]
        [Min(0f)] public float hungerDrainPerSecond = 0.075f;
        [Min(0f)] public float thirstDrainPerSecond = 0.11f;

        [Header("Thermoregulation")]
        public float comfortMinCelsius = 12f;
        public float comfortMaxCelsius = 26f;
        [Min(0f)] public float bodyTemperatureEasePerSecond = 0.028f;
        [Range(0f, 1f)] public float temperaturePullFactor = 0.25f;
        public float hypothermiaCelsius = 32f;
        public float hyperthermiaCelsius = 40.5f;
        public float minBodyTemperature = 28f;
        public float maxBodyTemperature = 43f;

        [Header("Gradual damage")]
        [Min(0f)] public float environmentDamagePerSecond = 0.28f;
        [Min(0f)] public float starvationDamagePerSecond = 0.18f;
        [Min(0f)] public float dehydrationDamagePerSecond = 0.22f;

        public SurvivalSettingsData ToData()
        {
            return new SurvivalSettingsData
            {
                maxHealth = maxHealth,
                maxHunger = maxHunger,
                maxThirst = maxThirst,
                restingBodyTemperature = restingBodyTemperature,
                hungerDrainPerSecond = hungerDrainPerSecond,
                thirstDrainPerSecond = thirstDrainPerSecond,
                comfortMinCelsius = comfortMinCelsius,
                comfortMaxCelsius = comfortMaxCelsius,
                bodyTemperatureEasePerSecond = bodyTemperatureEasePerSecond,
                temperaturePullFactor = temperaturePullFactor,
                hypothermiaCelsius = hypothermiaCelsius,
                hyperthermiaCelsius = hyperthermiaCelsius,
                environmentDamagePerSecond = environmentDamagePerSecond,
                starvationDamagePerSecond = starvationDamagePerSecond,
                dehydrationDamagePerSecond = dehydrationDamagePerSecond,
                minBodyTemperature = minBodyTemperature,
                maxBodyTemperature = maxBodyTemperature
            };
        }
    }
}
