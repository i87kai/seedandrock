using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace SeedAndRock.World
{
    /// <summary>
    /// Shared tree/rock prototypes for Unity's native terrain tree renderer. One mesh per silhouette is built
    /// once (trunk + canopy as two sub-meshes, Cozy materials) and every tree in the world is a lightweight
    /// TreeInstance: GPU instanced, distance culled by the terrain, zero GameObjects. Harvest colliders are
    /// added on demand by <see cref="MapMagicResourceStreamer"/> only around the player.
    /// </summary>
    public sealed class MapMagicPrototypes
    {
        public sealed class Proto
        {
            public GameObject gameObject;
            public PlacementKind kind;
            public int variant;
            public float variation;
            /// <summary>Approximate collider height/radius in prototype units (scale 1).</summary>
            public float height, radius;
        }

        public readonly List<Proto> Protos=new List<Proto>();
        public TreePrototype[] TreePrototypes {get;private set;}
        readonly List<Mesh> meshes=new List<Mesh>();
        GameObject root;

        public int FirstTree {get;private set;}
        public int TreeCount {get;private set;}
        public int FirstRock {get;private set;}
        public int RockCount {get;private set;}

        public MapMagicPrototypes(Transform parent,WorldGenerationPalette palette,WorldMaterials materials)
        {
            root=new GameObject("MapMagic prototypes (shared meshes)");root.transform.SetParent(parent,false);root.SetActive(false);
            FirstTree=Protos.Count;
            // Three broadleaf, two conifer and one dry shrub silhouette; per-instance scale/rotation add the rest.
            AddTree(1,.15f,palette,materials);AddTree(1,.5f,palette,materials);AddTree(1,.85f,palette,materials);
            AddTree(0,.3f,palette,materials);AddTree(0,.7f,palette,materials);
            AddTree(2,.5f,palette,materials);
            TreeCount=Protos.Count-FirstTree;
            FirstRock=Protos.Count;
            AddRock(0,.2f,materials);AddRock(1,.5f,materials);AddRock(2,.8f,materials);
            RockCount=Protos.Count-FirstRock;
            TreePrototypes=new TreePrototype[Protos.Count];
            for(int i=0;i<Protos.Count;i++)TreePrototypes[i]=new TreePrototype{prefab=Protos[i].gameObject,bendFactor=0};
        }

        /// <summary>Prototype index for a deterministic 0..1 value.</summary>
        public int PickTree(float u,bool preferShrub){if(preferShrub&&u>.65f)return FirstTree+TreeCount-1;return FirstTree+Mathf.Min((int)(u*(TreeCount-1)),TreeCount-2);}
        public int PickRock(float u)=>FirstRock+Mathf.Min((int)(u*RockCount),RockCount-1);

        void AddTree(int variant,float variation,WorldGenerationPalette palette,WorldMaterials materials)
        {
            var trunk=new MeshData("Trunk");var canopy=new MeshData("Canopy");
            var p=new PlacementInstance{kind=PlacementKind.Tree,scale=1,variant=variant,variation=variation,moisture=.6f,biome=SeedAndRockBiome.Forest};
            PropMeshBuilder.AppendTree(trunk,canopy,in p,palette);
            var mesh=Combine("Tree "+variant+"-"+variation,trunk,canopy);
            var go=Make("Tree "+Protos.Count,mesh,new[]{materials.trunk,materials.foliage});
            Protos.Add(new Proto{gameObject=go,kind=PlacementKind.Tree,variant=variant,variation=variation,height=mesh.bounds.max.y,radius=Mathf.Max(.25f,mesh.bounds.extents.x*.28f)});
        }

        void AddRock(int variant,float variation,WorldMaterials materials)
        {
            var data=new MeshData("Rock");
            var p=new PlacementInstance{kind=PlacementKind.Rock,scale=1,variant=variant,variation=variation};
            PropMeshBuilder.AppendRock(data,in p);
            var mesh=Combine("Rock "+variant,data,null);
            var go=Make("Rock "+Protos.Count,mesh,new[]{materials.rock});
            Protos.Add(new Proto{gameObject=go,kind=PlacementKind.Rock,variant=variant,variation=variation,height=mesh.bounds.max.y,radius=Mathf.Max(.4f,mesh.bounds.extents.x*.8f)});
        }

        GameObject Make(string name,Mesh mesh,Material[] mats)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=mats;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
            r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return go;
        }

        Mesh Combine(string name,MeshData a,MeshData b)
        {
            var mesh=new Mesh{name=name};
            var verts=new List<Vector3>(a.Vertices);var norms=new List<Vector3>(a.Normals);var cols=new List<Color>(a.Colors);
            var tris=new List<int>(a.Triangles);
            int subMeshes=1;List<int> trisB=null;
            if(b!=null&&!b.IsEmpty){int off=verts.Count;verts.AddRange(b.Vertices);norms.AddRange(b.Normals);cols.AddRange(b.Colors);trisB=new List<int>(b.Triangles.Count);foreach(int t in b.Triangles)trisB.Add(t+off);subMeshes=2;}
            mesh.indexFormat=verts.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(verts);
            if(norms.Count==verts.Count)mesh.SetNormals(norms);
            if(cols.Count==verts.Count)mesh.SetColors(cols);
            mesh.subMeshCount=subMeshes;
            mesh.SetTriangles(tris,0,true);
            if(subMeshes==2)mesh.SetTriangles(trisB,1,true);
            if(norms.Count!=verts.Count)mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);return mesh;
        }

        public void Dispose(){foreach(var m in meshes)if(m!=null)Object.Destroy(m);meshes.Clear();if(root!=null)Object.Destroy(root);}
    }
}
