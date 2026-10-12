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

Fog has a separate runtime loop: 8 times slower (17.28 seconds), with smooth
crossfades between the original frames and 40% of the source opacity. Its dedicated
material softens edges over 1.5 source pixels and fades from world Y=0 to completely
transparent at Y=6.5. Other artwork keeps its original animation speed and material.
The builder regenerates a clip override excluding Fog, so the imported animator
cannot overwrite this independent loop. The Aseprite source remains unchanged.

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

The warehouse/stash NPC uses `Characters/WarehouseNpcIdle.png`: nine 28×28
frames at 24 PPU, 200 ms per frame, looping over 1.8 seconds. It replaces only
the visual on `StashNPC.prefab`; the storage service, label and scene position
remain unchanged. Reapply with **Tools → Hub → Apply Warehouse NPC Animation**.
The center pivot keeps the opaque feet at floor height for the existing y=0.5 root.

This idle was generated through the official PixelLab MCP `animate_image` tool
from the supplied `warehouseNPC.png`, with the same sprite pinned as the end
pose: job `100506e7-cb67-4a13-952c-c65a8f3995f9`, seed 1210, eight generated
frames plus the original first frame, one generation charged. The first result
was accepted after reviewing every frame: gentle upper-body breathing with
fixed lower four pixel rows and no redesign. All nine delivered frames are
included without scaling or repainting. The Animator changes only the sprite.

The market NPC uses `Characters/MarketNpcIdle.png`: five 28×28 frames at
24 PPU, 300 ms per frame, looping over 1.5 seconds. **Tools → Hub → Apply
Market NPC Animation** updates only the existing `MarketNPC.prefab` visual.
The market service, label and saved scene placement are retained. Its pivot
(14,14 pixels) places the fixed opaque feet on the floor for the y=0.5 root.
The label uses its original local y=0.5 position. The merchant is 22 pixels
tall, matching the tavern NPC, with the same squat proportions and sparse shading.

PixelLab MCP provenance: the earlier tall Pixflux merchant was rejected for
style mismatch. The replacement is a direct Pro Flash costume edit of the
tavern sprite, job `4e626e5b-19e5-4c97-82c0-59697c52b0f9` (seed 1217).
Only transparent padding was removed from the 32×32 output to obtain 28×28;
no character pixels were resized. Idle job
`b98cdb04-37ba-449a-8dd2-dea61f7be28c` (seed 1218) produced four frames plus
the original pose. PixelLab's free workbench stabilized the head (rows 0–13)
and lower body (rows 21–27), retaining the generated torso movement.
Final workbench id: `fe01c9d9-c5b8-4d7b-88aa-c0d0fd4e8b65`. The exported frames
were arranged into a horizontal sheet without resizing. This replacement cost
six generations (five for the sprite, one for idle). GUIDs and gameplay objects
are retained; only the replacement artwork is imported.
