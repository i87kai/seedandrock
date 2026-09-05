using System.Collections;
using SeedAndRock.Items;
using MapMagic.Terrains;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>Converts installed MapMagic detail outputs into gameplay entities using Unity's native instance positions.
    /// Contains no scatter, noise, density, terrain, or biome generation algorithm.</summary>
    public sealed class MapMagicGameplayTile : MonoBehaviour
    {
        public bool Ready {get;private set;}
        GameObject content;
        public void Begin(TerrainTile tile)
        {
            StopAllCoroutines();if(content!=null){content.SetActive(false);Destroy(content);}Ready=false;
            content=new GameObject("MapMagic gameplay instances");content.transform.SetParent(tile.main.terrain.transform,false);
            StartCoroutine(Apply(tile));
        }
        IEnumerator Apply(TerrainTile tile)
        {
            Terrain t=tile.main.terrain;TerrainData data=t.terrainData;
            var world=WorldGenerator.Active;var palette=world.Settings.ToPalette();var materials=world.Materials;
            int generated=0;
            for(int layer=0;layer<data.detailPrototypes.Length;layer++){
                int kind=data.detailPrototypes[layer].noiseSeed-100;if(kind<0||kind>5)continue;
                for(int z=0;z<data.detailPatchCount;z++)for(int x=0;x<data.detailPatchCount;x++){
                    var instances=data.ComputeDetailInstanceTransforms(x,z,layer,1,out _);
                    for(int i=0;i<instances.Length;i++){
                        var d=instances[i];Vector3 p=t.transform.position+new Vector3(d.posX,d.posY,d.posZ);
                        p.y=t.SampleHeight(p)+t.transform.position.y;
                        string id="mm1:"+tile.coord.x+":"+tile.coord.z+":"+kind+":"+x+":"+z+":"+i;
                        if(ExpeditionWorld.Active!=null&&ExpeditionWorld.Active.Depleted.Contains(id))continue;
                        if(kind<2){var prop=new PlacementInstance{kind=kind==0?PlacementKind.Tree:PlacementKind.Rock,x=p.x,y=p.y,z=p.z,scale=kind==0?2.5f+(d.rotationY%1)*1.2f:.9f+(d.rotationY%1),rotationDegrees=d.rotationY*Mathf.Rad2Deg,variant=i%3==0?0:1,variation=d.rotationY/7,biome=SeedAndRockBiome.Forest};ResourceNode.CreateProp(content.transform,prop,palette,materials,id);}
                        else if(kind==5)Wildlife.Create(content.transform,p,i%3,id);
                        else CreatePlant(content.transform,p,kind==2?"cloth":kind==3?"berries":"mushroom",id);
                        if(++generated%24==0)yield return null;
                    }
                }
                // The native output is a placement source; avoid rendering proxy meshes a second time.
                data.SetDetailLayer(0,0,layer,new int[data.detailHeight,data.detailWidth]);
            }
            Ready=true;
        }
        public static void CreatePlant(Transform parent,Vector3 p,string item,string id)
        {
            var root=new GameObject(item=="cloth"?"Wild cotton":item=="berries"?"Berry bush":"Edible mushrooms");root.transform.SetParent(parent,false);root.transform.position=p;
            var n=root.AddComponent<ResourceNode>();n.StableId=id;n.ItemId=item;n.HandGather=true;n.Remaining=item=="cloth"?15:4;
            var stem=PlaceholderModels.Part(root.transform,PrimitiveType.Cylinder,new Vector3(0,.25f,0),new Vector3(.07f,.25f,.07f),new Color(.23f,.35f,.1f));
            for(int i=0;i<3;i++)PlaceholderModels.Part(root.transform,PrimitiveType.Sphere,new Vector3((i-1)*.2f,.45f+(i%2)*.1f,0),item=="mushroom"?new Vector3(.32f,.14f,.28f):Vector3.one*.27f,ItemCatalog.Get(item).tint);
            var c=root.AddComponent<BoxCollider>();c.center=new Vector3(0,.4f,0);c.size=new Vector3(.8f,.8f,.6f);
        }
        public void Clear(){StopAllCoroutines();Ready=false;if(content!=null){content.SetActive(false);Destroy(content);}}
        void OnDestroy(){Clear();}
    }
}
