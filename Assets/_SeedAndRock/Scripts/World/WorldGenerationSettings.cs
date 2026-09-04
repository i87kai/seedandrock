using System;
using UnityEngine;

namespace SeedAndRock.World
{
    /// <summary>Per-biome dressing densities and palette. Densities feed the deterministic placer; tints feed the meshes.</summary>
    [Serializable]
    public struct BiomeTuning
    {
        [Range(0f, 1f)] public float grassDensity;
        [Range(0f, 1f)] public float treeDensity;
        [Range(0f, 1f)] public float rockDensity;
        public Color terrainTint;
        public Color grassTint;

        public BiomeDensity ToDensity() => new BiomeDensity(grassDensity, treeDensity, rockDensity);
    }

    /// <summary>
    /// Main-thread snapshot of the presentation values consumed while building worker-thread mesh buffers.
    /// It deliberately contains no UnityEngine.Object references.
    /// </summary>
    public sealed class WorldGenerationPalette
    {
        private readonly Color[] terrainTints;
        private readonly Color[] grassTints;

        public float WaterLevel { get; }
        public float TerrainHeight { get; }

        public WorldGenerationPalette(float waterLevel, float terrainHeight, Color[] terrainTints, Color[] grassTints)
        {
            WaterLevel = waterLevel;
            TerrainHeight = terrainHeight;
            this.terrainTints = terrainTints;
            this.grassTints = grassTints;
        }

        public Color TerrainTint(SeedAndRockBiome biome) => terrainTints[Mathf.Clamp((int)biome, 0, terrainTints.Length - 1)];
        public Color GrassTint(SeedAndRockBiome biome) => grassTints[Mathf.Clamp((int)biome, 0, grassTints.Length - 1)];
    }

    /// <summary>
    /// Designer-facing generation settings. This asset is only a container: <see cref="ToData"/> produces the
    /// immutable snapshot consumed by the engine-independent core, so tweaking values here never leaks
    /// engine state into the deterministic pipeline.
    /// </summary>
    [CreateAssetMenu(fileName = "SR_WorldGenerationSettings", menuName = "SeedAndRock/World Generation Settings")]
    public sealed class WorldGenerationSettings : ScriptableObject
    {
        [Header("Determinism")]
        public int seed = 240613;
        public bool generateOnPlay = true;

        [Header("Terrain")]
        [Min(64f)] public float worldSize = 1000f;
        [Range(33, 513)] public int terrainResolution = 257;
        [Range(1, 8)] public int terrainChunks = 4;
        [Range(8f, 160f)] public float terrainHeight = 64f;
        [Range(-5f, 20f)] public float waterLevel = 3.5f;
        [Range(0.0005f, 0.08f)] public float continentFrequency = 0.0032f;
        [Range(0.001f, 0.14f)] public float detailFrequency = 0.02f;
        [Range(2, 7)] public int terrainOctaves = 4;
        [Range(0f, 1f)] public float mountainCoverage = 0.22f;
        [Range(0f, 1f)] public float plainsCoverage = 0.45f;

        [Header("Biome thresholds")]
        [Range(0f, 1f)] public float forestMoistureThreshold = 0.58f;
        [Range(0f, 1f)] public float highlandHeightThreshold = 0.62f;

        [Header("Hydrology")]
        [Range(32, 512)] public int hydrologyResolution = 192;
        [Range(8f, 2000f)] public float riverCatchmentCells = 180f;
        [Range(0.5f, 40f)] public float riverMinWidth = 2.6f;
        [Range(0.5f, 80f)] public float riverMaxWidth = 9f;
        [Range(0.2f, 12f)] public float riverDepth = 1.6f;
        [Range(0.05f, 20f)] public float lakeMinDepth = 1.1f;
        [Range(1, 200)] public int lakeMinCells = 6;

        [Header("Dressing")]
        [Range(0.75f, 20f)] public float grassSpacing = 2.4f;
        [Range(1f, 40f)] public float dressingSpacing = 6f;
        [Range(0f, 3f)] public float globalDressingDensity = 1f;
        [Min(0)] public int maxGrassBlades = 70000;
        [Min(0)] public int maxTrees = 9000;
        [Min(0)] public int maxRocks = 6000;
        [Range(16, 512)] public int waterResolution = 320;

