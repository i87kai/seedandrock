using System;
using UnityEngine;

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

    [Serializable]
    public struct BiomeTuning
    {
        [Range(0f, 1f)] public float grassDensity;
        [Range(0f, 1f)] public float treeDensity;
        [Range(0f, 1f)] public float rockDensity;
        public Color terrainTint;
        public Color grassTint;
    }

    [CreateAssetMenu(fileName = "SR_WorldGenerationSettings", menuName = "SeedAndRock/World Generation Settings")]
    public sealed class WorldGenerationSettings : ScriptableObject
    {
        [Header("Determinism")]
        public int seed = 240613;
        public bool generateOnPlay = true;

        [Header("Terrain")]
        [Min(64f)] public float worldSize = 1400f;
        [Range(65, 257)] public int terrainResolution = 161;
        [Range(8f, 80f)] public float terrainHeight = 52f;
        [Range(-5f, 20f)] public float waterLevel = 3.5f;
        [Range(0.001f, 0.08f)] public float continentFrequency = 0.0038f;
        [Range(0.001f, 0.14f)] public float detailFrequency = 0.018f;
        [Range(2, 7)] public int terrainOctaves = 4;

        [Header("Biome thresholds")]
        [Range(0f, 1f)] public float forestMoistureThreshold = 0.53f;
        [Range(0f, 1f)] public float highlandHeightThreshold = 0.64f;

        [Header("Dressing")]
        [Range(0.75f, 8f)] public float grassSpacing = 5.25f;
        [Range(0.5f, 2.5f)] public float globalDressingDensity = 1f;
        [Range(16, 256)] public int waterResolution = 160;

        [Header("Biome tuning")]
        public BiomeTuning grassland = new BiomeTuning
        {
            grassDensity = 0.84f, treeDensity = 0.05f, rockDensity = 0.08f,
            terrainTint = new Color(0.36f, 0.54f, 0.20f), grassTint = new Color(0.45f, 0.72f, 0.24f)
        };
        public BiomeTuning forest = new BiomeTuning
        {
            grassDensity = 0.62f, treeDensity = 0.34f, rockDensity = 0.13f,
            terrainTint = new Color(0.24f, 0.43f, 0.18f), grassTint = new Color(0.28f, 0.56f, 0.20f)
        };
        public BiomeTuning highlands = new BiomeTuning
        {
            grassDensity = 0.16f, treeDensity = 0.02f, rockDensity = 0.42f,
            terrainTint = new Color(0.40f, 0.40f, 0.36f), grassTint = new Color(0.42f, 0.51f, 0.28f)
        };

        public BiomeTuning GetBiomeTuning(SeedAndRockBiome biome)
        {
            switch (biome)
            {
                case SeedAndRockBiome.Forest: return forest;
                case SeedAndRockBiome.Mountains: return highlands;
                case SeedAndRockBiome.Desert:
                    return new BiomeTuning { grassDensity = 0.05f, treeDensity = 0f, rockDensity = 0.08f, terrainTint = new Color(0.72f, 0.57f, 0.28f), grassTint = new Color(0.72f, 0.63f, 0.25f) };
                case SeedAndRockBiome.Snow:
                    return new BiomeTuning { grassDensity = 0.10f, treeDensity = 0.08f, rockDensity = 0.18f, terrainTint = new Color(0.84f, 0.88f, 0.87f), grassTint = new Color(0.44f, 0.58f, 0.42f) };
                case SeedAndRockBiome.Plains:
                    return new BiomeTuning { grassDensity = 0.72f, treeDensity = 0.02f, rockDensity = 0.03f, terrainTint = new Color(0.46f, 0.64f, 0.25f), grassTint = new Color(0.56f, 0.76f, 0.28f) };
                default: return grassland;
            }
        }

        private void OnValidate()
        {
            terrainResolution = Mathf.ClosestPowerOfTwo(Mathf.Max(terrainResolution - 1, 64)) + 1;
        }
    }
}
