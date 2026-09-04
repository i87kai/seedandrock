namespace SeedAndRock.World
{
    /// <summary>Deterministic climate at a world XZ sample, shared by biome classification and survival.</summary>
    public readonly struct ClimateSample
    {
        public readonly float Height;
        public readonly float NormalizedHeight;
        public readonly float Moisture;
        public readonly float Temperature01;
        public readonly float Slope;
        public readonly bool InWater;
        public readonly float WaterSurface;
        public readonly SeedAndRockBiome Biome;
        public readonly float AmbientCelsius;

        public ClimateSample(
            float height,
            float normalizedHeight,
            float moisture,
            float temperature01,
            float slope,
            bool inWater,
            float waterSurface,
            SeedAndRockBiome biome,
            float ambientCelsius)
        {
            Height = height;
            NormalizedHeight = normalizedHeight;
            Moisture = moisture;
            Temperature01 = temperature01;
            Slope = slope;
            InWater = inWater;
            WaterSurface = waterSurface;
            Biome = biome;
            AmbientCelsius = ambientCelsius;
        }

        public ClimateSample WithAmbientCelsius(float ambientCelsius)
        {
            return new ClimateSample(
                Height, NormalizedHeight, Moisture, Temperature01, Slope,
                InWater, WaterSurface, Biome, ambientCelsius);
        }
    }
}
