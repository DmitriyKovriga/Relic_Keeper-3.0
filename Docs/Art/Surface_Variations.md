# Mortfall wall details — V13

The accepted fine crack remains in Room 001. The damp streak and floating moss
blobs were rejected and removed. Wall growth is now painted with a native Unity
Tile Palette on a separate **WallDetails** Tilemap. Six thin moss patterns follow
the existing staggered mortar; two additional tiles make fine branching cracks.

## Paint details in Room 001

1. Open **MortfallRoom_001** in Prefab Mode.
2. Select **WallDetails** under the room's Grid in the Hierarchy.
3. Open **Window > 2D > Tile Palette**. Choose **Mortfall Wall Details** in the
   palette dropdown, and **WallDetails** as the painting target (called Active
   Tilemap or Active Target, depending on the editor layout).
4. Select a pattern from the palette and paint onto the existing masonry.
   Use the palette's Eraser to remove details. Save the room prefab normally.

The top row has four horizontal/junction moss variants. The bottom row has two
descending seam patterns, then two hairline cracks. Empty spaces separate the
swatches; pick one occupied square at a time. The sample uses only three wall
cells near the Room 001 entrance. Leave most stone untouched and vary the swatches.

Keep **WallDetails** aligned with **Background**, with scale 1 and rotation 0.
Each tile is 24×24 pixels at 24 PPU, matching the room's one-unit grid. Paint only
inside stone faces, away from openings and platform caps. These patterns are tuned
to Mortfall V2 masonry's mortar spacing; they are not universal ground tiles.
For weaker growth, reduce **WallDetails > Tilemap > Color alpha** in the Inspector.

## Add the painting layer to another room

All ten existing Mortfall rooms already have **WallDetails** under their Grid.
Rooms 002–010 start empty; Room 001 keeps its painted sample. Select the layer
and paint with the same palette in any room.

For a newly created room, drag **Assets/Prefabs/Visuals/MortfallSurfaceDetails/WallDetails.prefab** under that
room's existing **Grid**, alongside Background. The reusable layer starts empty;
select it as the Tile Palette target and paint normally. Keep its sorting above
Background and below props/characters (the current Mortfall defaults are
Background -1, WallDetails 0 on the Default sorting layer).

The original **CrackedMasonry.prefab** remains available as a larger, movable crack
stamp in the same folder. The palette and Tile assets live in
**Assets/Resources/Sprites/Pallets/Mortfall/WallDetails**.

## Rendering and preservation

The overlay has no collider, rigidbody, shadow caster or runtime update component.
It uses environment color grading and the existing local-light response. It does
not replace wall art, add normal maps or change movement, spawn points or portals.
All source art remains unchanged. Point filtering, uncompressed transparent pixels,
clear tile borders and disabled mipmaps avoid blurred edges and sheet seams.

The authoring setup preserves the accepted crack's placement and any existing
painted layer, including erased sample tiles. It removes only the two rejected
prefab samples, and does not spread decoration into other rooms automatically.

Artwork was made in **PixelLab**. The accepted crack's recipe remains in
`Surface_Variations_PixelLab.json`; the native 96×48 sheet and exact recipe are
recorded in `Wall_Details_PixelLab.json`. Earlier damp/moss recipes are retained
only as rejected art history, not shipped assets.

## Validation

Focused EditMode checks cover native palette discovery, all eight tiles, transparent
borders, pixel import settings, collision-free layering, existing layout preservation
and preservation of manual paint/erase edits. Native 480×270 room comparisons and
a camera pan use an isolated preview copy under `Logs/SurfaceVariations`.
This is a Room 001 art sample; broader room decoration remains level-design work.
