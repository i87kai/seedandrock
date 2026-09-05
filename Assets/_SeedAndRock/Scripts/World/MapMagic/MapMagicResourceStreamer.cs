using System.Collections.Generic;
using SeedAndRock.Items;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>
    /// Streams gameplay objects around the player from the per-tile candidate lists produced by
    /// <see cref="MapMagicGameplayTile"/>. Trees and stones are rendered by the terrain everywhere; this only
    /// adds a pooled collider + ResourceNode to the ones within reach, spawns small plants in a short radius
    /// and keeps a handful of animals alive nearby. Nothing here decides *where* things are.
    /// </summary>
    public sealed class MapMagicResourceStreamer : MonoBehaviour
    {
        public float HarvestRadius=55, PlantRadius=70, AnimalRadius=140, AnimalDespawnRadius=220;
        public int MaxAnimals=10;
        public float Interval=.35f;

        MapMagicBackend backend;Transform player;float next;
        readonly Dictionary<string,GameObject> active=new Dictionary<string,GameObject>();
        readonly Dictionary<string,(MapMagicGameplayTile tile,int candidate)> activeSource=new Dictionary<string,(MapMagicGameplayTile,int)>();
        readonly Stack<GameObject> proxyPool=new Stack<GameObject>();
        readonly List<string> toRemove=new List<string>();
        Transform content;
        int animals;

        public int ActiveObjects=>active.Count;
        public int ActiveAnimals=>animals;

        public void Initialize(MapMagicBackend backend,Transform player){this.backend=backend;this.player=player;content=new GameObject("Nearby gameplay objects").transform;content.SetParent(transform,false);}

        void LateUpdate()
        {
            if(backend==null||backend.Map==null||player==null||Time.time<next)return;
            next=Time.time+Interval;
            Vector3 pp=player.position;
            var depleted=ExpeditionWorld.Active!=null?ExpeditionWorld.Active.Depleted:null;

            // Release objects that are depleted, dead, or now too far.
            toRemove.Clear();
            foreach(var kv in active){
                var go=kv.Value;var (tile,ci)=activeSource[kv.Key];
                bool gone=go==null||!go.activeSelf||(depleted!=null&&depleted.Contains(kv.Key));
                if(gone){
                    if(tile!=null&&ci<tile.Candidates.Count){var c=tile.Candidates[ci];if(c.treeIndex>=0)tile.RemoveTreeInstance(c.treeIndex);}
                    toRemove.Add(kv.Key);continue;
                }
                float limit=go.GetComponent<Wildlife>()!=null?AnimalDespawnRadius:go.GetComponent<ResourceNode>()?.HandGather==true?PlantRadius+15:HarvestRadius+10;
                if(Vector3.SqrMagnitude(go.transform.position-pp)>limit*limit)toRemove.Add(kv.Key);
            }
            foreach(var id in toRemove)Release(id);

            // Activate candidates in range on the tiles around the player.
            foreach(var tile in backend.Map.tiles.All()){
                var gameplay=tile.GetComponent<MapMagicGameplayTile>();
                if(gameplay==null||!gameplay.Ready)continue;
                var terrain=gameplay.Terrain;if(terrain==null)continue;
                Vector3 o=terrain.transform.position;Vector3 s=terrain.terrainData.size;
                float dx=Mathf.Max(o.x-pp.x,0,pp.x-(o.x+s.x)),dz=Mathf.Max(o.z-pp.z,0,pp.z-(o.z+s.z));
                if(dx*dx+dz*dz>AnimalRadius*AnimalRadius)continue;
                var list=gameplay.Candidates;
                for(int i=0;i<list.Count;i++){
                    var c=list[i];
                    if(active.ContainsKey(c.id))continue;
                    float r=c.kind==SurvivalGraph.Placement.Animal?AnimalRadius:c.kind>=SurvivalGraph.Placement.Cloth&&c.kind<=SurvivalGraph.Placement.Mushroom?PlantRadius:HarvestRadius;
                    if(Vector3.SqrMagnitude(c.position-pp)>r*r)continue;
                    if(depleted!=null&&depleted.Contains(c.id))continue;
                    if(c.kind==SurvivalGraph.Placement.Animal&&animals>=MaxAnimals)continue;
                    if(c.treeIndex<0&&(c.kind==SurvivalGraph.Placement.ForestTree||c.kind==SurvivalGraph.Placement.ScatterTree||c.kind==SurvivalGraph.Placement.Stone))continue; // already harvested this session
                    Spawn(c,gameplay,i);
                }
            }
        }

        void Spawn(MapMagicGameplayTile.Candidate c,MapMagicGameplayTile tile,int index)
        {
            GameObject go;
            switch(c.kind){
                case SurvivalGraph.Placement.ForestTree:case SurvivalGraph.Placement.ScatterTree:case SurvivalGraph.Placement.Stone:{
                    bool stone=c.kind==SurvivalGraph.Placement.Stone;
                    go=proxyPool.Count>0?proxyPool.Pop():NewProxy();
                    go.name=stone?"Stone deposit":"Harvestable tree";
                    go.transform.SetPositionAndRotation(c.position,Quaternion.Euler(0,c.rotationDegrees,0));
                    var node=go.GetComponent<ResourceNode>();node.StableId=c.id;node.ItemId=stone?"stone":"wood";node.Remaining=stone?80:100;node.HandGather=false;
                    var col=go.GetComponent<CapsuleCollider>();
                    if(stone){col.radius=.9f*c.scale;col.height=1.2f*c.scale;col.center=new Vector3(0,.4f*c.scale,0);}
                    else{col.radius=.28f*c.scale+.12f;col.height=5.5f*c.scale;col.center=new Vector3(0,col.height*.5f,0);}
                    go.SetActive(true);break;}
                case SurvivalGraph.Placement.Animal:
                    Wildlife.Create(content,c.position,Mathf.Min((int)(c.random*3),2),c.id);
                    go=content.GetChild(content.childCount-1).gameObject;animals++;break;
                default:
                    CreatePlant(content,c.position,c.kind==SurvivalGraph.Placement.Cloth?"cloth":c.kind==SurvivalGraph.Placement.Berries?"berries":"mushroom",c.id);
                    go=content.GetChild(content.childCount-1).gameObject;break;
            }
            active[c.id]=go;activeSource[c.id]=(tile,index);
        }

        GameObject NewProxy()
        {
            var go=new GameObject("Harvest proxy",typeof(CapsuleCollider),typeof(ResourceNode));
            go.transform.SetParent(content,false);go.SetActive(false);return go;
        }

        void Release(string id)
        {
            if(active.TryGetValue(id,out var go)&&go!=null){
                if(go.GetComponent<Wildlife>()!=null){animals=Mathf.Max(0,animals-1);Destroy(go);}
                else if(go.GetComponent<CapsuleCollider>()!=null&&go.transform.childCount==0){go.SetActive(false);proxyPool.Push(go);}
                else Destroy(go);
            }
            active.Remove(id);activeSource.Remove(id);
        }

        public static void CreatePlant(Transform parent,Vector3 p,string item,string id)
        {
            var root=new GameObject(item=="cloth"?"Wild cotton":item=="berries"?"Berry bush":"Edible mushrooms");root.transform.SetParent(parent,false);root.transform.position=p;
            var n=root.AddComponent<ResourceNode>();n.StableId=id;n.ItemId=item;n.HandGather=true;n.Remaining=item=="cloth"?15:4;
            var stem=PlaceholderModels.Part(root.transform,PrimitiveType.Cylinder,new Vector3(0,.25f,0),new Vector3(.07f,.25f,.07f),new Color(.23f,.35f,.1f));
            for(int i=0;i<3;i++)PlaceholderModels.Part(root.transform,PrimitiveType.Sphere,new Vector3((i-1)*.2f,.45f+(i%2)*.1f,0),item=="mushroom"?new Vector3(.32f,.14f,.28f):Vector3.one*.27f,ItemCatalog.Get(item).tint);
            var c=root.AddComponent<BoxCollider>();c.center=new Vector3(0,.4f,0);c.size=new Vector3(.8f,.8f,.6f);
        }

        void OnDestroy(){active.Clear();activeSource.Clear();}
    }
}
