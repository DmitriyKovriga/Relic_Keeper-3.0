# Source glow — V17

Torches, the forge fire and portals have small source-owned halos. The existing
flame/portal sprites are unchanged. The halo uses a new 64x64 PixelLab density
sprite with Point filtering, no mipmaps and no compression, rendered on the
480x270 world view. There is no full-screen bloom or blur.

## Editing the look

Open **Tools > Relic Keeper > Visuals > World Appearance**, choose **Hub** or
**Mortfall**, then **Lighting > Source glow**:

- **Enable glow** switches these halos on or off for the location.
- **Glow strength (%)** changes the brightness of all source halos there.
- **Glow radius (%)** changes their size. 100% uses each source's own radius.
- **Glow softness (%)** spreads the fade around the center. Higher values make
  the outer bands brighter without changing the maximum radius.

Use the existing **Save**, **Compare** and **Discard** controls. These values
share the location's appearance profile and follow hub/dungeon transitions.
They do not change the flame lighting or ceiling-beam settings.

To adjust an individual source, select its flame-light object or portal and
edit **Source glow** in the Inspector: enable, strength, radius in world units,
edge softness, color and position offset. Save the room prefab or scene as
usual. Flame colors multiply the environment's flame-light color; portal colors
multiply their sprite tint so a purple portal keeps a purple halo. Existing and newly placed torch tiles receive the same default
halo through the existing torch binding. Soft room fills do not receive halos.

## Placement

Keep the halo near the luminous part of the source. Start small: a torch's
default radius is 0.65 world units, and a portal's is 1.3. Use the offset only
when the sprite pivot is unusual. A halo is a visible glow around fire or magic;
use a room fill to illuminate a larger area, or a ceiling beam for directional
light from a roof opening.

Halos are behind actors and ordinary ground masonry. They are additive, so
they do not darken their surroundings or soften sprite edges. The underlying
Light2D still supplies terrain shadows and illumination on actors. Halos do not
perform a second terrain ray trace: keep their radius small at corners. Flames
use the same irregular breathing sample as their light, rather than an unrelated
pulse. Portals keep a steady halo beneath their existing animation.

## Ownership and artwork

`SourceGlowVisual` owns one hidden, unsaved renderer per source. It uses shared
Resources sprite/material assets and reuses its property block between cameras.
Disable/despawn releases the renderer and camera subscriptions; enable recreates
one renderer. Only a camera in the source's scene with an active atmosphere
controller can render it. The visual-only room preview copies portal appearance
without running portal gameplay. Scene-view previews use the selected Mortfall
appearance profile. Nothing is added to the UI or to unrelated skill effects.

PixelLab effect: `fe996660-eb05-488b-bee4-433fdb30d762`; image:
`bc6450fc-6160-47f4-bb5e-10409e40b202`. The PNG is exported directly from PixelLab;
its grayscale bands become glow density in the additive shader. No sprite pixels
were repainted locally. Export metadata is in `Source_Glow_PixelLab.json`.

[Open the PixelLab effect](https://pixellab.ai/create-vfx?effect=fe996660-eb05-488b-bee4-433fdb30d762).

## Verification

Unity compilation and 27 focused EditMode checks passed across source glow,
local lighting, ambient particles and the appearance window. GPU readback checks
cover strength, radius, softness, source tint/visibility, camera ownership and
location restoration. Saving an individual source retains its settings and
excludes the temporary renderer from the prefab asset.

The hub forge and Mortfall Room 001 torches/portal were compared with glow off/on
at 480x270. Play Mode captures also exercised the live hub forge and portals and
the Mortfall visual preview. No standalone player build or full dungeon traversal
was run. Existing scene and room placement were not rewritten.
