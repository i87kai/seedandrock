using SeedAndRock.Items;
using UnityEngine;
namespace SeedAndRock.World
{
    public sealed class Wildlife : MonoBehaviour
    {
        public string StableId;
        public int Species;
        public float Health=45;
        Vector3 home,target;float nextDecision;
        public static void Create(Transform parent,Vector3 position,int species,string id)
        {
            var root=new GameObject(new[]{"Deer","Boar","Rabbit"}[species]);root.transform.SetParent(parent,false);root.transform.position=position;
            var a=root.AddComponent<Wildlife>();a.Species=species;a.StableId=id;a.home=position;a.target=position;
            float scale=species==2?.4f:1;Color c=species==0?new Color(.48f,.3f,.16f):species==1?new Color(.26f,.23f,.21f):new Color(.65f,.58f,.47f);
            PlaceholderModels.Part(root.transform,PrimitiveType.Sphere,new Vector3(0,.85f,0)*scale,new Vector3(.7f,.8f,1.3f)*scale,c);
            PlaceholderModels.Part(root.transform,PrimitiveType.Sphere,new Vector3(0,1.25f,.65f)*scale,new Vector3(.4f,.55f,.5f)*scale,c);
            for(int i=0;i<4;i++)PlaceholderModels.Part(root.transform,PrimitiveType.Cylinder,new Vector3(i%2==0?-.23f:.23f,.35f,i<2?-.4f:.4f)*scale,new Vector3(.12f,.35f,.12f)*scale,c);
            if(species!=1)for(int i=0;i<2;i++)PlaceholderModels.Part(root.transform,PrimitiveType.Cylinder,new Vector3(i==0?-.15f:.15f,1.7f,.65f)*scale,new Vector3(.08f,.3f,.08f)*scale,c);
            var box=root.AddComponent<BoxCollider>();box.center=new Vector3(0,.9f,.15f)*scale;box.size=new Vector3(.8f,1.6f,1.8f)*scale;
        }
        void Update()
        {
            if(UI.SeedAndRockGameFlow.Instance?.State!=UI.GameFlowState.Playing)return;
            var player=Player.PlayerSpawner.Find();if(player==null || Vector3.SqrMagnitude(player.transform.position-transform.position)>160*160)return;
            if(Time.time>nextDecision){nextDecision=Time.time+4+Species;float angle=Time.time*.41f+home.x;
                target=home+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*18;
                if(Vector3.Distance(player.transform.position,transform.position)<7)target=transform.position+(transform.position-player.transform.position).normalized*12;
            }
            var delta=target-transform.position;delta.y=0;if(delta.magnitude<.7f)return;
            Vector3 next=transform.position+delta.normalized*Time.deltaTime*(Species==2?2:1.2f);
            var world=WorldGenerator.Active;if(world==null)return;
            if(world.TryGetWaterSurfaceAt(next.x,next.z,out _)||world.GetSlopeAt(next.x,next.z)>.4f){target=home;return;}
            next.y=world.GetHeightAt(next.x,next.z);transform.position=next;transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*3);
        }
        public void Hit(float damage)
        {
            Health-=damage;if(Health>0)return;
            ExpeditionWorld.Active.Depleted.Add(StableId);WorldLoot.Create("meat",Species==2?2:5,transform.position+Vector3.up*.25f,ExpeditionWorld.Active.transform);gameObject.SetActive(false);
        }
    }
}
