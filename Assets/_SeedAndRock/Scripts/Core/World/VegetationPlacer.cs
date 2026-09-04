using System;
using System.Collections.Generic;

namespace SeedAndRock.World
{
    /// <summary>One placed prop. The mesh layer turns these into batched geometry.</summary>
    public struct PlacementInstance
    {
        public PlacementKind kind;
        public float x, y, z;
        public float scale;
        public float rotationDegrees;
        /// <summary>Silhouette family (e.g. 0 conifer, 1 broadleaf, 2 dry shrub for trees).</summary>
        public int variant;
        /// <summary>0..1 deterministic per-instance value for colour/shape variation.</summary>
        public float variation;
        public float moisture;
        public float snow;
        public SeedAndRockBiome biome;
    }

    public sealed class PlacementResult
    {
        public readonly List<PlacementInstance> Trees = new List<PlacementInstance>();
        public readonly List<PlacementInstance> Rocks = new List<PlacementInstance>();
        public readonly List<PlacementInstance> Grass = new List<PlacementInstance>();
    }

    /// <summary>
    /// Deterministic vegetation and rock placement driven by biome, slope, height, moisture,
    /// water distance, low-frequency density masks and clustering. No engine calls, no global RNG.
    /// </summary>
    public static class VegetationPlacer
    {
        public static PlacementResult Place(WorldSampler sampler, Action<float> progress = null)
        {
            PlacementResult result = new PlacementResult();
            PlaceTreesAndRocks(sampler, result, value => progress?.Invoke(value * 0.5f));
            PlaceGrass(sampler, result, value => progress?.Invoke(0.5f + value * 0.5f));
            WorldSettingsData settings = sampler.Settings;
            Thin(result.Trees, settings.maxTrees, settings.seed + 41);
            Thin(result.Rocks, settings.maxRocks, settings.seed + 43);
            Thin(result.Grass, settings.maxGrassBlades, settings.seed + 47);
            return result;
        }

        private static void PlaceTreesAndRocks(WorldSampler sampler, PlacementResult result, Action<float> progress)
        {
            WorldSettingsData settings = sampler.Settings;
            int seed = settings.seed;
            float spacing = settings.dressingSpacing;
            int cells = Math.Max(1, SRMath.CeilToInt(settings.worldSize / spacing));
            float half = settings.HalfSize;
            float clusterFrequency = 1f / (spacing * 9f);

            for (int z = 0; z < cells; z++)
            {
                if ((z & 7) == 0) progress?.Invoke(z / (float)cells);
                for (int x = 0; x < cells; x++)
                {
                    float px = SRMath.Lerp(-half, half, (x + SeedNoise.Hash01(seed + 2003, x, z)) / cells);
                    float pz = SRMath.Lerp(-half, half, (z + SeedNoise.Hash01(seed + 2017, x, z)) / cells);
                    SurfaceSample s = sampler.SampleSurface(px, pz);
                    if (s.isWater || s.waterDistance < 1.2f) continue;
                    float lip = sampler.GetWaterSurfaceCandidate(px, pz);
                    if (s.height < lip + 0.25f && s.waterDistance < 6f) continue;

                    BiomeDensity density = settings.GetDensity(s.biome);
                    float roll = SeedNoise.Hash01(seed + 2111, x, z);
                    float variation = SeedNoise.Hash01(seed + 2203, x, z);

                    float treeCluster = SRMath.SmoothStep(0.30f, 0.74f, SeedNoise.Fractal01(seed + 3001, px, pz, 2, clusterFrequency));
                    float treeChance = density.tree * settings.globalDressingDensity * SRMath.Lerp(0.12f, 1.65f, treeCluster);
                    if (s.biome == SeedAndRockBiome.Grassland || s.biome == SeedAndRockBiome.Plains)
                    {
                        float bank = SRMath.SmoothStep(1.2f, 3.5f, s.waterDistance) * (1f - SRMath.SmoothStep(8f, 18f, s.waterDistance));
                        treeChance += 0.20f * bank * settings.globalDressingDensity;
                    }

                    treeChance *= 1f - SRMath.SmoothStep(0.45f, 0.72f, s.slope);
                    treeChance *= 1f - s.snow * 0.45f;

                    if (roll < treeChance)
                    {
                        PlacementInstance tree = new PlacementInstance
                        {
                            kind = PlacementKind.Tree,
                            x = px, y = s.height - 0.08f, z = pz,
                            rotationDegrees = SeedNoise.Hash01(seed + 2309, x, z) * 360f,
                            variation = variation,
                            moisture = s.moisture,
                            snow = s.snow,
                            biome = s.biome,
                            variant = ChooseTreeVariant(s, SeedNoise.Hash01(seed + 2411, x, z))
                        };
                        float sizeBase = s.biome == SeedAndRockBiome.Forest ? 1.15f : s.biome == SeedAndRockBiome.Desert ? 0.6f : 0.95f;
                        tree.scale = sizeBase * SRMath.Lerp(0.72f, 1.42f, SeedNoise.Hash01(seed + 2503, x, z)) * SRMath.Lerp(0.85f, 1.1f, s.moisture);
                        result.Trees.Add(tree);
                        continue;
                    }

                    float rockCluster = SeedNoise.Fractal01(seed + 3203, px, pz, 2, clusterFrequency * 1.4f);
                    float rockChance = density.rock * settings.globalDressingDensity * (0.35f + s.slope * 2.2f + s.mountains * 0.6f) * SRMath.Lerp(0.3f, 1.4f, rockCluster);
                    if (roll < treeChance + rockChance && s.slope < 0.92f)
                    {
                        PlacementInstance rock = new PlacementInstance
                        {
                            kind = PlacementKind.Rock,
                            x = px, y = s.height, z = pz,
                            rotationDegrees = SeedNoise.Hash01(seed + 2309, x, z) * 360f,
                            variation = variation,
                            moisture = s.moisture,
                            snow = s.snow,
                            biome = s.biome,
                            variant = (int)(SeedNoise.Hash01(seed + 2617, x, z) * 3f) % 3,
                            scale = SRMath.Lerp(0.35f, 1.6f, SRMath.Pow(SeedNoise.Hash01(seed + 2719, x, z), 1.8f)) * (1f + s.mountains * 0.5f)
                        };
                        result.Rocks.Add(rock);
                    }
                }
            }

            progress?.Invoke(1f);
        }

