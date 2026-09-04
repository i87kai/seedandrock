using System.Collections.Generic;
using NUnit.Framework;
using SeedAndRock.World;

namespace SeedAndRock.Tests
{
    public sealed class WorldDeterminismTests
    {
        private static WorldSettingsData SmallSettings(int seed)
        {
            return new WorldSettingsData
            {
                seed = seed,
                worldSize = 400f,
                terrainResolution = 65,
                hydrologyResolution = 96,
                grassSpacing = 6f,
                dressingSpacing = 10f
            };
        }

        private static readonly (float x, float z)[] Probes =
        {
            (0f, 0f), (37.5f, -81.25f), (-150f, 120f), (199f, 199f), (-199f, -60f), (12.3f, 145.6f), (-88f, -88f), (66f, 5f)
        };

        [Test]
        public void SameSeedProducesIdenticalHeightBiomeAndWaterSamples()
        {
            WorldSampler a = WorldSampler.Build(SmallSettings(240613));
            WorldSampler b = WorldSampler.Build(SmallSettings(240613));

            foreach ((float x, float z) in Probes)
            {
                Assert.That(b.GetHeightAt(x, z), Is.EqualTo(a.GetHeightAt(x, z)), "height differs at " + x + "," + z);
                Assert.That(b.GetBiomeAt(x, z), Is.EqualTo(a.GetBiomeAt(x, z)), "biome differs at " + x + "," + z);
                bool waterA = a.TryGetWaterSurfaceAt(x, z, out float surfaceA);
                bool waterB = b.TryGetWaterSurfaceAt(x, z, out float surfaceB);
                Assert.That(waterB, Is.EqualTo(waterA));
                if (waterA) Assert.That(surfaceB, Is.EqualTo(surfaceA));
            }
        }

        [Test]
        public void SameSeedProducesIdenticalPlacementAndSpawn()
        {
            WorldSampler a = WorldSampler.Build(SmallSettings(99));
            WorldSampler b = WorldSampler.Build(SmallSettings(99));
            PlacementResult placementA = VegetationPlacer.Place(a);
            PlacementResult placementB = VegetationPlacer.Place(b);

            Assert.That(placementB.Trees.Count, Is.EqualTo(placementA.Trees.Count));
            Assert.That(placementB.Rocks.Count, Is.EqualTo(placementA.Rocks.Count));
            Assert.That(placementB.Grass.Count, Is.EqualTo(placementA.Grass.Count));
            for (int i = 0; i < placementA.Trees.Count; i++)
            {
                Assert.That(placementB.Trees[i].x, Is.EqualTo(placementA.Trees[i].x));
                Assert.That(placementB.Trees[i].y, Is.EqualTo(placementA.Trees[i].y));
                Assert.That(placementB.Trees[i].z, Is.EqualTo(placementA.Trees[i].z));
                Assert.That(placementB.Trees[i].scale, Is.EqualTo(placementA.Trees[i].scale));
                Assert.That(placementB.Trees[i].variant, Is.EqualTo(placementA.Trees[i].variant));
            }

            SpawnFinder.SpawnPoint spawnA = SpawnFinder.Find(a);
            SpawnFinder.SpawnPoint spawnB = SpawnFinder.Find(b);
            Assert.That(spawnB.x, Is.EqualTo(spawnA.x));
            Assert.That(spawnB.y, Is.EqualTo(spawnA.y));
            Assert.That(spawnB.z, Is.EqualTo(spawnA.z));
        }

        [Test]
        public void DifferentSeedsProduceMeaningfullyDifferentWorlds()
        {
            WorldSampler a = WorldSampler.Build(SmallSettings(1));
            WorldSampler b = WorldSampler.Build(SmallSettings(2));

            int differentHeights = 0;
            int differentBiomes = 0;
            const int grid = 24;
            for (int z = 0; z < grid; z++)
            {
                for (int x = 0; x < grid; x++)
                {
                    float px = SRMath.Lerp(-190f, 190f, x / (grid - 1f));
                    float pz = SRMath.Lerp(-190f, 190f, z / (grid - 1f));
                    if (SRMath.Abs(a.GetHeightAt(px, pz) - b.GetHeightAt(px, pz)) > 0.5f) differentHeights++;
                    if (a.GetBiomeAt(px, pz) != b.GetBiomeAt(px, pz)) differentBiomes++;
                }
            }

            Assert.That(differentHeights, Is.GreaterThan(grid * grid / 2), "most heights should differ between seeds");
            Assert.That(differentBiomes, Is.GreaterThan(grid * grid / 6), "biome layout should differ between seeds");
        }

        [Test]
        public void SpawnIsDryAndWalkable()
        {
            foreach (int seed in new[] { 1, 7, 42, 240613, 918273 })
            {
                WorldSampler sampler = WorldSampler.Build(SmallSettings(seed));
                SpawnFinder.SpawnPoint spawn = SpawnFinder.Find(sampler);
                SurfaceSample sample = sampler.SampleSurface(spawn.x, spawn.z);
                Assert.That(sample.isWater, Is.False, "spawn is in water for seed " + seed);
                Assert.That(spawn.y, Is.GreaterThanOrEqualTo(sampler.WaterLevel), "spawn below water level for seed " + seed);
                Assert.That(spawn.y, Is.GreaterThan(sample.height), "spawn should sit slightly above the surface");
            }
        }

