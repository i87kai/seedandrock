#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace SeedAndRock.World.Editor
{
    public static class MapMagicWorldAssets
    {
        [MenuItem("SeedAndRock/MapMagic/Create world assets")]
        public static void Create()
        {
            const string root="Assets/_SeedAndRock/Resources";
            Color[] colors={new Color(.38f,.5f,.24f),new Color(.22f,.32f,.16f),new Color(.7f,.63f,.44f),new Color(.43f,.44f,.43f),new Color(.88f,.92f,.94f)};
            string[] names={"Meadow","Forest floor","Sand","Mountain rock","Snow"};var layers=new TerrainLayer[5];
            for(int i=0;i<5;i++){
                string texturePath=root+"/MM_Texture_"+i+".asset";
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);bool fresh=tex==null;if(fresh)tex=new Texture2D(32,32){name=names[i]};
                var pixels=new Color[1024];for(int p=0;p<pixels.Length;p++)pixels[p]=colors[i]*Mathf.Lerp(.96f,1.04f,Mathf.PerlinNoise((p%32)*.2f,(p/32)*.2f));tex.SetPixels(pixels);tex.Apply();
                if(fresh)AssetDatabase.CreateAsset(tex,texturePath);else EditorUtility.SetDirty(tex);
                string layerPath=root+"/MM_Layer_"+i+".terrainlayer";layers[i]=AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);if(layers[i]==null){layers[i]=new TerrainLayer();AssetDatabase.CreateAsset(layers[i],layerPath);}layers[i].name=names[i];layers[i].diffuseTexture=tex;layers[i].tileSize=new Vector2(6,6);EditorUtility.SetDirty(layers[i]);
            }
            if(AssetDatabase.LoadAssetAtPath<Material>(root+"/SR_MapMagicTerrain.mat")==null){var mat=new Material(Shader.Find("Cozy/Terrain")??Shader.Find("Universal Render Pipeline/Terrain/Lit"));AssetDatabase.CreateAsset(mat,root+"/SR_MapMagicTerrain.mat");}
            string proxyPath=root+"/MM_PlacementProxy.prefab";var proxy=AssetDatabase.LoadAssetAtPath<GameObject>(proxyPath);
            if(proxy==null){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="MapMagic placement proxy";Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.localScale=Vector3.one*.01f;proxy=PrefabUtility.SaveAsPrefabAsset(go,proxyPath);Object.DestroyImmediate(go);}
            var graph=SurvivalGraph.Create(layers,proxy);string graphPath=root+"/SR_MapMagicWorld.asset";var existing=AssetDatabase.LoadAssetAtPath<MapMagic.Nodes.Graph>(graphPath);
            if(existing==null)AssetDatabase.CreateAsset(graph,graphPath);else{graph.OnBeforeSerialize();EditorUtility.CopySerialized(graph,existing);existing.OnAfterDeserialize();EditorUtility.SetDirty(existing);Object.DestroyImmediate(graph);}AssetDatabase.SaveAssets();
        }
    }
}
#endif
