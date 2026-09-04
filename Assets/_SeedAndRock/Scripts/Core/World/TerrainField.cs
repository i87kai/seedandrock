namespace SeedAndRock.World
{
    /// <summary>Regional composition of one terrain point before hydrology carving is applied.</summary>
    public struct TerrainSample
    {
        /// <summary>Height in metres before rivers are carved.</summary>
        public float baseHeight;
        /// <summary>0..1 membership of broad, readable lowland plains.</summary>
        public float plains;
        /// <summary>0..1 membership of mountain regions.</summary>
        public float mountains;
        /// <summary>0..1 rolling-hill relief mask.</summary>
        public float hills;
    }

    /// <summary>
    /// Pure terrain shape: large-scale regional composition (continent, plains, hills, mountains)
    /// with domain warping so region borders meander rather than following noise cells.
    /// </summary>
    public sealed class TerrainField
    {
        private readonly int seed;
        private readonly float continentFrequency;
        private readonly float detailFrequency;
        private readonly int octaves;
        private readonly float height;
        private readonly float waterLevel;
        private readonly float mountainThreshold;
        private readonly float plainsThreshold;
        private readonly float warpAmount;
        private readonly float halfSize;

        public TerrainField(WorldSettingsData settings)
        {
            seed = settings.seed;
            halfSize = SRMath.Max(settings.worldSize * 0.5f, 1f);
            continentFrequency = settings.continentFrequency;
            detailFrequency = settings.detailFrequency;
            octaves = settings.terrainOctaves;
            height = settings.terrainHeight;
            waterLevel = settings.waterLevel;
            // Coverage settings map to noise thresholds: higher coverage means a lower threshold.
            // Fractal01 values cluster around 0.5 (roughly 0.25..0.75), so thresholds live inside that band.
            mountainThreshold = SRMath.Lerp(0.78f, 0.50f, settings.mountainCoverage);
            plainsThreshold = SRMath.Lerp(0.05f, 0.62f, settings.plainsCoverage);
            warpAmount = 0.16f / SRMath.Max(continentFrequency, 0.0002f);
        }

        public float WaterLevel => waterLevel;
        public float MaxHeight => height;

        /// <summary>Low-frequency coordinate warp shared by every regional layer so their borders agree.</summary>
        public void Warp(float x, float z, out float wx, out float wz)
        {
            float frequency = continentFrequency * 0.55f;
            float ox = SeedNoise.Fractal(seed + 701, x, z, 2, frequency, 2f, 0.5f);
            float oz = SeedNoise.Fractal(seed + 733, x + 791.3f, z - 421.7f, 2, frequency, 2f, 0.5f);
            wx = x + ox * warpAmount;
            wz = z + oz * warpAmount;
        }

        public TerrainSample Sample(float x, float z)
        {
            Warp(x, z, out float wx, out float wz);

            float continent = SeedNoise.Fractal(seed + 17, wx, wz, 4, continentFrequency, 2.03f, 0.50f);
            float broad = SeedNoise.Fractal(seed + 47, wx, wz, 3, continentFrequency * 0.52f, 2.0f, 0.52f);

            float mountainNoise = SeedNoise.Fractal01(seed + 233, wx, wz, 3, continentFrequency * 1.1f);
            float mountains = SRMath.SmoothStep(mountainThreshold - 0.10f, mountainThreshold + 0.07f, mountainNoise);

            // Plains live where the broad layer is close to flat and no mountains claim the area.
            float plainsNoise = SeedNoise.Fractal01(seed + 61, wx, wz, 2, continentFrequency * 0.6f);
            float plains = SRMath.SmoothStep(plainsThreshold - 0.22f, plainsThreshold + 0.10f, 1f - SRMath.Abs(broad) * 0.7f - (1f - plainsNoise) * 0.5f);
            plains *= 1f - mountains;

            float rolling = SeedNoise.Fractal(seed + 101, wx, wz, octaves, detailFrequency, 2.02f, 0.48f);
            float fine = SeedNoise.Fractal(seed + 151, x, z, 2, detailFrequency * 2.7f, 2.0f, 0.42f);
            float hillsMask = (1f - plains) * (1f - mountains * 0.6f);

            float ridged = SeedNoise.Ridged(seed + 277, wx, wz, 4, detailFrequency * 0.55f, 2.05f, 0.5f, 2.6f);

            // Land slopes gently toward the world border so drainage has an outlet and the map reads as a
            // coast-bounded region instead of a box with clipped terrain; the interior keeps its basins.
            float radial = SRMath.Length(x, z) / halfSize * 0.35f + SRMath.Max(SRMath.Abs(x), SRMath.Abs(z)) / halfSize * 0.65f;
            float edge = SRMath.SmoothStep(0.86f, 1.12f, radial);
            float baseShape = waterLevel + height * 0.17f + continent * height * 0.27f + broad * height * 0.12f - edge * height * 0.5f;
            float relief = rolling * height * SRMath.Lerp(0.035f, 0.13f, hillsMask) + fine * height * 0.018f * (1f - plains * 0.7f);
            float peaks = mountains * (height * 0.2f + ridged * height * 0.95f);

            TerrainSample sample;
            sample.baseHeight = baseShape + relief + peaks;
            sample.plains = plains;
            sample.mountains = mountains;
            sample.hills = hillsMask;
            return sample;
        }

        public float BaseHeight(float x, float z) => Sample(x, z).baseHeight;
    }

    /// <summary>Climate layers used for biome selection. Temperature drops with altitude; moisture rises near water.</summary>
    public sealed class ClimateField
    {
        private readonly int seed;
        private readonly float frequency;
        private readonly float worldSize;
        private readonly TerrainField terrain;

        public ClimateField(WorldSettingsData settings, TerrainField terrain)
        {
            seed = settings.seed;
            frequency = settings.continentFrequency;
            worldSize = settings.worldSize;
            this.terrain = terrain;
        }

        /// <summary>Raw 0..1 temperature before altitude cooling.</summary>
        public float Temperature(float x, float z)
        {
            terrain.Warp(x, z, out float wx, out float wz);
            float noise = SeedNoise.Fractal01(seed + 463, wx, wz, 3, frequency * 0.95f);
            // Fractal01 clusters around 0.5; stretch it so hot and cold extremes actually occur.
            float stretched = (noise - 0.5f) * 1.55f + 0.55f;
            // A gentle north/south gradient keeps cold and hot regions readable on the map.
            float latitude = SRMath.Clamp(z / SRMath.Max(worldSize, 1f), -0.5f, 0.5f);
            return SRMath.Clamp01(stretched - latitude * 0.36f);
        }

        /// <summary>Raw 0..1 moisture before water-proximity bonus.</summary>
        public float Moisture(float x, float z)
        {
            terrain.Warp(x + 311f, z - 127f, out float wx, out float wz);
            float noise = SeedNoise.Fractal01(seed + 419, wx, wz, 3, frequency * 1.05f);
            return SRMath.Clamp01((noise - 0.5f) * 1.8f + 0.5f);
        }
    }
}
