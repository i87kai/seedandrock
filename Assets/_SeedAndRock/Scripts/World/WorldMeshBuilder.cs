using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SeedAndRock.World
{
    /// <summary>Per-vertex surface data for the whole terrain grid, sampled once and shared by all chunks.</summary>
    public sealed class TerrainGrid
    {
        public readonly int Resolution;
        public readonly float HalfSize;
        public readonly float Step;
        public readonly SurfaceSample[] Samples;

        public TerrainGrid(int resolution, float worldSize)
        {
            Resolution = resolution;
            HalfSize = worldSize * 0.5f;
            Step = worldSize / (resolution - 1);
            Samples = new SurfaceSample[resolution * resolution];
        }

        public float PositionX(int ix) => -HalfSize + ix * Step;
        public float PositionZ(int iz) => -HalfSize + iz * Step;

        public ref SurfaceSample At(int ix, int iz) => ref Samples[Mathf.Clamp(iz, 0, Resolution - 1) * Resolution + Mathf.Clamp(ix, 0, Resolution - 1)];

        /// <summary>Samples every grid node. Rows are independent so the work is spread over worker threads deterministically.</summary>
        public static TerrainGrid Sample(WorldSampler sampler, CancellationToken token)
        {
            WorldSettingsData settings = sampler.Settings;
            TerrainGrid grid = new TerrainGrid(settings.terrainResolution, settings.worldSize);
            int n = grid.Resolution;
            Parallel.For(0, n, new ParallelOptions { CancellationToken = token }, iz =>
            {
                float pz = grid.PositionZ(iz);
                for (int ix = 0; ix < n; ix++)
                    grid.Samples[iz * n + ix] = sampler.SampleSurface(grid.PositionX(ix), pz);
            });
            return grid;
        }

        /// <summary>Central-difference normal; edge nodes fall back to the sampler so chunk borders match.</summary>
        public Vector3 NormalAt(WorldSampler sampler, int ix, int iz)
        {
            float left = ix > 0 ? At(ix - 1, iz).height : sampler.GetHeightAt(PositionX(ix) - Step, PositionZ(iz));
            float right = ix < Resolution - 1 ? At(ix + 1, iz).height : sampler.GetHeightAt(PositionX(ix) + Step, PositionZ(iz));
            float down = iz > 0 ? At(ix, iz - 1).height : sampler.GetHeightAt(PositionX(ix), PositionZ(iz) - Step);
            float up = iz < Resolution - 1 ? At(ix, iz + 1).height : sampler.GetHeightAt(PositionX(ix), PositionZ(iz) + Step);
            return new Vector3(left - right, 2f * Step, down - up).normalized;
        }
    }

    /// <summary>Builds chunked terrain and water mesh data from the deterministic sampler. Thread-safe (no engine objects).</summary>
    public static class WorldMeshBuilder
    {
        public static MeshData[] BuildTerrainChunks(WorldSampler sampler, TerrainGrid grid, WorldGenerationSettings palette, ChunkGrid chunks, CancellationToken token)
        {
            int n = grid.Resolution;
            int cellsPerChunk = (n - 1) / chunks.Count;
            if (cellsPerChunk * chunks.Count != n - 1)
                throw new InvalidOperationException("Terrain resolution - 1 must be divisible by the chunk count.");

            MeshData[] result = new MeshData[chunks.Total];
            int verticesPerSide = cellsPerChunk + 1;
            Parallel.For(0, chunks.Total, new ParallelOptions { CancellationToken = token }, chunk =>
            {
                int chunkX = chunk % chunks.Count;
                int chunkZ = chunk / chunks.Count;
                MeshData data = new MeshData(chunks.NameFor("SR_Terrain", chunk));
                data.Reserve(verticesPerSide * verticesPerSide, cellsPerChunk * cellsPerChunk * 6);
                int startX = chunkX * cellsPerChunk;
                int startZ = chunkZ * cellsPerChunk;

                for (int lz = 0; lz < verticesPerSide; lz++)
                {
                    for (int lx = 0; lx < verticesPerSide; lx++)
                    {
                        int ix = startX + lx;
                        int iz = startZ + lz;
                        ref SurfaceSample s = ref grid.At(ix, iz);
                        data.Vertices.Add(new Vector3(s.x, s.height, s.z));
                        data.Normals.Add(grid.NormalAt(sampler, ix, iz));
                        data.Colors.Add(TerrainColor(in s, palette));
                        data.Uv0.Add(new Vector2(s.x / grid.HalfSize * 0.5f + 0.5f, s.z / grid.HalfSize * 0.5f + 0.5f));
                        data.Uv1.Add(new Vector4(s.wetness, s.snow, s.sand, s.rockiness));
                    }
                }

                for (int lz = 0; lz < cellsPerChunk; lz++)
                {
                    for (int lx = 0; lx < cellsPerChunk; lx++)
                    {
                        int i = lz * verticesPerSide + lx;
                        // Alternate the diagonal so long slopes do not show a uniform saw-tooth pattern.
                        bool flip = ((startX + lx + startZ + lz) & 1) == 0;
                        if (flip)
                        {
                            data.Triangles.Add(i); data.Triangles.Add(i + verticesPerSide); data.Triangles.Add(i + 1);
                            data.Triangles.Add(i + 1); data.Triangles.Add(i + verticesPerSide); data.Triangles.Add(i + verticesPerSide + 1);
                        }
                        else
                        {
                            data.Triangles.Add(i); data.Triangles.Add(i + verticesPerSide + 1); data.Triangles.Add(i + 1);
                            data.Triangles.Add(i); data.Triangles.Add(i + verticesPerSide); data.Triangles.Add(i + verticesPerSide + 1);
                        }
                    }
                }

                result[chunk] = data;
            });

            return result;
        }

        private static Color TerrainColor(in SurfaceSample s, WorldGenerationSettings palette)
        {
            Color tint = palette.GetBiomeTuning(s.biome).terrainTint;
            // Soften biome borders: pull the tint toward neighbouring climates using continuous factors.
            Color forest = palette.forest.terrainTint;
            Color plains = palette.plains.terrainTint;
            if (s.biome == SeedAndRockBiome.Grassland || s.biome == SeedAndRockBiome.Plains)
            {
                float lush = Mathf.InverseLerp(0.35f, 0.7f, s.moisture);
                tint = Color.Lerp(Color.Lerp(plains, tint, 0.5f), forest, lush * 0.45f);
            }

            tint = Color.Lerp(tint, palette.desert.terrainTint, s.sand * 0.85f);
            tint = Color.Lerp(tint, palette.highlands.terrainTint, s.rockiness * 0.5f);
            tint = Color.Lerp(tint, palette.snow.terrainTint, s.snow);
            float elevation = Mathf.InverseLerp(palette.waterLevel - palette.terrainHeight * 0.35f, palette.waterLevel + palette.terrainHeight, s.height);
            return new Color(tint.r, tint.g, tint.b, elevation);
        }

        /// <summary>
        /// Water surface as a regular grid clipped to cells that touch water. Vertex heights come from the
        /// hydrology field, so lake planes are flat and river ribbons descend smoothly; shorelines are the
        /// natural intersection with the terrain rather than cell boundaries.
        /// </summary>
        public static MeshData[] BuildWaterChunks(WorldSampler sampler, int resolution, ChunkGrid chunks, CancellationToken token)
        {
            WorldSettingsData settings = sampler.Settings;
            resolution = Mathf.Clamp(resolution, 16, 1024);
            float half = settings.HalfSize;
            float step = settings.worldSize / (resolution - 1);

            int vertexCount = resolution * resolution;
            bool[] isWater = new bool[vertexCount];
            float[] surface = new float[vertexCount];
            float[] depth = new float[vertexCount];
            float[] flow = new float[vertexCount];
            Parallel.For(0, resolution, new ParallelOptions { CancellationToken = token }, iz =>
            {
                float z = -half + iz * step;
                for (int ix = 0; ix < resolution; ix++)
                {
                    float x = -half + ix * step;
                    int index = iz * resolution + ix;
                    float height = sampler.GetHeightAt(x, z);
                    isWater[index] = sampler.TryGetWaterSurfaceAt(x, z, height, out float found);
                    float candidate = isWater[index] ? found : sampler.GetWaterSurfaceCandidate(x, z);
                    // Land vertices never lift the sheet above their own ground: where a hillside stream's
                    // surface would otherwise hang over a lower bank, the edge hugs the terrain instead.
                    if (!isWater[index] && candidate > height + 1.0f) candidate = height + 0.05f;
                    surface[index] = candidate;
                    depth[index] = candidate - height;
                    flow[index] = sampler.Hydrology.Sample(sampler.Hydrology.RiverStrength, x, z);
                }
            });

            MeshData[] result = new MeshData[chunks.Total];
            for (int i = 0; i < result.Length; i++) result[i] = new MeshData(chunks.NameFor("SR_Water", i));
            int[] remap = new int[vertexCount];
            for (int chunk = 0; chunk < chunks.Total; chunk++)
            {
                token.ThrowIfCancellationRequested();
                MeshData data = result[chunk];
                for (int i = 0; i < vertexCount; i++) remap[i] = -1;
                int chunkX = chunk % chunks.Count;
                int chunkZ = chunk / chunks.Count;
                int cellsPerChunk = Mathf.CeilToInt((resolution - 1) / (float)chunks.Count);
                int startX = chunkX * cellsPerChunk;
                int startZ = chunkZ * cellsPerChunk;
                int endX = Mathf.Min(startX + cellsPerChunk, resolution - 1);
                int endZ = Mathf.Min(startZ + cellsPerChunk, resolution - 1);

                for (int iz = startZ; iz < endZ; iz++)
                {
                    for (int ix = startX; ix < endX; ix++)
                    {
                        int a = iz * resolution + ix;
                        int b = a + 1;
                        int c = a + resolution;
                        int d = c + 1;
                        if (!isWater[a] && !isWater[b] && !isWater[c] && !isWater[d]) continue;
                        int ia = Vertex(a), ib = Vertex(b), ic = Vertex(c), id = Vertex(d);
                        data.Triangles.Add(ia); data.Triangles.Add(ic); data.Triangles.Add(ib);
                        data.Triangles.Add(ib); data.Triangles.Add(ic); data.Triangles.Add(id);
                    }
                }

                int Vertex(int index)
                {
                    if (remap[index] >= 0) return remap[index];
                    int ix = index % resolution;
                    int iz = index / resolution;
                    float x = -half + ix * step;
                    float z = -half + iz * step;
                    remap[index] = data.Vertices.Count;
                    data.Vertices.Add(new Vector3(x, surface[index], z));
                    data.Normals.Add(Vector3.up);
                    data.Uv0.Add(new Vector2(x, z));
                    data.Uv1.Add(new Vector4(Mathf.Max(depth[index], 0f), flow[index], 0f, 0f));
                    return remap[index];
                }
            }

            return result;
        }

        /// <summary>A large ring of flat sea at water level surrounding the terrain so the coast never ends at a hard edge.</summary>
        public static MeshData BuildSeaSkirt(WorldSettingsData settings, float extent)
        {
            MeshData data = new MeshData("SR_SeaSkirt");
            float inner = settings.HalfSize - 0.5f;
            float outer = settings.HalfSize + extent;
            float y = settings.waterLevel;
            Vector3[] ring =
            {
                new Vector3(-outer, y, -outer), new Vector3(outer, y, -outer), new Vector3(outer, y, outer), new Vector3(-outer, y, outer),
                new Vector3(-inner, y, -inner), new Vector3(inner, y, -inner), new Vector3(inner, y, inner), new Vector3(-inner, y, inner)
            };
            foreach (Vector3 v in ring)
            {
                data.Vertices.Add(v);
                data.Normals.Add(Vector3.up);
                data.Uv0.Add(new Vector2(v.x, v.z));
                data.Uv1.Add(new Vector4(30f, 0f, 0f, 0f));
            }

            int[] quads = { 0, 1, 5, 4, 1, 2, 6, 5, 2, 3, 7, 6, 3, 0, 4, 7 };
            for (int q = 0; q < 4; q++)
            {
                int a = quads[q * 4], b = quads[q * 4 + 1], c = quads[q * 4 + 2], d = quads[q * 4 + 3];
                data.Triangles.Add(a); data.Triangles.Add(c); data.Triangles.Add(b);
                data.Triangles.Add(a); data.Triangles.Add(d); data.Triangles.Add(c);
            }

            return data;
        }
    }
}
