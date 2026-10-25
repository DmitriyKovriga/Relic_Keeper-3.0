# Layered background movement — V09

Implemented 2026-10-25 using the existing hub artwork and ParallaxEffect's
camera-follow convention: 0 is fixed in the world, 1 follows the view.

## Adjust it

Open **Tools > Relic Keeper > Visuals > World Appearance**, choose Hub or
Mortfall, then **Movement**. Higher scenery percentages suggest greater distance.
Use **Move preview** for a gentle left/right camera movement, or position it with
**Look left / right**. These controls only move the sample view.

**Save** keeps colors, mist and movement together for the selected location.
**Compare with saved** and **Discard changes** apply to all three. The existing
mist amount, tint, drift and color-profile tuning are preserved.

## Layers and ownership

The hub sky follows horizontally at 100%. Mountains and their castle share the
90% far setting; distant forest uses 65%; near forest and both existing fog
crossfade renderers use 30%. Ground, Buildings, TreesAndPlants, masks, contact
shadows, colliders, actors and services stay at their authored positions. Vertical
movement defaults to zero in the hub to keep the forest rooted at ground level.
The Aseprite sprites, animation and roof sway are unchanged. Rebuilding through
Hub20Builder recreates the layer group.

Mortfall retains its original tiled cave backdrop at 90% and adds four sparse
middle-distance stone columns at 65%, behind depth mist and gameplay terrain.
Vertical motion multiplies each layer's strength by 75%. Columns have native
24 PPU artwork, point filtering and no collision. **Distant columns** shows or
hides them; visible stone is opaque, with distance conveyed by its quiet palette.
The existing large backdrop still supplies continuous coverage.

LayeredParallaxGroup applies pixel-rounded absolute camera offsets immediately
before rendering, then restores every authored transform after the render. It
also restores on disable. This avoids cumulative drift, animation overwrites,
scene-save offsets and another camera inheriting a shifted composition. Arrays
are reused; there is no scene scan or allocation per frame.

DungeonController resets the reference to each room's camera-bounds center,
alongside the existing backdrop alignment. The legacy one-layer component is
temporarily disabled where a layered group owns the backdrop, and restored for
unconfigured dungeons. Disabled hub/background roots do not render their group.
Sample previews use native tiled Mortfall artwork, matching the game rather than
stretching a single copy to the preview window.

## Art provenance and checks

MortfallDistantColumn.png was generated with PixelLab Pixen, job
`de486f36-f983-47b0-a3d9-066c3c14e932`, seed 102509, one generation. PixelLab's
free workbench removed generated debris outside the central shaft, result
`23446e02-a953-4ee5-a315-580c838add33`. No source artwork was uploaded or repainted.
The sprite's top/bottom borders keep its caps outside the tiled central section.

Unity compilation and 33 focused checks passed: real rendered hub/Mortfall layers,
fixed ground pixels, restored transforms, different parent/child speeds, native
pixel offsets, room resets, legacy fallback, unified saving, mist and hub fog.
Still captures at multiple positions are in ignored `Logs/Parallax/`.
The animated hub sample was inspected in the actual editor window during Play
Mode; native 480x270 hub/Mortfall captures were also inspected during that run.
The new columns are visible behind Mortfall terrain. Play was stopped afterward;
no dungeon run or progression change was needed for these checks.
This is a hub/Mortfall pilot; other biomes need their own selected artwork.
No standalone player build or target-device GPU measurement is claimed. Mortfall
adds up to four transparent background renderers per active backdrop.

Solid-stone follow-up: replaced the 40% alpha fade with opaque stone and a
show/hide control. Existing positive `parallaxSilhouettes` values mean visible,
so saved profiles work without resetting other settings. PixelLab workbench
result `d53e598a-b32f-4630-97da-1e6b9561afa4` removed the remaining 62 pale edge
pixels and remapped the generated greens/black to four subdued blue-gray stone
tones. Sprite coverage remains binary; the existing mist still draws in front.
A GPU regression changes the cave behind a pillar from red to green and requires
the pillar's interior pixel to stay identical while the open cave changes.
Compilation and 24 focused parallax/window/mist checks passed after this change.
Mortfall captures at native 480x270 were inspected during Play Mode: stone is
solid, the cool palette stays subordinate to terrain, and the pale edge debris
is gone. Existing mist and movement settings were retained.
