using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cozy.Rendering
{
    /// <summary>
    /// Builds a tiny, throw-away visual test area (rolling ground, pond, a few
    /// stylized trees, rocks and grass patches) so every Cozy shader can be
    /// evaluated in one view. Meshes are generated in memory and never saved;
    /// this is a rendering test rig, NOT a world generator (MapMagic 2 owns the
    /// real world). Delete the object when you no longer need the showcase.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Cozy Rendering/Cozy Showcase Props (test rig)")]
    public sealed class CozyShowcaseProps : MonoBehaviour
    {
        [Header("Materials")]
        public Material groundMaterial;   // Cozy/Terrain Mesh
        public Material waterMaterial;    // Cozy/Water
        public Material trunkMaterial;    // Cozy/Lit (wind: object)
        public Material canopyMaterial;   // Cozy/Foliage (wind: object)
        public Material rockMaterial;     // Cozy/Lit
        public Material grassMaterial;    // Cozy/Grass (wind: vertex)

        [Header("Layout")]
        [Range(20f, 200f)] public float size = 90f;
        [Range(0f, 12f)] public float hillHeight = 6f;
        public float waterLevel = 0.6f;
        [Range(0, 40)] public int treeCount = 18;
        [Range(0, 40)] public int rockCount = 14;
        [Range(0, 6000)] public int grassBlades = 3000;
        public int seed = 7;

        private Transform root;
        private readonly List<Mesh> meshes = new List<Mesh>();

        private void OnEnable() => Rebuild();
        private void OnDisable() => Clear();
        private void OnValidate()
        {
#if UNITY_EDITOR
            // Object creation is not allowed inside OnValidate; defer one editor tick.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
#endif
        }

        public void Rebuild()
        {
            Clear();
            root = new GameObject("Showcase (generated, not saved)").transform;
            root.SetParent(transform, false);
            root.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            var rng = new System.Random(seed);
            BuildGround();
            BuildWater();
            for (int i = 0; i < treeCount; i++) PlaceTree(rng);
            for (int i = 0; i < rockCount; i++) PlaceRock(rng);
            BuildGrass(rng);
        }

        private void Clear()
        {
            if (root != null)
            {
                if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
                root = null;
            }
            // Also catch a leftover from a previous domain reload.
            var stale = transform.Find("Showcase (generated, not saved)");
            if (stale != null) { if (Application.isPlaying) Destroy(stale.gameObject); else DestroyImmediate(stale.gameObject); }
            foreach (var m in meshes) if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
            meshes.Clear();
        }

        // ------------------------------------------------------------------
        private float Height(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.035f + 13.1f, z * 0.035f + 7.7f);
            float n2 = Mathf.PerlinNoise(x * 0.11f, z * 0.11f) * 0.25f;
            // A shallow bowl in the middle for the pond.
            float bowl = Mathf.Clamp01(1f - new Vector2(x, z).magnitude / (size * 0.22f));
            return (n + n2) * hillHeight - bowl * bowl * (hillHeight * 0.9f + 2f);
        }

        private void BuildGround()
        {
            int res = 96;
            var verts = new Vector3[(res + 1) * (res + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[res * res * 6];
            for (int z = 0; z <= res; z++)
                for (int x = 0; x <= res; x++)
                {
                    float fx = (x / (float)res - 0.5f) * size;
                    float fz = (z / (float)res - 0.5f) * size;
                    float h = Height(fx, fz);
                    int i = z * (res + 1) + x;
                    verts[i] = new Vector3(fx, h, fz);
                    uvs[i] = new Vector2(x / (float)res, z / (float)res);
                    float elev = Mathf.InverseLerp(-hillHeight, hillHeight * 1.2f, h);
                    Color meadow = new Color(0.55f, 0.78f, 0.36f);
                    Color forest = new Color(0.32f, 0.58f, 0.30f);
                    float biome = Mathf.PerlinNoise(fx * 0.02f + 40f, fz * 0.02f);
                    cols[i] = Color.Lerp(meadow, forest, biome);
                    cols[i].a = elev;
                }
            int t = 0;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int i = z * (res + 1) + x;
                    tris[t++] = i; tris[t++] = i + res + 1; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + res + 1; tris[t++] = i + res + 2;
                }
            var mesh = NewMesh("Ground");
            mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = Spawn("Ground", mesh, groundMaterial, Vector3.zero, Quaternion.identity, Vector3.one);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private void BuildWater()
        {
            // Subdivided so the vertex swells have something to move.
            int res = 24; float s = size * 0.5f;
            var verts = new Vector3[(res + 1) * (res + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[res * res * 6];
            for (int z = 0; z <= res; z++)
                for (int x = 0; x <= res; x++)
                {
                    int i = z * (res + 1) + x;
                    verts[i] = new Vector3((x / (float)res - 0.5f) * s, 0f, (z / (float)res - 0.5f) * s);
                    uvs[i] = new Vector2(x / (float)res, z / (float)res);
                }
            int t = 0;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int i = z * (res + 1) + x;
                    tris[t++] = i; tris[t++] = i + res + 1; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + res + 1; tris[t++] = i + res + 2;
                }
            var mesh = NewMesh("Water");
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = Spawn("Water", mesh, waterMaterial, new Vector3(0f, waterLevel, 0f), Quaternion.identity, Vector3.one);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private bool RandomLandPoint(System.Random rng, float minHeightAboveWater, out Vector3 p)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * size * 0.9f;
                float z = ((float)rng.NextDouble() - 0.5f) * size * 0.9f;
                float h = Height(x, z);
                if (h > waterLevel + minHeightAboveWater) { p = new Vector3(x, h, z); return true; }
            }
            p = Vector3.zero; return false;
        }

        private void PlaceTree(System.Random rng)
        {
            if (!RandomLandPoint(rng, 0.8f, out var pos)) return;
            float scale = Mathf.Lerp(0.8f, 1.5f, (float)rng.NextDouble());
            var tree = new GameObject("Tree");
            tree.transform.SetParent(root, false);
            tree.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            tree.transform.localScale = Vector3.one * scale;
            tree.hideFlags = HideFlags.DontSave;

            // Trunk: tapered cylinder, pivot at the base (object-pivot wind).
            var trunk = NewMesh("Trunk");
            BuildCylinder(trunk, 0.32f, 0.18f, 3.2f, 8);
            Spawn("Trunk", trunk, trunkMaterial, Vector3.zero, Quaternion.identity, Vector3.one, tree.transform);

            // Canopy: three offset spheres sharing the tree pivot so bending matches the trunk.
            var canopy = NewMesh("Canopy");
            var parts = new List<CombineInstance>();
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            void Blob(Vector3 c, float r) => parts.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(c, Quaternion.identity, Vector3.one * r * 2f) });
            Blob(new Vector3(0f, 4.2f, 0f), 1.9f);
            Blob(new Vector3(0.9f, 3.4f, 0.4f), 1.4f);
            Blob(new Vector3(-0.8f, 3.6f, -0.6f), 1.5f);
            Blob(new Vector3(0.1f, 5.4f, 0.3f), 1.2f);
            canopy.CombineMeshes(parts.ToArray(), true, true);
            canopy.RecalculateBounds();
            Spawn("Canopy", canopy, canopyMaterial, Vector3.zero, Quaternion.identity, Vector3.one, tree.transform);
        }

        private void PlaceRock(System.Random rng)
        {
            if (!RandomLandPoint(rng, -0.4f, out var pos)) return;
            var mesh = NewMesh("Rock");
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var v = sphere.vertices;
            var n = sphere.normals;
            float bump = 0.18f + (float)rng.NextDouble() * 0.15f;
            float ox = (float)rng.NextDouble() * 50f;
            for (int i = 0; i < v.Length; i++)
            {
                float d = Mathf.PerlinNoise(v[i].x * 2.2f + ox, v[i].z * 2.2f + v[i].y * 1.7f) - 0.5f;
                v[i] += n[i] * d * bump;
                v[i].y *= 0.7f;
            }
            mesh.vertices = v; mesh.triangles = sphere.triangles; mesh.uv = sphere.uv;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            float s = Mathf.Lerp(0.6f, 2.4f, (float)rng.NextDouble());
            Spawn("Rock", mesh, rockMaterial, pos - Vector3.up * s * 0.15f,
                Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(s, s * 0.8f, s * 1.2f));
        }

        private void BuildGrass(System.Random rng)
        {
            // Batched quads using the Cozy/Grass VERTEX wind contract:
            // UV0 = quad uv (y: root..tip), UV1 = (random, blade height), COLOR = tint.
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uv0 = new List<Vector2>(); var uv1 = new List<Vector2>();
            var cols = new List<Color>(); var tris = new List<int>();
            for (int i = 0; i < grassBlades; i++)
            {
                if (!RandomLandPoint(rng, 0.15f, out var p)) continue;
                float rnd = (float)rng.NextDouble();
                float h = Mathf.Lerp(0.5f, 1.1f, (float)rng.NextDouble());
                float w = 0.22f;
                float ang = (float)rng.NextDouble() * Mathf.PI;
                var right = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * w * 0.5f;
                var normal = Vector3.Cross(right.normalized, Vector3.up);
                var tint = Color.Lerp(new Color(0.55f, 0.85f, 0.35f), new Color(0.35f, 0.7f, 0.3f), rnd);
                int b = verts.Count;
                verts.Add(p - right); verts.Add(p + right); verts.Add(p + right + Vector3.up * h); verts.Add(p - right + Vector3.up * h);
                uv0.Add(new Vector2(0, 0)); uv0.Add(new Vector2(1, 0)); uv0.Add(new Vector2(1, 1)); uv0.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) { uv1.Add(new Vector2(rnd, h)); cols.Add(tint); norms.Add(normal); }
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            if (verts.Count == 0) return;
            var mesh = NewMesh("Grass");
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
            var go = Spawn("Grass", mesh, grassMaterial, Vector3.zero, Quaternion.identity, Vector3.one);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------
        private static void BuildCylinder(Mesh mesh, float radiusBottom, float radiusTop, float height, int segments)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir * radiusBottom); norms.Add(dir); uvs.Add(new Vector2(i / (float)segments, 0f));
                verts.Add(dir * radiusTop + Vector3.up * height); norms.Add(dir); uvs.Add(new Vector2(i / (float)segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        private Mesh NewMesh(string name)
        {
            var m = new Mesh { name = "CozyShowcase_" + name, hideFlags = HideFlags.DontSave };
            meshes.Add(m);
            return m;
        }

        private GameObject Spawn(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent = null)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }
    }
}
