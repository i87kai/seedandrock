using SeedAndRock.Interaction;
using SeedAndRock.Player;
using UnityEngine;

namespace SeedAndRock.World
{
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

        public WorldGenerationSettings Settings => settings;

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        public void LoadWorldSeed(int seed)
        {
            if (settings == null)
            {
                Debug.LogError("[SeedAndRock] World generation settings are missing.", this);
                return;
            }

            settings.seed = seed;
            GenerateWorld();
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

            ClearGeneratedWorld();
            Transform generatedRoot = new GameObject(GeneratedRootName).transform;
            generatedRoot.SetParent(transform, false);

            Mesh terrain = WorldMeshBuilder.BuildTerrain(this, settings);
            GameObject terrainObject = CreateMeshObject("Terrain", generatedRoot, terrain, terrainMaterial, true);
            terrainObject.layer = 0;

            Mesh water = WorldMeshBuilder.BuildWater(this, settings);
            CreateMeshObject("Water", generatedRoot, water, waterMaterial, false);

            Mesh grass = WorldMeshBuilder.BuildGrass(this, settings);
            CreateMeshObject("Grass", generatedRoot, grass, grassMaterial, false);

            WorldMeshBuilder.BuildEnvironment(this, settings, out Mesh trunks, out Mesh foliage, out Mesh rocks);
            CreateMeshObject("TreeTrunks", generatedRoot, trunks, trunkMaterial, false);
            CreateMeshObject("TreeFoliage", generatedRoot, foliage, foliageMaterial, false);
            CreateMeshObject("Rocks", generatedRoot, rocks, rockMaterial, false);

            if (createPlayerFoundation)
                CreatePlayer(generatedRoot);
            if (createInteractionProof)
                CreateInteractionMarker(generatedRoot);
        }

