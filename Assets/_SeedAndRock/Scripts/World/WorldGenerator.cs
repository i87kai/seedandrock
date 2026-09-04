using System;
using System.Collections;
using System.Threading;
using SeedAndRock.Interaction;
using SeedAndRock.Player;
using UnityEngine;

namespace SeedAndRock.World
{
    /// <summary>
    /// Thin scene-facing component: owns the settings/material references, starts the staged
    /// <see cref="WorldGenerationPipeline"/>, and exposes the current <see cref="WorldSampler"/> for
    /// pure world queries. It contains no generation math of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldGenerator : MonoBehaviour
    {
        public static WorldGenerator Active { get; private set; }

        [SerializeField] private WorldGenerationSettings settings;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material grassMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material trunkMaterial;
        [SerializeField] private Material foliageMaterial;
        [SerializeField] private Material rockMaterial;
        [SerializeField] private bool createPlayerFoundation = true;
        [SerializeField] private bool createInteractionProof = true;

        private const string GeneratedRootName = "__GeneratedWorld";

        private WorldSampler sampler;
        private CancellationTokenSource generationCancellation;
        private Coroutine generationRoutine;

        public WorldGenerationSettings Settings => settings;
        public bool IsGenerating { get; private set; }
        public WorldBuildResult LastResult { get; private set; }
        public int CurrentSeed { get; private set; }

        /// <summary>Pure query object for the world currently generated (or for the settings seed when none has been generated yet).</summary>
        public WorldSampler Sampler
        {
            get
            {
                if (sampler == null && settings != null)
                {
                    sampler = WorldSampler.Build(settings.ToData());
                    CurrentSeed = settings.seed;
                }

                return sampler;
            }
        }

        public WorldMaterials Materials => new WorldMaterials
        {
            terrain = terrainMaterial, water = waterMaterial, grass = grassMaterial,
            trunk = trunkMaterial, foliage = foliageMaterial, rock = rockMaterial
        };

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            CancelGeneration();
            if (Active == this)
                Active = null;
        }

        /// <summary>Starts asynchronous generation. Completion and failure are reported through the callbacks; the returned coroutine can be awaited.</summary>
        public Coroutine GenerateWorldAsync(int seed, Action<WorldGenerationReport> progress, Action<WorldBuildResult> onComplete, Action<Exception> onError)
        {
            if (settings == null)
            {
                onError?.Invoke(new InvalidOperationException("World generation settings are missing on the WorldGenerator."));
                return null;
            }

            CancelGeneration();
            generationCancellation = new CancellationTokenSource();
            generationRoutine = StartCoroutine(GenerateRoutine(seed, progress, onComplete, onError, generationCancellation.Token));
            return generationRoutine;
        }

        /// <summary>Stops any in-flight generation without invoking its callbacks and removes partial output.</summary>
        public void CancelGeneration()
        {
            if (generationCancellation != null)
            {
                generationCancellation.Cancel();
                generationCancellation.Dispose();
                generationCancellation = null;
            }

            if (generationRoutine != null)
            {
                StopCoroutine(generationRoutine);
                generationRoutine = null;
                ClearGeneratedWorld();
            }

            IsGenerating = false;
        }

        private IEnumerator GenerateRoutine(int seed, Action<WorldGenerationReport> progress, Action<WorldBuildResult> onComplete, Action<Exception> onError, CancellationToken token)
        {
            IsGenerating = true;
            ClearGeneratedWorld();
            sampler = null;
            CurrentSeed = seed;
            WorldGenerationPipeline pipeline = new WorldGenerationPipeline(settings, Materials);
            yield return pipeline.Run(seed, transform, GeneratedRootName, progress, token,
                result =>
                {
                    IsGenerating = false;
                    generationRoutine = null;
                    sampler = result.Sampler;
                    LastResult = result;
                    FinishWorld(result);
                    onComplete?.Invoke(result);
                },
                exception =>
                {
                    IsGenerating = false;
                    generationRoutine = null;
                    onError?.Invoke(exception);
                });
        }

        /// <summary>Synchronous generation for editor buttons and legacy callers. Blocks until the world exists.</summary>
        public void LoadWorldSeed(int seed)
        {
            if (settings == null)
            {
                Debug.LogError("[SeedAndRock] World generation settings are missing.", this);
                return;
            }

            CancelGeneration();
            ClearGeneratedWorld();
            sampler = null;
            CurrentSeed = seed;
            Exception failure = null;
            WorldBuildResult built = null;
            WorldGenerationPipeline pipeline = new WorldGenerationPipeline(settings, Materials) { FrameBudgetMilliseconds = float.MaxValue };
            IEnumerator routine = pipeline.Run(seed, transform, GeneratedRootName, null, CancellationToken.None, result => built = result, exception => failure = exception);
            RunToCompletion(routine);
            if (failure != null)
            {
                Debug.LogException(failure, this);
                return;
            }

            if (built == null) return;
            sampler = built.Sampler;
            LastResult = built;
            FinishWorld(built);
        }

        public void SetSeedAndRegenerate(int seed) => LoadWorldSeed(seed);

        [ContextMenu("Generate / Regenerate World")]
        public void GenerateWorld()
        {
            if (settings == null)
            {
                Debug.LogError("[SeedAndRock] Assign WorldGenerationSettings before generating.", this);
                return;
            }

            LoadWorldSeed(settings.seed);
        }

        [ContextMenu("Clear Generated World")]
        public void ClearGeneratedWorld()
        {
            LastResult = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != GeneratedRootName) continue;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        // ---- Pure query pass-throughs used by editor tools and gameplay --------------------------------

        public float GetHeightAt(float x, float z) => Sampler?.GetHeightAt(x, z) ?? 0f;
        public SeedAndRockBiome GetBiomeAt(float x, float z) => Sampler?.GetBiomeAt(x, z) ?? SeedAndRockBiome.Grassland;
        public float GetSlopeAt(float x, float z) => Sampler?.GetSlopeAt(x, z) ?? 0f;

        public bool TryGetWaterSurfaceAt(float x, float z, out float surface)
        {
            surface = settings != null ? settings.waterLevel : 0f;
            return Sampler != null && Sampler.TryGetWaterSurfaceAt(x, z, out surface);
        }

        private void FinishWorld(WorldBuildResult result)
        {
            if (createPlayerFoundation)
                PlayerSpawner.EnsurePlayer(result.SpawnPosition);
            if (createInteractionProof && result.Root != null)
                CreateInteractionMarker(result.Root, result.SpawnPosition, result.Sampler);
        }

        private static void CreateInteractionMarker(Transform parent, Vector3 spawn, WorldSampler worldSampler)
        {
            float x = spawn.x + 3f;
            float z = spawn.z + 4f;
            float y = worldSampler.GetHeightAt(x, z);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Resonant Seed (Interaction Test)";
            marker.transform.SetParent(parent, true);
            marker.transform.position = new Vector3(x, y + 0.65f, z);
            marker.transform.localScale = Vector3.one * 0.65f;
            marker.AddComponent<SeedAndRockInteractable>();
        }

        /// <summary>Steps a coroutine (including nested enumerators) to completion on the calling thread.</summary>
        private static void RunToCompletion(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                    RunToCompletion(nested);
                else if (routine.Current is Coroutine)
                    throw new InvalidOperationException("Synchronous generation cannot wait on Unity Coroutine objects.");
                // Other yields (null / frame waits) are skipped: the worker tasks are simply polled again.
            }
        }
    }
}
