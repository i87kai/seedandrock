# SeedAndRock world-quality and presentation pass

This Unity 6.6 URP project has a functional prototype flow and deterministic seed-based world generation. Continue from the existing systems; do not replace working save/world-flow code wholesale and do not add unrelated gameplay systems.

## Required outcome

Deliver a substantial, integrated quality pass covering world generation, graphics, UI/UX, loading, and gameplay presentation.

### World and terrain

- Replace patchy, grid-like water with terrain-driven hydrology: connected river paths, lakes in low basins, and coastlines/shorelines that follow terrain.
- Preserve deterministic generation: the same seed must recreate the same world; different seeds must be meaningfully different.
- Shape a very large, believable landscape with broad plains, rolling hills, valleys, restrained mountain regions, forests, snow, desert, grassland, rivers, and lakes.
- Use coherent low-frequency regional structure, domain warping, and smooth transitions rather than disconnected high-frequency noise.
- Keep sizeable readable plains; do not make most of the map mountainous.

### Procedural composition and temporary art

- Place vegetation and rocks using biome, moisture, height, slope, water distance, density masks, and clustering.
- Produce clustered forests, open grasslands, sparse mountain/desert/snow vegetation, and riverbank vegetation.
- Improve the generated stylized tree, rock, and grass meshes with varied silhouettes, scales, proportions, and deterministic rotation/variation.
- Keep the art original/procedural and performant; do not download third-party asset packs.

### Materials, water, and atmosphere

- Improve the existing URP shaders/materials with subtle deterministic variation, biome blending, slope rock, snow/sand/grass blending, darker wet banks, and soft shoreline transitions without obvious tiling.
- Give water shallow/deep color, subtle animated waves, restrained foam, and performant reflection/refraction cues where practical.
- Improve sky, fog, atmospheric perspective, directional light, soft shadows, ambient light, anti-aliasing, and the existing Global Volume for a calm cozy-fantasy look that is not oversaturated.

### UI/UX and loading

- Redesign the runtime UI into a polished SeedAndRock identity with consistent hierarchy, spacing, typography, button states, transitions, and subtle motion.
- Main menu: title/logo treatment, world-backed or dedicated visual background, Play, Settings, Quit.
- World browser: cards showing name, seed, difficulty, last played, and creation date; Play, Create New World, Delete with confirmation, Back.
- Creation screen: name, seed, random seed, difficulty, Create, Cancel/Back; empty seed auto-generates; validate invalid names and prevent duplicate/unsafe save paths.
- Add a responsive loading screen between selection and gameplay. Show real stage names such as terrain, biomes, rivers, vegetation, and player preparation. Only show numeric progress if it is genuine; otherwise use an indeterminate animation. Split expensive generation across frames or safe jobs/tasks so UI continues repainting.
- Add a short fade into gameplay.

### Gameplay presentation

- Remove the normal top debug bar. If retained, debug information must be behind an F3-style developer overlay.
- ESC opens a real pause menu with Resume, Save World, Settings, Save & Main Menu, and Quit.
- Saving must be safe/atomic. Returning and restarting must preserve the world metadata and player state.

## Constraints

- Do not add gathering, inventory, crafting, building, farming, NPCs, civilizations, or multiplayer.
- Keep changes inside the current Unity project and preserve current serialized references/GUIDs where possible.
- Do not commit `Library`, `Temp`, `Logs`, generated IDE project files, builds, or credentials.
- Avoid APIs/packages unavailable in Unity 6000.6.0f1 and URP 17.6.
- The live Unity Editor may not be accessible in the cloud environment. At minimum, keep both runtime and editor C# assemblies compile-clean and add deterministic edit-mode tests for pure generation/save logic where practical.

## Existing implementation map

- `Assets/_SeedAndRock/Scripts/UI/SeedAndRockGameFlow.cs`: runtime-created menu, saves, world selection/creation, and gameplay entry.
- `Assets/_SeedAndRock/Scripts/World/WorldGenerator.cs`: deterministic terrain/biome queries and world orchestration.
- `Assets/_SeedAndRock/Scripts/World/WorldMeshBuilder.cs`: procedural terrain, water, grass, trees, and rock meshes.
- `Assets/_SeedAndRock/Scripts/World/WorldHydrology.cs`: initial data types intended for a better connected hydrology implementation.
- `Assets/_SeedAndRock/Shaders/`: current stylized terrain, water, grass, and environment shaders.
- `Assets/_SeedAndRock/Scenes/World.unity`: enabled gameplay scene.

## Verification and delivery

1. Compile and resolve every error.
2. Inspect changed serialized assets for broken GUID/file references.
3. Exercise or test the complete flow: Main Menu -> Play -> World Selection -> Create -> Loading -> Gameplay -> Pause -> Save & Main Menu -> reload the same world.
4. Verify deterministic terrain/water/biome samples for the same seed and variation across different seeds.
5. Check that water geometry does not use visibly disconnected square cells and that generation does not monopolize a single frame for an extended period.
6. Review performance and avoid excessive GameObjects/draw calls or unbounded mesh sizes.
7. Commit all project changes and push them to the task branch. Open/update the pull request with a concise summary, tests performed, remaining visual limitations, and performance concerns.
