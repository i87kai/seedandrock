using System.Collections;
using System.Collections.Generic;
using MapMagic.Terrains;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>
    /// Converts the installed MapMagic detail outputs of one tile into gameplay data using Unity's native detail
    /// instance positions. Contains no scatter, noise, density, terrain or biome generation algorithm.
    ///
    /// Trees and stones become native terrain TreeInstances (instanced, distance culled, no GameObjects).
    /// Every placement is kept as a lightweight <see cref="Candidate"/>; <see cref="MapMagicResourceStreamer"/>
    /// turns only the candidates near the player into harvestable objects, plants and animals.
    /// </summary>
    public sealed class MapMagicGameplayTile : MonoBehaviour
    {
        public struct Candidate
        {
            public SurvivalGraph.Placement kind;
            public Vector3 position;
            public float rotationDegrees, scale, random;
            public string id;
            /// <summary>Index into terrainData.treeInstances for trees/stones, -1 otherwise.</summary>
            public int treeIndex;
        }

        public bool Ready {get;private set;}
        public Terrain Terrain {get;private set;}
        public readonly List<Candidate> Candidates=new List<Candidate>();
        public int TreeInstanceCount {get;private set;}

        public void Begin(TerrainTile tile,MapMagicPrototypes prototypes)
        {
            StopAllCoroutines();Ready=false;Candidates.Clear();TreeInstanceCount=0;
            Terrain=tile.main.terrain;
            StartCoroutine(Apply(tile,prototypes));
        }

        IEnumerator Apply(TerrainTile tile,MapMagicPrototypes prototypes)
        {
            Terrain t=tile.main.terrain;TerrainData data=t.terrainData;
            var depleted=ExpeditionWorld.Active!=null?ExpeditionWorld.Active.Depleted:null;
            var trees=new List<TreeInstance>(512);
            Vector3 size=data.size;Vector3 origin=t.transform.position;
            int processed=0;
            for(int layer=0;layer<data.detailPrototypes.Length;layer++){
                int kind=data.detailPrototypes[layer].noiseSeed-100;
                if(kind<0||kind>=(int)SurvivalGraph.Placement.Count)continue;
                var placement=(SurvivalGraph.Placement)kind;
                for(int z=0;z<data.detailPatchCount;z++)for(int x=0;x<data.detailPatchCount;x++){
                    var instances=data.ComputeDetailInstanceTransforms(x,z,layer,1,out _);
                    for(int i=0;i<instances.Length;i++){
                        var d=instances[i];
                        Vector3 p=origin+new Vector3(d.posX,0,d.posZ);
                        p.y=t.SampleHeight(p)+origin.y;
                        if(p.y<SurvivalGraph.SeaLevel+.6f)continue; // never in water, whatever the mask says
                        string id="mm2:"+tile.coord.x+":"+tile.coord.z+":"+kind+":"+x+":"+z+":"+i;
                        if(depleted!=null&&depleted.Contains(id))continue;
                        float u=Frac(d.rotationY*.1591549f+i*.137f); // deterministic 0..1 from the native transform
                        var c=new Candidate{kind=placement,position=p,rotationDegrees=d.rotationY*Mathf.Rad2Deg,random=u,treeIndex=-1};
                        if(placement==SurvivalGraph.Placement.ForestTree||placement==SurvivalGraph.Placement.ScatterTree||placement==SurvivalGraph.Placement.Stone){
                            bool stone=placement==SurvivalGraph.Placement.Stone;
                            bool lone=placement==SurvivalGraph.Placement.ScatterTree;
                            c.scale=stone?.8f+u*1.1f:lone?2.2f+u*1.3f:2.4f+Frac(u*3.7f)*1.4f;
                            int proto=stone?prototypes.PickRock(u):prototypes.PickTree(Frac(u*7.3f),lone);
                            c.treeIndex=trees.Count;
                            trees.Add(new TreeInstance{
                                position=new Vector3((p.x-origin.x)/size.x,(p.y-origin.y)/size.y,(p.z-origin.z)/size.z),
                                widthScale=c.scale,heightScale=c.scale,rotation=d.rotationY,color=Color.white,lightmapColor=Color.white,prototypeIndex=proto});
                        }
                        else c.scale=1;
                        c.id=id;Candidates.Add(c);
                        if(++processed%400==0)yield return null;
                    }
                }
                // The native output is a placement source only; the proxy meshes must never render.
                data.SetDetailLayer(0,0,layer,new int[data.detailHeight,data.detailWidth]);
            }
            data.treePrototypes=prototypes.TreePrototypes;
            if(data.treePrototypes.Length!=prototypes.TreePrototypes.Length)Debug.LogWarning("[SeedAndRock] Terrain rejected "+(prototypes.TreePrototypes.Length-data.treePrototypes.Length)+" tree prototypes; trees on tile "+tile.coord+" may be missing.");
            data.SetTreeInstances(trees.ToArray(),false);
            TreeInstanceCount=trees.Count;
            Ready=true;
        }

        /// <summary>Removes one native tree instance (harvested tree / mined stone) and fixes up candidate indices.</summary>
        public void RemoveTreeInstance(int treeIndex)
        {
            if(Terrain==null||Terrain.terrainData==null||treeIndex<0)return;
            var data=Terrain.terrainData;var all=data.treeInstances;
            if(treeIndex>=all.Length)return;
            var next=new TreeInstance[all.Length-1];
            for(int i=0,j=0;i<all.Length;i++)if(i!=treeIndex)next[j++]=all[i];
            data.SetTreeInstances(next,false);TreeInstanceCount=next.Length;
            for(int i=0;i<Candidates.Count;i++){var c=Candidates[i];if(c.treeIndex==treeIndex)c.treeIndex=-1;else if(c.treeIndex>treeIndex)c.treeIndex--;Candidates[i]=c;}
        }

        static float Frac(float v)=>v-Mathf.Floor(v);
        public void Clear(){StopAllCoroutines();Ready=false;Candidates.Clear();}
        void OnDestroy(){Clear();}
    }
}
