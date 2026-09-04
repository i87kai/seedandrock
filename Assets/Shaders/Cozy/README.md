# Cozy Stylized Rendering Framework (URP, Unity 6)

Original, dependency-free stylized rendering for a cozy survival world: soft
toon-like lighting with warm shadows, wind-animated foliage and grass, stylized
water, a procedural sky with soft clouds, day/night atmosphere and a tasteful
post-processing volume. World generation is **not** part of this framework -
MapMagic 2 owns terrain, biomes and vegetation placement; Cozy only renders it.

## Layout

| Path | Purpose |
| --- | --- |
| `Assets/Shaders/Cozy/CozyCommon.hlsl` | Global atmosphere / wind / underwater uniforms, math + noise helpers, `CozyApplyFog` |
| `Assets/Shaders/Cozy/CozyLighting.hlsl` | The Cozy lighting model (`CozySurface`, `CozyStyle`, `CozyShade`) |
| `Assets/Shaders/Cozy/CozyWind.hlsl` | Wind field, tree bend, leaf flutter, grass waves, object/vertex wind sources |
| `Assets/Shaders/Cozy/CozyDepthPasses.hlsl` | Shared ShadowCaster / DepthOnly / DepthNormals passes |
| `Cozy/Lit` (`CozyLit.shader`) | General surfaces (rocks, props, bark). Optional wind bending |
| `Cozy/Foliage` (`CozyFoliage.shader`) | Tree canopies / bushes: bend + flutter, puffy normals, translucency |
| `Cozy/Grass` (`CozyGrass.shader`) | Grass: procedural blade, texture card or solid mesh; root-pinned wind |
| `Cozy/Terrain` (`CozyTerrain.shader`) | **Unity Terrain** (MapMagic output): 4 splat layers, tints, slope rock, snow, shoreline |
| `Cozy/Terrain Mesh` (`CozyTerrainMesh.shader`) | Mesh-renderer terrain (legacy procedural mesh / showcase ground) |
| `Cozy/Water` (`CozyWater.shader`) | Depth gradient, animated ripples, Fresnel, foam, refraction, underwater |
| `Cozy/Sky` (`CozySky.shader`) | Procedural skybox: gradient, sun, sunset, two cloud layers, stars, moon |
| `Assets/Scripts/Cozy/CozyAtmosphere.cs` | Time of day -> sun light, sky colours, ambient, fog, shader globals |
| `Assets/Scripts/Cozy/CozyWind.cs` | Global wind parameters (direction, strength, speed, gusts, flutter) |
| `Assets/Scripts/Cozy/CozyCameraSetup.cs` | URP camera flags + underwater state / volume blend |
| `Assets/Scripts/Cozy/CozyShowcaseProps.cs` | Throw-away test rig used by `Assets/Scenes/CozyShowcase.unity` |
| `Assets/Scripts/Cozy/Editor/CozySceneSetup.cs` | `Tools > Cozy Rendering` menu: one-click scene setup and material conversion |
| `Assets/Materials/Cozy/*.mat` | Ready-to-use materials |
| `Assets/Settings/Cozy/CozyVolumeProfile.asset` | Bloom, ACES tonemapping, colour adjustments, white balance, vignette |
| `Assets/Settings/Cozy/CozyUnderwaterProfile.asset` | Blended in by `CozyCameraSetup` while submerged |

## Quick start (MapMagic scene)

1. Open your MapMagic scene and run **Tools > Cozy Rendering > Setup Cozy Rendering In Scene**.
   This adds `Cozy Rendering` (atmosphere + wind), a global volume, an underwater
   volume, `CozyCameraSetup` on game cameras, enables Depth/Opaque textures on the
   active URP asset and assigns `CozyTerrain.mat` to every Terrain.
2. In MapMagic's terrain settings set the material template to `Assets/Materials/Cozy/CozyTerrain.mat`
   so streamed tiles use it too. Keep at most 4 Terrain Layers per terrain (no add-pass);
   paint mood with the per-layer tints instead of extra layers.
3. Select your tree / rock / grass prefabs (or their materials) and use
   **Tools > Cozy Rendering > Convert Selected Materials > ...**:
   bark -> `Cozy Lit (bark, wind bending)`, leaves -> `Cozy Foliage`, grass cards -> `Cozy Grass (texture card)`,
   rocks/props -> `Cozy Lit`. Textures, base colour and alpha clip are carried over.
4. Put a `Cozy/Water` material on your water plane and point `CozyCameraSetup.waterSurface` at it.
5. Tune: `CozyAtmosphere` (time of day, sun, sky, fog), `CozyWind`, the volume profile, and material sliders.

## Wind sources

Foliage/grass/lit materials have a **Wind Source** dropdown:

* **Object** (default) - one transform per plant (prefabs, Terrain trees, MapMagic
  objects). Pivot = object origin, height = object-space Y, random = hash of position.
  Works with GPU instancing; nothing to author. Keep prefab pivots at the trunk base.
* **Vertex** - batched meshes with baked data: `UV0.x` height above base, `UV0.y` random,
  `UV1` pivot XZ (object space); foliage also `UV2.x` canopy 0..1, `UV2.y` canopy height;
  grass `UV0.y` root..tip, `UV1.x` random. The showcase grass uses this.

## Artist controls (highlights)

* Lighting (every surface shader): `Light Ramp Offset/Softness`, `Light Wrap`, `Shadow Tint` +
  strength, `Specular Strength/Softness`, `Rim Color/Strength/Power`, `Saturation`.
* Foliage: bottom/top canopy colours, per-tree variation, `Bend Influence`,
  `Flutter Strength`, `Backlight Strength/Color`.
* Grass: root/tip colours, blade taper/curve, `Blade Height` (object mode), `Wind Influence`, backlight.
* Water: shallow/deep colours + opacity, swell amplitude/length/speed, ripple scale/strength,
  reflection/Fresnel, sun specular + sparkle, foam distance/cutoff/bands, refraction toggle
  (uses the opaque texture; turn off on low-end), `High Detail Ripples` toggle.
* Sky: cloud coverage/density/softness/scale/height/speed/direction, silver lining, sun disc/glow,
  sunset band, stars, moon. Colours come from `CozyAtmosphere` when present.
* Atmosphere: `timeOfDay`, `dayLengthMinutes` (0 = static), sun azimuth/elevation, colour
  gradients over the day, fog density, height fog, sun in-scatter.
* Wind: direction + wander, strength, speed, gustiness, turbulence scale, leaf flutter.

## Performance notes

* All effects are single-pass forward; no extra render features are required. SSAO is optional
  (the PC renderer already has the URP SSAO feature; the shaders honour `_SCREEN_SPACE_OCCLUSION`).
* Expensive toggles are material keywords: water `Refraction`, water `High Detail Ripples`.
  Sky cloud cost is fixed (two FBM layers, ~5 noise taps each).
* Grass and canopy shadow passes exist but dense grass renderers should have `Cast Shadows` off.
* Terrain shader supports draw-instancing and per-pixel normals like URP TerrainLit.

## Limitations

* `Cozy/Terrain` renders the first four Terrain Layers only (no add-pass shader) and has no
  normal/mask map support - it is intentionally painterly.
* Underwater is a camera-side tint/absorption + volume blend, not a full volumetric effect.
* Cloud shadows on the ground are not implemented.
* Shaders were validated offline with DXC against URP 17.6 includes (124 variants); the
  Unity Editor should show no compile errors, but Inspector tuning is expected.
