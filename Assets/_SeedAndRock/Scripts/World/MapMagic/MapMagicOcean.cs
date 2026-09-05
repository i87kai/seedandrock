using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>Water presentation at the graph's beach level. Terrain shape and coastline remain MapMagic-owned.</summary>
    public sealed class MapMagicOcean : MonoBehaviour
    {
        Mesh mesh;
        void Start()
        {
            mesh=new Mesh{name="Ocean surface"};mesh.vertices=new[]{new Vector3(-6000,0,-6000),new Vector3(-6000,0,6000),new Vector3(6000,0,6000),new Vector3(6000,0,-6000)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.SetUVs(1,new System.Collections.Generic.List<Vector4>{new Vector4(5,0,0,0),new Vector4(5,0,0,0),new Vector4(5,0,0,0),new Vector4(5,0,0,0)});mesh.RecalculateNormals();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=WorldGenerator.Active.Materials.water;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void LateUpdate(){var p=Player.PlayerSpawner.Find();if(p!=null)transform.position=new Vector3(p.transform.position.x,SurvivalGraph.SeaLevel,p.transform.position.z);}
        void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
