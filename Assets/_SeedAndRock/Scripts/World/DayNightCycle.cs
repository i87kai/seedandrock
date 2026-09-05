using UnityEngine;
using UnityEngine.Rendering;
namespace SeedAndRock.World
{
    public sealed class DayNightCycle : MonoBehaviour
    {
        public float Hour=7f;
        [Min(60)] public float DayLengthSeconds=900;
        Light sun,moon; Material sky,previousSky;
        void Start()
        {
            sun=RenderSettings.sun;
            if(sun==null)foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){sun=l;break;}
            if(sun==null){var go=new GameObject("Sun");go.transform.SetParent(transform);sun=go.AddComponent<Light>();sun.type=LightType.Directional;}
            RenderSettings.sun=sun;
            var m=new GameObject("Moon light");m.transform.SetParent(transform);moon=m.AddComponent<Light>();moon.type=LightType.Directional;moon.color=new Color(.38f,.53f,.85f);
            previousSky=RenderSettings.skybox;var shader=Shader.Find("Skybox/Procedural");
            if(shader!=null){sky=new Material(shader);sky.SetFloat("_SunSize",.035f);RenderSettings.skybox=sky;}
        }
        void LateUpdate()
        {
            if(sun==null)return;
            if(UI.SeedAndRockGameFlow.Instance?.State==UI.GameFlowState.Playing)Hour=Mathf.Repeat(Hour+Time.deltaTime*24/Mathf.Max(60,DayLengthSeconds),24);
            float elevation=Mathf.Sin((Hour-6)/24*Mathf.PI*2);
            float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.35f,elevation));
            float warm=1-Mathf.SmoothStep(0,1,Mathf.Abs(elevation)*3);
            sun.transform.rotation=Quaternion.Euler((Hour-6)*15, -35,0);sun.intensity=1.35f*daylight;
            sun.color=Color.Lerp(new Color(1,.94f,.82f),new Color(1,.43f,.19f),warm);
            moon.transform.rotation=Quaternion.Euler((Hour+6)*15,-35,0);moon.intensity=.13f*(1-daylight);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.045f,.065f,.12f),new Color(.52f,.68f,.85f),daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.035f,.045f,.07f),Color.Lerp(new Color(.52f,.59f,.56f),new Color(.68f,.36f,.22f),warm),daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.018f,.024f,.03f),new Color(.2f,.23f,.18f),daylight);
            bool under=Player.PlayerSpawner.Find()?.GetComponent<Player.PlayerExpedition>()?.Underwater ?? false;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=under?new Color(.035f,.24f,.28f):Color.Lerp(new Color(.035f,.055f,.105f),Color.Lerp(new Color(.65f,.77f,.84f),new Color(.85f,.49f,.3f),warm),daylight);
            RenderSettings.fogDensity=under?.065f:Mathf.Lerp(.003f,.0015f,daylight);
            if(sky!=null){sky.SetColor("_SkyTint",Color.Lerp(new Color(.04f,.065f,.15f),new Color(.48f,.62f,.8f),daylight));sky.SetFloat("_Exposure",Mathf.Lerp(.12f,1.1f,daylight));sky.SetColor("_GroundColor",RenderSettings.ambientGroundColor);}
        }
        void OnDestroy(){if(sky!=null){RenderSettings.skybox=previousSky;Destroy(sky);}}
    }
}
