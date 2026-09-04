using System.Collections.Generic;
using UnityEngine;

namespace SeedAndRock.World
{
    /// <summary>A deterministic descending watercourse sampled as a smooth ribbon.</summary>
    public sealed class RiverPathData
    {
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<float> widths = new List<float>();
        public readonly List<float> surfaces = new List<float>();
    }

    /// <summary>A lake basin connected to a river path.</summary>
    public sealed class LakeData
    {
        public Vector2 center;
        public float radiusX;
        public float radiusZ;
        public float surface;
        public float rotation;
    }
}
