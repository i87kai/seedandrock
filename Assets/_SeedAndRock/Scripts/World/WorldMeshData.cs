using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SeedAndRock.World
{
    /// <summary>
    /// Plain mesh buffers that can be filled on a worker thread. Only <see cref="ToMesh"/> touches the
    /// engine and must run on the main thread.
    /// </summary>
    public sealed class MeshData
    {
        public readonly string Name;
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<Vector2> Uv0 = new List<Vector2>();
        public readonly List<Vector4> Uv1 = new List<Vector4>();
        public readonly List<int> Triangles = new List<int>();

        public MeshData(string name)
        {
            Name = name;
        }

        public int VertexCount => Vertices.Count;
        public int TriangleCount => Triangles.Count / 3;
        public bool IsEmpty => Vertices.Count == 0;

        public void Reserve(int vertexCount, int indexCount)
        {
            Vertices.Capacity = Mathf.Max(Vertices.Capacity, vertexCount);
            Normals.Capacity = Mathf.Max(Normals.Capacity, vertexCount);
            Colors.Capacity = Mathf.Max(Colors.Capacity, vertexCount);
            Triangles.Capacity = Mathf.Max(Triangles.Capacity, indexCount);
        }

        /// <summary>Adds a flat-shaded triangle (counter-clockwise when viewed from outside).</summary>
        public void AddFlatTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            float magnitude = normal.magnitude;
            normal = magnitude > 1e-6f ? normal / magnitude : Vector3.up;
            int index = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
            Colors.Add(color); Colors.Add(color); Colors.Add(color);
            Triangles.Add(index); Triangles.Add(index + 1); Triangles.Add(index + 2);
        }

        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh { name = Name };
            mesh.indexFormat = Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            if (Normals.Count == Vertices.Count) mesh.SetNormals(Normals);
            if (Colors.Count == Vertices.Count) mesh.SetColors(Colors);
            if (Uv0.Count == Vertices.Count) mesh.SetUVs(0, Uv0);
            if (Uv1.Count == Vertices.Count) mesh.SetUVs(1, Uv1);
            mesh.SetTriangles(Triangles, 0, true);
            if (Normals.Count != Vertices.Count) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Square chunk grid shared by terrain, water and prop meshes so culling granularity matches.</summary>
    public readonly struct ChunkGrid
    {
        public readonly int Count;
        public readonly float WorldSize;
        public readonly float ChunkSize;

        public ChunkGrid(int count, float worldSize)
        {
            Count = Mathf.Max(1, count);
            WorldSize = worldSize;
            ChunkSize = worldSize / Count;
        }

        public int Total => Count * Count;

        public int IndexOf(float x, float z)
        {
            float half = WorldSize * 0.5f;
            int cx = Mathf.Clamp(Mathf.FloorToInt((x + half) / ChunkSize), 0, Count - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((z + half) / ChunkSize), 0, Count - 1);
            return cz * Count + cx;
        }

        public string NameFor(string prefix, int index) => prefix + "_" + (index % Count) + "_" + (index / Count);
    }
}
