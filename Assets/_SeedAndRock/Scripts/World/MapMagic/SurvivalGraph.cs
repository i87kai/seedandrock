using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEngine;
namespace SeedAndRock.World
{
    /// <summary>
    /// Authors the survival island graph entirely from installed MapMagic nodes. No terrain sampling or noise
    /// implementation lives here - only the world *composition*: a bounded ~1.05 km island with a flat, open
    /// starting meadow, two named forests plus small woodland patches, rolling plains, a lake feeding a river
    /// to the south coast, a second river running from the foothills to the north-west bay, a lookout hill and
    /// one broad, smooth mountain in the north-east.
    ///
    /// Layout (world metres, island centre = (800, 800), land radius ~530):
    ///   Spawn meadow   (660, 600)   flat clearing r95, open plains around it
    ///   Lake           (930, 560)   r100, below sea level so it shares the water surface
    ///   South river    lake -> (880, 275)      channel ~30 m wide inside a soft valley
    ///   North river    (940, 985) -> bay       from the foothills to the north-west bay
    ///   West forest    (480, 900)   r250  - main lumber region, borders the north river
    ///   East forest    (1060, 780)  r190  - between lake and mountain
    ///   Foothills      (980, 1050)  r380  - gentle rise
    ///   Mountain       (1030, 1110) r230  - broad, ~90 m above sea, snow only on the cap
    ///   Lookout hill   (500, 500)   r130  - 12 m landmark hill SW of spawn
    ///   Bay / cove     (440, 1230) / (1330, 560) - coastline indentations with beaches
    /// </summary>
    public static class SurvivalGraph
    {
        public const float TerrainHeight=200, SeaLevel=40, TileSize=320;
        public const float IslandCenter=800, IslandRadius=800;
        public static readonly Vector3 StartPosition=new Vector3(660,50,600);

        /// <summary>Gameplay placement layers. The index is encoded in DetailPrototype.noiseSeed (100+kind).</summary>
        public enum Placement { ForestTree=0, Stone=1, Cloth=2, Berries=3, Mushroom=4, Animal=5, ScatterTree=6, Count=7 }

        static readonly Vector2[] SouthRiver={new Vector2(925,500),new Vector2(945,462),new Vector2(933,424),new Vector2(950,384),new Vector2(928,344),new Vector2(897,312),new Vector2(890,272),new Vector2(872,240)};
        static readonly Vector2[] NorthRiver={new Vector2(940,985),new Vector2(890,1000),new Vector2(845,1030),new Vector2(800,1036),new Vector2(760,1060),new Vector2(725,1095),new Vector2(680,1105),new Vector2(640,1125),new Vector2(600,1160),new Vector2(560,1170),new Vector2(520,1190),new Vector2(485,1215)};

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
            // A polyline of soft spots merged with "max": the only way to draw a hand-placed path with the
            // installed matrix nodes (no spline module). Spots overlap so the band has no bulges.
            Blend200 Path(Vector2[] points,float radius,float hardness,float intensity=1)
            {
                var spots=new System.Collections.Generic.List<Spot210>();
                for(int i=0;i<points.Length-1;i++){
                    float len=Vector2.Distance(points[i],points[i+1]);int steps=Mathf.Max(1,Mathf.CeilToInt(len/(radius*.55f)));
                    for(int s=0;s<steps;s++){var p=Vector2.Lerp(points[i],points[i+1],s/(float)steps);spots.Add(Spot(p.x,p.y,radius,intensity,hardness));}
                }
                var last=points[points.Length-1];spots.Add(Spot(last.x,last.y,radius,intensity,hardness));
                var merge=new Blend200{layers=new Blend200.Layer[spots.Count]};
                for(int i=0;i<spots.Count;i++)merge.layers[i]=new Blend200.Layer{algorithm=Blend200.BlendAlgorithm.max};
                Add(merge);
                for(int i=0;i<spots.Count;i++)Link(spots[i],merge.layers[i].inlet);
                return merge;
            }

            // ------------------------------------------------------------------ height
            // Lowland plateau ~48 m with gentle rolling relief (+-6 m) and a slightly larger, slower undulation
            // so the plains are not flat.
            var lowland=Add(new Constant200{level=.235f});
            var rolling=Add(new Noise200{seed=17,size=180,detail=.3f,intensity=.03f});
            var undulation=Add(new Noise200{seed=23,size=420,detail=.2f,intensity=.03f});
            var land=Blend(Blend(lowland,rolling,Blend200.BlendAlgorithm.add),undulation,Blend200.BlendAlgorithm.add);

            // Foothills: a wide, soft rise. Mountain: a single broad dome with low ridge noise on top.
            var foothills=Spot(980,1050,380,.12f,.05f);
            var core=Spot(1030,1110,230,.30f,0f);
            var ridgeNoise=Add(new Noise200{seed=77,size=160,detail=.35f,intensity=.4f});
            var ridges=Blend(core,ridgeNoise,Blend200.BlendAlgorithm.multiply);          // 0..0.12
            var mountain=Blend(Blend(core,ridges,Blend200.BlendAlgorithm.add),foothills,Blend200.BlendAlgorithm.add);
            // A lone 12 m hill south-west of the spawn: a readable landmark you can climb to see the whole island.
            var lookoutHill=Spot(500,500,130,.06f,.1f);
            var relief=Blend(Blend(land,mountain,Blend200.BlendAlgorithm.add),lookoutHill,Blend200.BlendAlgorithm.add); // max ~0.78 = 156 m

