using System;
using System.Collections.Generic;

namespace SeedAndRock.World
{
    /// <summary>Summary of a detected lake basin (or the sea) for diagnostics and placement rules.</summary>
    public struct LakeInfo
    {
        public float centerX;
        public float centerZ;
        public float surface;
        public int cellCount;
        public float maxDepth;
        public bool isSea;
    }

    /// <summary>
    /// Grid-based hydrology result. All arrays are row-major (z * Resolution + x) over a square grid that
    /// covers the whole world. Queries interpolate bilinearly so shorelines and river ribbons are smooth.
    /// </summary>
    public sealed class HydrologyField
    {
        public readonly int Resolution;
        public readonly float CellSize;
        public readonly float HalfSize;

        /// <summary>Terrain height before river carving, sampled at each grid node.</summary>
        public readonly float[] BaseHeight;
        /// <summary>Depression-filled surface used for flow routing.</summary>
        public readonly float[] Filled;
        /// <summary>1 where a lake or the sea covers the node, otherwise 0.</summary>
        public readonly float[] LakeMask;
        /// <summary>Lake/sea surface height; land nodes hold the surface of the nearest lake so water meshes can extend past the shore.</summary>
        public readonly float[] LakeSurface;
        /// <summary>0..1 river ribbon profile: 1 at the channel centre, 0 at the outer bank.</summary>
        public readonly float[] RiverStrength;
        /// <summary>Wider 0..1 falloff around rivers; wherever it is positive, RiverSurface/RiverBed hold the nearest river's values.</summary>
        public readonly float[] RiverProximity;
        /// <summary>Water surface of the river influencing the node.</summary>
        public readonly float[] RiverSurface;
        /// <summary>Channel bed height of the river influencing the node.</summary>
        public readonly float[] RiverBed;
        /// <summary>Metres to the nearest lake, sea or river node.</summary>
        public readonly float[] WaterDistance;
        /// <summary>Flow accumulation in cells (for diagnostics and moisture).</summary>
        public readonly float[] Accumulation;

        public readonly List<LakeInfo> Lakes = new List<LakeInfo>();
        public int RiverCellCount;

        public HydrologyField(int resolution, float worldSize)
        {
            Resolution = Math.Max(2, resolution);
            HalfSize = worldSize * 0.5f;
            CellSize = worldSize / (Resolution - 1);
            int count = Resolution * Resolution;
            BaseHeight = new float[count];
            Filled = new float[count];
            LakeMask = new float[count];
            LakeSurface = new float[count];
            RiverStrength = new float[count];
            RiverProximity = new float[count];
            RiverSurface = new float[count];
            RiverBed = new float[count];
            WaterDistance = new float[count];
            Accumulation = new float[count];
        }

        public float NodeX(int ix) => -HalfSize + ix * CellSize;
        public float NodeZ(int iz) => -HalfSize + iz * CellSize;

        public void ToGrid(float x, float z, out float gx, out float gz)
        {
            gx = (x + HalfSize) / CellSize;
            gz = (z + HalfSize) / CellSize;
        }

        public float Sample(float[] field, float x, float z)
        {
            ToGrid(x, z, out float gx, out float gz);
            return SRMath.SampleBilinear(field, Resolution, Resolution, gx, gz);
        }

        /// <summary>Samples a sparse river field using RiverProximity as interpolation weight.</summary>
        public float SampleRiver(float[] field, float x, float z, float fallback = 0f)
        {
            ToGrid(x, z, out float gx, out float gz);
            return SRMath.SampleBilinearWeighted(field, RiverProximity, Resolution, Resolution, gx, gz, fallback);
        }

        /// <summary>Fraction of nodes covered by lakes or sea.</summary>
        public float WaterCoverage()
        {
            float sum = 0f;
            for (int i = 0; i < LakeMask.Length; i++) sum += LakeMask[i];
            return sum / LakeMask.Length;
        }
    }

    /// <summary>
    /// Builds connected, terrain-driven hydrology: depression filling (priority flood), D8 flow routing,
    /// flow accumulation, river extraction with width, lake detection in basins, and distance fields.
    /// Everything is deterministic for a given terrain field and settings.
    /// </summary>
    public static class HydrologyBuilder
    {
        private const float FillEpsilon = 3e-5f;
        private static readonly int[] NeighbourDx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] NeighbourDz = { 0, 1, 1, 1, 0, -1, -1, -1 };

        public const int StepHeightsSampled = 1;
        public const int StepDepressionsFilled = 2;
        public const int StepFlowRouted = 3;
        public const int StepLakesDetected = 4;
        public const int StepRiversTraced = 5;
        public const int StepComplete = 6;

