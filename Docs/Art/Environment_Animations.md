# Small environmental animations — V11

Implemented 2026-10-25: one smoke plume at the tavern chimney and occasional
ceiling drips throughout Mortfall. The original Room 001 pilot is now expanded
to 79 sources across all 10 room prefabs (6–10 sources per room). Existing building
motion and source-light flicker remain part of their own systems.

## Editing

Open **Tools > Relic Keeper > Visuals > World Appearance > Details**.
Choose Hub for the chimney, or Mortfall / any room / **Ceiling drops** for drips.
**Drop location** visits each saved spot in that room, framing the landing area
even when its ceiling is above the camera.

- **Small animations** enables the smoke and drip emitters.
- **Chimney smoke (%)** changes smoke opacity; 0 hides it.
- **Water drops (%)** changes drop frequency; 100 is roughly one drop per seven
  seconds, 200 is more frequent, and 0 disables drops.
- Use the same **Save**, **Compare with saved** and **Discard changes** controls.

These fields live in each location's existing `AtmosphereProfileSO`. The inline
preview frames the corresponding source. Its time continues while gameplay is
paused or unfocused. New emitters inherit the location's settings.

## Art and placement

All new raster art was authored in PixelLab's VFX workbench. Source sheets and
exported atlas metadata are saved in `Assets/Visuals/EnvironmentAnimations/`:

| Clip | Frames | Cell | Playback | Palette |
| --- | --- | --- | --- | --- |
| ChimneySmoke | 16 | 32×48 | 8 fps loop | muted grey-green, four colors |
| CaveDrip | 8 | 16×16 | formation/fall/impact phases | desaturated slate-teal, four colors |

Both use 24 PPU, point filtering, transparent backgrounds, no mipmaps and no
texture compression. The smoke's stable origin is (12,43) measured from the
top-left of its cell; the drip uses (8,8). Atlas frames retain this fixed origin.

The smoke includes a thin wisp at its base, so the rising billows stay connected
to the chimney lip. Its saved hub-layout anchor is (208,132), converted with the
existing hub builder. It does not move the chimney or building; hub rebuilding
reattaches it. `ChimneySmoke.prefab` and `CaveDrip.prefab` are reusable sources.

The drip placement is checked against all collidable tilemaps, including
platforms. Its vertical fall ends at the first solid cell, with a 1.5–30 unit
clear gap, and stays at least 0.8 units horizontally away from torches along its
path. The impact aligns to the landing surface. No gameplay collider is added.

`MortfallDripSetup` selects spaced, deterministic sources from solid ceiling
undersides in the air region connected to the player spawn. Sealed wall cavities
are excluded. Four-unit source spacing and coverage along wider ceilings avoid
putting all visible water at the opening corner. Tall ceilings are included;
the earlier six-unit height cap excluded most of the room. One-way platforms
can catch drops but cannot emit them. A blocked
short gap is rejected rather than traced through, and sources have different
phases so they do not all drip together. Placement is baked into each prefab
and refreshed when saving a Mortfall room in Prefab Mode; no runtime terrain
search is added. Rebuilding replaces generated sources rather than duplicating
them. Custom sources with other names remain untouched.

The attempted ceiling-rim fade was removed: the straight seam was texture
bleeding, not bright terrain art. Original tile opacity is restored.
`MortfallSprites.spriteatlas` packs ground, wall and water sprites with eight
pixels of separation, point filtering, no rotation, no tight packing and no
mipmaps or compression. This keeps neighboring source-sheet rows out of the
sprite's edge at fractional camera positions. Tile choices, collision shapes
and original image files are unchanged. The atlas is included in player builds.

PixelLab edit links:

- [Tavern chimney smoke](https://pixellab.ai/create-vfx?effect=6eb5a40d-61e1-4cf2-94d5-f284935559d8)
- [Cave water drip](https://pixellab.ai/create-vfx?effect=b8cdfca7-322a-4753-ab2f-e36d54a928d4)

The authored recipes and direction notes are in
`Environment_Animations_PixelLab.json`. The workbench rendered these recipes;
the built-in image generator and local image painting were not used.

## Runtime and preview

`EnvironmentAnimation` owns one persistent SpriteRenderer under its `Visual`
child. Camera callbacks select the frame, opacity and whole-pixel fall offset,
then restore the renderer's initial state after rendering. The emitter transform
stays anchored; there is no per-drop object spawning or material instantiation.
Render settings exclude HUD and keep these details behind characters.

The drip waits for 6/frequency seconds, forms for 0.25 seconds, falls with a
visual acceleration of 9 units/s², and plays a 0.3-second impact. Different source
phases and a slow smoke loop keep the ambient timing calm. Ground checks happen
while placing the prefab; no physics simulation or raycast runs per frame.

Disabling a source or its location restores its visuals. Hub/dungeon switching
uses the existing controller's assigned profile; isolated appearance previews
copy the same emitter data. These effects have no damage, status, collision or
invulnerability behavior.

## Verification

`EnvironmentAnimationTests` covers the quiet/formation/fall/impact sequence,
frequency controls, sprite import settings, saved placements, torch clearance,
actual GPU rendering, camera ownership, and restoration of temporary frame,
position, opacity and visibility. The appearance-window save test covers the
new fields as part of the existing Save action. Native 480×270 source-scene
captures are in `Logs/EnvironmentAnimations/`.
Unity compilation passed. The combined environment-animation, lighting, color,
preview-window, sprite-FX, fog, mist and parallax suite passed 59 tests; the
26 relevant color/window/animation checks were rerun after the final binding
change and passed as well.
The source-scene smoke/fall/impact sequences were inspected at 480×270 during
Play Mode; the Play/Stop cycle completed without new console errors. This did
not enter a real dungeon run or alter player progress.

The expanded placement and ceiling treatment are covered by `MortfallDripTests`.
The seam was reproduced in full-HD captures at fractional camera positions;
padding removed the leaked edge pixels. `MortfallSpriteAtlasTests` checks the
packing/build settings and actual GPU edge colors across eight camera offsets.
Animation checks include a drop from an offscreen ceiling landing inside the
view. Placement checks cover tall ceilings, coverage, sealed cavities, platform
interception, fire clearance, repeatability and every saved preview spot.
The corrected version compiled and passed 32 focused checks. Full-HD seam
captures were repeated in Play Mode after a reload; the actual terrain sprites
were confirmed bound to the padded 512×512 atlas. Multi-drop reference-resolution
fall/impact captures were inspected as well. No dungeon progression was started.
Standalone builds and target-device performance profiling remain unmeasured.