        [Test]
        public void WorldContainsLandWaterAndMultipleBiomes()
        {
            WorldSettingsData settings = new WorldSettingsData { seed = 240613, worldSize = 1000f, hydrologyResolution = 128 };
            WorldSampler sampler = WorldSampler.Build(settings);
            HashSet<SeedAndRockBiome> biomes = new HashSet<SeedAndRockBiome>();
            int water = 0;
            const int grid = 40;
            for (int z = 0; z < grid; z++)
            {
                for (int x = 0; x < grid; x++)
                {
                    float px = SRMath.Lerp(-480f, 480f, x / (grid - 1f));
                    float pz = SRMath.Lerp(-480f, 480f, z / (grid - 1f));
                    SurfaceSample sample = sampler.SampleSurface(px, pz);
                    biomes.Add(sample.biome);
                    if (sample.isWater) water++;
                }
            }

            float waterFraction = water / (float)(grid * grid);
            Assert.That(waterFraction, Is.GreaterThan(0.05f).And.LessThan(0.6f), "water coverage should be believable");
            Assert.That(biomes.Count, Is.GreaterThanOrEqualTo(4), "expected a varied biome mix");
            Assert.That(biomes, Does.Contain(SeedAndRockBiome.Plains).Or.Contain(SeedAndRockBiome.Grassland));
        }
    }

    public sealed class HydrologyTests
    {
        private static WorldSettingsData Settings(int seed) => new WorldSettingsData { seed = seed, worldSize = 600f, hydrologyResolution = 128 };

        [Test]
        public void FilledSurfaceNeverDropsBelowTerrainAndRiversDescend()
        {
            WorldSampler sampler = WorldSampler.Build(Settings(240613));
            HydrologyField field = sampler.Hydrology;
            for (int i = 0; i < field.Filled.Length; i++)
                Assert.That(field.Filled[i], Is.GreaterThanOrEqualTo(field.BaseHeight[i] - 1e-4f));

            Assert.That(field.RiverCellCount, Is.GreaterThan(0), "expected at least one river");
            for (int i = 0; i < field.RiverStrength.Length; i++)
            {
                if (field.RiverStrength[i] < 0.999f) continue;
                Assert.That(field.RiverSurface[i], Is.LessThanOrEqualTo(field.Filled[i] + 1e-3f), "river surface should not float above the bank");
                Assert.That(field.RiverBed[i], Is.LessThan(field.RiverSurface[i]));
            }
        }

        [Test]
        public void RiversAreConnectedToLakesOrTheEdge()
        {
            WorldSampler sampler = WorldSampler.Build(Settings(7));
            HydrologyField field = sampler.Hydrology;
            int n = field.Resolution;
            int isolated = 0;
            int total = 0;
            for (int z = 1; z < n - 1; z++)
            {
                for (int x = 1; x < n - 1; x++)
                {
                    int index = z * n + x;
                    if (field.RiverStrength[index] < 0.999f) continue;
                    total++;
                    bool hasNeighbour = false;
                    for (int dz = -1; dz <= 1 && !hasNeighbour; dz++)
                        for (int dx = -1; dx <= 1 && !hasNeighbour; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int ni = (z + dz) * n + (x + dx);
                            hasNeighbour = field.RiverStrength[ni] >= 0.999f || field.LakeMask[ni] > 0.5f;
                        }

                    if (!hasNeighbour) isolated++;
                }
            }

            Assert.That(total, Is.GreaterThan(20));
            Assert.That(isolated, Is.EqualTo(0), "every river cell must touch another river cell or a lake");
        }

        [Test]
        public void LakesHaveFlatSurfacesAboveTheirBeds()
        {
            WorldSampler sampler = WorldSampler.Build(Settings(42));
            HydrologyField field = sampler.Hydrology;
            Assert.That(field.Lakes.Count, Is.GreaterThan(0));
            for (int i = 0; i < field.LakeMask.Length; i++)
            {
                if (field.LakeMask[i] < 0.5f) continue;
                Assert.That(field.LakeSurface[i], Is.GreaterThan(field.BaseHeight[i]));
            }
        }

        [Test]
        public void WaterSurfaceQueriesSitAboveTerrain()
        {
            WorldSampler sampler = WorldSampler.Build(Settings(918273));
            int found = 0;
            for (int z = 0; z < 60; z++)
            {
                for (int x = 0; x < 60; x++)
                {
                    float px = SRMath.Lerp(-290f, 290f, x / 59f);
                    float pz = SRMath.Lerp(-290f, 290f, z / 59f);
                    if (!sampler.TryGetWaterSurfaceAt(px, pz, out float surface)) continue;
                    found++;
                    Assert.That(surface, Is.GreaterThan(sampler.GetHeightAt(px, pz)));
                }
            }

            Assert.That(found, Is.GreaterThan(0));
        }
    }
}