        /// <param name="onStep">Optional callback receiving the Step* constants as each phase finishes (may be called from a worker thread).</param>
        public static HydrologyField Build(WorldSettingsData settings, TerrainField terrain, Action<int> onStep = null)
        {
            HydrologyField field = new HydrologyField(settings.hydrologyResolution, settings.worldSize);
            SampleBaseHeights(field, terrain);
            onStep?.Invoke(StepHeightsSampled);
            FillDepressions(field, settings.waterLevel);
            onStep?.Invoke(StepDepressionsFilled);
            int[] flowTarget = ComputeFlow(field);
            onStep?.Invoke(StepFlowRouted);
            DetectLakes(field, settings);
            onStep?.Invoke(StepLakesDetected);
            TraceRivers(field, flowTarget, settings);
            onStep?.Invoke(StepRiversTraced);
            BuildDistanceFields(field);
            onStep?.Invoke(StepComplete);
            return field;
        }

        private static void SampleBaseHeights(HydrologyField field, TerrainField terrain)
        {
            int n = field.Resolution;
            for (int z = 0; z < n; z++)
            {
                float pz = field.NodeZ(z);
                for (int x = 0; x < n; x++)
                    field.BaseHeight[z * n + x] = terrain.BaseHeight(field.NodeX(x), pz);
            }
        }

