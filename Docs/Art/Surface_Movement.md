# Surface movement effects — V15

Ground contacts use the existing `GroundingVisualController`, with two authored
surface presets: soft earth in the hub and restrained stone grit in Mortfall.
The same presets serve the player and grounded enemies. No scenery, movement
physics, combat values, invulnerability, or save-progression fields are added.
V14 remains skipped.

## Editing and preview

Open **Tools > Relic Keeper > Visuals > World Appearance > Details**.
Select **Hub** or **Mortfall**, then **Ground movement** in **Preview area**.
The visual-only sample loops running, a short hop and a landing. It does not
start room gameplay, add preview colliders, or move the actual player.

Under **Movement on ground**:

- **Enable ground effects** toggles authored earth/stone contact effects.
- **Running dust (%)** controls footstep frequency. 0% hides running dust while
  retaining landing feedback. The actor must travel a stride and wait a minimum
  interval; standing still, moving slowly, or jittering in place produces none.
- **Visibility (%)** adjusts all authored ground-contact effects.
- **Landing strength (%)** changes the size of landing feedback, within a fixed
  maximum. It does not change damage or knockback.

Press **Save** to keep changes for that location. Compare/discard use the same
temporary-draft workflow as existing colors, mist, lights and ambient details.
Defaults: 75% running dust, 85% visibility, 100% landing strength.

## Behavior

The supporting collider determines the surface. Hub `GroundCollision` uses
**Earth**. Mortfall's solid ground and one-way platform tilemaps use **Stone**.
At ledges, nearby foot support takes priority over a distant lower floor.
Contact effects remain at their world-space origin after the actor moves away.
Dust cannot snap to a remote floor during an airborne action.
Physical dust shares the environment's color profile and torch lighting. Running
puffs trail the feet; landing grit spreads to either side, behind the character.

The controller records downward speed during a fall. Contacts below the surface
preset's landing threshold remain quiet; harder landings get a wider, slightly
stronger puff. Heavy actors have a bounded bonus. Both relative width and total
world width are capped, including falls at extreme speed and large enemies.
Re-enabling a controller clears pending fall/stride state.

Normal takeoff and ground dashes use the surface palette and a short contact
puff. Ground dashes no longer add the old scrape on top of that puff. Air-jump
art, dash-jump trails, dodge afterimages and unrelated skill effects retain
their existing behavior. Unassigned surfaces retain the legacy grounding FX.

## Assets and maintenance

All four eight-frame clips were made with **PixelLab MCP vfx_workbench**:
`EarthStep`, `EarthLanding`, `StoneStep`, `StoneLanding`. Exact recipes, effect
IDs and image IDs are in `Surface_Movement_PixelLab.json`. Exported pixels are
unchanged; Unity slices the atlases at 24 PPU with point filtering and no mipmaps.

The clips and **Earth.asset / Stone.asset** live in
`Assets/Visuals/SurfaceEffects`. Advanced stride, clip, speed threshold and size
limits can be tuned in those surface assets. `GroundSurfaceVisuals` links a
supporting collider/tilemap to its preset without changing that collider.
The hub builder preserves its earth assignment; saved Mortfall rooms carry
their stone assignments. A future biome can assign its own surface preset.
No automatic terrain scan or extra movement component is installed per actor.

## Verification

Unity compilation and 73 focused EditMode checks cover surface selection,
ledges, cadence, impact limits, saved controls, preview, jumps, ground dashes,
shadows, lighting and existing ambient details. Hub contact frames were also
captured from the live player in Play mode; earth and stone preview sequences
were checked at the 480×270 reference resolution. All four exported clips were
inspected. Full room traversal and a standalone player build were not run.