        [ContextMenu("Clear Generated World")]
        public void ClearGeneratedWorld()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing == null)
                return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        public float GetHeightAt(float x, float z)
        {
            if (settings == null)
                return 0f;

            int seed = settings.seed;
            Vector2 warped = DomainWarp(seed, x, z);
            float continent = SeedNoise.Fractal(seed + 17, warped.x, warped.y, 4, settings.continentFrequency, 2.03f, 0.50f);
            float broad = SeedNoise.Fractal(seed + 47, warped.x, warped.y, 3, settings.continentFrequency * 0.48f, 2.0f, 0.52f);
            float rolling = SeedNoise.Fractal(seed + 101, warped.x, warped.y, settings.terrainOctaves, settings.detailFrequency, 2.02f, 0.48f);
            float fine = SeedNoise.Fractal(seed + 151, x, z, 2, settings.detailFrequency * 2.7f, 2.0f, 0.42f);

            float mountainRegion = Mathf.SmoothStep(0.34f, 0.78f,
                SeedNoise.Fractal(seed + 233, warped.x, warped.y, 3, settings.continentFrequency * 0.72f, 2f, 0.5f) * 0.5f + 0.5f);
            mountainRegion *= mountainRegion;
            float ridges = 1f - Mathf.Abs(SeedNoise.Fractal(seed + 277, warped.x, warped.y, 4, settings.detailFrequency * 0.55f, 2.05f, 0.5f));
            ridges = Mathf.Pow(Mathf.Clamp01(ridges), 3.2f) * mountainRegion;

            float plainsMask = 1f - Mathf.SmoothStep(0.34f, 0.68f, Mathf.Abs(broad));
            float baseShape = continent * 0.40f + broad * 0.22f;
            float relief = rolling * Mathf.Lerp(0.08f, 0.20f, 1f - plainsMask) + fine * 0.035f;
            float height = (baseShape + relief + ridges * 0.55f) * settings.terrainHeight;

            float river = GetRiverMask(x, z);
            float valleyFloor = settings.waterLevel - Mathf.Lerp(0.8f, 2.4f, river);
            height = Mathf.Lerp(height, Mathf.Min(height, valleyFloor), river * 0.92f);
            return height;
        }

        public SeedAndRockBiome GetBiomeAt(float x, float z)
        {
            float height = GetHeightAt(x, z);
            float normalizedHeight = Mathf.InverseLerp(-settings.terrainHeight * 0.25f, settings.terrainHeight * 0.8f, height);
            Vector2 warped = DomainWarp(settings.seed + 400, x, z);
            float moisture = SeedNoise.Fractal(settings.seed + 419, warped.x, warped.y, 3, settings.continentFrequency * 0.70f, 2f, 0.52f) * 0.5f + 0.5f;
            float temperature = SeedNoise.Fractal(settings.seed + 463, warped.x, warped.y, 3, settings.continentFrequency * 0.52f, 2f, 0.52f) * 0.5f + 0.5f;
            temperature -= normalizedHeight * 0.33f;

            if (normalizedHeight > settings.highlandHeightThreshold + 0.15f)
                return temperature < 0.38f ? SeedAndRockBiome.Snow : SeedAndRockBiome.Mountains;
            if (temperature < 0.28f)
                return SeedAndRockBiome.Snow;
            if (temperature > 0.68f && moisture < 0.43f)
                return SeedAndRockBiome.Desert;
            if (moisture > settings.forestMoistureThreshold && normalizedHeight < 0.68f)
                return SeedAndRockBiome.Forest;
            if (GetSlopeAt(x, z) < 0.20f && normalizedHeight < 0.52f)
                return SeedAndRockBiome.Plains;
            return SeedAndRockBiome.Grassland;
        }

        public float GetSlopeAt(float x, float z)
        {
            const float sampleDistance = 2.5f;
            float dx = GetHeightAt(x + sampleDistance, z) - GetHeightAt(x - sampleDistance, z);
            float dz = GetHeightAt(x, z + sampleDistance) - GetHeightAt(x, z - sampleDistance);
            return Mathf.Clamp01(new Vector2(dx, dz).magnitude / (sampleDistance * 2.5f));
        }

        public bool TryGetWaterSurfaceAt(float x, float z, out float surface)
        {
            float terrain = GetHeightAt(x, z);
            float river = GetRiverMask(x, z);
            float lake = GetLakeMask(x, z);
            bool oceanOrLake = terrain < settings.waterLevel - 0.15f && (lake > 0.22f || terrain < settings.waterLevel - 1.25f);
            bool riverChannel = river > 0.50f && terrain < settings.waterLevel + 0.35f;
            surface = settings.waterLevel + (riverChannel ? 0.08f : 0f);
            return oceanOrLake || riverChannel;
        }

        private Vector2 DomainWarp(int seed, float x, float z)
        {
            float frequency = settings.continentFrequency * 0.42f;
            float amount = settings.worldSize * 0.055f;
            float wx = SeedNoise.Fractal(seed + 701, x, z, 3, frequency, 2f, 0.5f);
            float wz = SeedNoise.Fractal(seed + 733, x + 791.3f, z - 421.7f, 3, frequency, 2f, 0.5f);
            return new Vector2(x + wx * amount, z + wz * amount);
        }

        private float GetRiverMask(float x, float z)
        {
            Vector2 warped = DomainWarp(settings.seed + 809, x, z);
            float lineA = Mathf.Abs(SeedNoise.Fractal(settings.seed + 821, warped.x, warped.y, 3, settings.continentFrequency * 0.62f, 2f, 0.5f));
            float lineB = Mathf.Abs(SeedNoise.Fractal(settings.seed + 853, warped.x + 317f, warped.y - 179f, 2, settings.continentFrequency * 0.43f, 2f, 0.5f));
            float network = Mathf.Min(lineA, lineB * 1.14f);
            return 1f - Mathf.SmoothStep(0.025f, 0.105f, network);
        }

        private float GetLakeMask(float x, float z)
        {
            Vector2 warped = DomainWarp(settings.seed + 911, x, z);
            float basins = SeedNoise.Fractal(settings.seed + 929, warped.x, warped.y, 3, settings.continentFrequency * 0.60f, 2f, 0.5f) * 0.5f + 0.5f;
            return Mathf.SmoothStep(0.68f, 0.82f, basins);
        }

        private static GameObject CreateMeshObject(string objectName, Transform parent, Mesh mesh, Material material, bool collider)
        {
            GameObject instance = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            instance.transform.SetParent(parent, false);
            instance.GetComponent<MeshFilter>().sharedMesh = mesh;
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (collider)
                instance.AddComponent<MeshCollider>().sharedMesh = mesh;
            return instance;
        }

        private void CreatePlayer(Transform parent)
        {
            GameObject player = new GameObject("SeedAndRock_Player", typeof(CharacterController), typeof(FirstPersonExplorerController), typeof(SeedAndRockInteractionRaycaster));
            player.transform.SetParent(parent, false);
            player.transform.position = FindSafeSpawn();
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
        }

        private Vector3 FindSafeSpawn()
        {
            for (int ring = 0; ring < 20; ring++)
            {
                float radius = ring * 12f;
                for (int step = 0; step < 12; step++)
                {
                    float angle = step * Mathf.PI * 2f / 12f;
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    float y = GetHeightAt(x, z);
                    if (y > settings.waterLevel + 0.8f && GetSlopeAt(x, z) < 0.42f)
                        return new Vector3(x, y + 0.15f, z);
                }
            }
            return new Vector3(0f, GetHeightAt(0f, 0f) + 1f, 0f);
        }

        private void CreateInteractionMarker(Transform parent)
        {
            Vector3 origin = FindSafeSpawn();
            float x = origin.x + 3f;
            float z = origin.z + 4f;
            float y = GetHeightAt(x, z);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Resonant Seed (Interaction Test)";
            marker.transform.SetParent(parent, true);
            marker.transform.position = new Vector3(x, y + 0.65f, z);
            marker.transform.localScale = Vector3.one * 0.65f;
            marker.AddComponent<SeedAndRockInteractable>();
        }
    }
}
