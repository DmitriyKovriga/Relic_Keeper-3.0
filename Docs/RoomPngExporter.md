# Room PNG Exporter

Open **Tools → Dungeons → Room PNG Exporter**. Choose a dungeon, select one or
more rooms, choose an output folder, then click **Export selected rooms and open
folder**. The window also displays the last exported PNG. The default folder is
`Pictures/RelicKeeper/RoomExports`.

PNG filenames match the room prefab name, for example `MortfallRoom_001.png`.
Exporting the same room again replaces its existing PNG in the selected folder.

The catalog combines opening, normal and boss room paths, with duplicate rooms
listed once. The exporter reads saved room prefabs, so save room edits before
exporting. It requires the dungeon's Background Sprite Resource Path.

Default framing uses the room's camera bounds to exclude large off-screen boundary
walls. Rooms without camera bounds fall back to visible art bounds. Enable
**Include outer prefab terrain** when the artist needs the entire terrain prefab.

The PNG includes room geometry/decorations plus the dungeon's tiled
background, with no enemies, player or UI. The background uses its native sprite
scale, tiled over the full room instead of stretched. Animated decorations are
frozen at the first frame. Standard Sprite-Lit room materials use neutral white
lighting for a consistent art reference; camera postprocessing is excluded.
Parallax movement has no single full-room camera view and is represented by a
static background in this export.

Native output is **24 pixels per world unit**. Padding is measured in native
pixels. Optional 2×/4× enlargement repeats pixels without interpolation. Give the
artist the 1× version for drawing at the game's pixel density. The exporter snaps
the image bounds outward to the pixel grid to avoid clipping room edges.

Each capture uses an isolated Editor preview scene. Room entry/spawning are never
called, scripts are disabled, and embedded enemies are hidden. Open gameplay
scenes, room prefabs and save data are not edited. Exporting is disabled in Play
Mode. All preview objects and GPU/readback textures are released after success
or failure. Oversized images are rejected before image allocation; lower the
scale/padding rather than silently reducing pixel density.

Relevant verification: `RelicKeeper.Tests.EditMode.RoomPngExporterTests`, including
an actual GPU export of MortfallRoom_001 and checks for preview-scene cleanup and
preserving the active scene.
