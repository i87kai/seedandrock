using System.Collections;
using SeedAndRock.Items;
using SeedAndRock.Saves;
using SeedAndRock.Survival;
using SeedAndRock.UI;
using SeedAndRock.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SeedAndRock.Player
{
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerExpedition : MonoBehaviour,ISurvivalModifier
    {
        public PlayerInventory Inventory {get;private set;}
        public bool InventoryOpen {get;private set;}
        public bool Waking {get;private set;}
        public bool Underwater {get;private set;}
        public float Oxygen {get;private set;}=1;
        public string Prompt {get;private set;}="";
        public string Message {get;private set;}="";
        public float WakeFade {get;private set;}
        FirstPersonExplorerController controller; PlayerSurvival survival;
        GameObject held;Light torch;float cooldown,messageUntil,swing;
        Volume underwaterVolume;VolumeProfile underwaterProfile;
        public bool CanAct => !Waking&&!InventoryOpen&&SeedAndRockGameFlow.Instance?.State==GameFlowState.Playing&&!survival.IsDead;
        void Awake()
        {
            controller=GetComponent<FirstPersonExplorerController>();survival=GetComponent<PlayerSurvival>();Inventory=GetComponent<PlayerInventory>();Inventory.Changed+=RefreshHeld;
            survival.RefreshModifiers();
            var v=new GameObject("Underwater color");v.transform.SetParent(transform,false);underwaterVolume=v.AddComponent<Volume>();underwaterVolume.isGlobal=true;underwaterVolume.priority=100;underwaterVolume.weight=0;
            underwaterProfile=ScriptableObject.CreateInstance<VolumeProfile>();underwaterVolume.sharedProfile=underwaterProfile;
            var color=underwaterProfile.Add<ColorAdjustments>();color.colorFilter.Override(new Color(.44f,.82f,.87f));color.postExposure.Override(-.35f);
            var vignette=underwaterProfile.Add<Vignette>();vignette.intensity.Override(.35f);vignette.color.Override(new Color(.02f,.11f,.14f));
        }
        public void Restore(ExpeditionState state){Inventory.Restore(state);SetInventory(false);Oxygen=1;Underwater=false;}
        public ExpeditionState Capture(){var s=new ExpeditionState{slots=System.Array.ConvertAll(Inventory.Slots,x=>x?.Copy()),selected=Inventory.Selected,clothed=Inventory.Clothed};ExpeditionWorld.Active?.Capture(s);return s;}
        public IEnumerator WakeUp()
        {
            Waking=true;controller.enabled=false;Inventory.Select(0);var cam=controller.ViewCamera;float elapsed=0;
            while(elapsed<3.8f){elapsed+=Time.deltaTime;float t=Mathf.SmoothStep(0,1,elapsed/3.8f);
                if(cam!=null){cam.transform.localPosition=Vector3.Lerp(new Vector3(0,.28f,0),new Vector3(0,1.65f,0),t);cam.transform.localRotation=Quaternion.Euler(Mathf.Lerp(-24,0,t),0,Mathf.Lerp(64,0,t));}
                WakeFade=Mathf.Clamp01(1-elapsed/1.7f)+Mathf.Exp(-Mathf.Pow((elapsed-1.15f)*6,2))*.65f;yield return null;
            }
            WakeFade=0;controller.SetView(0,0);Waking=false;RefreshHeld();Notify("Gather supplies in the clearing. Tab opens your inventory.");
        }
        public void SetInventory(bool open)
        {
            InventoryOpen=open;bool playing=SeedAndRockGameFlow.Instance?.State==GameFlowState.Playing;
            controller.enabled=playing&&!open&&!Waking&&!survival.IsDead;Cursor.lockState=controller.enabled?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!controller.enabled;
        }
        public void Notify(string text){Message=text;messageUntil=Time.unscaledTime+3;}
        void Update()
        {
            if(Time.unscaledTime>messageUntil)Message="";
            bool playing=SeedAndRockGameFlow.Instance?.State==GameFlowState.Playing;
            if(!playing){if(InventoryOpen)SetInventory(false);if(held!=null)held.SetActive(false);return;}
            if(held!=null)held.SetActive(!Waking&&!InventoryOpen);
            UpdateWater();if(Waking)return;
            if(survival.IsDead){controller.enabled=false;Prompt="You died — press R to wake at camp (inventory is dropped)";if(Keyboard.current?.rKey.wasPressedThisFrame==true)Respawn();return;}
            var k=Keyboard.current;var mouse=Mouse.current;
            if(k?.tabKey.wasPressedThisFrame==true||k?.iKey.wasPressedThisFrame==true)SetInventory(!InventoryOpen);
            if(!CanAct)return;
            for(int i=0;i<6;i++)if(k!=null&&k[(Key)((int)Key.Digit1+i)].wasPressedThisFrame)Inventory.Select(i);
            if(mouse!=null&&Mathf.Abs(mouse.scroll.ReadValue().y)>.01f)Inventory.Select(Inventory.Selected+(mouse.scroll.ReadValue().y>0?-1:1));
            if(k?.qKey.wasPressedThisFrame==true)Drop(Inventory.Selected);
            Prompt="LMB use tool   E gather / pick up   Tab inventory";
            var camera=controller.ViewCamera;
            if(camera!=null && Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,Inventory.Equipped?.id=="spear"?4.5f:3.5f,~0,QueryTriggerInteraction.Ignore)){
                var node=hit.collider.GetComponentInParent<ResourceNode>();var loot=hit.collider.GetComponentInParent<WorldLoot>();var animal=hit.collider.GetComponentInParent<Wildlife>();
                if(node!=null)Prompt=(node.HandGather?"E gather ":"LMB harvest ")+node.Label+"  ["+node.Remaining+"]";
                if(loot!=null)Prompt="E pick up "+ItemCatalog.Get(loot.ItemId).displayName+" x"+loot.Count;
                if(animal!=null)Prompt="LMB hunt "+animal.name;
                if(k?.eKey.wasPressedThisFrame==true){if(loot!=null)loot.Pickup(Inventory);else if(node!=null&&node.HandGather)Gather(node);}
                if(mouse?.leftButton.wasPressedThisFrame==true && Time.time>=cooldown){cooldown=Time.time+.55f;swing=.4f;if(node!=null&&!node.HandGather)Gather(node);else if(animal!=null)animal.Hit(Inventory.Equipped?.damage??0);else UseSelected();}
            }else if(mouse?.leftButton.wasPressedThisFrame==true&&Time.time>=cooldown){cooldown=Time.time+.55f;swing=.4f;UseSelected();}
            if(held!=null){swing=Mathf.Max(0,swing-Time.deltaTime);held.transform.localRotation=Quaternion.Euler(-15+Mathf.Sin(swing/.4f*Mathf.PI)*65,0,-18);}
        }
        void Gather(ResourceNode node){int n=node.Harvest(Inventory,Inventory.Equipped);Notify(n>0?"+"+n+" "+node.Label:"No harvest: choose a suitable tool or free inventory space.");}
        public void UseSelected()
        {
            var d=Inventory.Equipped;if(d==null)return;
            if(d.nutrition>0){Inventory.Take(Inventory.Selected,1);survival.RestoreHunger(d.nutrition);survival.RestoreThirst(3);}
            else if(d.healing>0){Inventory.Take(Inventory.Selected,1);survival.RestoreHealth(d.healing);}
            else if(d.id=="shirt"){if(Inventory.Clothed){Notify("You already wear a tunic.");return;}Inventory.Take(Inventory.Selected,1);Inventory.Clothed=true;Notify("Cloth tunic equipped: improved insulation.");}
        }
        public void Drop(int slot)
        {
            if(ExpeditionWorld.Active==null)return;var s=Inventory.Take(slot);if(s==null)return;
            Vector3 p=transform.position+transform.forward*1.3f;p.y=WorldGenerator.Active.GetHeightAt(p.x,p.z)+.35f;
            if(WorldGenerator.Active.TryGetWaterSurfaceAt(p.x,p.z,out float water))p.y=Mathf.Max(p.y,water+.15f);
            WorldLoot.Create(s.id,s.count,p,ExpeditionWorld.Active.transform);
        }
        void Respawn()
        {
            for(int i=0;i<PlayerInventory.SlotCount;i++)Drop(i);
            survival.ApplySnapshot(survival.MaxHealth,survival.MaxHunger,survival.MaxThirst,37);Inventory.Restore(null);PlayerSpawner.Teleport(controller,ExpeditionWorld.Active.SpawnPoint,0,0);Oxygen=1;SetInventory(false);
        }
        void RefreshHeld()
        {
            if(held!=null){held.SetActive(false);Destroy(held);}var d=Inventory.Equipped;if(d==null||controller.ViewCamera==null)return;
            held=d.heldPrefab!=null?Instantiate(d.heldPrefab):PlaceholderModels.Item(d,true);held.transform.SetParent(controller.ViewCamera.transform,false);held.transform.localPosition=new Vector3(.38f,-.34f,.65f);
            foreach(var c in held.GetComponentsInChildren<Collider>())c.enabled=false;
            if(d.id=="torch"){torch=held.AddComponent<Light>();torch.type=LightType.Point;torch.color=new Color(1,.49f,.15f);torch.range=14;torch.intensity=3;}
        }
        void UpdateWater()
        {
            var world=WorldGenerator.Active;var cam=controller.ViewCamera;
            Underwater=world!=null&&cam!=null&&world.TryGetWaterSurfaceAt(transform.position.x,transform.position.z,out float level)&&cam.transform.position.y<level-.06f;
            underwaterVolume.weight=Mathf.MoveTowards(underwaterVolume.weight,Underwater?1:0,Time.deltaTime*4);
            Oxygen=Mathf.Clamp01(Oxygen+Time.deltaTime*(Underwater?-1f/25:1f/5));if(Oxygen<=0)survival.ApplyDamage(Time.deltaTime*8);
            if(torch!=null)torch.enabled=!Underwater;
        }
        public void Modify(ref SurvivalTickContext context){context.Insulation=Inventory!=null&&Inventory.Clothed?.65f:0;var cycle=ExpeditionWorld.Active?.GetComponent<DayNightCycle>();if(cycle!=null)context.AmbientOffsetCelsius+=Mathf.Sin((cycle.Hour-8)/24*Mathf.PI*2)*5;}
        void OnDestroy(){if(Inventory!=null)Inventory.Changed-=RefreshHeld;if(underwaterProfile!=null)Destroy(underwaterProfile);}
    }
}
