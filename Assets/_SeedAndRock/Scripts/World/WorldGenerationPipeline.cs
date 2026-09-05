using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SeedAndRock.World
{
    public enum WorldGenerationStage
    {
        PreparingWorld,
        GeneratingTerrain,
        GeneratingRivers,
        CreatingBiomes,
        SculptingTerrain,
        PlacingVegetation,
        PreparingPlayer,
        Complete
    }

    /// <summary>Progress snapshot. <see cref="Fraction"/> is only set when it reflects real measured progress.</summary>
    public readonly struct WorldGenerationReport
    {
        public readonly WorldGenerationStage Stage;
        public readonly float? Fraction;

        public WorldGenerationReport(WorldGenerationStage stage, float? fraction)
        {
            Stage = stage;
            Fraction = fraction;
        }

        public int StageIndex => (int)Stage;
        public int StageCount => (int)WorldGenerationStage.Complete;

        public string Label
        {
            get
            {
                switch (Stage)
                {
                    case WorldGenerationStage.PreparingWorld: return "Preparing world";
                    case WorldGenerationStage.GeneratingTerrain: return "Generating terrain";
                    case WorldGenerationStage.GeneratingRivers: return "Generating rivers";
                    case WorldGenerationStage.CreatingBiomes: return "Creating biomes";
                    case WorldGenerationStage.SculptingTerrain: return "Sculpting terrain";
                    case WorldGenerationStage.PlacingVegetation: return "Placing vegetation";
                    case WorldGenerationStage.PreparingPlayer: return "Preparing player";
                    default: return "Ready";
                }
            }
        }
    }

    /// <summary>Materials used by the generated renderers.</summary>
    [Serializable]
    public struct WorldMaterials
    {
        public Material terrain;
        public Material water;
        public Material grass;
        public Material trunk;
        public Material foliage;
        public Material rock;
    }

    /// <summary>Everything produced by one generation run.</summary>
    public sealed class WorldBuildResult
    {
        public WorldSampler Sampler;
        public Transform Root;
        public Vector3 SpawnPosition;
        public int TerrainTriangles;
        public int WaterTriangles;
        public int PropTriangles;
        public int TreeCount;
        public int RockCount;
        public int GrassCount;
        public int RendererCount;
        public double Seconds;

        public int TotalTriangles => TerrainTriangles + WaterTriangles + PropTriangles;
    }

    /// <summary>
    /// Staged generation runner. Pure work (sampling, hydrology, placement, mesh buffers) runs on worker
    /// tasks; only mesh upload and GameObject creation touch the main thread, time-sliced per frame so
    /// the loading UI keeps repainting. Supports cancellation and reports typed errors to the caller.
    /// </summary>
    public sealed class WorldGenerationPipeline
    {
        /// <summary>Main-thread budget per frame for mesh uploads.</summary>
        public float FrameBudgetMilliseconds = 9f;
        public float SeaSkirtExtent = 1600f;

        private readonly WorldGenerationSettings settings;
        private readonly WorldMaterials materials;

        public WorldGenerationPipeline(WorldGenerationSettings settings, WorldMaterials materials)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.materials = materials;
        }

        public IEnumerator Run(int seed, Transform parent, string rootName, Action<WorldGenerationReport> progress, CancellationToken token, Action<WorldBuildResult> onComplete, Action<Exception> onError)
        {
            Stopwatch total = Stopwatch.StartNew();
            WorldBuildResult result = new WorldBuildResult();
            Transform root = null;

            // ---- Stage: preparing world (settings snapshot + terrain/climate fields) -------------------
            Report(progress, WorldGenerationStage.PreparingWorld, null);
            WorldSettingsData data;
            WorldGenerationPalette palette;
            TerrainField terrain;
            ClimateField climate;
            try
            {
                data = settings.ToData(seed);
                palette = settings.ToPalette();
                terrain = new TerrainField(data);
                climate = new ClimateField(data, terrain);
                root = new GameObject(rootName).transform;
                root.SetParent(parent, false);
                result.Root = root;
            }
            catch (Exception exception)
            {
                Fail(root, onError, exception);
                yield break;
            }

            yield return null;

            // ---- Stage: generating terrain, then rivers (hydrology grid on a worker) -------------------
            Report(progress, WorldGenerationStage.GeneratingTerrain, null);
            int hydrologyStep = 0;
            Task<HydrologyField> hydrologyTask = Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return HydrologyBuilder.Build(data, terrain, step => Volatile.Write(ref hydrologyStep, step));
            }, token);
            bool reportedRivers = false;
            while (!hydrologyTask.IsCompleted)
            {
                if (!reportedRivers && Volatile.Read(ref hydrologyStep) >= HydrologyBuilder.StepHeightsSampled)
                {
                    reportedRivers = true;
                    Report(progress, WorldGenerationStage.GeneratingRivers, null);
                }

                yield return null;
            }

            if (Faulted(hydrologyTask, root, onError)) yield break;

            WorldSampler sampler = new WorldSampler(data, terrain, climate, hydrologyTask.Result);
            result.Sampler = sampler;

            // ---- Stage: creating biomes (per-vertex surface classification) -----------------------------
            Report(progress, WorldGenerationStage.CreatingBiomes, null);
            Task<TerrainGrid> gridTask = Task.Run(() => TerrainGrid.Sample(sampler, token), token);
            yield return WaitFor(gridTask);
            if (Faulted(gridTask, root, onError)) yield break;

            // ---- Stage: sculpting terrain (mesh buffers on worker, upload sliced on main thread) -------
            Report(progress, WorldGenerationStage.SculptingTerrain, null);
            ChunkGrid chunks = new ChunkGrid(data.terrainChunks, data.worldSize);
            int waterResolution = settings.waterResolution;
            Task<MeshData[]> terrainTask = Task.Run(() => WorldMeshBuilder.BuildTerrainChunks(sampler, gridTask.Result, palette, chunks, token), token);
            Task<MeshData[]> waterTask = Task.Run(() => WorldMeshBuilder.BuildWaterChunks(sampler, waterResolution, chunks, token), token);
            yield return WaitFor(terrainTask);
            if (Faulted(terrainTask, root, onError)) yield break;

            Transform terrainRoot = CreateGroup(root, "Terrain");
            int uploaded = 0;
            int uploadTotal = terrainTask.Result.Length;
            Stopwatch frame = Stopwatch.StartNew();
            foreach (MeshData chunk in terrainTask.Result)
            {
                if (token.IsCancellationRequested) { Fail(root, onError, new OperationCanceledException(token)); yield break; }
                try
                {
                    Mesh mesh = chunk.ToMesh();
                    CreateRenderer(chunk.Name, terrainRoot, mesh, materials.terrain, true);
                    result.TerrainTriangles += chunk.TriangleCount;
                    result.RendererCount++;
                }
                catch (Exception exception)
                {
                    Fail(root, onError, exception);
                    yield break;
                }

                uploaded++;
                Report(progress, WorldGenerationStage.SculptingTerrain, uploaded / (float)uploadTotal);
                if (frame.Elapsed.TotalMilliseconds > FrameBudgetMilliseconds)
                {
                    yield return null;
                    frame.Restart();
                }
            }

            yield return WaitFor(waterTask);
            if (Faulted(waterTask, root, onError)) yield break;

            Transform waterRoot = CreateGroup(root, "Water");
            try
            {
                foreach (MeshData chunk in waterTask.Result)
                {
                    if (chunk.IsEmpty) continue;
                    CreateRenderer(chunk.Name, waterRoot, chunk.ToMesh(), materials.water, false, false);
                    result.WaterTriangles += chunk.TriangleCount;
                    result.RendererCount++;
                }

                MeshData skirt = WorldMeshBuilder.BuildSeaSkirt(data, SeaSkirtExtent);
                CreateRenderer(skirt.Name, waterRoot, skirt.ToMesh(), materials.water, false, false);
                result.RendererCount++;
            }
            catch (Exception exception)
            {
                Fail(root, onError, exception);
                yield break;
            }

            yield return null;

            // ---- Stage: placing vegetation --------------------------------------------------------------
            Report(progress, WorldGenerationStage.PlacingVegetation, null);
            float placementProgress = 0f;
            PlacementResult harvestPlacement = null;
            Task<PropMeshSet> propsTask = Task.Run(() =>
            {
                PlacementResult placement = VegetationPlacer.Place(sampler, value => Volatile.Write(ref placementProgress, value * 0.8f));
                token.ThrowIfCancellationRequested();
                result.TreeCount = placement.Trees.Count;
                result.RockCount = placement.Rocks.Count;
                result.GrassCount = placement.Grass.Count;
                harvestPlacement = placement;
                var grassOnly = new PlacementResult();
                grassOnly.Grass.AddRange(placement.Grass);
                PropMeshSet set = PropMeshBuilder.Build(grassOnly, palette, chunks, token);
                Volatile.Write(ref placementProgress, 1f);
                return set;
            }, token);
            while (!propsTask.IsCompleted)
            {
                Report(progress, WorldGenerationStage.PlacingVegetation, Volatile.Read(ref placementProgress));
                yield return null;
            }

            if (Faulted(propsTask, root, onError)) yield break;

            Transform propRoot = CreateGroup(root, "Props");
            PropMeshSet props = propsTask.Result;
            MeshData[][] groups = { props.Trunks, props.Foliage, props.Rocks, props.Grass };
            Material[] groupMaterials = { materials.trunk, materials.foliage, materials.rock, materials.grass };
            frame.Restart();
            for (int g = 0; g < groups.Length; g++)
            {
                foreach (MeshData chunk in groups[g])
                {
                    if (chunk.IsEmpty) continue;
                    if (token.IsCancellationRequested) { Fail(root, onError, new OperationCanceledException(token)); yield break; }
                    try
                    {
                        CreateRenderer(chunk.Name, propRoot, chunk.ToMesh(), groupMaterials[g], false, g != 3);
                        result.PropTriangles += chunk.TriangleCount;
                        result.RendererCount++;
                    }
                    catch (Exception exception)
                    {
                        Fail(root, onError, exception);
                        yield break;
                    }

                    if (frame.Elapsed.TotalMilliseconds > FrameBudgetMilliseconds)
                    {
                        yield return null;
                        frame.Restart();
                    }
                }
            }

            int nodeIndex = 0;
            foreach (var p in harvestPlacement.Trees)
            {
                SeedAndRock.Items.ResourceNode.CreateProp(propRoot, p, palette, materials, "tree-" + nodeIndex++);
                if (frame.Elapsed.TotalMilliseconds > FrameBudgetMilliseconds) { yield return null; frame.Restart(); }
            }
            nodeIndex = 0;
            foreach (var p in harvestPlacement.Rocks)
            {
                SeedAndRock.Items.ResourceNode.CreateProp(propRoot, p, palette, materials, "stone-" + nodeIndex++);
                if (frame.Elapsed.TotalMilliseconds > FrameBudgetMilliseconds) { yield return null; frame.Restart(); }
            }
            // ---- Stage: preparing player ----------------------------------------------------------------
            Report(progress, WorldGenerationStage.PreparingPlayer, null);
            try
            {
                SpawnFinder.SpawnPoint spawn = SpawnFinder.Find(sampler);
                result.SpawnPosition = new Vector3(spawn.x, spawn.y, spawn.z);
                Physics.SyncTransforms();
            }
            catch (Exception exception)
            {
                Fail(root, onError, exception);
                yield break;
            }

            yield return null;
            result.Seconds = total.Elapsed.TotalSeconds;
            Report(progress, WorldGenerationStage.Complete, 1f);
            onComplete?.Invoke(result);
        }

        private static IEnumerator WaitFor(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
        }

        private static bool Faulted(Task task, Transform root, Action<Exception> onError)
        {
            if (task.IsCanceled)
            {
                Fail(root, onError, new OperationCanceledException("World generation was cancelled."));
                return true;
            }

            if (task.IsFaulted)
            {
                Exception exception = task.Exception != null ? task.Exception.GetBaseException() : new Exception("World generation failed.");
                Fail(root, onError, exception);
                return true;
            }

            return false;
        }

        private static void Fail(Transform root, Action<Exception> onError, Exception exception)
        {
            if (root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
                else UnityEngine.Object.DestroyImmediate(root.gameObject);
            }

            onError?.Invoke(exception);
        }

        private static void Report(Action<WorldGenerationReport> progress, WorldGenerationStage stage, float? fraction)
        {
            progress?.Invoke(new WorldGenerationReport(stage, fraction));
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject CreateRenderer(string objectName, Transform parent, Mesh mesh, Material material, bool collider, bool castShadows = true)
        {
            GameObject instance = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            instance.transform.SetParent(parent, false);
            instance.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (collider)
                instance.AddComponent<MeshCollider>().sharedMesh = mesh;
            return instance;
        }
    }
}