            // Island boundary, a bay and a cove so the coast is not a circle.
            var island=Spot(IslandCenter,IslandCenter,IslandRadius,1,.6f);
            var coast=Blend(relief,island,Blend200.BlendAlgorithm.multiply);
            var bay=Spot(440,1230,280,.14f,.2f);
            var cove=Spot(1330,560,200,.1f,.1f);
            var shaped=Blend(Blend(coast,bay,Blend200.BlendAlgorithm.subtract),cove,Blend200.BlendAlgorithm.subtract);

            // Soften the summits: only values above ~0.5 are compressed, lowlands are untouched.
            var soften=Add(new UnityCurve200{curve=new AnimationCurve(
                new Keyframe(0f,0f,1f,1f),new Keyframe(.5f,.5f,1f,1f),new Keyframe(.8f,.73f,.6f,.6f),new Keyframe(1f,.84f,.4f,.4f))});
            Link(shaped,soften.srcIn);

            // Lake basin below the shared sea-level water surface.
            var lakeMask=Spot(930,560,100,1,.25f);var lakeBottom=Add(new Constant200{level=32/TerrainHeight});
            var lake=Mask(soften,lakeBottom,lakeMask);

            // Rivers: a wide, soft valley pulled toward 44 m, then a channel set to 35 m (5 m under the water
            // surface). Same surface as the sea, so the water plane fills them automatically.
            var valleyMask=Levels(Blend(Path(SouthRiver,90,.1f),Path(NorthRiver,95,.1f),Blend200.BlendAlgorithm.max),0,1,0,.8f);
            var valleyLevel=Add(new Constant200{level=44/TerrainHeight});
            var valley=Mask(lake,valleyLevel,valleyMask);
            var channelMask=Blend(Path(SouthRiver,28,.25f),Path(NorthRiver,32,.25f),Blend200.BlendAlgorithm.max);
            var channelLevel=Add(new Constant200{level=35/TerrainHeight});
            var rivers=Mask(valley,channelLevel,channelMask);

            var beach=Add(new Beach210{level=SeaLevel,size=20,height=2.5f,relax=10});Link(rivers,beach);

            // Spawn clearing: flat meadow at 50 m, soft edge so it reads as a natural plateau.
            var clearing=Spot(StartPosition.x,StartPosition.z,95,1,.25f);
            var campLevel=Add(new Constant200{level=50/TerrainHeight});
            var camp=Mask(beach,campLevel,clearing);
            var heights=Add(new HeightOutput200());Link(camp,heights);

            // ------------------------------------------------------------------ masks
            var gentle=Add(new Slope200{from=0,to=30,range=8});Link(camp,gentle);
            var dry=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(SeaLevel+1.5f,SeaLevel+4),to=new Vector2(120,140)});Link(camp,dry);
            var habitat=Blend(dry,gentle,Blend200.BlendAlgorithm.multiply);
            var notClearing=Invert(clearing);
            var notRiver=Invert(channelMask);

            // Forests: two regions + a small grove near spawn, plus thresholded woodland patches that never
            // crowd the open starting plains. Nothing grows in the river channels.
            var westForest=Spot(480,900,250,1,.35f);
            var eastForest=Spot(1060,780,190,1,.3f);
            var spawnGrove=Spot(570,690,60,.85f,.3f);
            var regions=Blend(Blend(westForest,eastForest,Blend200.BlendAlgorithm.max),spawnGrove,Blend200.BlendAlgorithm.max);
            var patchNoise=Add(new Noise200{seed=419,size=130,detail=.3f});
            var patches=Levels(patchNoise,.58f,.8f,0,.6f);
            var openPlains=Invert(Spot(720,640,220,1,.3f));
            var patchesOpen=Blend(patches,openPlains,Blend200.BlendAlgorithm.multiply);
            var forestShape=Blend(regions,patchesOpen,Blend200.BlendAlgorithm.max);
            var edgeNoise=Levels(Add(new Noise200{seed=433,size=80,detail=.35f}),0,1,.55f,1);
            var forest=Blend(Blend(Blend(Blend(forestShape,edgeNoise,Blend200.BlendAlgorithm.multiply),habitat,Blend200.BlendAlgorithm.multiply),notClearing,Blend200.BlendAlgorithm.multiply),notRiver,Blend200.BlendAlgorithm.multiply);

            var rock=Add(new Slope200{from=28,to=89,range=12});Link(camp,rock);
            var snow=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(126,140),to=new Vector2(TerrainHeight,TerrainHeight+10)});Link(camp,snow);
            var waterEdge=Add(new Selector200{units=Selector200.Units.World,rangeDet=Selector200.RangeDet.MinMax,from=new Vector2(SeaLevel-6,SeaLevel-3),to=new Vector2(SeaLevel+2.5f,SeaLevel+4.5f)});Link(camp,waterEdge);
            var sand=Blend(beach.sandMaskOut,waterEdge,Blend200.BlendAlgorithm.max);

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
                var plains=Blend(Blend(Blend(habitat,notForest,Blend200.BlendAlgorithm.multiply),notClearing,Blend200.BlendAlgorithm.multiply),notRiver,Blend200.BlendAlgorithm.multiply);
                // Lone trees on the plains come in loose clusters instead of an even sprinkle.
                var clusterNoise=Levels(Add(new Noise200{seed=88,size=70,detail=.2f}),.62f,.85f);
                var scatter=Blend(plains,clusterNoise,Blend200.BlendAlgorithm.multiply);
                // Stones: foothills/slopes plus a guaranteed outcrop next to the spawn.
                var stony=Add(new Slope200{from=9,to=42,range=8});Link(camp,stony);
                var stoneGround=Blend(Blend(stony,dry,Blend200.BlendAlgorithm.multiply),Spot(745,655,50,.7f,.3f),Blend200.BlendAlgorithm.max);
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
