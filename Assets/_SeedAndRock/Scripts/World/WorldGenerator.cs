using System;
using System.Collections;
using SeedAndRock.Player;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>Game-flow facade. MapMagic is the sole runtime generation and streaming backend.</summary>
    public sealed class WorldGenerator : MonoBehaviour
    {
        public static WorldGenerator Active {get;private set;}
        [SerializeField] WorldGenerationSettings settings;
        [SerializeField] Material terrainMaterial,grassMaterial,waterMaterial,trunkMaterial,foliageMaterial,rockMaterial;
        public WorldGenerationSettings Settings=>settings;
        public WorldMaterials Materials=>new WorldMaterials{terrain=terrainMaterial,grass=grassMaterial,water=waterMaterial,trunk=trunkMaterial,foliage=foliageMaterial,rock=rockMaterial};
        public MapMagicBackend Backend {get;private set;}
        public WorldSampler Sampler=>null; // Legacy diagnostic compatibility; never constructs custom terrain.
        public bool IsGenerating {get;private set;}
        public WorldBuildResult LastResult {get;private set;}
        public int CurrentSeed {get;private set;}
        Coroutine routine;
        void Awake(){Active=this;}
        void OnDestroy(){CancelGeneration();if(Active==this)Active=null;}
        public Coroutine GenerateWorldAsync(int seed,Action<WorldGenerationReport> progress,Action<WorldBuildResult> complete,Action<Exception> error)
        {
            CancelGeneration();ClearGeneratedWorld();CurrentSeed=seed;IsGenerating=true;
            var root=new GameObject("__GeneratedWorld");root.transform.SetParent(transform,false);Backend=root.AddComponent<MapMagicBackend>();
            var player=PlayerSpawner.EnsurePlayer(SurvivalGraph.StartPosition+Vector3.up*2);player.enabled=false;
            var saved=UI.SeedAndRockGameFlow.Instance?.CurrentWorld;
            if(saved!=null&&saved.hasVisited&&saved.GetPlayerState().IsFinite)PlayerSpawner.Teleport(player,new Vector3(saved.playerX,saved.playerY,saved.playerZ),saved.playerYaw,saved.playerPitch);
            routine=StartCoroutine(Backend.Generate(seed,player.transform,progress,result=>{
                IsGenerating=false;routine=null;LastResult=result;
                var expedition=new GameObject("Expedition");expedition.transform.SetParent(root.transform,false);expedition.AddComponent<ExpeditionWorld>().Initialize(result);
                complete?.Invoke(result);
            },ex=>{IsGenerating=false;routine=null;Backend.Stop();error?.Invoke(ex);}));return routine;
        }
        public void CancelGeneration(){if(routine!=null)StopCoroutine(routine);routine=null;IsGenerating=false;Backend?.Stop();}
        public void ClearGeneratedWorld(){Backend?.Stop();Backend=null;LastResult=null;for(int i=transform.childCount-1;i>=0;i--){var c=transform.GetChild(i);if(c.name!="__GeneratedWorld")continue;c.gameObject.SetActive(false);if(Application.isPlaying)Destroy(c.gameObject);else DestroyImmediate(c.gameObject);}}
        public bool TryGetHeightAt(float x,float z,out float height){height=0;return Backend!=null&&Backend.TryHeight(x,z,out height);}
        public float GetHeightAt(float x,float z)=>TryGetHeightAt(x,z,out float height)?height:float.NaN;
        public SurfaceSample SampleSurface(float x,float z)=>Backend.Sample(x,z);
        public SeedAndRockBiome GetBiomeAt(float x,float z)=>SampleSurface(x,z).biome;
        public float GetSlopeAt(float x,float z)=>SampleSurface(x,z).slope;
        public ClimateSample GetClimateAt(float x,float z){if(!TryGetHeightAt(x,z,out _))return default;var s=SampleSurface(x,z);float ambient=settings!=null?settings.ToAmbientCelsius(s.temperature,s.isWater):WorldClimate.Temperature01ToCelsius(s.temperature);return new ClimateSample(s.height,s.normalizedHeight,s.moisture,s.temperature,s.slope,s.isWater,s.waterSurface,s.biome,ambient);}
        public bool TryGetWaterSurfaceAt(float x,float z,out float surface){surface=SurvivalGraph.SeaLevel;return TryGetHeightAt(x,z,out float height)&&height<surface;}
        public void LoadWorldSeed(int seed){if(!Application.isPlaying){Debug.LogWarning("MapMagic world generation is managed in Play Mode; edit the graph asset to author terrain.");return;}GenerateWorldAsync(seed,null,null,Debug.LogException);}
        public void SetSeedAndRegenerate(int seed)=>LoadWorldSeed(seed);
        public void GenerateWorld()=>LoadWorldSeed(settings!=null?settings.seed:240613);
    }
}
