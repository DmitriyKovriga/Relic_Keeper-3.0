# Mortfall smart walls

Use the existing **MortFallWallRuleTile** from the existing wall palette.
Its asset/GUID and all painted room cells are retained. All ten Mortfall room
Background layers now have their Ground and Platforms linked for edge selection.

## Painting a room

Paint walls on **Background**, floors on **Ground**, and platforms on **Platforms**
as usual. The wall checks the row immediately beneath it:

- Another wall continues the masonry.
- Ground or a platform closes the wall's contact edge against that surface.
- Empty space selects an exposed, chipped underside.

You no longer need to extend a wall one cell beneath a floor just to influence
its edge. Existing painted extensions are preserved; remove any unwanted ones
with the normal wall eraser. Ground and platforms remain separate painting layers.
Windows and gaps remain empty cells; support detection never places extra wall tiles.
WallDetails is decoration and does not count as support.

Nearby wall edges refresh when support tiles are painted, erased, or undone.
This runs in the Editor and in the game, without a room scan every frame.
Keep the room's original Grid alignment. Support lookup uses world positions,
so it does not assume the support Tilemap has the same local origin.

For a new room, duplicate an existing Mortfall room to keep its linked layers.
The **Mortfall Wall Support** component on Background exposes its Ground and
Platforms references. The current ten rooms are already configured. This
component changes tile appearance only: it adds no collider or gameplay object.

## Corners and artwork

The rules normalize irrelevant diagonal neighbours: a diagonal contributes only
when both adjoining sides connect. This produces the standard 47 useful wall
patterns and prevents a distant diagonal cell from changing an exposed corner.

Thirteen exposed-bottom variants correct the old underside/corner selections.
They were produced with PixelLab's pixel tools by masking copies of the existing
Mortfall brick pixels, preserving the palette, mortar spacing and 24 PPU scale.
The remaining connected-wall sprites retain the existing art and rule ordering.
Source sheets are unchanged. The new sheet is **ExposedBottoms.png** under
**Assets/Visuals/Environment/MortfallWalls**; exact provenance and masks are in
`Mortfall_Walls_PixelLab.json`.

The original rule asset now uses MortfallWallRuleTile, a RuleTile subclass. It
keeps its 71 serialized rules, default sprite and collider values. Fixed,
single-sprite rules are the supported Mortfall configuration. Scene and prefab
references continue using the same asset GUID. The World Appearance preview
copies and remaps the support references to its own temporary room layers.

## Verification

EditMode checks cover ground/platform contacts, automatic paint/erase refresh,
irrelevant diagonals, decorative layers, translated support maps, disabling the
component, holes, collider values, all 47 shapes, pixel import settings, all ten
room links and isolated World Appearance preview references.

Before/after captures at 480×270 cover the entrance, the right side of Room 001,
and controlled hanging blocks, floor/platform contacts, a one-cell opening, a
narrow pillar and a stepped overhang. A nine-position camera pan checks motion.
These are isolated visual copies; no active scene edits are saved or discarded.
Room snapshots under ignored `Logs/WallRules/Before` allow comparison of every
original Tilemap and collider block; only generated sprite caches change.

Unity compilation and all 27 focused checks passed (walls, wall details,
World Appearance and stone relief). The comparisons were inspected in Play Mode
and the Editor returned to Edit Mode. No standalone player build was run.
