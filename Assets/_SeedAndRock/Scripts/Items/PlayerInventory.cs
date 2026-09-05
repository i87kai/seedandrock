using System;
using SeedAndRock.Saves;
using UnityEngine;
namespace SeedAndRock.Items
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        public const int BeltSize=6, SlotCount=30;
        public ItemStackData[] Slots { get; private set; } = new ItemStackData[SlotCount];
        public int Selected { get; private set; }
        public bool Clothed;
        public event Action Changed;
        public ItemDefinition Equipped => ItemCatalog.Get(Slots[Selected]?.id);
        public void Select(int slot) { Selected=(slot%BeltSize+BeltSize)%BeltSize; Changed?.Invoke(); }
        public void Restore(ExpeditionState state)
        {
            Slots=new ItemStackData[SlotCount]; Clothed=state?.clothed ?? false;
            if(state?.slots == null) { Slots[0]=new ItemStackData("rock",1); Slots[1]=new ItemStackData("torch",1); }
            else for(int i=0;i<Math.Min(SlotCount,state.slots.Length);i++) {
                var s=state.slots[i]; var d=ItemCatalog.Get(s?.id);
                if(d!=null && s.count>0) Slots[i]=new ItemStackData(s.id,Math.Min(s.count,Math.Max(1,d.maxStack)));
            }
            Select(state?.selected ?? 0);
        }
        public int Count(string id) { int n=0; foreach(var s in Slots) if(s?.id==id)n+=s.count; return n; }
        public int Add(string id,int count)
        {
            var d=ItemCatalog.Get(id); if(d==null || count<=0)return count;
            int left=count, max=Math.Max(1,d.maxStack);
            for(int pass=0;pass<2;pass++) for(int j=0;j<SlotCount && left>0;j++) {
                int i=(j+BeltSize)%SlotCount; var s=Slots[i];
                if(pass==0 && s?.id==id) {int n=Math.Min(left,max-s.count);s.count+=n;left-=n;}
                if(pass==1 && s==null) {int n=Math.Min(left,max);Slots[i]=new ItemStackData(id,n);left-=n;}
            }
            Changed?.Invoke(); return left;
        }
        public bool Remove(string id,int amount)
        {
            if(amount<0 || Count(id)<amount)return false;
            for(int i=0;i<SlotCount && amount>0;i++) if(Slots[i]?.id==id){int n=Math.Min(amount,Slots[i].count);Slots[i].count-=n;amount-=n;if(Slots[i].count==0)Slots[i]=null;}
            Changed?.Invoke();return true;
        }
        public ItemStackData Take(int slot,int amount=int.MaxValue)
        {
            if(slot<0 || slot>=SlotCount || Slots[slot]==null)return null;
            var s=Slots[slot]; int n=Math.Min(s.count,amount); if(n<=0)return null;
            var result=new ItemStackData(s.id,n);s.count-=n;if(s.count==0)Slots[slot]=null;Changed?.Invoke();return result;
        }
        public void Move(int from,int to,bool split=false)
        {
            if(from<0 || to<0 || from>=SlotCount || to>=SlotCount || from==to || Slots[from]==null)return;
            var a=Slots[from];var b=Slots[to];int amount=split ? (a.count+1)/2 : a.count;
            if(b==null){Slots[to]=new ItemStackData(a.id,amount);a.count-=amount;}
            else if(b.id==a.id){int n=Math.Min(amount,Math.Max(1,ItemCatalog.Get(a.id).maxStack)-b.count);b.count+=n;a.count-=n;}
            else if(!split){Slots[from]=b;Slots[to]=a;}
            if(a.count==0)Slots[from]=null; Changed?.Invoke();
        }
        public bool Craft(string id)
        {
            var d=ItemCatalog.Get(id);if(d==null || d.recipe.Length==0)return false;
            var before=Array.ConvertAll(Slots,s=>s?.Copy());
            foreach(var cost in d.recipe) if(!Remove(cost.id,cost.count)){Slots=before;Changed?.Invoke();return false;}
            if(Add(id,1)>0){Slots=before;Changed?.Invoke();return false;}return true;
        }
    }
}
