using System;

namespace SeedAndRock.World
{
    /// <summary>Everything the presentation layer needs to know about one point on the surface.</summary>
    public struct SurfaceSample
    {
        public float x, z;
        public float height;
        public float baseHeight;
        /// <summary>0..1 slope where roughly 0.8 == 45 degrees.</summary>
        public float slope;
        /// <summary>0..1 height above water level relative to the terrain height budget.</summary>
        public float normalizedHeight;
        public float moisture;
        public float temperature;
        public float plains;
        public float mountains;
        public float waterDistance;
        public float riverStrength;
        public float wetness;
        public float snow;
        public float sand;
        public float rockiness;
        public bool isWater;
        public float waterSurface;
        public SeedAndRockBiome biome;
    }

    /// <summary>
    /// Pure, side-effect-free world query API. Construct once per seed/settings pair and query any
    /// world position; the same inputs always return bit-identical results.
    /// </summary>
    public sealed class WorldSampler
    {
        public readonly WorldSettingsData Settings;
        public readonly TerrainField Terrain;
        public readonly ClimateField Climate;
        public readonly HydrologyField Hydrology;

        public WorldSampler(WorldSettingsData settings, TerrainField terrain, ClimateField climate, HydrologyField hydrology)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            Climate = climate ?? throw new ArgumentNullException(nameof(climate));
            Hydrology = hydrology ?? throw new ArgumentNullException(nameof(hydrology));
        }

        /// <summary>Convenience one-shot build. The staged pipeline uses the individual constructors instead.</summary>
        public static WorldSampler Build(WorldSettingsData settings)
        {
            settings.Sanitize();
            TerrainField terrain = new TerrainField(settings);
            ClimateField climate = new ClimateField(settings, terrain);
            HydrologyField hydrology = HydrologyBuilder.Build(settings, terrain);
            return new WorldSampler(settings, terrain, climate, hydrology);
        }

        public float WaterLevel => Settings.waterLevel;

        public float GetHeightAt(float x, float z)
        {
            float baseHeight = Terrain.BaseHeight(x, z);
            return CarveHeight(x, z, baseHeight);
        }

        public float GetSlopeAt(float x, float z)
        {
            float step = Hydrology.CellSize;
            float dx = Hydrology.Sample(Hydrology.BaseHeight, x + step, z) - Hydrology.Sample(Hydrology.BaseHeight, x - step, z);
            float dz = Hydrology.Sample(Hydrology.BaseHeight, x, z + step) - Hydrology.Sample(Hydrology.BaseHeight, x, z - step);
            float gradient = SRMath.Length(dx, dz) / (2f * step);
            return SRMath.Clamp01(gradient * 0.8f);
        }

        public SeedAndRockBiome GetBiomeAt(float x, float z) => SampleSurface(x, z).biome;

        public bool TryGetWaterSurfaceAt(float x, float z, out float surface)
        {
            float height = GetHeightAt(x, z);
            return TryGetWaterSurface(x, z, height, out surface);
        }

        /// <summary>Water test for a point whose terrain height is already known (avoids a second height evaluation).</summary>
        public bool TryGetWaterSurfaceAt(float x, float z, float knownHeight, out float surface) => TryGetWaterSurface(x, z, knownHeight, out surface);

        /// <summary>Water height used for mesh vertices; always defined so shore quads can extend under the terrain.</summary>
        public float GetWaterSurfaceCandidate(float x, float z)
        {
            float lake = Hydrology.Sample(Hydrology.LakeSurface, x, z);
            float proximity = Hydrology.Sample(Hydrology.RiverProximity, x, z);
            if (proximity <= 0.01f) return lake;
            float river = Hydrology.SampleRiver(Hydrology.RiverSurface, x, z, lake);
            float lakeMask = Hydrology.Sample(Hydrology.LakeMask, x, z);
            return SRMath.Lerp(lake, river, SRMath.SmoothStep(0.01f, 0.35f, proximity) * (1f - lakeMask));
        }

