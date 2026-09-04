using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SeedAndRock.World
{
    /// <summary>Result of batching placements into chunked meshes.</summary>
    public sealed class PropMeshSet
    {
        public MeshData[] Trunks;
        public MeshData[] Foliage;
        public MeshData[] Rocks;
        public MeshData[] Grass;
    }

    /// <summary>
    /// Turns deterministic placements into batched, flat-shaded low-poly geometry: layered conifers,
    /// blob-canopy broadleaves, dry shrubs, jittered boulders and crossed grass blades. All variation is
    /// derived from the placement's own hash values so the same seed always builds the same meshes.
    /// </summary>
    public static class PropMeshBuilder
    {
        private static readonly Vector3[] OctahedronVertices =
        {
            Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back
        };

        private static readonly int[] OctahedronFaces =
        {
            0, 4, 2, 0, 2, 5, 0, 5, 3, 0, 3, 4,
            1, 2, 4, 1, 5, 2, 1, 3, 5, 1, 4, 3
        };

        private static readonly Vector3[] IcosahedronVertices = BuildIcosahedron();
        private static readonly int[] IcosahedronFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        public static PropMeshSet Build(PlacementResult placement, WorldGenerationSettings palette, ChunkGrid chunks, CancellationToken token)
        {
            PropMeshSet set = new PropMeshSet
            {
                Trunks = CreateChunkArray("SR_TreeTrunks", chunks),
                Foliage = CreateChunkArray("SR_TreeFoliage", chunks),
                Rocks = CreateChunkArray("SR_Rocks", chunks),
                Grass = CreateChunkArray("SR_Grass", chunks)
            };

            for (int i = 0; i < placement.Trees.Count; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                PlacementInstance tree = placement.Trees[i];
                int chunk = chunks.IndexOf(tree.x, tree.z);
                AppendTree(set.Trunks[chunk], set.Foliage[chunk], in tree, palette);
            }

            for (int i = 0; i < placement.Rocks.Count; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                PlacementInstance rock = placement.Rocks[i];
                AppendRock(set.Rocks[chunks.IndexOf(rock.x, rock.z)], in rock);
            }

            for (int i = 0; i < placement.Grass.Count; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                PlacementInstance blade = placement.Grass[i];
                AppendGrass(set.Grass[chunks.IndexOf(blade.x, blade.z)], in blade, palette);
            }

            return set;
        }

        private static MeshData[] CreateChunkArray(string prefix, ChunkGrid chunks)
        {
            MeshData[] result = new MeshData[chunks.Total];
            for (int i = 0; i < result.Length; i++) result[i] = new MeshData(chunks.NameFor(prefix, i));
            return result;
        }

        // ------------------------------------------------------------------ trees

        private static void AppendTree(MeshData trunks, MeshData foliage, in PlacementInstance p, WorldGenerationSettings palette)
        {
            Vector3 origin = new Vector3(p.x, p.y, p.z);
            YawRotation yaw = new YawRotation(p.rotationDegrees);
            float s = p.scale;
            float v = p.variation;
            Color trunkColor = Color.Lerp(new Color(0.33f, 0.20f, 0.10f), new Color(0.45f, 0.33f, 0.20f), v);
            Color leaf = palette.GetBiomeTuning(p.biome).grassTint;

            switch (p.variant)
            {
                case 0:
                {
                    Color dark = Color.Lerp(new Color(0.11f, 0.30f, 0.17f), new Color(0.16f, 0.36f, 0.16f), v);
                    Color light = Color.Lerp(dark, new Color(0.34f, 0.52f, 0.24f), 0.55f);
                    float trunkHeight = 1.35f * s;
                    AppendTaperedCylinder(trunks, origin, yaw, 0.15f * s, 0.07f * s, trunkHeight * 1.5f, 5, trunkColor, v);
                    int layers = v > 0.62f ? 4 : 3;
                    for (int i = 0; i < layers; i++)
                    {
                        float t = i / (float)layers;
                        float y = trunkHeight * 0.45f + i * 0.92f * s * Mathf.Lerp(1f, 0.82f, t);
                        float radius = Mathf.Lerp(1.35f, 0.45f, t) * s * Mathf.Lerp(0.85f, 1.15f, Frac(v * 7.3f + i * 0.37f));
                        float height = Mathf.Lerp(1.7f, 1.25f, t) * s;
                        Color color = Color.Lerp(dark, light, t);
                        AppendCone(foliage, origin + Vector3.up * y, yaw, radius, height, 7, color, v + i * 0.19f, p.snow);
                    }
                    break;
                }
                case 2:
                {
                    Color dry = Color.Lerp(new Color(0.42f, 0.44f, 0.22f), new Color(0.55f, 0.47f, 0.24f), v);
                    AppendTaperedCylinder(trunks, origin, yaw, 0.08f * s, 0.04f * s, 0.7f * s, 4, trunkColor, v);
                    AppendBlob(foliage, origin + yaw * new Vector3(0.1f * s, 0.75f * s, 0f), 0.55f * s, new Vector3(1.15f, 0.7f, 1f), 0, dry, v, 0f);
                    AppendBlob(foliage, origin + yaw * new Vector3(-0.35f * s, 0.55f * s, 0.25f * s), 0.42f * s, new Vector3(1f, 0.75f, 1.1f), 0, dry * 0.92f, v + 0.5f, 0f);
                    break;
                }
                default:
                {
                    Color canopy = Color.Lerp(leaf, new Color(0.30f, 0.50f, 0.20f), 0.35f);
                    canopy = Color.Lerp(canopy, new Color(0.62f, 0.62f, 0.26f), (1f - p.moisture) * 0.35f);
                    canopy = Color.Lerp(canopy, canopy * 1.18f, v * 0.5f);
                    float trunkHeight = 1.9f * s;
                    Vector3 lean = yaw * new Vector3(0.12f * s * (v - 0.5f), 0f, 0.08f * s * (Frac(v * 3.1f) - 0.5f));
                    AppendTaperedCylinder(trunks, origin, yaw, 0.19f * s, 0.11f * s, trunkHeight + 0.6f * s, 6, trunkColor, v, lean);
                    Vector3 crown = origin + lean * 0.8f + Vector3.up * (trunkHeight + 0.55f * s);
                    AppendBlob(foliage, crown, 1.35f * s, new Vector3(1f, 0.78f, 1f), 1, canopy, v, p.snow);
                    AppendBlob(foliage, crown + yaw * new Vector3(0.62f * s, 0.32f * s, 0.28f * s), 0.85f * s, new Vector3(1f, 0.85f, 1f), 0, canopy * 1.06f, v + 0.33f, p.snow);
                    AppendBlob(foliage, crown + yaw * new Vector3(-0.55f * s, -0.15f * s, -0.4f * s), 0.7f * s, new Vector3(1.1f, 0.8f, 1f), 0, canopy * 0.94f, v + 0.66f, p.snow);
                    break;
                }
            }
        }

        private static void AppendTaperedCylinder(MeshData data, Vector3 origin, YawRotation yaw, float radiusBottom, float radiusTop, float height, int sides, Color color, float variation, Vector3 lean = default)
        {
            Vector3 top = origin + Vector3.up * height + lean;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides;
                float a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 r0 = yaw * new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 r1 = yaw * new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                float bulge0 = 1f + (Frac(variation * 11.7f + i * 0.61f) - 0.5f) * 0.25f;
                float bulge1 = 1f + (Frac(variation * 11.7f + (i + 1) % sides * 0.61f) - 0.5f) * 0.25f;
                Vector3 b0 = origin + r0 * radiusBottom * bulge0 - Vector3.up * 0.15f;
                Vector3 b1 = origin + r1 * radiusBottom * bulge1 - Vector3.up * 0.15f;
                Vector3 t0 = top + r0 * radiusTop;
                Vector3 t1 = top + r1 * radiusTop;
                Color shade = color * Mathf.Lerp(0.85f, 1.1f, Frac(variation * 5.3f + i * 0.29f));
                shade.a = 1f;
                data.AddFlatTriangle(b0, t0, t1, shade);
                data.AddFlatTriangle(b0, t1, b1, shade);
            }
        }

        private static void AppendCone(MeshData data, Vector3 center, YawRotation yaw, float radius, float height, int sides, Color color, float variation, float snow)
        {
            Vector3 tip = center + Vector3.up * height + yaw * new Vector3((Frac(variation * 4.7f) - 0.5f) * 0.12f * radius, 0f, (Frac(variation * 9.1f) - 0.5f) * 0.12f * radius);
            Color snowColor = new Color(0.90f, 0.94f, 0.97f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides;
                float a1 = Mathf.PI * 2f * (i + 1) / sides;
                float r0 = radius * (0.82f + Frac(variation * 13.1f + i * 0.47f) * 0.36f);
                float r1 = radius * (0.82f + Frac(variation * 13.1f + ((i + 1) % sides) * 0.47f) * 0.36f);
                float droop0 = Frac(variation * 6.3f + i * 0.71f) * 0.18f * radius;
                float droop1 = Frac(variation * 6.3f + ((i + 1) % sides) * 0.71f) * 0.18f * radius;
                Vector3 p0 = center + yaw * new Vector3(Mathf.Cos(a0) * r0, -droop0, Mathf.Sin(a0) * r0);
                Vector3 p1 = center + yaw * new Vector3(Mathf.Cos(a1) * r1, -droop1, Mathf.Sin(a1) * r1);
                Color faceColor = color * Mathf.Lerp(0.9f, 1.08f, Frac(variation * 3.3f + i * 0.53f));
                faceColor.a = 1f;
                faceColor = Color.Lerp(faceColor, snowColor, snow * 0.7f);
                data.AddFlatTriangle(p0, tip, p1, faceColor);
                // Underside so the canopy reads as solid from below.
                data.AddFlatTriangle(p1, center - Vector3.up * 0.05f * radius, p0, color * 0.7f);
            }
        }

        /// <summary>Flat-shaded jittered octahedron (subdivided once for level 1) used for canopies and shrubs.</summary>
        private static void AppendBlob(MeshData data, Vector3 center, float radius, Vector3 squash, int subdivisions, Color color, float variation, float snow)
        {
            List<Vector3> verts = new List<Vector3>(OctahedronVertices);
            List<int> faces = new List<int>(OctahedronFaces);
            for (int s = 0; s < subdivisions; s++) Subdivide(verts, faces);

            Color snowColor = new Color(0.90f, 0.94f, 0.97f);
            for (int f = 0; f < faces.Count; f += 3)
            {
                Vector3 a = Jitter(verts[faces[f]], radius, squash, variation);
                Vector3 b = Jitter(verts[faces[f + 1]], radius, squash, variation);
                Vector3 c = Jitter(verts[faces[f + 2]], radius, squash, variation);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                float facing = Mathf.Clamp01(normal.y * 0.5f + 0.5f);
                Color faceColor = Color.Lerp(color * 0.72f, color * 1.1f, facing);
                faceColor.a = 1f;
                faceColor = Color.Lerp(faceColor, snowColor, snow * Mathf.SmoothStep(0.2f, 0.8f, normal.y));
                data.AddFlatTriangle(center + a, center + b, center + c, faceColor);
            }
        }

        private static Vector3 Jitter(Vector3 direction, float radius, Vector3 squash, float variation)
        {
            float h = Frac(Mathf.Sin(Vector3.Dot(direction, new Vector3(12.9898f, 78.233f, 37.719f)) + variation * 91.7f) * 43758.5453f);
            float r = radius * Mathf.Lerp(0.8f, 1.16f, h);
            return Vector3.Scale(direction * r, squash);
        }

        private static void Subdivide(List<Vector3> verts, List<int> faces)
        {
            Dictionary<long, int> midpoints = new Dictionary<long, int>();
            List<int> result = new List<int>(faces.Count * 4);
            for (int f = 0; f < faces.Count; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                result.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            faces.Clear();
            faces.AddRange(result);

            int Midpoint(int i, int j)
            {
                long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
                if (midpoints.TryGetValue(key, out int index)) return index;
                verts.Add(((verts[i] + verts[j]) * 0.5f).normalized);
                index = verts.Count - 1;
                midpoints[key] = index;
                return index;
            }
        }

        // ------------------------------------------------------------------ rocks

        private static void AppendRock(MeshData data, in PlacementInstance p)
        {
            Vector3 origin = new Vector3(p.x, p.y, p.z);
            YawRotation rotation = new YawRotation(p.rotationDegrees);
            float s = p.scale;
            Color baseColor = Color.Lerp(new Color(0.36f, 0.37f, 0.38f), new Color(0.50f, 0.44f, 0.36f), p.variation);
            switch (p.variant)
            {
                case 1:
                    AppendBoulder(data, origin - Vector3.up * 0.25f * s, rotation, s, new Vector3(1.6f, 0.55f, 1.05f), baseColor, p);
                    break;
                case 2:
                    AppendBoulder(data, origin - Vector3.up * 0.2f * s, rotation, s * 0.8f, new Vector3(1f, 0.85f, 1f), baseColor, p);
                    AppendBoulder(data, origin + rotation * new Vector3(0.75f * s, -0.15f * s, 0.2f * s), rotation, s * 0.5f, new Vector3(1.1f, 0.7f, 1f), baseColor * 0.95f, p);
                    AppendBoulder(data, origin + rotation * new Vector3(-0.55f * s, -0.12f * s, -0.5f * s), rotation, s * 0.38f, new Vector3(1f, 0.8f, 1.2f), baseColor * 1.05f, p);
                    break;
                default:
                    AppendBoulder(data, origin - Vector3.up * 0.22f * s, rotation, s, new Vector3(1.05f, 0.85f, 1f), baseColor, p);
                    break;
            }
        }

        private static void AppendBoulder(MeshData data, Vector3 center, YawRotation rotation, float radius, Vector3 squash, Color color, in PlacementInstance p)
        {
            Color moss = new Color(0.30f, 0.45f, 0.22f);
            Color snowColor = new Color(0.90f, 0.94f, 0.97f);
            for (int f = 0; f < IcosahedronFaces.Length; f += 3)
            {
                Vector3 a = rotation * Jitter(IcosahedronVertices[IcosahedronFaces[f]], radius, squash, p.variation + 0.11f);
                Vector3 b = rotation * Jitter(IcosahedronVertices[IcosahedronFaces[f + 1]], radius, squash, p.variation + 0.11f);
                Vector3 c = rotation * Jitter(IcosahedronVertices[IcosahedronFaces[f + 2]], radius, squash, p.variation + 0.11f);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                float up = Mathf.Clamp01(normal.y);
                Color faceColor = color * Mathf.Lerp(0.82f, 1.08f, Frac(p.variation * 17.3f + f * 0.37f));
                faceColor.a = 1f;
                faceColor = Color.Lerp(faceColor, moss, up * p.moisture * 0.55f * (1f - p.snow));
                faceColor = Color.Lerp(faceColor, snowColor, p.snow * Mathf.SmoothStep(0.3f, 0.9f, up));
                data.AddFlatTriangle(center + a, center + b, center + c, faceColor);
            }
        }

        // ------------------------------------------------------------------ grass

        private static void AppendGrass(MeshData data, in PlacementInstance p, WorldGenerationSettings palette)
        {
            Vector3 origin = new Vector3(p.x, p.y - 0.03f, p.z);
            Color tint = palette.GetBiomeTuning(p.biome).grassTint;
            tint = Color.Lerp(tint, new Color(0.66f, 0.66f, 0.30f), (1f - p.moisture) * 0.4f);
            tint = Color.Lerp(tint, tint * 1.15f, p.variation * 0.5f);
            tint.a = 1f;
            float height = p.scale * 1.05f;
            float width = p.scale * 0.16f;
            AppendGrassQuad(data, origin, height, width, p.rotationDegrees, tint);
            AppendGrassQuad(data, origin, height * 0.9f, width * 0.85f, p.rotationDegrees + 70f, tint * 0.94f);
        }

        private static void AppendGrassQuad(MeshData data, Vector3 position, float height, float width, float rotation, Color color)
        {
            int index = data.Vertices.Count;
            Vector3 right = new YawRotation(rotation) * Vector3.right * width;
            Vector3 normal = Vector3.up;
            Color tip = Color.Lerp(color, Color.white, 0.12f);
            tip.a = 1f;
            data.Vertices.Add(position - right);
            data.Vertices.Add(position + right);
            data.Vertices.Add(position + right * 0.35f + Vector3.up * height);
            data.Vertices.Add(position - right * 0.35f + Vector3.up * height);
            for (int i = 0; i < 4; i++) data.Normals.Add(normal);
            data.Uv0.Add(new Vector2(0f, 0f)); data.Uv0.Add(new Vector2(1f, 0f)); data.Uv0.Add(new Vector2(1f, 1f)); data.Uv0.Add(new Vector2(0f, 1f));
            data.Colors.Add(color); data.Colors.Add(color); data.Colors.Add(tip); data.Colors.Add(tip);
            data.Triangles.Add(index); data.Triangles.Add(index + 2); data.Triangles.Add(index + 1);
            data.Triangles.Add(index); data.Triangles.Add(index + 3); data.Triangles.Add(index + 2);
        }

        private static float Frac(float value) => value - Mathf.Floor(value);

        private static Vector3[] BuildIcosahedron()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] v =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }
    }

    /// <summary>Rotation about the world Y axis. Pure managed math so prop meshes can be built on worker threads.</summary>
    public readonly struct YawRotation
    {
        private readonly float sin;
        private readonly float cos;

        public YawRotation(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            sin = Mathf.Sin(radians);
            cos = Mathf.Cos(radians);
        }

        public static Vector3 operator *(YawRotation rotation, Vector3 v) =>
            new Vector3(v.x * rotation.cos + v.z * rotation.sin, v.y, -v.x * rotation.sin + v.z * rotation.cos);
    }
}
