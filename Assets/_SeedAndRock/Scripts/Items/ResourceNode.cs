using SeedAndRock.World;
using UnityEngine;
namespace SeedAndRock.Items
{
    public sealed class ResourceNode : MonoBehaviour
    {
        public string StableId, ItemId;
        public int Remaining=80;
        public bool HandGather;
        public string Label => ItemCatalog.Get(ItemId)?.displayName ?? ItemId;
        public int Harvest(PlayerInventory inventory,ItemDefinition tool)
        {
            if(Remaining<=0)return 0;
            float power=HandGather ? Remaining : tool==null ? 0 : ItemId=="wood" ? tool.woodPower : tool.stonePower;
            int requested=Mathf.Min(Remaining,Mathf.FloorToInt(power)); if(requested<=0)return 0;
            int received=requested-inventory.Add(ItemId,requested); Remaining-=received;
            if(Remaining==0){ ExpeditionWorld.Active?.Depleted.Add(StableId);gameObject.SetActive(false); }
            return received;
        }
        public static void CreateProp(Transform parent,PlacementInstance p,WorldGenerationPalette palette,WorldMaterials materials,string id)
        {
            bool tree=p.kind==PlacementKind.Tree;
            var go=new GameObject(tree?"Harvestable tree":"Stone deposit");go.transform.SetParent(parent,false);
            go.transform.position=new Vector3(p.x,p.y,p.z);
            p.x=p.y=p.z=0;
            var node=go.AddComponent<ResourceNode>();node.StableId=id;node.ItemId=tree?"wood":"stone";node.Remaining=tree?100:80;
            var a=new MeshData("Harvest mesh");var b=new MeshData("Canopy");
            if(tree)PropMeshBuilder.AppendTree(a,b,p,palette);else PropMeshBuilder.AppendRock(a,p);
            AddMesh(go.transform,a,tree?materials.trunk:materials.rock,true);
            if(tree)AddMesh(go.transform,b,materials.foliage,false);
        }
        static void AddMesh(Transform parent,MeshData data,Material mat,bool collide)
        {
            var go=new GameObject(data.Name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            Mesh mesh=data.ToMesh();go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            go.AddComponent<OwnedMesh>();if(collide)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
    public sealed class OwnedMesh : MonoBehaviour
    {
        void OnDestroy(){var f=GetComponent<MeshFilter>();if(f!=null && f.sharedMesh!=null)Destroy(f.sharedMesh);}
    }
    public sealed class WorldLoot : MonoBehaviour
    {
        public string ItemId; public int Count;
        public void Pickup(PlayerInventory inventory) {Count=inventory.Add(ItemId,Count);if(Count<=0){gameObject.SetActive(false);Destroy(gameObject);}}
        public static WorldLoot Create(string id,int count,Vector3 position,Transform parent)
        {
            var def=ItemCatalog.Get(id);if(def==null || count<=0)return null;
            GameObject go=def.worldPrefab!=null?Instantiate(def.worldPrefab):PlaceholderModels.Item(def,false);
            go.name="Dropped "+def.displayName;go.transform.SetParent(parent,true);go.transform.position=position;
            if(go.GetComponentInChildren<Collider>()==null)go.AddComponent<SphereCollider>().radius=.25f;
            var loot=go.AddComponent<WorldLoot>();loot.ItemId=id;loot.Count=count;return loot;
        }
    }
}