        /// <summary>Priority-flood depression filling. Map edges and the global water level act as outlets.</summary>
        private static void FillDepressions(HydrologyField field, float waterLevel)
        {
            int n = field.Resolution;
            float[] h = field.BaseHeight;
            float[] filled = field.Filled;
            bool[] visited = new bool[n * n];
            MinHeap heap = new MinHeap(n * 4);

            for (int i = 0; i < n; i++)
            {
                PushBorder(i, 0);
                PushBorder(i, n - 1);
                PushBorder(0, i);
                PushBorder(n - 1, i);
            }

            void PushBorder(int x, int z)
            {
                int index = z * n + x;
                if (visited[index]) return;
                visited[index] = true;
                filled[index] = SRMath.Max(h[index], waterLevel);
                heap.Push(filled[index], index);
            }

            while (heap.Count > 0)
            {
                heap.Pop(out float key, out int index);
                int cx = index % n;
                int cz = index / n;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + NeighbourDx[d];
                    int nz = cz + NeighbourDz[d];
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int ni = nz * n + nx;
                    if (visited[ni]) continue;
                    visited[ni] = true;
                    filled[ni] = SRMath.Max(h[ni], key + FillEpsilon);
                    heap.Push(filled[ni], ni);
                }
            }
        }

        /// <summary>D8 steepest descent on the filled surface followed by accumulation in descending order.</summary>
        private static int[] ComputeFlow(HydrologyField field)
        {
            int n = field.Resolution;
            int count = n * n;
            float[] filled = field.Filled;
            int[] target = new int[count];
            float cell = field.CellSize;
            float diagonal = cell * 1.41421356f;

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int index = z * n + x;
                    float best = 0f;
                    int bestIndex = -1;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + NeighbourDx[d];
                        int nz = z + NeighbourDz[d];
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                        int ni = nz * n + nx;
                        float drop = (filled[index] - filled[ni]) / ((d & 1) == 1 ? diagonal : cell);
                        if (drop > best)
                        {
                            best = drop;
                            bestIndex = ni;
                        }
                    }

                    target[index] = bestIndex;
                }
            }

            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int compare = filled[b].CompareTo(filled[a]);
                return compare != 0 ? compare : a.CompareTo(b);
            });

            float[] accumulation = field.Accumulation;
            for (int i = 0; i < count; i++) accumulation[i] = 1f;
            for (int i = 0; i < count; i++)
            {
                int index = order[i];
                int down = target[index];
                if (down >= 0) accumulation[down] += accumulation[index];
            }

            return target;
        }

        /// <summary>Flooded connected components become lakes when they are deep and large enough; anything at the water level is sea.</summary>
        private static void DetectLakes(HydrologyField field, WorldSettingsData settings)
        {
            int n = field.Resolution;
            int count = n * n;
            float[] h = field.BaseHeight;
            float[] filled = field.Filled;
            int[] component = new int[count];
            for (int i = 0; i < count; i++) component[i] = -1;
            Stack<int> stack = new Stack<int>();
            List<int> members = new List<int>();
            float waterLevel = settings.waterLevel;
            int componentId = 0;

            for (int start = 0; start < count; start++)
            {
                if (component[start] >= 0 || filled[start] <= h[start] + 1e-4f) continue;

                members.Clear();
                stack.Push(start);
                component[start] = componentId;
                float maxDepth = 0f;
                float surface = float.MaxValue;
                double sumX = 0, sumZ = 0;
                while (stack.Count > 0)
                {
                    int index = stack.Pop();
                    members.Add(index);
                    maxDepth = SRMath.Max(maxDepth, filled[index] - h[index]);
                    surface = SRMath.Min(surface, filled[index]);
                    int cx = index % n;
                    int cz = index / n;
                    sumX += cx;
                    sumZ += cz;
                    for (int d = 0; d < 8; d += 2)
                    {
                        int nx = cx + NeighbourDx[d];
                        int nz = cz + NeighbourDz[d];
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                        int ni = nz * n + nx;
                        if (component[ni] >= 0 || filled[ni] <= h[ni] + 1e-4f) continue;
                        component[ni] = componentId;
                        stack.Push(ni);
                    }
                }

                componentId++;
                bool isSea = surface <= waterLevel + 0.02f;
                if (isSea) surface = waterLevel;
                bool keep = isSea || (maxDepth >= settings.lakeMinDepth && members.Count >= settings.lakeMinCells);
                if (!keep) continue;

                for (int i = 0; i < members.Count; i++)
                {
                    int index = members[i];
                    if (h[index] >= surface - 0.01f) continue;
                    field.LakeMask[index] = 1f;
                    field.LakeSurface[index] = surface;
                }

                field.Lakes.Add(new LakeInfo
                {
                    centerX = field.NodeX(0) + (float)(sumX / members.Count) * field.CellSize,
                    centerZ = field.NodeZ(0) + (float)(sumZ / members.Count) * field.CellSize,
                    surface = surface,
                    cellCount = members.Count,
                    maxDepth = maxDepth,
                    isSea = isSea
                });
            }
        }

        /// <summary>Marks river cells from flow accumulation, assigns widths and non-increasing surfaces, then paints soft ribbons.</summary>
        private static void TraceRivers(HydrologyField field, int[] flowTarget, WorldSettingsData settings)
        {
            int n = field.Resolution;
            int count = n * n;
            float[] filled = field.Filled;
            float[] accumulation = field.Accumulation;
            float threshold = settings.riverCatchmentCells;
            bool[] isRiver = new bool[count];
            float[] width = new float[count];
            float[] surface = new float[count];
            float[] bed = new float[count];

            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int compare = filled[a].CompareTo(filled[b]);
                return compare != 0 ? compare : a.CompareTo(b);
            });

            int riverCells = 0;
            // Ascending order means the downstream cell is always resolved before the cell that feeds it.
            for (int i = 0; i < count; i++)
            {
                int index = order[i];
                if (field.LakeMask[index] > 0.5f || accumulation[index] < threshold) continue;
                if (filled[index] <= settings.waterLevel + 0.05f) continue;

                float normalized = SRMath.Sqrt(accumulation[index] / threshold);
                float w = SRMath.Clamp(settings.riverMinWidth * normalized, settings.riverMinWidth, settings.riverMaxWidth);
                float widthT = SRMath.InverseLerp(settings.riverMinWidth, settings.riverMaxWidth, w);
                float drop = SRMath.Lerp(0.22f, 0.45f, widthT);
                float depth = settings.riverDepth * SRMath.Lerp(0.55f, 1f, widthT);

                float own = filled[index] - drop;
                int down = flowTarget[index];
                float downstreamSurface = float.MinValue;
                if (down >= 0)
                {
                    if (isRiver[down]) downstreamSurface = surface[down];
                    else if (field.LakeMask[down] > 0.5f) downstreamSurface = field.LakeSurface[down];
                }

                float s = SRMath.Min(filled[index], SRMath.Max(own, downstreamSurface));
                isRiver[index] = true;
                width[index] = w;
                surface[index] = s;
                bed[index] = s - depth;
                riverCells++;
            }

            field.RiverCellCount = riverCells;
            float cell = field.CellSize;
            for (int index = 0; index < count; index++)
            {
                if (!isRiver[index]) continue;
                int cx = index % n;
                int cz = index / n;
                float w = width[index];
                float outer = w * 1.25f + cell * 0.5f;
                float inner = w * 0.5f;
                // Surface/bed are painted a few cells beyond the visible ribbon so water-mesh vertices on the
                // bank interpolate toward this river instead of a distant lake.
                float reach = outer + cell * 2.5f;
                int radius = SRMath.CeilToInt(reach / cell);
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int nz = cz + dz;
                    if (nz < 0 || nz >= n) continue;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = cx + dx;
                        if (nx < 0 || nx >= n) continue;
                        float distance = SRMath.Length(dx * cell, dz * cell);
                        float proximity = SRMath.Clamp01((reach - distance) / reach);
                        if (proximity <= 0f) continue;
                        int ni = nz * n + nx;
                        if (proximity > field.RiverProximity[ni])
                        {
                            field.RiverProximity[ni] = proximity;
                            field.RiverSurface[ni] = surface[index];
                            field.RiverBed[ni] = bed[index];
                        }

                        float strength = SRMath.Clamp01((outer - distance) / SRMath.Max(outer - inner, 0.01f));
                        if (strength > field.RiverStrength[ni]) field.RiverStrength[ni] = strength;
                    }
                }
            }
        }

        /// <summary>Chamfer distance transform from water nodes, plus propagation of the nearest lake surface onto land.</summary>
        private static void BuildDistanceFields(HydrologyField field)
        {
            int n = field.Resolution;
            int count = n * n;
            float[] distance = field.WaterDistance;
            float[] lakeDistance = new float[count];
            for (int i = 0; i < count; i++)
            {
                bool water = field.LakeMask[i] > 0.5f || field.RiverStrength[i] >= 0.999f;
                distance[i] = water ? 0f : float.MaxValue;
                lakeDistance[i] = field.LakeMask[i] > 0.5f ? 0f : float.MaxValue;
                if (field.RiverStrength[i] > 0f && field.RiverStrength[i] < 0.999f && field.LakeMask[i] < 0.5f)
                    distance[i] = SRMath.Min(distance[i], (1f - field.RiverStrength[i]) * field.CellSize * 2f);
            }

            Chamfer(distance, null, n, field.CellSize);
            Chamfer(lakeDistance, field.LakeSurface, n, field.CellSize);
        }

        private static void Chamfer(float[] distance, float[] payload, int n, float cell)
        {
            float diagonal = cell * 1.41421356f;
            for (int pass = 0; pass < 2; pass++)
            {
                bool forward = pass == 0;
                for (int zi = 0; zi < n; zi++)
                {
                    int z = forward ? zi : n - 1 - zi;
                    for (int xi = 0; xi < n; xi++)
                    {
                        int x = forward ? xi : n - 1 - xi;
                        int index = z * n + x;
                        int step = forward ? -1 : 1;
                        Relax(x + step, z, cell);
                        Relax(x, z + step, cell);
                        Relax(x + step, z + step, diagonal);
                        Relax(x - step, z + step, diagonal);

                        void Relax(int nx, int nz, float weight)
                        {
                            if (nx < 0 || nz < 0 || nx >= n || nz >= n) return;
                            int ni = nz * n + nx;
                            if (distance[ni] == float.MaxValue) return;
                            float candidate = distance[ni] + weight;
                            if (candidate < distance[index])
                            {
                                distance[index] = candidate;
                                if (payload != null) payload[index] = payload[ni];
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < distance.Length; i++)
                if (distance[i] == float.MaxValue) distance[i] = n * cell;
        }

        /// <summary>Minimal binary min-heap keyed by float with deterministic ordering.</summary>
        private sealed class MinHeap
        {
            private float[] keys;
            private int[] values;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                keys = new float[Math.Max(16, capacity)];
                values = new int[keys.Length];
            }

            public void Push(float key, int value)
            {
                if (Count == keys.Length)
                {
                    Array.Resize(ref keys, keys.Length * 2);
                    Array.Resize(ref values, values.Length * 2);
                }

                int i = Count++;
                keys[i] = key;
                values[i] = value;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (!Less(i, parent)) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public void Pop(out float key, out int value)
            {
                key = keys[0];
                value = values[0];
                Count--;
                if (Count > 0)
                {
                    keys[0] = keys[Count];
                    values[0] = values[Count];
                    int i = 0;
                    while (true)
                    {
                        int left = i * 2 + 1;
                        int right = left + 1;
                        int smallest = i;
                        if (left < Count && Less(left, smallest)) smallest = left;
                        if (right < Count && Less(right, smallest)) smallest = right;
                        if (smallest == i) break;
                        Swap(i, smallest);
                        i = smallest;
                    }
                }
            }

            private bool Less(int a, int b) => keys[a] < keys[b] || (keys[a] == keys[b] && values[a] < values[b]);

            private void Swap(int a, int b)
            {
                float k = keys[a]; keys[a] = keys[b]; keys[b] = k;
                int v = values[a]; values[a] = values[b]; values[b] = v;
            }
        }
    }
}
