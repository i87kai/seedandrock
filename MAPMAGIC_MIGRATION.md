# MapMagic 2 migration — pending package import

The requested production backend is MapMagic 2. Do not expand the legacy TerrainField, hydrology builder, vegetation placer, or mesh terrain pipeline. They are retained temporarily for reference and existing tests, not as a MapMagic fallback.

## Verified dependency state

MapMagic.Core.MapMagicObject is absent from loaded Editor assemblies. No MapMagic assets or packages are present in Assets, Packages, the local Asset Store cache, or Downloads. Import the user's official MapMagic 2 package and available modules before implementing version-specific APIs or authoring a graph.

## Integration contract to implement against installed source

- MapMagic owns terrain shape, erosion, biome masks, texture outputs, foliage and object distributions, tile streaming, and deterministic graph seeds.
- Refactor WorldGenerator into the existing menus/save system's facade for MapMagic generation. Replace WorldSampler dependencies in gameplay with queries against completed MapMagic tiles and graph-authored semantic masks.
- Wait for the main-detail spawn tile to finish before placing the player or enabling movement. Pin an authored start clearing and supply area in the graph. Never assume an unloaded tile has height zero.
- Subscribe to MapMagic tile lifecycle events, filtered to the active world and main-detail tiles. Handle recycled coordinates, cancellation, world switching, and event unsubscription.
- Resource prefabs spawned through MapMagic outputs carry ResourceNode components. Derive stable identities from world seed, graph/output identity, tile coordinate, and instance identity. Reapply depleted states when tiles return.
- Animals use MapMagic placements and completed Terrain queries to roam. Climate uses graph biome/climate outputs, elevation, time and water volumes; it does not independently generate terrain or biome noise.
- Save world generator identity, graph/settings version, seed, inventory, time, resource changes and dropped loot. Keep old-world compatibility explicit; do not silently reinterpret existing custom terrain seeds as equivalent MapMagic worlds.
- Configure URP terrain layers, coherent region graphs and object outputs using the actual installed modules. Do not substitute a second custom generator for missing modules.

## Gameplay work in progress

Items, stack inventory, six-slot HUD, crafting, gathering components, held placeholders, wake coroutine, swimming and day/night code have been added. Editor compilation was verified after these additions. Full runtime/gameplay validation has NOT been completed. ExpeditionWorld.Initialize still contains legacy placement integration and must be replaced with MapMagic graph outputs. The current generation backend is still legacy; MapMagic integration is not implemented.

## Required validation after integration

New world -> completed start tile -> wake -> rock equipped -> gather wood/stone/cloth/food -> inventory transfers/splitting/crafting/drop/pickup -> swim and recover oxygen -> day/night -> save/reload -> travel across tile boundaries -> revisit depleted resources without duplication. Check at least several seeds, seam continuity, cancellation during generation, and switching saved worlds.
