using System.Collections.Generic;
using SeedAndRock.Items;
using SeedAndRock.Saves;
using UnityEngine;
namespace SeedAndRock.World
{
    public sealed class ExpeditionWorld : MonoBehaviour
    {
        public static ExpeditionWorld Active {get;private set;}
        public readonly HashSet<string> Depleted=new HashSet<string>();
        public Vector3 SpawnPoint;
        public void Initialize(WorldBuildResult result)
        {
            Active=this;SpawnPoint=result.SpawnPosition;
            var marker=new GameObject("Starting area / Spawn point");marker.transform.SetParent(transform,false);marker.transform.position=SpawnPoint;
            PlaceholderModels.Part(marker.transform,PrimitiveType.Cube,new Vector3(0,-.14f,0),new Vector3(1.1f,.1f,2f),new Color(.44f,.37f,.25f));
            // Guaranteed nearby resources; all other placements follow biome moisture and temperature.
            string[] starter={"cloth","berries","mushroom","cloth","berries","cloth"};
            for(int i=0;i<starter.Length;i++)Plant(SpawnPoint.x-5+i*2,SpawnPoint.z+5+(i%2)*2,starter[i],"start-"+i);
            for(int i=0;i<2;i++) {
                float x=SpawnPoint.x+(i==0?-7:7),z=SpawnPoint.z+10;
                var p=new PlacementInstance{kind=i==0?PlacementKind.Tree:PlacementKind.Rock,x=x,y=WorldGenerator.Active.GetHeightAt(x,z),z=z,scale=i==0?2:1.4f,variant=1,biome=SeedAndRockBiome.Grassland};
                ResourceNode.CreateProp(transform,p,WorldGenerator.Active.Settings.ToPalette(),WorldGenerator.Active.Materials,"start-resource-"+i);
            }
            gameObject.AddComponent<DayNightCycle>();
            var ocean=new GameObject("Ocean");ocean.transform.SetParent(transform,false);ocean.AddComponent<MapMagicOcean>();
        }
        void Plant(float x,float z,string item,string id)
        {
            var s=WorldGenerator.Active.SampleSurface(x,z);if(s.isWater)return;
            var root=new GameObject(item=="cloth"?"Wild cotton":item=="berries"?"Berry bush":"Edible mushrooms");root.transform.SetParent(transform,false);root.transform.position=new Vector3(x,s.height,z);
            var node=root.AddComponent<ResourceNode>();node.ItemId=item;node.StableId=id;node.HandGather=true;node.Remaining=item=="cloth"?15:4;
            PlaceholderModels.Part(root.transform,PrimitiveType.Cylinder,new Vector3(0,.3f,0),new Vector3(.08f,.3f,.08f),new Color(.24f,.4f,.12f));
            for(int i=0;i<3;i++)PlaceholderModels.Part(root.transform,PrimitiveType.Sphere,new Vector3((i-1)*.2f,.5f+(i%2)*.2f,0),item=="mushroom"?new Vector3(.35f,.12f,.3f):Vector3.one*.27f,ItemCatalog.Get(item).tint);
            root.AddComponent<BoxCollider>().center=new Vector3(0,.4f,0);root.GetComponent<BoxCollider>().size=new Vector3(.9f,.9f,.7f);
        }
        public void Restore(ExpeditionState state)
        {
            Depleted.Clear();if(state?.depleted!=null)foreach(string id in state.depleted)Depleted.Add(id);
            foreach(var n in GetComponentsInChildren<ResourceNode>(true))if(Depleted.Contains(n.StableId))n.gameObject.SetActive(false);
            // Props are siblings of this component under the generated root.
            foreach(var n in transform.parent.GetComponentsInChildren<ResourceNode>(true))if(Depleted.Contains(n.StableId))n.gameObject.SetActive(false);
            foreach(var a in GetComponentsInChildren<Wildlife>())if(Depleted.Contains(a.StableId))a.gameObject.SetActive(false);
            if(state?.loot!=null)foreach(var l in state.loot)WorldLoot.Create(l.id,l.count,new Vector3(l.x,l.y,l.z),transform);
            GetComponent<DayNightCycle>().Hour=state?.hour ?? 7f;
        }
        public void Capture(ExpeditionState state)
        {
            state.depleted=new List<string>(Depleted);state.hour=GetComponent<DayNightCycle>().Hour;state.loot=new List<LootState>();
            foreach(var l in GetComponentsInChildren<WorldLoot>())if(l.Count>0){var p=l.transform.position;state.loot.Add(new LootState{id=l.ItemId,count=l.Count,x=p.x,y=p.y,z=p.z});}
        }
        void OnDestroy(){if(Active==this)Active=null;}
    }
}

