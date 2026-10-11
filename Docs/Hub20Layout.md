# Hub 2.0 layout

The authoritative environment artwork is
`Assets/Resources/HubObj/Hub2.0/HubLayout.aseprite`, copied from the supplied
`хаб_4.aseprite`. Only the layer names in this project copy were translated;
image chunks, cel coordinates, opacity and 12 × 180 ms frame timing are retained.

The original assets are organized under **Background**, **Buildings**, **Plants**
and **Trees**, with English filenames. All 62 existing asset/folder GUIDs were
preserved by AssetDatabase moves. Sprite import settings are 24 PPU, point
filtering, no mipmaps and uncompressed textures. Animation source files use
canvas pivots; individual foreground assets use bottom-center alignment.

`HubEnvironment.prefab` instantiates the native Aseprite-generated animated model.
The drawing groups are kept as the supplied document layers, preserving exact
placement and animation. Buildings and trees/plants are composite artwork layers;
edit their individual placements in the Aseprite document, then rebuild. The
standalone asset sources remain available for further Unity placement work.

Coordinate conversion is:

```
worldX = (pixelX - 779) / 24
worldY = (295 - pixelY) / 24
```

The ground surface is world Y=0. The environment has a ground collider, side
boundaries and camera confinement bounds matching the 1558×348 drawing.
The virtual camera's orthographic size is 5.625 (270 native pixels at 24 PPU),
so its viewport fits inside the new vertical confinement bounds and follows the
player instead of falling back to the whole-map center.
The old standalone LevelBounds/wall generator is removed; the new side colliders
provide boundaries without blocking the relocated floor-selection portal.
Existing Tavern, Stash and Market NPC objects are preserved and positioned near
their matching buildings. Both existing portals are preserved in the gateway
area; the floor-selection portal is placed beside the main portal so their
interaction areas remain separate. Existing labels and gameplay components stay
with their actors. The player and HubSpawnPoint start by the tavern.

The existing training dummy gameplay object is retained at the drawing's mannequin
position (top-left pixel 818,262). Its visual uses the new 27×33 mannequin through
a dedicated sprite-sheet data copy with unchanged stats/AI settings. A SpriteMask
cuts only the baked mannequin silhouette from the Buildings renderer, so the
source drawing pixels remain intact and the live dummy can flash/respond to hits
without a second static mannequin underneath. DPS and level progression remain.

The animated hub sky stays inside HubWorld. A separate, normally hidden
DungeonSharedBackground renderer is explicitly wired to DungeonController, so
dungeon transitions do not detach a renderer from the hub's animation hierarchy.
The scene's UI, player systems, dungeon settings and service references remain.

Use **Tools → Hub → Rebuild Hub 2.0 from Aseprite Layout** with HubScene open and
Play Mode stopped to regenerate the environment and replace its scene instance.
The action saves the scene and preserves the three service NPCs and two portals.
The first pre-rebuild scene backup is written to the temporary
`codex-hub-layout/HubScene-before-rebuild.unity` directory; Git also retains the
previous saved scene. The builder's dimensions/ground row describe this supplied
layout and must be updated if its canvas dimensions or ground height change.

Validation covers import settings, pixel coordinates, floor collision, the
animation loop, actor identities/positions, scene references and an actual GPU
capture. Live Play Mode validation checks player containment, running animation,
and the dungeon-background switch/restore without beginning a dungeon run or
changing saved progression.
