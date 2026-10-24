# Visual depth and atmosphere — planning review

Date: 2026-10-24. Source revision: `__REVISION__`. Status: **proposal, no runtime implementation**.
This review uses the two supplied screenshots and saved project sources/assets.
No new live Editor capture, Play Mode comparison or GPU profile was performed.
Screenshot observations and proposed visual gains are art-direction judgments,
not measured improvements or evidence of the precise runtime state in the images.

## Recommended direction

Put the character into a coherent space through **value hierarchy, contact,
shared lighting logic, and layered background distance**. Use a restrained
world color profile to tie those decisions together. Darkening the whole screen
or adding particles everywhere is unlikely to solve the current composition.

Begin with one hub composition and one dungeon room. Compare still images and
movement before applying the result to every biome. Preserve the existing pixel
art and 24 PPU / 480×270 reference grid. The UI remains a separate overlay.

## What the screenshots suggest

- Hub: mountains already establish a distant scale. The dense tree band behind
  the buildings has a similarly strong dark mass; the playable plane and nearby
  background compete. The forge is an obvious opportunity for a warm visual
  anchor and for light shared by ground, architecture and character.
- Dungeon: cave silhouettes, masonry and enemies sit in similar cool/dark
  ranges. Platforms are readable, but the middle distance has few distinctive
  landmarks. More deliberate gaps, niches and local light could turn a tile
  layout into a specific place.
- In both views, grounding, common shadow colors and a clear focal hierarchy
  are more relevant to integration than a bright outline around the hero.
- Motion, actual parallax strength, fog speed and temporal shimmer cannot be
  judged from these two still frames. Existing systems must be evaluated in
  motion before deciding they are absent or need replacement.

## Confirmed project facilities and constraints

1. Unity **6000.3.11f1**, URP **17.3.0**, Renderer2D. Renderer2D already has the
   active Relic Atmosphere feature. Its checked full-screen pass blends fog,
   darkness and vignette and supports player reveal; it does not sample scene
   color for a full color-grading operation. Do not describe it as an existing LUT.
2. Saved `HubScene.unity` Main Camera references
   `Assets/Visuals/RelicFX/Preview/AtmosphereBasic.asset`, **not AtmosphereHub**.
   Its darkness/fogOpacity/vignette are 0.35/0.30/0.20; reveal radius/softness
   are 10/8 world units. With a 480-wide reference at 24 PPU, radius 10 is half
   a reference viewport width. The large clear area is worth checking before
   increasing fog: it can remove much of the effect around the player.
   This is a saved-asset finding, not a guarantee that the screenshots used it.
3. Main Camera's saved `m_RenderPostProcessing` is 0. URP asset supports HDR.
   Standard grading/bloom needs its own verified camera/profile wiring.
   The custom atmosphere feature is separate from the standard post-process toggle.
4. The hub is a layered Aseprite environment with already animated trees,
   independent fog crossfading, and roof/foliage sway. Do not rebuild these as
   duplicate systems. HubFogVisual opacity is 0.4 of its source alpha, not an
   assumption that 40% of the entire screen is fog.
5. `HubArt.mat` uses Sprite-Unlit-Default; the Buildings layer uses the custom
   unlit HubBuildingWind shader. The custom Relic Sprite FX shader also does not
   currently calculate the 2D shape-light contribution. Therefore adding Light2D
   does not automatically illuminate all current artwork. Preserve roof sway,
   sprite flash, dissolve, status effects and atlas behavior in any material pilot.
6. `WorldLight2DSetup` adds the configured lit sorting layers to **all found lights**.
   Simply authoring a light with a smaller layer target set may not be enough.
   Selective world/background lighting needs a deliberate policy before rollout.
   Global Light 2D in the saved scene has intensity 1.
7. `GroundingVisualController` already provides contact shadows, run/landing/
   jump/dash effects; PlayerMovement and EnemyEntity add it automatically.
   Start with tuning and surface presets, not a second shadow/dust component.
8. `ParallaxEffect` exists; **1 follows the camera, 0 stays world-fixed**.
   The saved DungeonSharedBackground uses 0.9 on X and Y. DungeonController
   swaps/alines one background renderer and restores it on hub return. Multi-layer
   biome backgrounds require extending that lifecycle and resetting each anchor.
   The checked hub environment prefab has no ParallaxEffect component: source
   artwork layers exist, but a multi-speed hub rig is not thereby confirmed.