        [Header("Biome tuning")]
        public BiomeTuning plains = new BiomeTuning
        {
            grassDensity = 0.70f, treeDensity = 0.025f, rockDensity = 0.03f,
            terrainTint = new Color(0.46f, 0.62f, 0.26f), grassTint = new Color(0.56f, 0.74f, 0.30f)
        };
        public BiomeTuning grassland = new BiomeTuning
        {
            grassDensity = 0.80f, treeDensity = 0.07f, rockDensity = 0.08f,
            terrainTint = new Color(0.37f, 0.54f, 0.22f), grassTint = new Color(0.45f, 0.70f, 0.26f)
        };
        public BiomeTuning forest = new BiomeTuning
        {
            grassDensity = 0.55f, treeDensity = 0.42f, rockDensity = 0.10f,
            terrainTint = new Color(0.25f, 0.42f, 0.19f), grassTint = new Color(0.30f, 0.55f, 0.22f)
        };
        public BiomeTuning desert = new BiomeTuning
        {
            grassDensity = 0.06f, treeDensity = 0.01f, rockDensity = 0.10f,
            terrainTint = new Color(0.76f, 0.63f, 0.38f), grassTint = new Color(0.70f, 0.62f, 0.30f)
        };
        public BiomeTuning snow = new BiomeTuning
        {
            grassDensity = 0.08f, treeDensity = 0.09f, rockDensity = 0.16f,
            terrainTint = new Color(0.86f, 0.90f, 0.92f), grassTint = new Color(0.55f, 0.66f, 0.55f)
        };
        public BiomeTuning highlands = new BiomeTuning
        {
            grassDensity = 0.14f, treeDensity = 0.03f, rockDensity = 0.40f,
            terrainTint = new Color(0.44f, 0.43f, 0.39f), grassTint = new Color(0.44f, 0.53f, 0.31f)
        };

        public BiomeTuning GetBiomeTuning(SeedAndRockBiome biome)
        {
            switch (biome)
            {
                case SeedAndRockBiome.Plains: return plains;
                case SeedAndRockBiome.Forest: return forest;
                case SeedAndRockBiome.Desert: return desert;
                case SeedAndRockBiome.Snow: return snow;
                case SeedAndRockBiome.Mountains: return highlands;
                default: return grassland;
            }
        }

        /// <summary>Creates the immutable, engine-free snapshot used by the generation core.</summary>
        public WorldSettingsData ToData(int? seedOverride = null)
        {
            WorldSettingsData data = new WorldSettingsData
            {
                seed = seedOverride ?? seed,
                worldSize = worldSize,
                terrainResolution = terrainResolution,
                terrainChunks = terrainChunks,
                terrainHeight = terrainHeight,
                waterLevel = waterLevel,
                continentFrequency = continentFrequency,
                detailFrequency = detailFrequency,
                terrainOctaves = terrainOctaves,
                mountainCoverage = mountainCoverage,
                plainsCoverage = plainsCoverage,
                forestMoistureThreshold = forestMoistureThreshold,
                highlandHeightThreshold = highlandHeightThreshold,
                hydrologyResolution = hydrologyResolution,
                riverCatchmentCells = riverCatchmentCells,
                riverMinWidth = riverMinWidth,
                riverMaxWidth = riverMaxWidth,
                riverDepth = riverDepth,
                lakeMinDepth = lakeMinDepth,
                lakeMinCells = lakeMinCells,
                grassSpacing = grassSpacing,
                dressingSpacing = dressingSpacing,
                globalDressingDensity = globalDressingDensity,
                maxGrassBlades = maxGrassBlades,
                maxTrees = maxTrees,
                maxRocks = maxRocks,
                densities = new BiomeDensity[WorldSettingsData.BiomeCount]
            };

            for (int i = 0; i < WorldSettingsData.BiomeCount; i++)
                data.densities[i] = GetBiomeTuning((SeedAndRockBiome)i).ToDensity();

            data.Sanitize();
            return data;
        }

        /// <summary>Copies all designer colours needed by background mesh construction on the main thread.</summary>
        public WorldGenerationPalette ToPalette()
        {
            Color[] terrain = new Color[WorldSettingsData.BiomeCount];
            Color[] grass = new Color[WorldSettingsData.BiomeCount];
            for (int i = 0; i < WorldSettingsData.BiomeCount; i++)
            {
                BiomeTuning tuning = GetBiomeTuning((SeedAndRockBiome)i);
                terrain[i] = tuning.terrainTint;
                grass[i] = tuning.grassTint;
            }

            return new WorldGenerationPalette(waterLevel, terrainHeight, terrain, grass);
        }

        private void OnValidate()
        {
            // Chunked terrain needs (resolution - 1) divisible by the chunk count; snap to the nearest power of two plus one.
            terrainResolution = Mathf.ClosestPowerOfTwo(Mathf.Max(terrainResolution - 1, 32)) + 1;
            terrainChunks = Mathf.ClosestPowerOfTwo(Mathf.Clamp(terrainChunks, 1, 8));
            riverMaxWidth = Mathf.Max(riverMaxWidth, riverMinWidth);
        }
    }
}
