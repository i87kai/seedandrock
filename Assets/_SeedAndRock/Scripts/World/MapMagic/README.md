# MapMagic survival island

MapMagic 2 owns terrain, biomes and placement. This folder only *composes* the graph from installed
MapMagic nodes and bridges its outputs to gameplay.

| File | Role |
| --- | --- |
| `SurvivalGraph.cs` | The world design: island shape, spawn meadow, forests, lake, two rivers, bay/cove, lookout hill, foothills, mountain, texture masks and placement masks/densities. Built from `Spot / Noise / Blend / Levels / Mask / Selector / Slope / UnityCurve / Beach` nodes only. Rivers are hand-placed polylines rasterised as overlapping `Spot`s merged with a multi-layer `Blend(max)` (a valley `Mask` toward 44 m, then a channel `Mask` to 35 m under the shared 40 m water surface). |
| `MapMagicBackend.cs` | Configures the `MapMagicObject` (tile size, ranges, terrain quality budget) and exposes height / surface sampling. Builds the graph from `SurvivalGraph` at runtime (`BuildGraphFromCode`), so tuning the C# applies immediately; the serialized `Resources/SR_MapMagicWorld.asset` is still produced by **SeedAndRock ▸ MapMagic ▸ Create world assets** for graph-editor preview. |
| `MapMagicPrototypes.cs` | Six tree and three rock meshes (Cozy bark/foliage/rock materials) shared by every instance. |
| `MapMagicGameplayTile.cs` | Per tile: reads the native detail-instance positions MapMagic produced, turns trees/stones into **terrain `TreeInstance`s** (instanced, distance culled, zero GameObjects) and keeps every placement as a tiny candidate record. |
| `MapMagicResourceStreamer.cs` | Around the player only: pooled harvest colliders for trees/stones (55 m), small plants (70 m), a capped number of animals (140 m). Harvesting removes the terrain instance. |
| `MapMagicOcean.cs` | Water plane at `SurvivalGraph.SeaLevel`. |

## World budget

| Setting | Value | Why |
| --- | --- | --- |
| Terrain height / sea level | 200 m / 40 m | Mountain tops out ~130 m (≈90 m above sea) after the summit curve. |
| Island | centre (800, 800), land ≈ 1.17 km across, 0.96 km² | Small enough to learn; every landmark is visible from the lookout hill. |
| Tile | 320 m, 257 px main / 65 px draft | 1.25 m per height pixel. |
| `mainRange` / `generateRange` | 1 / 3 | 3×3 full tiles (960 m) around the player, 7×7 with drafts covers the whole island. |
| Pixel error / base map | 5 / 500 m | |
| Tree distance | 650 m | Terrain culls trees beyond; far forests are hidden by fog anyway. |
| Detail distance / density | 60 m / 0.6 | Only used if real grass details are added later. |
| Camera far clip | 1500 m | Was 2400–2500 m. |
| Shadow cascades | 2 (distance 50 m) | Was 4. |

Placement densities (per m², applied to the masks in `SurvivalGraph`): forest trees 0.003, plains
scatter trees 0.001 (clustered), stones 0.0005, cotton 0.0004, berries 0.0006 (forest edges),
mushrooms 0.0006, animals 0.00002. Whole island ≈ 600–700 trees; the old graph placed ~1000 trees
**per tile** everywhere and instantiated each one as 3 GameObjects with a unique mesh and MeshCollider.

## Regions

```
                 N
     bay ~~~ north river ~~~ foothills / mountain (snow cap)
        west forest                 east forest
                 spawn ✚    lake ~ south river
   lookout hill       plains         cove/beach
                 S
```

Spawn `(660, 600)` is a flat 50 m meadow (r 95) with a small grove 130 m north-west, a stone
outcrop 100 m east, the lake 270 m east (draining south to the coast through the south river), the
lookout hill 190 m south-west, the north river 450 m north and the mountain on the horizon 600 m
north-east.