9. AtmosphereProfile declares backgroundMist, shafts, wispStrength, lantern and
   motes. A RelicDepthMist shader exists. No checked runtime C# binding consumes
   those new fields for depth mist/lantern/motes; the current full-screen feature
   does not pass them. Treat these as partial groundwork requiring integration,
   not as working inspector sliders. A matching property/asset alone proves no effect.
10. Render order intentionally places Hero above ordinary environment. A source
    art group named Foreground is not proof of hero occlusion. True foreground
    overlap needs a render-stack category/policy and readability behavior.
11. Hub layout uses composite Buildings and TreesAndPlants drawing layers. A
    light or occlusion that needs an individual object may require a mask or
    artwork separation; per-object editing is not automatically cheap.
12. Existing working changes in Addressables link files, HubPalette and
    ProjectSettings were left intact. This task writes only this documentation
    directory and unmodified copies of the user reference screenshots.

## How to read the estimates

Visual gain: 1 minor accent, 3 noticeable improvement in context, 5 changes the
scene's spatial reading or identity. This is expected art-direction impact,
not a promised score from users. Effects interact; scores must not be added.

Setup: 1 existing setting/prefab tuning, 2 small reusable content/preset work,
3 a modest reusable system or artwork split, 4 renderer/material integration,
5 a new substantial content-production pipeline.

Maintenance: 1 a common preset with little per-room work, 2 a reusable prefab
with occasional content checks, 3 authoring/verification per biome or room type,
4 recurring geometry/material integration, 5 recurring work per asset/animation.

Prototype days are rough working-day estimates for an experienced developer,
using existing art, for **one hub view plus one representative dungeon room**.
They exclude new painted/animated assets unless explicitly stated, broad content
rollout and hardware certification. They are not measured project estimates.
Shared work means the rows should not simply be summed. GPU cost must be measured
on a chosen target device; maintenance difficulty and rendering cost are different.

## Option matrix

__TABLE__

## Candidate art directions

| Direction | Hub | Dungeon | Tradeoff |
|---|---|---|---|
| Cool world, warm inhabited places — recommended | Cool forest and distant mountains, warm forge/windows, quieter sky | Cool stone/cave depth, occasional amber lamps and distinct magical accents | Familiar restrained palette; requires actual focal placement, not one orange filter |
| Quiet misty dark fantasy | Muted greens, pale remote landscape, sparse warm points | Layered silhouettes and broken light through old structures | Strong mood; avoid crushing platform/enemy contrast |
| More colorful graphic adventure | Clear warm/cool blocks, brighter local accents | Distinct chamber color motifs and restrained dark masses | More immediately lively; needs palette discipline to avoid a busy combat screen |

These are alternative directions, not three effects to enable together. None
requires replacing all current art as a first step. Choose by a representative
side-by-side capture after the basic spatial hierarchy is improved.

## Suggested packages and order

**First experiment — base integration:** V01/V02/V03/V04/V05/V08/V09. Tune
existing facilities, separate background distance, use a shared palette and
contact. If layering requires too much art work, test V01/V04/V05 first on the
existing assets. V02 stays restrained while the material/value relationships settle.

**Balanced destination:** base + V06/V11/V12/V14. Light has visible sources;
each representative composition has one landmark and a few ambient events.
V06 begins with a material-compatibility pilot, not a global material swap.

**Alternative with lower lighting integration cost:** base + V07/V11/V14.
Authored light patches work with the current unlit artwork but do not provide
true local illumination on a moving actor. This is a different tradeoff, not
an equivalent technical replacement for V06. Avoid doubling V06 and V07 everywhere.

After the first comparison, consider V10/V13/V15/V16 in the locations that need
them. V17 is finishing polish. Defer V18/V19 until the base scene already reads
well. Full dynamic shadows, normal maps, day/night simulation, depth-of-field,
motion blur, chromatic aberration and full-screen grain are weak first investments
for the stated problem. A dark vignette is not a substitute for visual depth.

## Implementation boundary for the next coding task

- Implement the package/direction authorized by the subsequent user request.
  This document is a reviewable proposal, not implementation authorization;
  it introduces no extra approval gate when the later request is already clear.
- Reuse AtmosphereController/Profile, render-stack conventions, grounding and
  parallax instead of introducing parallel managers for each effect.
