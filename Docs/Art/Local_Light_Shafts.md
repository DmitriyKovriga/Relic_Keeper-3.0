# Room lighting and ceiling shafts

Use **room fills** to make an area feel pleasant and readable. Use **shafts** to
suggest light entering through an opening or to draw attention to a location.
A visible beam alone does not illuminate the character.

## Choose a light

| Variant | Use | Starting point |
|---|---|---|
| Cool room fill | Soft skylight in a dungeon or an open area | Brightness 0.18, radius 4.5 |
| Warm room fill | Warm bounced light around a sheltered room | Brightness 0.18, radius 4.5 |
| Focused ceiling beam | A crack, entrance, important platform or landmark | Original narrow PixelLab shaft |
| Soft ceiling shaft | A wider, quieter patch of dusty light | Soft-shaft preset, opacity 8% |

Fills use actual 2D lighting on characters and nearby scenery, with the same
terrain shadows as torches. They have their own color and brightness, no flame
flicker, and no visible cone. They do not recolor distant background artwork or
combat effects. Change **World Appearance > Colors / Background** when you want
a different overall background mood.

## Place lights in a level

### Edit an existing roof beam beside its preview

Open **World Appearance > Mortfall > Beams**, then choose a room and placed beam.
Room 001 contains the initial example. This panel includes **source position,
radius, rotation, strength, opacity, color, edge softness, end fade, and dust**.
All of them update the preview immediately, including clipping against terrain.
The environment-wide brightness and dust controls remain at the top.

The shaft Inspector uses the **same controls and percentage units** as this panel.
Select a beam by its object name in the preview's **Placed beam** list, or select
that object in the room Hierarchy. Each placed instance stores its own settings.
Duplicating a shaft copies those settings; subsequent edits affect that instance.

- **Show this beam** switches only the selected beam and its dust on or off.
- **Follow environment** is enabled initially to preserve the existing look.
  With it enabled, the beam's brightness and dust are multiplied by the overall
  values above. Turn it off to use the selected beam's own brightness and dust
  density, independently of those two sliders. The environment's **Show beams**
  switch remains a master switch; the air-particle master settings still apply.

For example, to keep one shaft dim and another bright, turn off **Follow
environment** on each and set **Opacity (%)** to 5 and 20 respectively.
The percentages mean the same thing in the Inspector and the preview.

**Radius** is the distance from the source to the visible tip, in world units.
Changing it scales the existing beam shape uniformly. **Rotation (degrees)** is
the visible direction: 0 points down, positive angles turn right, and negative
angles turn left. The Scene guide and tip handle follow that actual direction.
**Strength** changes color brightness; **Opacity** changes transparency. These
controls are separate. Existing widths, lengths and placements remain preserved
internally, so the new controls do not reset your authored beam shapes.

The separate **Dust** section has Enable, Amount, Dust opacity and Drift speed.
Dust can remain visible with beam opacity or strength at zero. Turning off Show
this beam hides both. The environment's particle master switch still applies.

Press **Save** to save that room's edited beams and the environment settings.
**Compare with saved** includes beam placement and appearance; it keeps your
draft. **Discard changes** reloads the selected room's saved beams. Drafts remain
available when switching rooms during the same editor session. Close the window
only after saving changes you want to keep.

### Edit in Scene view and the Inspector

The same beam is visible, with its dust, when its room is open in Prefab Mode.
Use its Inspector fields and Scene handles for placement and shaping. Save the
room to see those changes in World Appearance. If the same beam was separately
edited in both places, the appearance window detects the conflict and preserves
the draft instead of overwriting the room.

1. Open **Tools > Relic Keeper > Visuals > World Appearance**. Choose **Mortfall**
   and the room you want to edit.
2. Open **Lighting** and press **Open this room to place lights**. This opens the
   actual room prefab; its lighting can now be selected in the Hierarchy.
3. In Scene view, focus the area you want to light. Choose **GameObject > Light >
   Relic Keeper**, then one of the four variants above. New lights appear at the
   Scene view's focus point. They are children of the open room.
