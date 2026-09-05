using System;
using System.Collections.Generic;
namespace SeedAndRock.Saves
{
    [Serializable] public sealed class ItemStackData
    {
        public string id;
        public int count;
        public ItemStackData() { }
        public ItemStackData(string item, int amount) { id = item; count = amount; }
        public ItemStackData Copy() => new ItemStackData(id, count);
    }
    [Serializable] public sealed class LootState
    {
        public string id; public int count; public float x, y, z;
    }
    [Serializable] public sealed class ExpeditionState
    {
        public ItemStackData[] slots;
        public int selected;
        public bool clothed;
        public float hour = 7f;
        public List<string> depleted = new List<string>();
        public List<LootState> loot = new List<LootState>();
        public ExpeditionState Copy() => new ExpeditionState {
            slots = slots == null ? null : Array.ConvertAll(slots, s => s?.Copy()),
            selected = selected, clothed = clothed, hour = hour,
            depleted = new List<string>(depleted ?? new List<string>()),
            loot = (loot ?? new List<LootState>()).ConvertAll(s => new LootState { id=s.id, count=s.count, x=s.x, y=s.y, z=s.z })
        };
    }
}
