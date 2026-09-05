using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>
    /// Authors the survival island graph entirely from installed MapMagic nodes. No terrain sampling or noise
    /// implementation lives here - only the world *composition*: a bounded ~1.4 km island with a flat, open
    /// starting meadow, two large named forests plus small woodland patches, rolling plains, a lake, a sandy
    /// bay, gentle foothills and one broad, smooth mountain region on the far side.
    ///
    /// Layout (world metres, island centre = (800, 800)):
    ///   Spawn meadow   (620, 560)   flat clearing r110, open plains around it
    ///   Lake           (980, 520)   r130, sits below sea level so it shares the water surface
    ///   West forest    (430, 900)   r330  - main lumber region
    ///   East forest    (1160, 760)  r270  - between lake and mountain
    ///   Foothills      (1050, 1150) r520  - gentle rise
    ///   Mountain       (1180, 1260) r320  - broad, ~110 m above sea, snow only on the cap
    ///   Lookout hill   (400, 420)   r150  - 12 m landmark hill SW of spawn
    ///   Bay / cove     (260, 1180) / (1520, 480) - coastline indentations with beaches
    /// </summary>
    public static class SurvivalGraph
    {
        public const float TerrainHeight=200, SeaLevel=40, TileSize=320;
        public const float IslandCenter=800, IslandRadius=1000;
        public static readonly Vector3 StartPosition=new Vector3(620,50,560);

        /// <summary>Gameplay placement layers. The index is encoded in DetailPrototype.noiseSeed (100+kind).</summary>
        public enum Placement { ForestTree=0, Stone=1, Cloth=2, Berries=3, Mushroom=4, Animal=5, ScatterTree=6, Count=7 }

        public static Graph Create(TerrainLayer[] layers, GameObject placementPrototype=null)
        {
            var g=ScriptableObject.CreateInstance<Graph>();g.name="Cozy Survival Island — MapMagic";
            int ordinal=0;
            T Add<T>(T n) where T:Generator {n.id=(ulong)(++ordinal);if(n is IMultiInlet mi)foreach(var port in mi.Inlets()){port.SetGen(n);port.Id=Id.Generate();}if(n is IMultiOutlet mo)foreach(var port in mo.Outlets()){port.SetGen(n);port.Id=Id.Generate();}n.guiPosition=new Vector2((ordinal%7)*230,(ordinal/7)*230);g.Add(n);return n;}
            void Link(IOutlet<MatrixWorld> output,IInlet<MatrixWorld> input)=>g.Link(output,input);
            Blend200 Blend(IOutlet<MatrixWorld> a,IOutlet<MatrixWorld> b,Blend200.BlendAlgorithm mode,float opacity=1) {var n=Add(new Blend200());n.layers[1].algorithm=mode;n.layers[1].opacity=opacity;Link(a,n.layers[0].inlet);Link(b,n.layers[1].inlet);return n;}
            Spot210 Spot(float x,float z,float radius,float intensity,float hardness=0) => Add(new Spot210{position=new Vector2D(x,z),radius=radius,intensity=intensity,hardness=hardness});
            Levels200 Levels(IOutlet<MatrixWorld> src,float inMin,float inMax,float outMin=0,float outMax=1){var n=Add(new Levels200{inMin=inMin,inMax=inMax,outMin=outMin,outMax=outMax});Link(src,n);return n;}
            Levels200 Invert(IOutlet<MatrixWorld> src)=>Levels(src,0,1,1,0);
            Mask200 Mask(IOutlet<MatrixWorld> a,IOutlet<MatrixWorld> b,IOutlet<MatrixWorld> mask){var n=Add(new Mask200());Link(a,n.aIn);Link(b,n.bIn);Link(mask,n.maskIn);return n;}

            // ------------------------------------------------------------------ height
            // Lowland plateau ~48 m with very gentle rolling relief (+-6 m).
            var lowland=Add(new Constant200{level=.24f});
            var rolling=Add(new Noise200{seed=17,size=260,detail=.3f,intensity=.03f});
            var land=Blend(lowland,rolling,Blend200.BlendAlgorithm.add);

            // Foothills: a wide, soft rise. Mountain: a single broad dome with low ridge noise on top.
            var foothills=Spot(1050,1150,520,.12f,.05f);
            var core=Spot(1180,1260,320,.30f,0f);
            var ridgeNoise=Add(new Noise200{seed=77,size=220,detail=.35f,intensity=.4f});
            var ridges=Blend(core,ridgeNoise,Blend200.BlendAlgorithm.multiply);          // 0..0.12
            var mountain=Blend(Blend(core,ridges,Blend200.BlendAlgorithm.add),foothills,Blend200.BlendAlgorithm.add);
            // A lone 12 m hill south-west of the spawn: a readable landmark you can climb to see the whole island.
            var lookoutHill=Spot(400,420,150,.06f,.1f);
            var relief=Blend(Blend(land,mountain,Blend200.BlendAlgorithm.add),lookoutHill,Blend200.BlendAlgorithm.add); // max ~0.78 = 156 m

            // Island boundary and a bay so the coast is not a circle.
            var island=Spot(IslandCenter,IslandCenter,IslandRadius,1,.6f);
            var coast=Blend(relief,island,Blend200.BlendAlgorithm.multiply);
            var bay=Spot(260,1180,380,.14f,.15f);
            var cove=Spot(1520,480,250,.1f,.1f);
            var shaped=Blend(Blend(coast,bay,Blend200.BlendAlgorithm.subtract),cove,Blend200.BlendAlgorithm.subtract);

            // Soften the summits: only values above ~0.5 are compressed, lowlands are untouched.
            var soften=Add(new UnityCurve200{curve=new AnimationCurve(
                new Keyframe(0f,0f,1f,1f),new Keyframe(.5f,.5f,1f,1f),new Keyframe(.8f,.73f,.6f,.6f),new Keyframe(1f,.84f,.4f,.4f))});
            Link(shaped,soften.srcIn);

            // Lake basin below the shared sea-level water surface.
            var lakeMask=Spot(980,520,130,1,.25f);var lakeBottom=Add(new Constant200{level=32/TerrainHeight});
            var lake=Mask(soften,lakeBottom,lakeMask);

            var beach=Add(new Beach210{level=SeaLevel,size=22,height=2.5f,relax=10});Link(lake,beach);

            // Spawn clearing: flat meadow at 50 m, soft edge so it reads as a natural plateau.
            var clearing=Spot(StartPosition.x,StartPosition.z,110,1,.25f);
            var campLevel=Add(new Constant200{level=50/TerrainHeight});
            var camp=Mask(beach,campLevel,clearing);
            var heights=Add(new HeightOutput200());Link(camp,heights);

            // ------------------------------------------------------------------ masks
            var gentle=Add(new Slope200{from=0,to=30,range=8});Link(camp,gentle);
            var dry=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(SeaLevel+1.5f,SeaLevel+4),to=new Vector2(120,140)});Link(camp,dry);
            var habitat=Blend(dry,gentle,Blend200.BlendAlgorithm.multiply);
            var notClearing=Invert(clearing);

            // Forests: two large regions + a small grove near spawn, plus thresholded woodland patches that
            // never crowd the open starting plains.
            var westForest=Spot(430,900,330,1,.35f);
            var eastForest=Spot(1160,760,270,1,.3f);
            var spawnGrove=Spot(510,650,75,.85f,.3f);
            var regions=Blend(Blend(westForest,eastForest,Blend200.BlendAlgorithm.max),spawnGrove,Blend200.BlendAlgorithm.max);
            var patchNoise=Add(new Noise200{seed=419,size=150,detail=.3f});
            var patches=Levels(patchNoise,.58f,.8f,0,.6f);
            var openPlains=Invert(Spot(700,600,270,1,.3f));
            var patchesOpen=Blend(patches,openPlains,Blend200.BlendAlgorithm.multiply);
            var forestShape=Blend(regions,patchesOpen,Blend200.BlendAlgorithm.max);
            var edgeNoise=Levels(Add(new Noise200{seed=433,size=90,detail=.35f}),0,1,.55f,1);
            var forest=Blend(Blend(Blend(forestShape,edgeNoise,Blend200.BlendAlgorithm.multiply),habitat,Blend200.BlendAlgorithm.multiply),notClearing,Blend200.BlendAlgorithm.multiply);

            var rock=Add(new Slope200{from=28,to=89,range=12});Link(camp,rock);
            var snow=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(128,142),to=new Vector2(TerrainHeight,TerrainHeight+10)});Link(camp,snow);
            var lakeShore=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(SeaLevel-6,SeaLevel-3),to=new Vector2(SeaLevel+2.5f,SeaLevel+4.5f)});Link(camp,lakeShore);
            var sand=Blend(beach.sandMaskOut,lakeShore,Blend200.BlendAlgorithm.max);

            var textures=Add(new TexturesOutput200());textures.layers=new TexturesOutput200.TextureLayer[layers.Length];
            for(int i=0;i<layers.Length;i++)textures.layers[i]=new TexturesOutput200.TextureLayer{prototype=layers[i],name=layers[i].name,gen=textures,id=(ulong)(100+i)};
            if(layers.Length>1)Link(forest,textures.layers[1]);
            if(layers.Length>2)Link(sand,textures.layers[2]);
            if(layers.Length>3)Link(rock,textures.layers[3]);
            if(layers.Length>4)Link(snow,textures.layers[4]);

            // ------------------------------------------------------------------ placement
            if(placementPrototype!=null)
            {
                var notForest=Invert(forest);
                var plains=Blend(Blend(habitat,notForest,Blend200.BlendAlgorithm.multiply),notClearing,Blend200.BlendAlgorithm.multiply);
                // Lone trees on the plains come in loose clusters instead of an even sprinkle.
                var clusterNoise=Levels(Add(new Noise200{seed=88,size=70,detail=.2f}),.62f,.85f);
                var scatter=Blend(plains,clusterNoise,Blend200.BlendAlgorithm.multiply);
                // Stones: foothills/slopes plus a guaranteed outcrop next to the spawn.
                var stony=Add(new Slope200{from=9,to=42,range=8});Link(camp,stony);
                var stoneGround=Blend(Blend(stony,dry,Blend200.BlendAlgorithm.multiply),Spot(700,640,60,.7f,.3f),Blend200.BlendAlgorithm.max);
                // Berries grow at forest edges: forest * (1-forest) peaks at 0.25 -> normalised.
                var edge=Levels(Blend(forest,notForest,Blend200.BlendAlgorithm.multiply),0,.25f);
                var berryGround=Blend(edge,habitat,Blend200.BlendAlgorithm.multiply);

                (Placement kind,IOutlet<MatrixWorld> mask,float density)[] outputs={
                    (Placement.ForestTree,forest,.003f),       // ~1 tree / 18 m^2 inside forests (~4 m spacing)
                    (Placement.ScatterTree,scatter,.001f),     // lone clustered trees on the plains
                    (Placement.Stone,stoneGround,.0005f),
                    (Placement.Cloth,plains,.0004f),
                    (Placement.Berries,berryGround,.0006f),
                    (Placement.Mushroom,forest,.0006f),
                    (Placement.Animal,habitat,.00002f)};
                foreach(var o in outputs){
                    var output=Add(new GrassOutput200{density=o.density,renderMode=GrassOutput200.GrassRenderMode.MeshVertexLit,
                        prototype=new DetailPrototype{prototype=placementPrototype,usePrototypeMesh=true,useInstancing=true,renderMode=DetailRenderMode.VertexLit,minWidth=1,maxWidth=1,minHeight=1,maxHeight=1,healthyColor=Color.white,dryColor=Color.white,noiseSeed=100+(int)o.kind}});
                    Link(o.mask,output);
                }
            }
            return g;
        }
    }
}
