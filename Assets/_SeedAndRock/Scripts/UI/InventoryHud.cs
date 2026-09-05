using SeedAndRock.Items;
using SeedAndRock.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace SeedAndRock.UI
{
    public sealed class InventoryHud : MonoBehaviour
    {
        GameObject root,backpack; TMP_Text prompt,notice,details;
        UnityEngine.UI.Image fade;
        readonly TMP_Text[] labels=new TMP_Text[30];
        readonly UnityEngine.UI.Image[] cards=new UnityEngine.UI.Image[30];
        PlayerExpedition player;int source=-1;bool split;
        void Start()
        {
            root=UiKit.CreateObject("Inventory HUD",transform);UiKit.Stretch(UiKit.RectOf(root));
            var belt=UiKit.CreateObject("Six slot toolbelt",root.transform);
            UiKit.Anchor(UiKit.RectOf(belt),new Vector2(.5f,0),new Vector2(588,92),new Vector2(0,24));
            for(int i=0;i<6;i++)Slot(belt.transform,i,new Vector2(i*98,0));
            backpack=UiKit.CreatePanel(root.transform,"Backpack",new Color(.045f,.065f,.065f,.97f),true,true).gameObject;
            UiKit.Anchor(UiKit.RectOf(backpack),new Vector2(.5f,.5f),new Vector2(1100,630));
            Text(backpack.transform,"INVENTORY",new Vector2(25,-20),new Vector2(600,42),26);
            for(int i=6;i<30;i++)Slot(backpack.transform,i,new Vector2(25+(i-6)%6*98,-85-(i-6)/6*100));
            details=Text(backpack.transform,"Click a stack, then a destination. Right-click splits.\nShift-click transfers between backpack and belt.\nQ drops the selected stack. LMB uses the held item.",new Vector2(25,-505),new Vector2(610,95),18);
            Text(backpack.transform,"CRAFTING",new Vector2(655,-20),new Vector2(420,42),26);
            int row=0;
            foreach(var d in ItemCatalog.All){if(d.recipe.Length==0)continue;string id=d.id;
                var button=UiKit.CreatePanel(backpack.transform,"Craft "+id,new Color(.13f,.2f,.19f),true,true);
                Place(button.rectTransform,new Vector2(655,-78-row*62),new Vector2(420,54));
                var click=button.gameObject.AddComponent<UnityEngine.UI.Button>();click.targetGraphic=button;click.onClick.AddListener(()=>{if(player!=null)player.Notify(player.Inventory.Craft(id)?"Crafted "+d.displayName:"Missing ingredients or inventory space.");});
                string costs="";foreach(var c in d.recipe)costs+=c.count+" "+c.id+"  ";
                var t=UiKit.CreateText(button.transform,"Recipe",d.displayName+"\n<size=15>"+costs+"</size>",19,Color.white);UiKit.Stretch(t.rectTransform,8,2,8,2);row++;
            }
            prompt=UiKit.CreateText(root.transform,"Interaction","",18,Color.white);UiKit.Anchor(prompt.rectTransform,new Vector2(.5f,.5f),new Vector2(800,40),new Vector2(0,-70));
            notice=UiKit.CreateText(root.transform,"Notice","",20,new Color(.85f,.92f,.76f));UiKit.Anchor(notice.rectTransform,new Vector2(.5f,0),new Vector2(1100,45),new Vector2(0,122));
            fade=UiKit.CreatePanel(transform,"Wake eyelids",Color.black,false,false);UiKit.Stretch(fade.rectTransform);fade.gameObject.SetActive(false);
        }
        static void Place(RectTransform rect,Vector2 pos,Vector2 size){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=pos;rect.sizeDelta=size;}
        static TMP_Text Text(Transform parent,string value,Vector2 p,Vector2 s,int size){var t=UiKit.CreateText(parent,value,value,size,Color.white,FontStyles.Normal,TextAlignmentOptions.TopLeft);Place(t.rectTransform,p,s);return t;}
        void Slot(Transform parent,int index,Vector2 position)
        {
            var card=UiKit.CreatePanel(parent,"Slot "+(index+1),new Color(.1f,.14f,.14f,.92f),true,true);Place(card.rectTransform,position,new Vector2(92,92));cards[index]=card;
            labels[index]=UiKit.CreateText(card.transform,"Item","",17,Color.white);UiKit.Stretch(labels[index].rectTransform,4,4,4,4);
            var events=card.gameObject.AddComponent<InventorySlotClick>();events.Click=(right)=>Click(index,right);
        }
        void Click(int i,bool right)
        {
            if(player==null)return;
            if(!player.InventoryOpen){player.Inventory.Select(i);return;}
            if(Keyboard.current?.leftShiftKey.isPressed==true){for(int j=i<6?6:0;j<(i<6?30:6);j++)if(player.Inventory.Slots[j]==null){player.Inventory.Move(i,j);break;}return;}
            if(source<0){source=i;split=right;}else{player.Inventory.Move(source,i,split);source=-1;}
        }
        void Update()
        {
            if(root==null)return;player=PlayerSpawner.Find()?.GetComponent<PlayerExpedition>();
            bool playing=SeedAndRockGameFlow.Instance?.State==GameFlowState.Playing;
            root.SetActive(playing&&player!=null);fade.gameObject.SetActive(player!=null&&player.Waking);if(player==null)return;
            fade.color=new Color(0,0,0,Mathf.Clamp01(player.WakeFade));if(!playing)return;
            backpack.SetActive(player.InventoryOpen);if(!player.InventoryOpen)source=-1;
            if(player.InventoryOpen&&source>=0&&Keyboard.current?.qKey.wasPressedThisFrame==true){player.Drop(source);source=-1;}
            prompt.text=player.InventoryOpen?"":player.Prompt;notice.text=player.Message+(player.Underwater?"   Oxygen "+Mathf.CeilToInt(player.Oxygen*100)+"%  |  Space rise / Ctrl dive":"");
            for(int i=0;i<30;i++){var s=player.Inventory.Slots[i];var d=ItemCatalog.Get(s?.id);
                labels[i].text=(i<6?"<size=13>"+(i+1)+"</size>\n":"")+(d==null?"—":d.displayName+"\n<size=15>x"+s.count+"</size>");
                cards[i].color=i==source?new Color(.4f,.35f,.16f):i==player.Inventory.Selected?new Color(.25f,.4f,.3f,.97f):new Color(.09f,.13f,.13f,.95f);
            }
        }
        void OnDestroy(){if(fade!=null)Destroy(fade.gameObject);}
    }
    public sealed class InventorySlotClick : MonoBehaviour,IPointerClickHandler
    {
        public System.Action<bool> Click;
        public void OnPointerClick(PointerEventData e)=>Click?.Invoke(e.button==PointerEventData.InputButton.Right);
    }
}
