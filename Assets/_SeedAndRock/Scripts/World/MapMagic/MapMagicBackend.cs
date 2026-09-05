using System;
using System.Collections;
using Den.Tools;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Terrains;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>MapMagic 2 is the sole terrain/biome/placement backend. This component only configures it,
    /// bridges its outputs to gameplay and exposes samples. World composition lives in <see cref="SurvivalGraph"/>.</summary>
    public sealed class MapMagicBackend : MonoBehaviour
    {
        public MapMagicObject Map {get;private set;}
        public MapMagicPrototypes Prototypes {get;private set;}
        public MapMagicResourceStreamer Streamer {get;private set;}
        public Graph GraphAsset;
        /// <summary>Build the graph from <see cref="SurvivalGraph"/> at runtime so code tuning applies without re-saving the
        /// asset. Turn off to run the serialized Resources/SR_MapMagicWorld asset (e.g. after hand-editing it in the graph editor).</summary>
        public bool BuildGraphFromCode=true;
        public float SeaLevel=>SurvivalGraph.SeaLevel;

        // ---- streaming / quality budget (see README section "World budget") ----
        public const int MainRange=1;          // 3x3 full-detail tiles (960 m) around the player
        public const int GenerateRange=3;      // 7x7 incl. drafts (2.2 km) - covers the whole island from anywhere on it
        public const int PixelError=5;
        public const float TreeDistance=650, DetailDistance=60, BaseMapDistance=500;

        void OnEnable(){TerrainTile.OnTileApplied+=TileApplied;TerrainTile.OnTileMoved+=TileMoved;}
        void OnDisable(){TerrainTile.OnTileApplied-=TileApplied;TerrainTile.OnTileMoved-=TileMoved;}
        void TileApplied(TerrainTile tile,MapMagic.Products.TileData data,MapMagic.Products.StopToken stop)
        {
            if(tile.mapMagic!=Map||data.isDraft||stop!=null&&stop.stop||Prototypes==null)return;
            var gameplay=tile.GetComponent<MapMagicGameplayTile>();if(gameplay==null)gameplay=tile.gameObject.AddComponent<MapMagicGameplayTile>();gameplay.Begin(tile,Prototypes);
        }
        void TileMoved(TerrainTile tile)
        {
            if(tile.mapMagic!=Map)return;
            var gameplay=tile.GetComponent<MapMagicGameplayTile>();if(gameplay==null)return;gameplay.Clear();
            var td=tile.main?.terrain?.terrainData;if(td!=null&&td.treeInstanceCount>0)td.SetTreeInstances(new TreeInstance[0],false);
        }
        public bool TryTerrain(float x,float z,out Terrain terrain)
        {
            terrain=null;if(Map==null)return false;
            var tile=Map.tiles.FindByWorldPosition(x,z);
            if(tile?.main==null||!tile.main.applyReady)return false;
            terrain=tile.main.terrain;return terrain!=null&&terrain.terrainData!=null;
        }
        public bool TryHeight(float x,float z,out float height)
        {
            height=0;if(!TryTerrain(x,z,out var t))return false;height=t.SampleHeight(new Vector3(x,0,z))+t.transform.position.y;return true;
        }
        public SurfaceSample Sample(float x,float z)
        {
            if(!TryTerrain(x,z,out var terrain))throw new InvalidOperationException("MapMagic main tile is not ready at "+x+", "+z);
            var td=terrain.terrainData;var p=terrain.transform.position;float u=Mathf.Clamp01((x-p.x)/td.size.x),v=Mathf.Clamp01((z-p.z)/td.size.z);
            float height=terrain.SampleHeight(new Vector3(x,0,z))+p.y;
            var splats=td.GetAlphamaps(Mathf.Clamp((int)(u*td.alphamapWidth),0,td.alphamapWidth-1),Mathf.Clamp((int)(v*td.alphamapHeight),0,td.alphamapHeight-1),1,1);
            int strongest=0;for(int i=1;i<td.alphamapLayers;i++)if(splats[0,0,i]>splats[0,0,strongest])strongest=i;
            float snow=td.alphamapLayers>4?splats[0,0,4]:0;
            return new SurfaceSample{x=x,z=z,height=height,normalizedHeight=height/SurvivalGraph.TerrainHeight,slope=td.GetSteepness(u,v)/90,
                biome=strongest==1?SeedAndRockBiome.Forest:strongest==2?SeedAndRockBiome.Desert:strongest==3?SeedAndRockBiome.Mountains:strongest==4?SeedAndRockBiome.Snow:SeedAndRockBiome.Plains,
                moisture=td.alphamapLayers>1?.4f+.5f*splats[0,0,1]:.4f,temperature=Mathf.Clamp01(.66f-(height-SeaLevel)/320-snow*.15f),snow=snow,
                isWater=height<SeaLevel,waterSurface=SeaLevel,waterDistance=Mathf.Abs(height-SeaLevel)*5};
        }

        Graph ResolveGraph()
        {
            if(BuildGraphFromCode){
                var layers=new System.Collections.Generic.List<TerrainLayer>();
                for(int i=0;i<5;i++){var l=Resources.Load<TerrainLayer>("MM_Layer_"+i);if(l!=null)layers.Add(l);}
                var proxy=Resources.Load<GameObject>("MM_PlacementProxy");
                if(layers.Count==5&&proxy!=null)return SurvivalGraph.Create(layers.ToArray(),proxy);
                Debug.LogWarning("[SeedAndRock] Terrain layers / placement proxy missing in Resources; falling back to the serialized graph asset. Run SeedAndRock/MapMagic/Create world assets.");
            }
            if(GraphAsset==null)GraphAsset=Resources.Load<Graph>("SR_MapMagicWorld");
            return GraphAsset!=null?Instantiate(GraphAsset):null;
        }

        public IEnumerator Generate(int seed,Transform player,Action<WorldGenerationReport> progress,Action<WorldBuildResult> complete,Action<Exception> failed)
        {
            var graph=ResolveGraph();
            if(graph==null){failed(new InvalidOperationException("MapMagic graph is missing. Run SeedAndRock/MapMagic/Create world assets."));yield break;}
            float start=Time.realtimeSinceStartup;
            var world=WorldGenerator.Active;
            Prototypes=new MapMagicPrototypes(transform,world.Settings.ToPalette(),world.Materials);
            var root=new GameObject("MapMagic World");root.transform.SetParent(transform,false);root.SetActive(false);
            Map=root.AddComponent<MapMagicObject>();Map.graph=graph;Map.graph.random=new Noise(seed,32768);
            Map.tileSize=new Vector2D(SurvivalGraph.TileSize,SurvivalGraph.TileSize);Map.tileResolution=MapMagicObject.Resolution._257;
            Map.globals.height=SurvivalGraph.TerrainHeight;Map.mainRange=MainRange;Map.tiles.generateRange=GenerateRange;Map.tiles.retainMargin=1;
            Map.globals.heightMainApply=MapMagic.Nodes.MatrixGenerators.HeightOutput200.ApplyType.SetHeights;
            var ts=Map.terrainSettings;
            ts.pixelError=PixelError;ts.baseMapDist=(int)BaseMapDistance;ts.drawInstanced=true;
            ts.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            ts.detailDraw=true;ts.detailDistance=DetailDistance;ts.detailDensity=.6f;
            ts.treeDistance=TreeDistance;ts.treeBillboardStart=TreeDistance;ts.treeFadeLength=20;ts.treeFullLod=120;ts.treeLODBiasMultiplier=1;
            ts.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
            Map.tiles.generateLimited=false;Map.tiles.generateInfinite=true;Map.tiles.genAroundMainCam=false;Map.tiles.genAroundTfms=true;Map.tiles.genAroundTfmsList=new[]{player};
            Map.draftsInPlaymode=true;Map.draftResolution=MapMagicObject.Resolution._65;Map.applyColliders=true;
            // Streamed tiles use the same Cozy terrain shader as the scene presentation material.
            Map.terrainSettings.material=Resources.Load<Material>("SR_MapMagicTerrain");
            root.SetActive(true);Map.tiles.Pin(new Coord(0,0),false,Map);Map.Refresh(true);
            Streamer=root.AddComponent<MapMagicResourceStreamer>();Streamer.Initialize(this,player);
            while(!TryHeight(SurvivalGraph.StartPosition.x,SurvivalGraph.StartPosition.z,out _)||!TryHeight(player.position.x,player.position.z,out _)){
                if(Time.realtimeSinceStartup-start>120){failed(new TimeoutException("MapMagic spawn tile did not complete within 120 seconds."));yield break;}
                progress?.Invoke(new WorldGenerationReport(WorldGenerationStage.GeneratingTerrain,Map.GetProgress()));yield return null;
            }
            TryHeight(SurvivalGraph.StartPosition.x,SurvivalGraph.StartPosition.z,out float y);
            var result=new WorldBuildResult{Root=transform,SpawnPosition=new Vector3(SurvivalGraph.StartPosition.x,y+.2f,SurvivalGraph.StartPosition.z),Seconds=Time.realtimeSinceStartup-start};
            complete(result);
        }

        /// <summary>Diagnostics for the F3 overlay / profiling: (main tiles, draft tiles, native tree instances, streamed objects).</summary>
        public (int mainTiles,int draftTiles,int treeInstances,int streamed) Stats()
        {
            int main=0,drafts=0,trees=0;
            if(Map!=null)foreach(var tile in Map.tiles.All()){
                if(tile.main!=null&&tile.main.terrain!=null&&tile.main.terrain.gameObject.activeInHierarchy){main++;var g=tile.GetComponent<MapMagicGameplayTile>();if(g!=null)trees+=g.TreeInstanceCount;}
                else if(tile.draft!=null&&tile.draft.terrain!=null&&tile.draft.terrain.gameObject.activeInHierarchy)drafts++;
            }
            return (main,drafts,trees,Streamer!=null?Streamer.ActiveObjects:0);
        }

        public void Stop(){if(Map!=null){Map.enabled=false;foreach(var tile in Map.tiles.All())tile.Stop();}}
        void OnDestroy(){Stop();if(Map!=null&&Map.graph!=null)Destroy(Map.graph);Prototypes?.Dispose();Prototypes=null;}
    }
}
