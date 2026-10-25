# Local source lighting — V06

Implemented 2026-10-25. The hub forge and every placed torch emit local light.
The ten saved dungeon rooms currently contain three torch tiles (two in Room
001, one in Room 002). No additional decorations were placed. Nearby actors and
surfaces receive the same real URP 2D point light.

## Editing

Open **Tools > Relic Keeper > Visuals > World Appearance**, choose **Hub** or
**Mortfall**, then **Lighting**. For Mortfall, select **Room 001** or **Room 002**
to see torches. The view frames the first source automatically.

- **Enable lights** switches this location's local lights on/off.
- **Strength (%)** controls added illumination; 100% is the source's authored value.
- **Reach (%)** scales its radius.
- **Light color** changes its warmth.

Use the same **Save**, **Compare with saved** and **Discard changes** controls as
Colors, Mist and Movement. Lighting fields live in the existing location's
`AtmosphereProfileSO` asset. This change preserves all existing color, mist and
parallax tuning. Runtime location switching already selects these profiles.

The hub forge reuses the fire drawn in the building artwork. Mortfall's source
is attached to each existing `torch2`/`torch3` tile or sprite. The obsolete
single-torch pilot instance was removed to avoid duplicate light. The reusable torch
prefab uses the full `torch2` sprite as well. No sprite data was edited,
no new generated artwork was required, and neither source adds a collider.

## Automatic binding and flame motion

`TorchLightBinding` links tiles/sprites whose asset names start with `torch` to
one source per placement. Lights sit near the flame tip, account for tile
anchor/transform, and follow their map or sprite when moved. Repeated refreshes
reuse sources; deleting a torch removes its managed light. Standalone torch
prefabs that already own a source are not given a duplicate.

Existing room prefabs have the binding. Room creation also ensures it for newly
authored rooms. Tile painting refreshes the links on the next editor update;
room Prefab Mode saves keep both the links and terrain blockers up to date.
Sources and their references are saved with the prefab, so preview copies and
player builds use the same placements. No maintenance button is required.

Every torch and the forge use independent smooth noise for flame motion:
brightness varies within ±12%, reach within ±2.5%. Most variation is slower
breathing with a smaller, quicker flutter. Stable placement-derived phases
prevent synchronized pulses, and evaluation depends on time rather than frame
count. Saved profile values remain unchanged; flicker only multiplies the
rendered light. Ordinary non-flame sources remain steady. New atmosphere
profiles enable local lights by default; an explicitly saved disabled setting
is respected.

## Rendering

`LocalLight2DSource` configures a real `Light2D` in the Renderer2D asset's existing
unmasked Additive blend style, slot 1. It enables illumination only for a camera
with an active atmosphere controller in the source's scene, then resets intensity
after that camera renders. Editor Scene view uses the matching main camera's
profile. Isolated appearance previews bind their own controller explicitly.

The local-light helper uses this slot's irradiance to brighten the existing
Environment/Characters albedo before world grading, preserving dark outlines
instead of applying a flat amber wash. Distant Background, Unaffected and
combat-effect roles do not receive it. Existing global-light behavior is retained. Default unlit sprites
use the existing reversible graded material variant; no global material replacement
or permanent material assignment is performed. Existing lit variants replace
URP's additive contribution from this slot with the same albedo-aware response.

The adapted shaders preserve hub roof sway and sprite flash/emission/dissolve.
Terrain now blocks local light. A render-only URP shadow shape uses the existing
hub ground box; Mortfall's solid tile cells are merged into convex rectangles.
The geometry includes interiors, so underground areas remain unlit as well as
areas behind a wall. Openings are preserved;
the approximation follows solid tile cells, rather than subpixel sprite outlines.
Exposed top edges leave a six-art-pixel receiving skin so platform caps can
catch torchlight. Vertical walls, underground stone, and internal joins between
stepped tiles remain opaque. Runs split at exposed-edge changes before merging;
the inset never carves a seam under a taller neighboring tile. This also fixes
the right-hand torch in Room 001 appearing inactive when there is no decorative
wall behind it: its platform now visibly receives light.
Room Prefab Mode saves rebuild these shapes after tile edits. Hub rebuilding
also refreshes its ground shape. Gameplay colliders are unchanged.

URP's global shadow registry is scoped to the camera's scene for these blockers;
an open Mortfall preview cannot cast shadows into the hub or another preview.
There are no new normal maps, bloom or character shadow casters. The native
Light2D remains registered at zero intensity between camera renders; disabling
the source clears its intensity without interfering with URP's reload teardown.

`WorldLight2DSetup` leaves source-owned target sorting layers intact. Hub rebuilding
reattaches the forge source. The two reusable prefabs are `WarmLocalLight` and
`WarmWallTorch`; adding them to other rooms remains an explicit art-placement task.

## Validation

`LocalLightingTests` checks source ownership/layer policy, saved pilot placement,
absence of added collision, actual GPU illumination on actor/stone/sway paths,
excluded background/effects, preserved hit flash and camera isolation. The
appearance-window save test covers the new lighting fields. Reload recovery is
tested so stale editor transition markers cannot leave previews permanently paused.
The combined lighting, color-profile, preview-window, sprite-FX, hub-fog, mist
and parallax checks also cover automatic torch links and flame motion. The terrain check
renders exposed/occluded actors, a wall interior, an underground probe, and an
overlapping foreign preview to verify both occlusion and scene isolation.
`TorchLightBindingTests` verifies automatic painting detection, one light per
torch, removal, transform following, saved room coverage, bounded smooth flame
variation and independent phases.
Validation: 56 atmosphere/lighting checks passed, plus 11 room-loot checks after
the room-initialization hook was added. Flame motion uses a presentation clock,
so manual previews still animate while gameplay is paused or unfocused.
Two native preview renders during Play Mode confirmed motion: 2,212 pixels near
the torch changed, with a maximum red-channel difference of 3/255, consistent
with the intended gentle variation.

Before/after source-scene captures are written to `Logs/LocalLighting/` by
`LocalLightingSetup.PrepareCaptures` followed by `Capture`. These are visual-only
isolated scenes, with no AI, combat or progress systems. Unity compilation and a
real Play/Stop cycle were checked; the hub rendered without pink materials or new
rendering errors. A temporary source beside the actual moving player was also
rendered on/off in Play Mode: 273 pixels in the player region changed, with a
maximum red-channel difference of 35/255; dark outlines remained intact. The
probe was then removed without moving the player or changing saved profiles.
A standalone player build and target-device GPU profiling have
not been performed. Many overlapping lights need measurement before expanding
this pilot across whole dungeons.

Ground-receiver fix: 25 relevant lighting/torch/ambient/preview tests passed,
including both saved Room 001 torch platforms gaining surface illumination and
their underground probes remaining unlit. A stepped-grid regression checks that
internal tile joins do not become light leaks. On/off/shadows-disabled captures
of both torches were compared in Edit Mode and in the visual preview during Play
Mode at 480x270; no new console errors appeared. Updated terrain blocker data is
saved in the Mortfall room prefabs; existing appearance profiles are preserved.
