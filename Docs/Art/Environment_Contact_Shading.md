# Environment contact shading — V05 pilot

Implemented 2026-10-25. This is a restrained pilot on the hub's two building
foundations and Mortfall room 001, using ordinary prefab decoration. The other
Mortfall rooms are unchanged pending an art review of the first room.

## Art and placement

`Assets/Visuals/Environment/ContactShade.png` is a 64x8 transparent strip authored
through PixelLab's exact pixel drawing tool, drawing ID
`15bf4e6f-a5d6-433f-902a-0cf5ad18b6e0`. Four rows use the same cool shadow color
`#10151d` with alpha 112, 72, 40, 16; the remaining rows are transparent. Narrow
end insets soften the last two bands. Native import: 24 PPU, point filtering,
no mipmaps, no compression, top-left pivot. No existing art was uploaded or changed.

The reusable `Assets/Prefabs/Visuals/EnvironmentContactShade.prefab` contains a
SpriteRenderer and WorldColorGroup (Environment), with 70% color alpha. The strip
uses the existing stock URP unlit material. Default sorting order 200 puts it
over normal terrain, below the character contact shadow and World/Hero actors.
Color profiles include it with the rest of the environment.

HubEnvironment has two strips under the tavern and warehouse, beginning one
source pixel below the grass edge. The existing composite roof/eave shadows are
already strong, so this pass does not add a second dark band over them. The hub
rebuild tool recreates these two decorations without moving the source art.

MortfallRoom_001 has eight short floor-joint and ceiling-recess stamps. The
editor baker finds empty cells adjacent to a connected wall and solid floor or
ceiling in the Ground tilemap. It does not outline free platform ends. Stamps
stay inside the solid material with one pixel of clearance from the surface;
two-sided narrow niches use shorter stamps to prevent overlap. No tiles,
colliders, spawn points, portals or camera bounds are edited.

## Authoring and comparison

**Tools > Relic Keeper > Developer > Visuals > Apply Environment Contact Pilot** prepares
the shared stamp and rebuilds the two pilot prefab groups. It replaces only
their EnvironmentContactShade groups, so repeating it does not add duplicates.
It does not save or rebuild the open scene. Existing connected prefab instances
inherit the decoration. The builder exposes ConfigureRoom for future room pilots.
This baker targets the existing axis-aligned, one-unit Ground tile grid.

For manual decoration, instantiate the shared prefab under the owning prop,
set its width and color alpha, and keep it inside that prop's contact region.
Changes to a baked group are replaced by the corresponding bake command;
edit the placement tool for changes that must survive rebuilding.

**Capture Environment Contact Pilot** captures on/off samples at 480x270 into
ignored `Logs/EnvironmentContact/` (Hub-before/after and Mortfall-before/after).
It uses isolated visual copies without running room gameplay. The existing
World Appearance sample views also include these baked sprites automatically.

## Verification

Unity compilation passed. Five focused tests cover repeated baking, no added
physics, selective corner placement, ceiling containment, and a GPU on/off check
that the floor visibly darkens while its bright top pixel row stays unchanged.
The original nine-layer hub artwork/animation check also passed: **6/6 total**.
Before/after samples were inspected in Edit Mode and Play Mode. The latter used
visual copies; no dungeon run or player progress change was required.

There is no runtime generator, per-frame scan, new light, or additional camera
in gameplay. Decorations belong to their existing room/prop prefab and follow
its normal spawn/unload lifecycle. Added cost is two static sprites in the hub
and eight in the pilot room; device frame time has not been measured.

V08 is implemented in [Background depth mist](Depth_Mist.md). The remaining
base-integration step is V09: multiple background parallax speeds.