4. Use Unity's Move tool (W) to position a fill in **open air**, above the floor.
   Drag its radius handle or edit **Radius** in the Inspector. Adjust **Local
   brightness**, **Color**, and **Bright center size**. A smaller center gives a
   gentler falloff. Keep fills out of solid tiles; walls intentionally block them.
5. For a shaft, put **Light source** in open air just below the opening. Use radius,
   rotation, strength and opacity; adjust the separate Dust section as needed.
   Scene handles move the source and adjust radius and rotation at the visible tip. The two
   style buttons provide editable starting values; they reset local shape and
   appearance, so use Undo if you want to recover your previous settings.
6. Save the **room prefab** (Ctrl+S). Shaft clipping is recalculated against its
   actual terrain, including platforms. The open World Appearance preview
   refreshes after the room is saved. Placement is shared by all future instances
   of this room, including generated dungeons.

Do this outside Play Mode to keep the changes. You can also drag the four prefabs
from `Assets/Prefabs/Visuals/` into an open room prefab.

## Adjust the environment

**World Appearance > Lighting > Soft room fills** has a separate enable toggle,
strength and reach. These multiply each fill's local brightness and radius.
Torch/forge strength, reach and warm tint keep their existing separate controls.
The Inspector's radius handle shows the local radius, before the environment's
reach multiplier.

**World Appearance > Beams** has both per-beam controls and environment-wide
strength and dust controls. The older **Details > Ceiling light beam** entry
opens the same controls. Beam dust also requires **Enable particles**.
Each beam's local dust amount multiplies the environment's dust setting.

Press **Save** in World Appearance to save the environment's appearance profile.
That button also saves beam edits made in its **Beams** panel to the selected room.
**Ctrl+S in the room prefab** saves edits made through Scene view and the Inspector.
Both editing routes are used in the game.

## Practical composition

Start with one weak, broad fill over a playable area. Keep dark gaps to preserve
depth. Large rooms usually work better with a few low-strength overlapping fills
than one very bright source. Because terrain blocks them, put another gentle
fill on the far side of a dividing wall when that area needs light too.

Use warm torches as local landmarks against a cooler room. Add a visible shaft
only where an opening justifies it. A soft shaft plus a restrained cool fill can
suggest daylight and light the character at the same time. Avoid putting a
visible shaft at every torch or making every corner equally bright.

Room 001 contains authored beams and a gentle cool-fill example. Existing
placement and appearance settings are preserved when updating beam masks.
V14 remains skipped.

## Rendering and artwork

Both shaft styles reuse the original PixelLab `ColdShaft.png` and PixelLab
`TorchDust` animation. No sprite pixels are changed. Exact provenance is in
`Local_Light_Shaft_PixelLab.json`. Soft fills use native URP Light2D and need no
new raster artwork. The HUD is unaffected.

Each visible artwork pixel is traced from the actual source through the solid
tile cells. The result is a 64x160 visibility mask saved with the beam and reused
by its shader and dust. Transparent sprite margins cannot block another part of
the beam. An overhang blocks only rays that actually cross it; clear rays can
pass underneath. This replaces the old parallel-column cutoff that caused a
sharp, unexplained stripe in the right-hand beam in Room 001. The original
PixelLab image is unchanged. Saving a room updates its masks automatically.
There are no terrain searches during gameplay. One small R8 texture per enabled
beam is uploaded and reused between cameras, then released with its owner.
Static geometry is supported; destructible/moving obstacles would need a
separate update. Room prefabs must retain their existing terrain light blockers
for fill shadows. Mortfall's normal prefab-save workflow maintains them.

## Verification

Unity compilation and 29 focused EditMode checks passed: beam visibility,
transformed terrain, radius/rotation alignment, real GPU mask rendering, separate
strength/opacity, per-beam saving, dust and appearance-window behavior. Saved
room masks were checked against fresh bakes after reopening the prefab.

The right-hand Room 001 beam was compared with the old and new clipping at
480x270 in Edit Mode and Play Mode. Diagnostic Before/After captures use 45%
opacity to make the cut visible; the SavedSettings capture uses the authored
values. Terrain cells, tile assets, colors and transforms were checked against
the pre-migration room snapshot and retained. No sprite pixels were changed.
A full generated-dungeon traversal and standalone player build were not run.