        private static int ChooseTreeVariant(in SurfaceSample s, float roll)
        {
            switch (s.biome)
            {
                case SeedAndRockBiome.Snow:
                case SeedAndRockBiome.Mountains:
                    return 0;
                case SeedAndRockBiome.Desert:
                    return 2;
                case SeedAndRockBiome.Forest:
                    return s.temperature < 0.45f || roll < 0.35f ? 0 : 1;
                default:
                    return roll < 0.2f ? 0 : 1;
            }
        }

        private static void PlaceGrass(WorldSampler sampler, PlacementResult result, Action<float> progress)
        {
            WorldSettingsData settings = sampler.Settings;
            int seed = settings.seed;
            float spacing = settings.grassSpacing;
            int cells = Math.Max(1, SRMath.CeilToInt(settings.worldSize / spacing));
            float half = settings.HalfSize;
            float clusterFrequency = 1f / (spacing * 11f);

            for (int z = 0; z < cells; z++)
            {
                if ((z & 15) == 0) progress?.Invoke(z / (float)cells);
                for (int x = 0; x < cells; x++)
                {
                    float px = SRMath.Lerp(-half, half, (x + SeedNoise.Hash01(seed + 501, x, z)) / cells);
                    float pz = SRMath.Lerp(-half, half, (z + SeedNoise.Hash01(seed + 733, x, z)) / cells);
                    SurfaceSample s = sampler.SampleSurface(px, pz);
                    if (s.isWater || s.slope > 0.62f) continue;
                    if (s.height < sampler.GetWaterSurfaceCandidate(px, pz) + 0.2f && s.waterDistance < 5f) continue;

                    BiomeDensity density = settings.GetDensity(s.biome);
                    float cluster = 0.55f + 0.45f * SRMath.SmoothStep(0.25f, 0.8f, SeedNoise.Fractal01(seed + 3407, px, pz, 2, clusterFrequency));
                    float chance = density.grass * settings.globalDressingDensity * cluster;
                    chance *= 1f - s.sand * 0.85f;
                    chance *= 1f - s.snow * 0.8f;
                    chance *= 1f - s.rockiness * 0.7f;
                    if (SeedNoise.Hash01(seed + 1031, x, z) > chance) continue;

                    result.Grass.Add(new PlacementInstance
                    {
                        kind = PlacementKind.Grass,
                        x = px, y = s.height, z = pz,
                        rotationDegrees = SeedNoise.Hash01(seed + 1301, x, z) * 360f,
                        scale = SRMath.Lerp(0.34f, 0.9f, SeedNoise.Hash01(seed + 1201, x, z)) * SRMath.Lerp(0.8f, 1.15f, s.moisture),
                        variation = SeedNoise.Hash01(seed + 1409, x, z),
                        moisture = s.moisture,
                        snow = s.snow,
                        biome = s.biome,
                        variant = 0
                    });
                }
            }

            progress?.Invoke(1f);
        }

        /// <summary>Deterministically removes a uniform subset when a list exceeds its budget.</summary>
        internal static void Thin(List<PlacementInstance> list, int max, int seed)
        {
            if (max <= 0) { list.Clear(); return; }
            if (list.Count <= max) return;
            float keep = max / (float)list.Count;
            int write = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (SeedNoise.Hash01(seed, i, 0) <= keep)
                    list[write++] = list[i];
            }

            list.RemoveRange(write, list.Count - write);
        }
    }

    /// <summary>Finds a deterministic, dry, gently sloped spawn near the world origin.</summary>
    public static class SpawnFinder
    {
        public struct SpawnPoint
        {
            public float x, y, z;
        }

        public static SpawnPoint Find(WorldSampler sampler)
        {
            float limit = sampler.Settings.HalfSize * 0.8f;
            for (int ring = 0; ring < 40; ring++)
            {
                float radius = ring * 9f;
                if (radius > limit) break;
                int steps = ring == 0 ? 1 : 14;
                for (int step = 0; step < steps; step++)
                {
                    float angle = step * SRMath.Pi * 2f / steps + ring * 0.37f;
                    float x = SRMath.Cos(angle) * radius;
                    float z = SRMath.Sin(angle) * radius;
                    SurfaceSample s = sampler.SampleSurface(x, z);
                    bool dry = !s.isWater && s.height > sampler.WaterLevel + 0.5f && s.waterDistance > 4f;
                    bool gentle = s.slope < 0.35f && s.biome != SeedAndRockBiome.Mountains;
                    if (dry && gentle)
                        return new SpawnPoint { x = x, y = s.height + 0.2f, z = z };
                }
            }

            float fallback = sampler.GetHeightAt(0f, 0f);
            return new SpawnPoint { x = 0f, y = SRMath.Max(fallback, sampler.WaterLevel) + 1.5f, z = 0f };
        }
    }
}