- Keep two distinct concepts: a global/world grade and a layer-placed background
  haze. The current screen-space reveal/fog should not be assumed to know object depth.
- For the initial light pilot: one forge/torch, one wall/floor section, one player
  class and one enemy. Verify all required material features survive, then widen.
- Make one profile per biome/location and explicitly apply/restore it during
  DungeonController transitions. No room should inherit a stale hub tint/fog.
- Package lamps, smoke, ambient emitters and contact darkening into shared
  prefabs. Each source owns its visuals; avoid manually paired disconnected objects.
- For new background layers, preserve seams and anchor reset at room transitions,
  camera bounds, hub return, jumps and teleports. Do not parent moving background
  art to a collider root or shift composite Buildings as a parallax plane.
- Respect existing MPB ownership and shared materials; do not create material
  instances every frame or overwrite status-effect properties.
- UI remains an overlay independent of world grading. No grading pass may change
  HP bar colors or inventory readability accidentally. World-space labels need
  an explicit decision because they are already rendered with the world.

## Acceptance examples for a vertical slice

1. Static comparisons at the same camera/player pose show clearly separated
   distant, middle and playable planes without losing the existing platform edge.
2. Player, weapon and enemy silhouettes remain readable in both the brightest
   VFX burst and the darkest room region; color alone is not required for detection.
3. At the forge, warmth is attached to a visible source. If V06 is chosen, a
   passing actor and nearby surface respond consistently; for V07 the documented
   absence of actor illumination is accepted rather than claimed otherwise.
4. Moving laterally, jumping, dashing and teleporting reveal no new seams,
   detached shadows, subpixel shimmer or large camera-locked fog patterns.
5. Fog does not cover a melee windup, small projectile or landing edge. Keep
   foreground forms away from NPC prompts and compulsory jump trajectories.
6. Hub -> dungeon -> next room -> hub restores background layers, profiles,
   anchors and ambient emitters. Disable/reenable and restart do not duplicate effects.
7. Compare a clean view and a crowded room on a named target machine; record
   CPU/GPU frame times, draw calls and transparent overdraw with effects on/off.
   No FPS, millisecond cost or universal light count is promised by this plan.
8. A second room can inherit most of the result by choosing a profile and
   placing a few shared prefabs. If each wall needs a bespoke mask, simplify
   the approach before rollout. That is the maintenance success criterion.

When implementing, use Unity Bridge compile and narrow relevant EditMode checks
such as GroundShadowTests, HubFogTests and RelicVisualFxRenderTests when affected,
plus manual Play Mode comparisons. No Unity tests were needed or claimed for
this planning-only task.

## Option details

__DETAILS__

## Evidence and references

Graphify MCP was used to locate AtmosphereController, RelicAtmosphereFeature,
WorldLight2DSetup, GroundingVisualController, HubFogVisual and ParallaxEffect.
Their sources and serialized scene/material/profile references were read directly.
The graph is discovery data, not proof of scene wiring. Exact snapshot hashes
for the central inspected files are stored in plan.json.

Project references (paths relative to repository root):

- Docs/Hub20Layout.md — composite source layers, fog, sway and layout constraints.
- Docs/RelicVisualFX.md — current sprite FX and camera/UI atmosphere contract.
- Assets/Scripts/Visuals/RENDER_STACK_GUIDE.md — ordering and actor visibility.
- Assets/Scenes/HubScene.unity — saved camera, profile, global light and background.
- Assets/Settings/Renderer2D.asset — active custom feature.
- Assets/Scripts/Visuals/Atmosphere/ — actual feature/controller/profile consumers.
- Assets/Shaders/RelicFX/RelicAtmosphere.shader and RelicDepthMist.shader.
- Assets/Editor/Hub/Hub20Builder.cs and HubBuildingWindBuilder.cs — materials/layout.
- Assets/Scripts/Dungeon/DungeonController.cs — one-background transition lifecycle.

Official Unity 6.3 documentation checked on 2026-10-24:

- [Color Adjustments](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/Post-Processing-Color-Adjustments.html): tone, exposure, contrast, tint and saturation of the rendered image.
- [2D light-compatible shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/PrepShader.html): Sprite-Lit and custom Sprite Lit shader paths; an unlit sprite is not made light-reactive merely by placing a Light2D.
- [Light 2D reference](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DLightProperties.html): light shape/blending and supporting options. These facilities do not determine their measured cost in this project.
