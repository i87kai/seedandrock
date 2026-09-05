using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>Authors a graph entirely from installed MapMagic nodes. No terrain sampling or noise implementation lives here.</summary>
    public static class SurvivalGraph
    {
        public const float TerrainHeight=320, SeaLevel=38, TileSize=384;
        public static readonly Vector3 StartPosition=new Vector3(192,50,192);
        public static Graph Create(TerrainLayer[] layers, GameObject placementPrototype=null)
        {
            var g=ScriptableObject.CreateInstance<Graph>();g.name="Cozy Survival — MapMagic";
            int ordinal=0;
            T Add<T>(T n) where T:Generator {n.id=(ulong)(++ordinal);if(n is IMultiInlet mi)foreach(var port in mi.Inlets()){port.SetGen(n);port.Id=Id.Generate();}if(n is IMultiOutlet mo)foreach(var port in mo.Outlets()){port.SetGen(n);port.Id=Id.Generate();}n.guiPosition=new Vector2((ordinal%6)*230,(ordinal/6)*230);g.Add(n);return n;}
            void Link(IOutlet<MatrixWorld> output,IInlet<MatrixWorld> input)=>g.Link(output,input);
            Blend200 Blend(IOutlet<MatrixWorld> a,IOutlet<MatrixWorld> b,Blend200.BlendAlgorithm mode) {var n=Add(new Blend200());n.layers[1].algorithm=mode;Link(a,n.layers[0].inlet);Link(b,n.layers[1].inlet);return n;}
            Spot210 Spot(float x,float z,float radius,float intensity,float hardness=0) => Add(new Spot210{position=new Vector2D(x,z),radius=radius,intensity=intensity,hardness=hardness});
            // A bounded island, with broad regional relief rather than a single world-wide noise ramp.
            var island=Spot(650,650,1900,1,.36f);
            var rolling=Add(new Noise200{seed=17,size=220,detail=.25f,intensity=.035f});
            var lowland=Add(new Constant200{level=.175f});
            var land=Blend(lowland,rolling,Blend200.BlendAlgorithm.add);
            var mountainA=Spot(650,900,820,.52f,.05f);
            var mountainB=Spot(1120,720,650,.38f,.02f);
            var massif=Blend(mountainA,mountainB,Blend200.BlendAlgorithm.max);
            var mountainNoise=Add(new Noise200{seed=77,size=310,detail=.28f,intensity=.18f});
            var broadRidges=Blend(massif,mountainNoise,Blend200.BlendAlgorithm.multiply);
            var peaks=Blend(massif,broadRidges,Blend200.BlendAlgorithm.add);
            var relief=Blend(land,peaks,Blend200.BlendAlgorithm.add);
            var coast=Blend(relief,island,Blend200.BlendAlgorithm.multiply);
            var bay=Spot(-100,180,490,.28f,.2f);
            var inlet=Blend(coast,bay,Blend200.BlendAlgorithm.subtract);
            var eroded=Add(new Erosion200{iterations=6,terrainDurability=.9f,relax=.06f});Link(inlet,eroded);
            // Lake basin stays below the same sea-level water surface but is separated by dry terrain.
            var lakeMask=Spot(650,200,155,1,.22f);var lakeBottom=Add(new Constant200{level=29/TerrainHeight});
            var lake=Add(new Mask200());Link(eroded,lake.aIn);Link(lakeBottom,lake.bIn);Link(lakeMask,lake.maskIn);
            var beach=Add(new Beach210{level=SeaLevel,size=18,height=2.5f,relax=12});Link(lake,beach);
            var clearing=Add(new Spot210{position=new Vector2D(StartPosition.x,StartPosition.z),radius=135,hardness=.2f});
            var campLevel=Add(new Constant200{level=50/TerrainHeight});
            var camp=Add(new Mask200());Link(beach,camp.aIn);Link(campLevel,camp.bIn);Link(clearing,camp.maskIn);
            var heights=Add(new HeightOutput200());Link(camp,heights);
            var moisture=Add(new Noise200{seed=419,size=160,detail=.25f});
            var forestRegion=Spot(425,440,530,1,.45f);
            var forestVariation=Add(new Levels200{outMin=.5f,outMax=1});Link(moisture,forestVariation);
            var forest=Blend(forestRegion,forestVariation,Blend200.BlendAlgorithm.multiply);
            var snow=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(135,185),to=new Vector2(330,340)});Link(camp,snow);
            var rock=Add(new Slope200{from=30,to=89,range=18});Link(camp,rock);
            var sand=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(-10,-5),to=new Vector2(40,46)});Link(camp,sand);
            var textures=Add(new TexturesOutput200());textures.layers=new TexturesOutput200.TextureLayer[layers.Length];
            for(int i=0;i<layers.Length;i++)textures.layers[i]=new TexturesOutput200.TextureLayer{prototype=layers[i],name=layers[i].name,gen=textures,id=(ulong)(100+i)};
            Link(forest,textures.layers[1]);Link(sand,textures.layers[2]);Link(rock,textures.layers[3]);Link(snow,textures.layers[4]);
            if(placementPrototype!=null)
            {
                var dry=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(41,47),to=new Vector2(130,170)});Link(camp,dry);
                var gentle=Add(new Slope200{from=0,to=32,range=8});Link(camp,gentle);
                var habitat=Blend(dry,gentle,Blend200.BlendAlgorithm.multiply);
                var openCamp=Blend(habitat,clearing,Blend200.BlendAlgorithm.subtract);
                var forestHabitat=Blend(openCamp,forest,Blend200.BlendAlgorithm.multiply);
                // Installed native Grass outputs own density, deterministic distribution, and tile products.
                // The gameplay adapter consumes Unity's native detail transforms to attach harvestable objects.
                string[] kinds={"wood","stone","cloth","berries","mushroom","animal"};
                float[] densities={.007f,.0018f,.002f,.0016f,.003f,.00004f};
                for(int i=0;i<kinds.Length;i++){
                    var output=Add(new GrassOutput200{density=densities[i],renderMode=GrassOutput200.GrassRenderMode.MeshVertexLit,
                        prototype=new DetailPrototype{prototype=placementPrototype,usePrototypeMesh=true,useInstancing=true,renderMode=DetailRenderMode.VertexLit,minWidth=1,maxWidth=1,minHeight=1,maxHeight=1,healthyColor=Color.white,dryColor=Color.white,noiseSeed=100+i}});
                    Link(i==0||i==4?forestHabitat:openCamp,output);
                }
            }
            return g;
        }
    }
}