        public SurfaceSample SampleSurface(float x, float z)
        {
            TerrainSample terrain = Terrain.Sample(x, z);
            SurfaceSample s = new SurfaceSample();
            s.x = x;
            s.z = z;
            s.baseHeight = terrain.baseHeight;
            s.plains = terrain.plains;
            s.mountains = terrain.mountains;
            s.height = CarveHeight(x, z, terrain.baseHeight);
            s.slope = GetSlopeAt(x, z);
            s.waterDistance = Hydrology.Sample(Hydrology.WaterDistance, x, z);
            s.riverStrength = Hydrology.Sample(Hydrology.RiverStrength, x, z);
            s.isWater = TryGetWaterSurface(x, z, s.height, out s.waterSurface);

            float budget = SRMath.Max(Settings.terrainHeight * 0.9f, 1f);
            s.normalizedHeight = SRMath.Clamp01((s.height - Settings.waterLevel) / budget);

            float rawTemperature = Climate.Temperature(x, z);
            s.temperature = SRMath.Clamp01(rawTemperature - s.normalizedHeight * 0.5f);
            float rawMoisture = Climate.Moisture(x, z);
            s.moisture = SRMath.Clamp01(rawMoisture * 0.86f - 0.04f + 0.22f * (1f - SRMath.SmoothStep(0f, 50f, s.waterDistance)));

            s.snow = SRMath.SmoothStep(0.33f, 0.20f, s.temperature);
            s.wetness = (1f - SRMath.SmoothStep(0.3f, 7f, s.waterDistance)) * (1f - s.snow * 0.6f);
            float heightAboveWater = s.height - (s.isWater ? s.waterSurface : GetWaterSurfaceCandidate(x, z));
            float shoreSand = (1f - SRMath.SmoothStep(0.5f, 9f, s.waterDistance)) * SRMath.SmoothStep(0.42f, 0.6f, s.temperature) * (1f - SRMath.SmoothStep(1.5f, 3.5f, heightAboveWater));
            s.rockiness = SRMath.Clamp01(SRMath.SmoothStep(0.35f, 0.75f, s.slope) + s.mountains * SRMath.SmoothStep(0.45f, 0.8f, s.normalizedHeight));

            s.biome = Classify(ref s, Settings);
            s.sand = SRMath.Clamp01(shoreSand + (s.biome == SeedAndRockBiome.Desert ? 1f - s.rockiness * 0.5f : 0f));
            if (s.biome == SeedAndRockBiome.Snow) s.snow = SRMath.Max(s.snow, 0.7f);
            return s;
        }

        private static SeedAndRockBiome Classify(ref SurfaceSample s, WorldSettingsData settings)
        {
            if (s.snow > 0.5f)
                return SeedAndRockBiome.Snow;
            bool high = s.normalizedHeight > settings.highlandHeightThreshold;
            if ((high && (s.slope > 0.14f || s.normalizedHeight > settings.highlandHeightThreshold + 0.15f)) || (s.mountains > 0.55f && s.normalizedHeight > 0.45f && s.slope > 0.1f))
                return SeedAndRockBiome.Mountains;
            if (s.temperature > 0.62f && s.moisture < 0.40f)
                return SeedAndRockBiome.Desert;
            if (s.moisture > settings.forestMoistureThreshold && s.normalizedHeight < 0.75f)
                return SeedAndRockBiome.Forest;
            if (s.plains > 0.6f && s.slope < 0.16f)
                return SeedAndRockBiome.Plains;
            return SeedAndRockBiome.Grassland;
        }

        private float CarveHeight(float x, float z, float baseHeight)
        {
            float height = baseHeight;
            float strength = Hydrology.Sample(Hydrology.RiverStrength, x, z);
            if (strength > 0.0005f)
            {
                float surface = Hydrology.SampleRiver(Hydrology.RiverSurface, x, z, baseHeight);
                float bed = Hydrology.SampleRiver(Hydrology.RiverBed, x, z, baseHeight);
                float profile = SRMath.SmoothStep(0.05f, 0.65f, strength);
                float channel = SRMath.Lerp(surface + 0.35f, bed, profile);
                height = SRMath.Lerp(height, SRMath.Min(height, channel), SRMath.Smooth01(strength * 1.3f));
            }

            float waterDistance = Hydrology.Sample(Hydrology.WaterDistance, x, z);
            if (waterDistance < 14f)
            {
                // Soften lake shores: compress the terrain slope in a band around the waterline so
                // beaches read as gentle transitions instead of noise-sharp edges.
                float lakeSurface = Hydrology.Sample(Hydrology.LakeSurface, x, z);
                float delta = height - lakeSurface;
                if (delta > -2f && delta < 3f)
                {
                    float shore = 1f - SRMath.SmoothStep(0f, 14f, waterDistance);
                    float band = 1f - SRMath.SmoothStep(1.5f, 3f, SRMath.Abs(delta));
                    height = lakeSurface + delta * SRMath.Lerp(1f, 0.6f, shore * band);
                }
            }

            return height;
        }

        private bool TryGetWaterSurface(float x, float z, float height, out float surface)
        {
            surface = float.MinValue;
            float lakeMask = Hydrology.Sample(Hydrology.LakeMask, x, z);
            if (lakeMask > 0.02f)
            {
                float lake = Hydrology.Sample(Hydrology.LakeSurface, x, z);
                if (lake > height + 0.02f) surface = lake;
            }

            float strength = Hydrology.Sample(Hydrology.RiverStrength, x, z);
            if (strength > 0.02f)
            {
                float river = Hydrology.SampleRiver(Hydrology.RiverSurface, x, z, float.MinValue);
                if (river > height + 0.02f) surface = SRMath.Max(surface, river);
            }

            return surface > float.MinValue;
        }
    }
}
