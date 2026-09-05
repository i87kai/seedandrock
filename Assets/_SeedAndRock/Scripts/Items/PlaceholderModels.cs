using System.Collections.Generic;
using UnityEngine;
namespace SeedAndRock.Items
{
    public static class PlaceholderModels
    {
        static readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        public static GameObject Part(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            var p=GameObject.CreatePrimitive(type);p.transform.SetParent(parent,false);p.transform.localPosition=position;p.transform.localScale=scale;
            var collider=p.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
            if(!materials.TryGetValue(color,out var mat)||mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;mat.enableInstancing=true;materials[color]=mat;}
            p.GetComponent<Renderer>().sharedMaterial=mat;return p;
        }
        public static GameObject Item(ItemDefinition d,bool held)
        {
            var root=new GameObject(d.displayName);var brown=new Color(.32f,.19f,.09f);
            bool shaft=d.id=="torch"||d.id=="axe"||d.id=="pickaxe"||d.id=="spear"||d.id=="knife";
            if(shaft){
                Part(root.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.065f,d.id=="spear"?.7f:.26f,.065f),brown);
                var head=Part(root.transform,d.id=="torch"?PrimitiveType.Sphere:PrimitiveType.Cube,new Vector3(0,.24f,0),d.id=="pickaxe"?new Vector3(.42f,.08f,.08f):new Vector3(.2f,.18f,.09f),d.tint);
                head.transform.localRotation=Quaternion.Euler(0,0,d.id=="knife"?0:20);
            } else Part(root.transform,PrimitiveType.Sphere,Vector3.zero,d.id=="rock"?new Vector3(.28f,.21f,.23f):new Vector3(.23f,.16f,.2f),d.tint);
            return root;
        }
    }
}
