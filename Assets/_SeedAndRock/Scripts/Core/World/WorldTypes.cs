using System;

namespace SeedAndRock.World
{
    public enum SeedAndRockBiome
    {
        Plains,
        Grassland,
        Forest,
        Desert,
        Snow,
        Mountains
    }

    public enum PlacementKind
    {
        Tree,
        Rock,
        Grass
    }

    /// <summary>Per-biome dressing probabilities consumed by the placement pass.</summary>
    [Serializable]
    public struct BiomeDensity
    {
        public float grass;
        public float tree;
        public float rock;

        public BiomeDensity(float grass, float tree, float rock)
        {
            this.grass = grass;
            this.tree = tree;
            this.rock = rock;
        }
    }

    /// <summary>
    /// Immutable snapshot of everything the deterministic core needs. The Unity ScriptableObject
    /// converts itself into one of these so no generation code touches engine objects.
    /// </summary>
    public sealed class WorldSettingsData
    {
        public const int BiomeCount = 6;

        public int seed;
        public float worldSize = 1000f;
        public int terrainResolution = 257;
        public int terrainChunks = 4;
        public float terrainHeight = 60f;
        public float waterLevel = 3.5f;
        public float continentFrequency = 0.0032f;
        public float detailFrequency = 0.02f;
        public int terrainOctaves = 4;
        public float mountainCoverage = 0.22f;
        public float plainsCoverage = 0.45f;

        public float forestMoistureThreshold = 0.58f;
        public float highlandHeightThreshold = 0.62f;

        public int hydrologyResolution = 192;
        public float riverCatchmentCells = 180f;
        public float riverMinWidth = 2.6f;
        public float riverMaxWidth = 9f;
        public float riverDepth = 1.6f;
        public float lakeMinDepth = 1.1f;
        public int lakeMinCells = 6;

        public float grassSpacing = 3.2f;
        public float dressingSpacing = 6f;
        public float globalDressingDensity = 1f;
        public int maxGrassBlades = 90000;
        public int maxTrees = 9000;
        public int maxRocks = 6000;

        public BiomeDensity[] densities = DefaultDensities();

        public static BiomeDensity[] DefaultDensities()
        {
            BiomeDensity[] result = new BiomeDensity[BiomeCount];
            result[(int)SeedAndRockBiome.Plains] = new BiomeDensity(0.70f, 0.025f, 0.03f);
            result[(int)SeedAndRockBiome.Grassland] = new BiomeDensity(0.80f, 0.07f, 0.08f);
            result[(int)SeedAndRockBiome.Forest] = new BiomeDensity(0.55f, 0.42f, 0.10f);
            result[(int)SeedAndRockBiome.Desert] = new BiomeDensity(0.06f, 0.01f, 0.10f);
            result[(int)SeedAndRockBiome.Snow] = new BiomeDensity(0.08f, 0.09f, 0.16f);
            result[(int)SeedAndRockBiome.Mountains] = new BiomeDensity(0.14f, 0.03f, 0.40f);
            return result;
        }

        public BiomeDensity GetDensity(SeedAndRockBiome biome)
        {
            int index = (int)biome;
            if (densities == null || index < 0 || index >= densities.Length)
                return new BiomeDensity(0.5f, 0.05f, 0.05f);
            return densities[index];
        }

        public float HalfSize => worldSize * 0.5f;

        /// <summary>Returns a copy with a different seed; everything else is preserved.</summary>
        public WorldSettingsData WithSeed(int newSeed)
        {
            WorldSettingsData copy = (WorldSettingsData)MemberwiseClone();
            copy.seed = newSeed;
            copy.densities = densities == null ? DefaultDensities() : (BiomeDensity[])densities.Clone();
            return copy;
        }

        /// <summary>Clamps every field into a range the generator can safely handle.</summary>
        public void Sanitize()
        {
            worldSize = SRMath.Clamp(worldSize, 64f, 4000f);
            terrainResolution = SRMath.Clamp(terrainResolution, 33, 513);
            terrainChunks = SRMath.Clamp(terrainChunks, 1, 8);
            terrainHeight = SRMath.Clamp(terrainHeight, 8f, 160f);
            terrainOctaves = SRMath.Clamp(terrainOctaves, 1, 8);
            continentFrequency = SRMath.Clamp(continentFrequency, 0.0002f, 0.1f);
            detailFrequency = SRMath.Clamp(detailFrequency, 0.0005f, 0.5f);
            mountainCoverage = SRMath.Clamp01(mountainCoverage);
            plainsCoverage = SRMath.Clamp01(plainsCoverage);
            hydrologyResolution = SRMath.Clamp(hydrologyResolution, 32, 512);
            riverCatchmentCells = SRMath.Max(8f, riverCatchmentCells);
            riverMinWidth = SRMath.Clamp(riverMinWidth, 0.5f, 40f);
            riverMaxWidth = SRMath.Clamp(riverMaxWidth, riverMinWidth, 80f);
            riverDepth = SRMath.Clamp(riverDepth, 0.2f, 12f);
            lakeMinDepth = SRMath.Clamp(lakeMinDepth, 0.05f, 20f);
            lakeMinCells = Math.Max(1, lakeMinCells);
            grassSpacing = SRMath.Clamp(grassSpacing, 0.5f, 20f);
            dressingSpacing = SRMath.Clamp(dressingSpacing, 1f, 40f);
            globalDressingDensity = SRMath.Clamp(globalDressingDensity, 0f, 3f);
            maxGrassBlades = Math.Max(0, maxGrassBlades);
            maxTrees = Math.Max(0, maxTrees);
            maxRocks = Math.Max(0, maxRocks);
            if (densities == null || densities.Length != BiomeCount)
                densities = DefaultDensities();
        }
    }
}
