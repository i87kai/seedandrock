using System;
using System.Collections.Generic;
using SeedAndRock.World;
using UnityEngine;

namespace SeedAndRock.Survival
{
    [DisallowMultipleComponent]
    public sealed class PlayerSurvival : MonoBehaviour
    {
        public static PlayerSurvival Active { get; private set; }

        [SerializeField] private SurvivalSettings settings;

        private SurvivalSettingsData data;
        private SurvivalVitals vitals;
        private ClimateSample lastClimate;
        private readonly List<ISurvivalModifier> modifiers = new List<ISurvivalModifier>();
        private string difficulty = "Normal";
        private bool initialized;

        public float Health => vitals.Health;
        public float Hunger => vitals.Hunger;
        public float Thirst => vitals.Thirst;
        public float BodyTemperatureCelsius => vitals.BodyTemperatureCelsius;
        public float MaxHealth => data.maxHealth;
        public float MaxHunger => data.maxHunger;
        public float MaxThirst => data.maxThirst;
        public float AmbientCelsius => lastClimate.AmbientCelsius;
        public float FeltCelsius { get; private set; }
        public ClimateSample CurrentClimate => lastClimate;
        public SurvivalWarning Warning { get; private set; }
        public bool IsDead { get; private set; }
        public string Difficulty => difficulty;

        public event Action VitalsChanged;
        public event Action Died;

        private void Awake()
        {
            Active = this;
            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (Active == null)
                Active = this;
            RefreshModifiers();
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        private void Update()
        {
            if (SeedAndRock.UI.SeedAndRockGameFlow.Instance != null && SeedAndRock.UI.SeedAndRockGameFlow.Instance.State != SeedAndRock.UI.GameFlowState.Playing) return;
            EnsureInitialized();
            Tick(Time.deltaTime);
        }

        public void SetDifficulty(string value)
        {
            difficulty = string.IsNullOrEmpty(value) ? "Normal" : value;
        }

        public void RefreshModifiers()
        {
            GetComponents(modifiers);
        }

        public void ApplySnapshot(float health, float hunger, float thirst, float bodyTemperatureCelsius)
        {
            EnsureInitialized();
            vitals.Health = Mathf.Clamp(health, 0f, data.maxHealth);
            vitals.Hunger = Mathf.Clamp(hunger, 0f, data.maxHunger);
            vitals.Thirst = Mathf.Clamp(thirst, 0f, data.maxThirst);
            vitals.BodyTemperatureCelsius = Mathf.Clamp(bodyTemperatureCelsius, data.minBodyTemperature, data.maxBodyTemperature);
            IsDead = vitals.Health <= 0f;
            Warning = SurvivalSimulation.EvaluateWarning(vitals.BodyTemperatureCelsius, data);
            VitalsChanged?.Invoke();
        }

        public SurvivalVitals CaptureSnapshot()
        {
            EnsureInitialized();
            return vitals;
        }

        public void ApplyDamage(float amount, SurvivalDamageType type = SurvivalDamageType.Generic)
        {
            if (amount <= 0f)
                return;

            EnsureInitialized();
            float previous = vitals.Health;
            vitals.Health = Mathf.Max(0f, vitals.Health - amount);
            RaiseHealthEvents(previous);
        }

        public void RestoreHealth(float amount)
        {
            if (amount <= 0f)
                return;

            EnsureInitialized();
            vitals.Health = Mathf.Min(data.maxHealth, vitals.Health + amount);
            if (vitals.Health > 0f)
                IsDead = false;
            VitalsChanged?.Invoke();
        }

        public void RestoreHunger(float amount)
        {
            EnsureInitialized();
            vitals.Hunger = Mathf.Clamp(vitals.Hunger + amount, 0f, data.maxHunger);
            VitalsChanged?.Invoke();
        }

        public void RestoreThirst(float amount)
        {
            EnsureInitialized();
            vitals.Thirst = Mathf.Clamp(vitals.Thirst + amount, 0f, data.maxThirst);
            VitalsChanged?.Invoke();
        }

        public void ApplyTemperatureImpulse(float celsiusDelta)
        {
            EnsureInitialized();
            vitals.BodyTemperatureCelsius = Mathf.Clamp(
                vitals.BodyTemperatureCelsius + celsiusDelta,
                data.minBodyTemperature,
                data.maxBodyTemperature);
            Warning = SurvivalSimulation.EvaluateWarning(vitals.BodyTemperatureCelsius, data);
            VitalsChanged?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            EnsureInitialized();
            lastClimate = SampleClimate();
            SurvivalTickContext context = SurvivalTickContext.CreateDefault(
                lastClimate,
                SurvivalSimulation.DifficultyScale(difficulty));

            for (int i = 0; i < modifiers.Count; i++)
                modifiers[i]?.Modify(ref context);

            context.FeltCelsius = context.AmbientCelsius + context.AmbientOffsetCelsius;
            if (context.Shelter > 0f)
            {
                float comfortMid = (data.comfortMinCelsius + data.comfortMaxCelsius) * 0.5f;
                context.FeltCelsius = Mathf.Lerp(context.FeltCelsius, comfortMid, Mathf.Clamp01(context.Shelter));
            }

            FeltCelsius = context.FeltCelsius;
            float previousHealth = vitals.Health;
            SurvivalSimulation.Tick(ref vitals, context, data, deltaTime);
            Warning = SurvivalSimulation.EvaluateWarning(vitals.BodyTemperatureCelsius, data);
            RaiseHealthEvents(previousHealth);
            VitalsChanged?.Invoke();
        }

        private ClimateSample SampleClimate()
        {
            WorldGenerator world = WorldGenerator.Active;
            if (world == null)
                return default;

            Vector3 position = transform.position;
            ClimateSample climate = world.GetClimateAt(position.x, position.z);
            if (climate.InWater && position.y > climate.WaterSurface + 0.9f && world.Settings != null)
                return climate.WithAmbientCelsius(climate.AmbientCelsius + world.Settings.waterCoolingCelsius);
            return climate;
        }

        private void RaiseHealthEvents(float previousHealth)
        {
            if (vitals.Health > 0f)
                IsDead = false;
            else if (previousHealth > 0f && !IsDead)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;

            if (settings == null)
                settings = Resources.Load<SurvivalSettings>("SR_SurvivalSettings");

            data = settings != null ? settings.ToData() : SurvivalSettingsData.CreateDefault();
            vitals = SurvivalVitals.CreateFull(data);
            FeltCelsius = data.comfortMinCelsius + (data.comfortMaxCelsius - data.comfortMinCelsius) * 0.5f;
            Warning = SurvivalWarning.None;
            initialized = true;
        }
    }
}
