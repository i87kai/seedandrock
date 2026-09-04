using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SeedAndRock.World
{
    /// <summary>Builds deterministic, batched meshes used by WorldGenerator.</summary>
    public static class WorldMeshBuilder
    {
        public static Mesh BuildTerrain(WorldGenerator world, WorldGenerationSettings settings)
        {
            int resolution = settings.terrainResolution;
            int vertexCount = resolution * resolution;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            float half = settings.worldSize * 0.5f;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    float px = Mathf.Lerp(-half, half, x / (float)(resolution - 1));
                    float pz = Mathf.Lerp(-half, half, z / (float)(resolution - 1));
                    float height = world.GetHeightAt(px, pz);
                    SeedAndRockBiome biome = world.GetBiomeAt(px, pz);
                    Color tint = settings.GetBiomeTuning(biome).terrainTint;

                    vertices[index] = new Vector3(px, height, pz);
                    uvs[index] = new Vector2(x / (float)(resolution - 1), z / (float)(resolution - 1));
                    // RGB holds the biome tint. Alpha is the broad elevation blend consumed by
                    // the terrain shader, so neighbouring biome vertices blend naturally.
                    colors[index] = new Color(tint.r, tint.g, tint.b, Mathf.InverseLerp(-settings.terrainHeight * 0.35f, settings.terrainHeight, height));
                }
            }

            int t = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int i = z * resolution + x;
                    triangles[t++] = i;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + resolution + 1;
                }
            }

            Mesh mesh = new Mesh { name = "SR_TerrainMesh", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh BuildWater(WorldGenerator world, WorldGenerationSettings settings)
        {
            int resolution = settings.waterResolution;
            List<Vector3> vertices = new List<Vector3>(resolution * resolution / 2);
            List<Vector2> uvs = new List<Vector2>(resolution * resolution / 2);
            List<int> triangles = new List<int>((resolution - 1) * (resolution - 1) * 6);
            float half = settings.worldSize * 0.5f;

            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    float x0 = Mathf.Lerp(-half, half, x / (float)(resolution - 1));
                    float x1 = Mathf.Lerp(-half, half, (x + 1) / (float)(resolution - 1));
                    float z0 = Mathf.Lerp(-half, half, z / (float)(resolution - 1));
                    float z1 = Mathf.Lerp(-half, half, (z + 1) / (float)(resolution - 1));
                    float y;
                    if (!world.TryGetWaterSurfaceAt(x0, z0, out y) && !world.TryGetWaterSurfaceAt(x1, z0, out y) && !world.TryGetWaterSurfaceAt(x0, z1, out y) && !world.TryGetWaterSurfaceAt(x1, z1, out y))
                        continue;

                    int i = vertices.Count;
                    vertices.Add(new Vector3(x0, y, z0)); vertices.Add(new Vector3(x1, y, z0));
                    vertices.Add(new Vector3(x1, y, z1)); vertices.Add(new Vector3(x0, y, z1));
                    uvs.Add(new Vector2(x / (float)(resolution - 1), z / (float)(resolution - 1)));
                    uvs.Add(new Vector2((x + 1) / (float)(resolution - 1), z / (float)(resolution - 1)));
                    uvs.Add(new Vector2((x + 1) / (float)(resolution - 1), (z + 1) / (float)(resolution - 1)));
                    uvs.Add(new Vector2(x / (float)(resolution - 1), (z + 1) / (float)(resolution - 1)));
                    triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                    triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
                }
            }

            return MakeMesh("SR_WaterMesh", vertices, triangles, uvs, null);
        }

        public static Mesh BuildGrass(WorldGenerator world, WorldGenerationSettings settings)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            float half = settings.worldSize * 0.5f;
            int cells = Mathf.CeilToInt(settings.worldSize / settings.grassSpacing);

            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float jitterX = SeedNoise.Hash01(settings.seed + 501, x, z);
                    float jitterZ = SeedNoise.Hash01(settings.seed + 733, x, z);
                    float px = Mathf.Lerp(-half, half, (x + jitterX) / cells);
                    float pz = Mathf.Lerp(-half, half, (z + jitterZ) / cells);
                    float height = world.GetHeightAt(px, pz);
                    SeedAndRockBiome biome = world.GetBiomeAt(px, pz);
                    BiomeTuning tuning = settings.GetBiomeTuning(biome);
                    float density = tuning.grassDensity * settings.globalDressingDensity;
                    float chance = SeedNoise.Hash01(settings.seed + 1031, x, z);

                    if (height <= settings.waterLevel + 0.35f || chance > density || world.GetSlopeAt(px, pz) > 0.62f)
                        continue;

                    float scale = Mathf.Lerp(0.35f, 0.82f, SeedNoise.Hash01(settings.seed + 1201, x, z));
                    float rotation = SeedNoise.Hash01(settings.seed + 1301, x, z) * 360f;
                    Color grassColor = tuning.grassTint;
                    AddGrassBlade(vertices, normals, uvs, colors, triangles, new Vector3(px, height, pz), scale, rotation, grassColor);
                }
            }

            return MakeMesh("SR_GrassMesh", vertices, triangles, uvs, colors, normals);
        }

        public static void BuildEnvironment(WorldGenerator world, WorldGenerationSettings settings, out Mesh trunks, out Mesh foliage, out Mesh rocks)
        {
            List<Vector3> trunkVertices = new List<Vector3>();
            List<Vector3> foliageVertices = new List<Vector3>();
            List<Vector3> rockVertices = new List<Vector3>();
            List<Vector3> trunkNormals = new List<Vector3>();
            List<Vector3> foliageNormals = new List<Vector3>();
            List<Vector3> rockNormals = new List<Vector3>();
            List<Color> trunkColors = new List<Color>();
            List<Color> foliageColors = new List<Color>();
            List<Color> rockColors = new List<Color>();
            List<int> trunkTriangles = new List<int>();
            List<int> foliageTriangles = new List<int>();
            List<int> rockTriangles = new List<int>();

            float spacing = 6.3f;
            int cells = Mathf.CeilToInt(settings.worldSize / spacing);
            float half = settings.worldSize * 0.5f;
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float px = Mathf.Lerp(-half, half, (x + SeedNoise.Hash01(settings.seed + 2003, x, z)) / cells);
                    float pz = Mathf.Lerp(-half, half, (z + SeedNoise.Hash01(settings.seed + 2017, x, z)) / cells);
                    float y = world.GetHeightAt(px, pz);
                    if (y <= settings.waterLevel + 0.45f || world.GetSlopeAt(px, pz) > 0.72f)
                        continue;

                    SeedAndRockBiome biome = world.GetBiomeAt(px, pz);
                    BiomeTuning tuning = settings.GetBiomeTuning(biome);
                    float choice = SeedNoise.Hash01(settings.seed + 2111, x, z);
                    float scale = Mathf.Lerp(0.75f, 1.45f, SeedNoise.Hash01(settings.seed + 2203, x, z));

                    if (choice < tuning.treeDensity * settings.globalDressingDensity)
                    {
                        Vector3 position = new Vector3(px, y, pz);
                        AppendCylinder(trunkVertices, trunkNormals, trunkColors, trunkTriangles, position, 0.16f * scale, 1.65f * scale, 5, new Color(0.34f, 0.18f, 0.08f));
                        AppendCone(foliageVertices, foliageNormals, foliageColors, foliageTriangles, position + Vector3.up * (1.05f * scale), 1.15f * scale, 2.45f * scale, 6, tuning.grassTint);
                        AppendCone(foliageVertices, foliageNormals, foliageColors, foliageTriangles, position + Vector3.up * (2.10f * scale), 0.82f * scale, 1.85f * scale, 6, Color.Lerp(tuning.grassTint, new Color(0.72f, 0.84f, 0.30f), 0.18f));
                    }
                    else if (choice < tuning.treeDensity + tuning.rockDensity)
                    {
                        AppendRock(rockVertices, rockNormals, rockColors, rockTriangles, new Vector3(px, y, pz), Mathf.Lerp(0.45f, 1.4f, scale / 1.45f), SeedNoise.Hash01(settings.seed + 2309, x, z));
                    }
                }
            }

            trunks = MakeMesh("SR_TreeTrunksMesh", trunkVertices, trunkTriangles, null, trunkColors, trunkNormals);
            foliage = MakeMesh("SR_TreeFoliageMesh", foliageVertices, foliageTriangles, null, foliageColors, foliageNormals);
            rocks = MakeMesh("SR_RocksMesh", rockVertices, rockTriangles, null, rockColors, rockNormals);
        }

        private static void AddGrassBlade(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> triangles, Vector3 position, float scale, float rotation, Color color)
        {
            float height = scale * 1.10f;
            float width = scale * 0.11f;
            AddGrassQuad(vertices, normals, uvs, colors, triangles, position, height, width, rotation, color);
            AddGrassQuad(vertices, normals, uvs, colors, triangles, position, height, width * 0.84f, rotation + 75f, Color.Lerp(color, Color.white, 0.08f));
        }

        private static void AddGrassQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> triangles, Vector3 position, float height, float width, float rotation, Color color)
        {
            int index = vertices.Count;
            Vector3 right = Quaternion.Euler(0f, rotation, 0f) * Vector3.right * width;
            Vector3 normal = Vector3.Cross(Vector3.up, right).normalized;
            vertices.Add(position - right);
            vertices.Add(position + right);
            vertices.Add(position + right + Vector3.up * height);
            vertices.Add(position - right + Vector3.up * height);
            normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            colors.Add(color); colors.Add(color); colors.Add(Color.Lerp(color, Color.white, 0.15f)); colors.Add(Color.Lerp(color, Color.white, 0.15f));
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
            triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 2);
        }

        private static void AppendCylinder(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles, Vector3 center, float radius, float height, int sides, Color color)
        {
            int start = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = Mathf.PI * 2f * i / sides;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(center + radial * radius);
                vertices.Add(center + radial * radius + Vector3.up * height);
                normals.Add(radial); normals.Add(radial);
                colors.Add(color); colors.Add(color);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int a = start + i * 2;
                int b = start + next * 2;
                triangles.Add(a); triangles.Add(b + 1); triangles.Add(a + 1);
                triangles.Add(a); triangles.Add(b); triangles.Add(b + 1);
            }
        }

        private static void AppendCone(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles, Vector3 center, float radius, float height, int sides, Color color)
        {
            int start = vertices.Count;
            Vector3 tip = center + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float angle = Mathf.PI * 2f * i / sides;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(center + radial * radius);
                normals.Add((radial + Vector3.up * 0.35f).normalized);
                colors.Add(color);
            }
            vertices.Add(tip);
            normals.Add(Vector3.up);
            colors.Add(Color.Lerp(color, Color.white, 0.12f));
            int tipIndex = vertices.Count - 1;

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                triangles.Add(start + i); triangles.Add(tipIndex); triangles.Add(start + next);
            }
        }

        private static void AppendRock(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles, Vector3 center, float scale, float random)
        {
            int start = vertices.Count;
            Color rockColor = Color.Lerp(new Color(0.23f, 0.24f, 0.25f), new Color(0.48f, 0.42f, 0.34f), random);
            Vector3[] points =
            {
                center + new Vector3(-scale, 0f, -scale * 0.6f),
                center + new Vector3(scale, 0f, -scale * 0.5f),
                center + new Vector3(scale * 0.72f, 0f, scale),
                center + new Vector3(-scale * 0.78f, 0f, scale * 0.8f),
                center + new Vector3(0f, scale * (0.9f + random * 0.7f), 0f),
                center + new Vector3(0f, -scale * 0.15f, 0f)
            };
            for (int i = 0; i < points.Length; i++)
            {
                vertices.Add(points[i]);
                normals.Add((points[i] - center).normalized);
                colors.Add(rockColor);
            }
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                triangles.Add(start + i); triangles.Add(start + 4); triangles.Add(start + next);
                triangles.Add(start + next); triangles.Add(start + 5); triangles.Add(start + i);
            }
        }

        private static Mesh MakeMesh(string meshName, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Color> colors, List<Vector3> normals = null)
        {
            Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (uvs != null && uvs.Count == vertices.Count) mesh.SetUVs(0, uvs);
            if (colors != null && colors.Count == vertices.Count) mesh.SetColors(colors);
            if (normals != null && normals.Count == vertices.Count) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
