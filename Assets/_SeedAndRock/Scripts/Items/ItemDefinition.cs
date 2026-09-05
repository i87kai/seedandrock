using System;
using System.Collections.Generic;
using UnityEngine;
namespace SeedAndRock.Items
{
    [Serializable] public struct Ingredient { public string id; public int count; public Ingredient(string i, int n) { id=i; count=n; } }
    [CreateAssetMenu(menuName="SeedAndRock/Item", fileName="Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string id, displayName;
        public int maxStack = 100;
        public Sprite icon;
        public GameObject worldPrefab, heldPrefab;
        public Color tint = Color.white;
        public float woodPower, stonePower, damage, nutrition, healing;
        public Ingredient[] recipe = Array.Empty<Ingredient>();
    }
    public static class ItemCatalog
    {
        static Dictionary<string, ItemDefinition> items;
        public static IEnumerable<ItemDefinition> All { get { Ensure(); return items.Values; } }
        public static ItemDefinition Get(string id) { Ensure(); return id != null && items.TryGetValue(id, out var item) ? item : null; }
        static void Ensure()
        {
            if (items != null) return;
            items = new Dictionary<string, ItemDefinition>();
            Add("wood", "Wood", 500, new Color(.48f,.29f,.12f));
            Add("stone", "Stone", 500, new Color(.56f,.61f,.65f));
            Add("cloth", "Cloth", 100, new Color(.84f,.81f,.62f));
            Add("berries", "Berries", 20, new Color(.7f,.16f,.28f), food:15);
            Add("mushroom", "Mushroom", 20, new Color(.85f,.55f,.3f), food:20);
            Add("meat", "Wild game", 20, new Color(.7f,.32f,.28f), food:25);
            Add("rock", "Rock", 1, Color.gray, 5, 5, 10);
            Add("torch", "Torch", 1, new Color(1,.6f,.15f), 1, 0, 5, recipe:new[]{new Ingredient("wood",20),new Ingredient("cloth",5)});
            Add("axe", "Stone axe", 1, new Color(.5f,.65f,.6f), 18, 2, 18, recipe:new[]{new Ingredient("wood",40),new Ingredient("stone",30)});
            Add("pickaxe", "Stone pickaxe", 1, new Color(.48f,.65f,.75f), 2, 18, 16, recipe:new[]{new Ingredient("wood",40),new Ingredient("stone",40)});
            Add("knife", "Stone knife", 1, new Color(.75f,.78f,.81f), 2, 1, 25, recipe:new[]{new Ingredient("stone",20),new Ingredient("cloth",5)});
            Add("spear", "Wooden spear", 1, new Color(.7f,.5f,.25f), 1, 0, 35, recipe:new[]{new Ingredient("wood",60)});
            Add("bandage", "Bandage", 10, new Color(.92f,.87f,.73f), heal:25, recipe:new[]{new Ingredient("cloth",10)});
            Add("shirt", "Cloth tunic", 1, new Color(.43f,.62f,.48f), recipe:new[]{new Ingredient("cloth",30)});
            foreach (var item in Resources.LoadAll<ItemDefinition>("Items")) if (!string.IsNullOrWhiteSpace(item.id)) items[item.id] = item;
        }
        static void Add(string id,string name,int stack,Color tint,float wood=0,float stone=0,float damage=0,float food=0,float heal=0,Ingredient[] recipe=null)
        {
            var d=ScriptableObject.CreateInstance<ItemDefinition>(); d.name=id; d.id=id; d.displayName=name; d.maxStack=stack; d.tint=tint;
            d.woodPower=wood; d.stonePower=stone; d.damage=damage; d.nutrition=food; d.healing=heal; d.recipe=recipe ?? Array.Empty<Ingredient>(); items[id]=d;
        }
    }
}
