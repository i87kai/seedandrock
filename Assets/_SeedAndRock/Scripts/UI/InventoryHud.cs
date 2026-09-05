using SeedAndRock.Items;
using SeedAndRock.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace SeedAndRock.UI
{
    /// <summary>Toolbelt, backpack + crafting panel, interaction prompt and notices. Styling follows
    /// <see cref="SeedAndRockTheme"/>; behaviour (select / move / split / shift-transfer / drop / craft) is unchanged.</summary>
    public sealed class InventoryHud : MonoBehaviour
    {
        const float SlotSize=80, SlotGap=10, BeltPad=12;
        static readonly Color SlotFill=new Color(.05f,.10f,.11f,.92f), SlotFillBackpack=new Color(.07f,.13f,.14f,1f);
        static readonly Color SlotBorder=new Color(.18f,.36f,.37f,.9f), SlotBorderSelected=SeedAndRockTheme.Gold, SlotBorderSource=new Color(1f,.86f,.45f,1f);
        static readonly Color SlotSelectedFill=new Color(.10f,.22f,.21f,1f), SlotSourceFill=new Color(.28f,.24f,.11f,1f);

        GameObject root,backpack; TMP_Text prompt,notice,details;
        UnityEngine.UI.Image fade,promptPill;
        readonly TMP_Text[] labels=new TMP_Text[30];
        readonly TMP_Text[] counts=new TMP_Text[30];
        readonly UnityEngine.UI.Image[] cards=new UnityEngine.UI.Image[30];
        readonly UnityEngine.UI.Image[] borders=new UnityEngine.UI.Image[30];
        PlayerExpedition player;int source=-1;bool split;

        void Start()
        {
            root=UiKit.CreateObject("Inventory HUD",transform);UiKit.Stretch(UiKit.RectOf(root));

            // --- toolbelt: a soft shadow, a dark frame and six framed slots -----------------------------
            float beltWidth=6*SlotSize+5*SlotGap+2*BeltPad, beltHeight=SlotSize+2*BeltPad;
            var beltShadow=UiKit.CreatePanel(root.transform,"Toolbelt shadow",new Color(0,0,0,.35f));beltShadow.sprite=UiKit.Soft;
            UiKit.Anchor(beltShadow.rectTransform,new Vector2(.5f,0),new Vector2(beltWidth+30,beltHeight+30),new Vector2(0,18));
            var belt=UiKit.CreatePanel(root.transform,"Six slot toolbelt",new Color(.02f,.05f,.06f,.78f),true,false);
            UiKit.Anchor(belt.rectTransform,new Vector2(.5f,0),new Vector2(beltWidth,beltHeight),new Vector2(0,22));
            for(int i=0;i<6;i++)Slot(belt.transform,i,new Vector2(BeltPad+i*(SlotSize+SlotGap),-BeltPad),SlotFill);

            // --- backpack + crafting card ------------------------------------------------------------------
            backpack=UiKit.CreateObject("Backpack",root.transform);UiKit.Stretch(UiKit.RectOf(backpack));
            var card=UiKit.CreateCard(backpack.transform,"Backpack card",new Vector2(1100,640),new Vector2(.5f,.5f),SeedAndRockTheme.Panel);
            var panel=card;
            Tab(panel,"INVENTORY",new Vector2(24,-18),new Vector2(190,34));
            Tab(panel,"CRAFTING",new Vector2(652,-18),new Vector2(170,34));
            var divider=UiKit.CreatePanel(panel,"Divider",new Color(SeedAndRockTheme.Border.r,SeedAndRockTheme.Border.g,SeedAndRockTheme.Border.b,.5f),false);
            Place(divider.rectTransform,new Vector2(626,-18),new Vector2(1,600));
            Text(panel,"Belt slots 1-6 are the row above; everything else rides in the pack.",new Vector2(24,-58),new Vector2(580,22),SeedAndRockTheme.SmallSize,SeedAndRockTheme.Muted);
            for(int i=6;i<30;i++)Slot(panel,i,new Vector2(24+(i-6)%6*(SlotSize+SlotGap),-92-(i-6)/6*(SlotSize+SlotGap)),SlotFillBackpack);
            details=Text(panel,"<b>Click</b> a stack, then a destination.  <b>Right-click</b> splits.\n<b>Shift-click</b> moves between pack and belt.  <b>Q</b> drops the selected stack.  <b>LMB</b> uses the held item.",new Vector2(24,-470),new Vector2(580,110),SeedAndRockTheme.SmallSize+1,SeedAndRockTheme.Muted);

            int row=0;
            foreach(var d in ItemCatalog.All){if(d.recipe.Length==0)continue;string id=d.id;
                var recipeBorder=UiKit.CreatePanel(panel,"Craft border",SeedAndRockTheme.Border,true,false);
                Place(recipeBorder.rectTransform,new Vector2(651,-59-row*64),new Vector2(426,58));
                var button=UiKit.CreatePanel(panel,"Craft "+id,SeedAndRockTheme.PanelRaised,true,true);
                Place(button.rectTransform,new Vector2(652,-60-row*64),new Vector2(424,56));
                var click=button.gameObject.AddComponent<UnityEngine.UI.Button>();click.targetGraphic=button;
                var colors=click.colors;colors.highlightedColor=new Color(1.15f,1.15f,1.15f);colors.pressedColor=new Color(.85f,.85f,.85f);colors.fadeDuration=.08f;click.colors=colors;
                click.onClick.AddListener(()=>{if(player!=null)player.Notify(player.Inventory.Craft(id)?"Crafted "+d.displayName:"Missing ingredients or inventory space.");});
                var accent=UiKit.CreatePanel(button.transform,"Accent",SeedAndRockTheme.Teal,true,false);Place(accent.rectTransform,new Vector2(0,0),new Vector2(5,56));
                string costs="";foreach(var c in d.recipe)costs+=(costs.Length>0?"   ":"")+c.count+"× "+(ItemCatalog.Get(c.id)?.displayName??c.id);
                var name=UiKit.CreateText(button.transform,"Name",d.displayName,SeedAndRockTheme.BodySize,SeedAndRockTheme.Pale,FontStyles.Bold,TextAlignmentOptions.TopLeft);Place(name.rectTransform,new Vector2(16,-8),new Vector2(300,24));
                var cost=UiKit.CreateText(button.transform,"Cost",costs,SeedAndRockTheme.SmallSize,SeedAndRockTheme.Muted,FontStyles.Normal,TextAlignmentOptions.BottomLeft);Place(cost.rectTransform,new Vector2(16,-30),new Vector2(390,20));
                var craft=UiKit.CreateText(button.transform,"Craft","CRAFT",SeedAndRockTheme.LabelSize,SeedAndRockTheme.Gold,FontStyles.Bold,TextAlignmentOptions.Right);craft.characterSpacing=4;Place(craft.rectTransform,new Vector2(310,-8),new Vector2(100,24));
                row++;
            }

            // --- prompt pill + notice ------------------------------------------------------------------------
            promptPill=UiKit.CreatePanel(root.transform,"Interaction pill",new Color(.02f,.05f,.06f,.7f),true,false);
            UiKit.Anchor(promptPill.rectTransform,new Vector2(.5f,.5f),new Vector2(420,38),new Vector2(0,-64));
            prompt=UiKit.CreateText(promptPill.transform,"Interaction","",SeedAndRockTheme.BodySize-1,SeedAndRockTheme.Pale,FontStyles.Bold);UiKit.Stretch(prompt.rectTransform,14,0,14,0);prompt.textWrappingMode=TextWrappingModes.NoWrap;
            notice=UiKit.CreateText(root.transform,"Notice","",SeedAndRockTheme.BodySize+1,new Color(.95f,.9f,.72f),FontStyles.Bold);UiKit.Anchor(notice.rectTransform,new Vector2(.5f,0),new Vector2(1100,45),new Vector2(0,beltHeight+52));
            fade=UiKit.CreatePanel(transform,"Wake eyelids",Color.black,false,false);UiKit.Stretch(fade.rectTransform);fade.gameObject.SetActive(false);
        }

        static void Place(RectTransform rect,Vector2 pos,Vector2 size){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=pos;rect.sizeDelta=size;}
        static TMP_Text Text(Transform parent,string value,Vector2 p,Vector2 s,float size,Color color){var t=UiKit.CreateText(parent,"Text",value,size,color,FontStyles.Normal,TextAlignmentOptions.TopLeft);Place(t.rectTransform,p,s);return t;}
        /// <summary>Section header drawn as a teal tab chip so the two halves of the panel read as organised areas.</summary>
        static void Tab(Transform parent,string caption,Vector2 pos,Vector2 size)
        {
            var chip=UiKit.CreatePanel(parent,"Tab "+caption,SeedAndRockTheme.TealDeep,true,false);Place(chip.rectTransform,pos,size);
            var label=UiKit.CreateLabel(chip.transform,caption,SeedAndRockTheme.Pale,TextAlignmentOptions.Center);UiKit.Stretch(label.rectTransform,10,0,10,0);
            var underline=UiKit.CreatePanel(parent,"Tab line",new Color(SeedAndRockTheme.Teal.r,SeedAndRockTheme.Teal.g,SeedAndRockTheme.Teal.b,.55f),false);Place(underline.rectTransform,pos+new Vector2(0,-size.y-4),new Vector2(size.x,2));
        }

        void Slot(Transform parent,int index,Vector2 position,Color fill)
        {
            var border=UiKit.CreatePanel(parent,"Slot border "+(index+1),SlotBorder,true,false);Place(border.rectTransform,position+new Vector2(-1.5f,1.5f),new Vector2(SlotSize+3,SlotSize+3));borders[index]=border;
            var card=UiKit.CreatePanel(parent,"Slot "+(index+1),fill,true,true);Place(card.rectTransform,position,new Vector2(SlotSize,SlotSize));cards[index]=card;
            if(index<6){
                var badge=UiKit.CreatePanel(card.transform,"Key",new Color(0,0,0,.45f),true,false);Place(badge.rectTransform,new Vector2(5,-5),new Vector2(20,18));
                var key=UiKit.CreateText(badge.transform,"Key",(index+1).ToString(),SeedAndRockTheme.LabelSize-1,SeedAndRockTheme.Muted,FontStyles.Bold);UiKit.Stretch(key.rectTransform);
            }
            labels[index]=UiKit.CreateText(card.transform,"Item","",SeedAndRockTheme.SmallSize+1,SeedAndRockTheme.Pale,FontStyles.Bold);UiKit.Stretch(labels[index].rectTransform,6,18,6,20);
            counts[index]=UiKit.CreateText(card.transform,"Count","",SeedAndRockTheme.LabelSize,SeedAndRockTheme.Gold,FontStyles.Bold,TextAlignmentOptions.BottomRight);UiKit.Stretch(counts[index].rectTransform,6,5,7,0);
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
            string promptText=player.InventoryOpen?"":player.Prompt;
            prompt.text=promptText;promptPill.gameObject.SetActive(!string.IsNullOrEmpty(promptText));
            notice.text=player.Message+(player.Underwater?"   Oxygen "+Mathf.CeilToInt(player.Oxygen*100)+"%  |  Space rise / Ctrl dive":"");
            for(int i=0;i<30;i++){var s=player.Inventory.Slots[i];var d=ItemCatalog.Get(s?.id);
                labels[i].text=d==null?"":d.displayName;
                counts[i].text=d==null||s.count<=1?"":"×"+s.count;
                bool selected=i==player.Inventory.Selected,isSource=i==source;
                cards[i].color=isSource?SlotSourceFill:selected?SlotSelectedFill:i<6?SlotFill:SlotFillBackpack;
                borders[i].color=isSource?SlotBorderSource:selected?SlotBorderSelected:SlotBorder;
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
