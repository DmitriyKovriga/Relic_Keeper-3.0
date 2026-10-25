# Small environmental animations — V11

Implemented 2026-10-25: one smoke plume at the tavern chimney and one occasional
water drip beneath the stone ceiling in Mortfall Room 001. Existing building
motion and source-light flicker remain part of their own systems.

## Editing

Open **Tools > Relic Keeper > Visuals > World Appearance > Details**.
Choose Hub for the chimney, or Mortfall / Room 001 for the drip.

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
platforms. Its vertical fall ends at the first solid cell, with a 1.5–6 unit
clear gap, and stays at least 0.8 units horizontally away from torches along its
path. The impact aligns to the landing surface. No gameplay collider is added.

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

This is a two-source pilot; other dungeon rooms do not gain ceiling drips yet.
Standalone builds and target-device performance profiling remain unmeasured.
