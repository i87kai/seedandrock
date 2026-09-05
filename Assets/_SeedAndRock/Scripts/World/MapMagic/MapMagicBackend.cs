using System;
using System.Collections;
using Den.Tools;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Terrains;
using UnityEngine;
namespace SeedAndRock.World
{
    public sealed class MapMagicBackend : MonoBehaviour
    {
        public MapMagicObject Map {get;private set;}
        public Graph GraphAsset;
        public float SeaLevel=>SurvivalGraph.SeaLevel;
        void OnEnable(){TerrainTile.OnTileApplied+=TileApplied;TerrainTile.OnTileMoved+=TileMoved;}
        void OnDisable(){TerrainTile.OnTileApplied-=TileApplied;TerrainTile.OnTileMoved-=TileMoved;}
        void TileApplied(TerrainTile tile,MapMagic.Products.TileData data,MapMagic.Products.StopToken stop)
        {
            if(tile.mapMagic!=Map||data.isDraft||stop!=null&&stop.stop)return;
            var gameplay=tile.GetComponent<MapMagicGameplayTile>();if(gameplay==null)gameplay=tile.gameObject.AddComponent<MapMagicGameplayTile>();gameplay.Begin(tile);
        }
        void TileMoved(TerrainTile tile){if(tile.mapMagic==Map)tile.GetComponent<MapMagicGameplayTile>()?.Clear();}
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
                moisture=td.alphamapLayers>1?.4f+.5f*splats[0,0,1]:.4f,temperature=Mathf.Clamp01(.64f-height/500-snow*.15f),snow=snow,
                isWater=height<SeaLevel,waterSurface=SeaLevel,waterDistance=Mathf.Abs(height-SeaLevel)*5};
        }
        public IEnumerator Generate(int seed,Transform player,Action<WorldGenerationReport> progress,Action<WorldBuildResult> complete,Action<Exception> failed)
        {
            if(GraphAsset==null)GraphAsset=Resources.Load<Graph>("SR_MapMagicWorld");
            if(GraphAsset==null){failed(new InvalidOperationException("MapMagic graph asset is missing. Run SeedAndRock/MapMagic/Create world assets."));yield break;}
            float start=Time.realtimeSinceStartup;
            var root=new GameObject("MapMagic World");root.transform.SetParent(transform,false);root.SetActive(false);
            Map=root.AddComponent<MapMagicObject>();Map.graph=Instantiate(GraphAsset);Map.graph.random=new Noise(seed,32768);
            Map.tileSize=new Vector2D(SurvivalGraph.TileSize,SurvivalGraph.TileSize);Map.tileResolution=MapMagicObject.Resolution._257;
            Map.globals.height=SurvivalGraph.TerrainHeight;Map.mainRange=1;Map.tiles.generateRange=4;Map.tiles.retainMargin=1;
            Map.globals.heightMainApply=MapMagic.Nodes.MatrixGenerators.HeightOutput200.ApplyType.SetHeights;
            Map.terrainSettings.pixelError=3;
            Map.terrainSettings.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            Map.tiles.generateLimited=false;Map.tiles.generateInfinite=true;Map.tiles.genAroundMainCam=false;Map.tiles.genAroundTfms=true;Map.tiles.genAroundTfmsList=new[]{player};
            Map.draftsInPlaymode=true;Map.draftResolution=MapMagicObject.Resolution._65;Map.applyColliders=true;
            Map.terrainSettings.material=Resources.Load<Material>("SR_MapMagicTerrain");
            root.SetActive(true);Map.tiles.Pin(new Coord(0,0),false,Map);Map.Refresh(true);
            while(!TryHeight(player.position.x,player.position.z,out _)){
                if(Time.realtimeSinceStartup-start>120){failed(new TimeoutException("MapMagic spawn tile did not complete within 120 seconds."));yield break;}
                progress?.Invoke(new WorldGenerationReport(WorldGenerationStage.GeneratingTerrain,Map.GetProgress()));yield return null;
            }
            TryHeight(SurvivalGraph.StartPosition.x,SurvivalGraph.StartPosition.z,out float y);
            var result=new WorldBuildResult{Root=transform,SpawnPosition=new Vector3(SurvivalGraph.StartPosition.x,y+.2f,SurvivalGraph.StartPosition.z),Seconds=Time.realtimeSinceStartup-start};
            complete(result);
        }
        public void Stop(){if(Map!=null){Map.enabled=false;foreach(var tile in Map.tiles.All())tile.Stop();}}
        void OnDestroy(){Stop();if(Map!=null&&Map.graph!=null)Destroy(Map.graph);}
    }
}
