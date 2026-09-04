using SeedAndRock.World;

namespace SeedAndRock.Survival
{
    /// <summary>Clothing, shelter, weather, food, and water implement this to adjust a survival tick.</summary>
    public interface ISurvivalModifier
    {
        void Modify(ref SurvivalTickContext context);
    }

    public struct SurvivalTickContext
    {
        public ClimateSample Climate;
        public float AmbientCelsius;
        public float FeltCelsius;
        public float AmbientOffsetCelsius;
        public float Insulation;
        public float Shelter;
        public float HungerDrainScale;
        public float ThirstDrainScale;
        public float TemperatureChangeScale;
        public float EnvironmentalDamageScale;
        public float HealthRestore;
        public float HungerRestore;
        public float ThirstRestore;

        public static SurvivalTickContext CreateDefault(in ClimateSample climate, float difficultyScale)
        {
            return new SurvivalTickContext
            {
                Climate = climate,
                AmbientCelsius = climate.AmbientCelsius,
                FeltCelsius = climate.AmbientCelsius,
                AmbientOffsetCelsius = 0f,
                Insulation = 0f,
                Shelter = 0f,
                HungerDrainScale = difficultyScale,
                ThirstDrainScale = difficultyScale,
                TemperatureChangeScale = 1f,
                EnvironmentalDamageScale = difficultyScale,
                HealthRestore = 0f,
                HungerRestore = 0f,
                ThirstRestore = 0f
            };
        }
    }
}
