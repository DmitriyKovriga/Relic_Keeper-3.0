# Background depth mist — V08

Implemented 2026-10-25 by connecting the existing RelicDepthMist shader to the
existing AtmosphereController. This is a sorted world/background layer; the
screen-space atmosphere profile retains its original settings and reference.
No new raster art or imported sprite edits were needed.

## Tune the environment

Open **Tools > Relic Keeper > Visuals > World Appearance**, choose Hub or Mortfall,
then select **Mist**. Adjust the sliders beside the live preview and press **Save**
to keep both mist and color changes for that location. **Compare with saved**
shows the saved appearance; **Discard changes** restores it. Closing the window
discards unsaved drafts. No separate Inspector or setup command is needed.

**Amount (%)** controls opacity (0 disables it), **Mist color** sets its tint,
and **Wisps / clear gaps (%)** separates the haze into wisps. Mist covers the whole
view by default, including floors below the dungeon entrance. Optional **Limit
height** reveals **Vertical position**, **Thickness**, and **Soft edges** to confine
it to a fixed band. **Size of wisps** and
**Drift speed (%)** control its pattern and movement.

Presets:

- `Assets/Visuals/RelicFX/HubDepthMist.asset`: Background Mist 0.10, cool pale
  tint, Wisp Strength 0.70; centre Y 3.4, height 7, feather 2.2 world units.
- `Assets/Visuals/RelicFX/MortfallDepthMist.asset`: Background Mist 0.17,
  blue-green tint, Wisp Strength 0.80; centre Y 2, height 12, feather 3.

Background Mist controls opacity (0 disables it). Background Tint controls its
color. Wisp Strength 0 gives uniform haze; 1 gives separated wisps with gaps.
Noise Scale, Drift Speed and Quality control the existing noise shader. Depth
Mist Center Y, Height and Feather shape the optional vertical belt. The centre is relative
to the static room/spawn anchor, rather than the jumping player's position.
The presets keep shafts at zero; lantern and mote fields remain separate work.

MainCamera holds Hub Depth Mist Profile and Default Dungeon Depth Mist Profile.
DungeonDataSO's Depth Mist Profile can override the dungeon default. An empty
dungeon override/default does not inherit hub mist. For an otherwise unconfigured
camera, the existing Profile can supply hub depth settings as a fallback.

## Rendering and transitions

DepthMistLayer owns one temporary mesh, material and MeshRenderer per active
atmosphere camera. It allocates them lazily when depth effects become visible.
Its plane covers the viewport with a margin, while noise samples stay on the
24 PPU world grid. Drift is quantized to the same grid. Panning the camera moves
the viewport across that pattern; it does not carry the wisps with the camera.

Hub order on Background is Fog 5, Forest 6, depth mist 7, Ground 8, Buildings 9,
TreesAndPlants 10. Only Forest's integer order changes; the original artwork
order relative to other source layers is retained. The hub builder reproduces
that ordering. Dungeon backdrops draw below the mist; Default terrain, World
enemies, Hero attacks and overlay UI draw above it. Mist tint receives the
camera's Background color grade in the shader without binding sprite variants.

The plane is enabled only for its owning orthographic base game camera's render,
then disabled afterward. Other cameras and camera stacks do not add a second
copy. Disable/destruction releases owned resources and unsubscribes callbacks.
HideAndDontSave prevents the temporary plane/material from entering scene files
or Play Mode snapshots. No new gameplay cameras or intermediate color buffers
are introduced. Existing binding-suspension guards also suppress mist while the
Editor is crossing save/reload/Play Mode boundaries.

Dungeon entry selects its color and mist presets together. Each room resets the
mist anchor and drift clock to PlayerSpawnPosition. Returning to the hub restores
the hub preset and zero anchor. This uses the existing DungeonController flow,
without a separate environment manager or room-specific render masks.

## Setup and verification

Developer maintenance only: **Tools > Relic Keeper > Developer > Visuals > Set Up Depth Mist** creates missing presets,
wires MainCamera's serialized shader/profile references and Mortfall's override,
and updates hub background ordering. Existing preset edits are preserved on
repeat setup. The saved scene edit is restricted to these reference fields;
an open hub camera is updated separately without saving unrelated scene edits.

**Capture Depth Mist** writes 480x270 before/after hub and Mortfall samples to
ignored `Logs/DepthMist/`. It freezes drift in temporary copies for a fair
comparison, including a lower-floor view of Mortfall Room 003. World Appearance sample views show the mist draft live; these capture
commands are also under Developer > Visuals.

Unity compilation passed; **34/34** focused checks passed: six depth-mist tests,
the existing color-profile/window suites, dungeon-background checks, and the
original hub artwork/animation check. GPU tests prove background visibility,
foreground terrain/hero preservation, vertical fade, stationary world sampling
while panning, visible wisp movement when advancing time, camera isolation,
resource cleanup/re-enable, neutral no-allocation, and preset fallback/restore.

The real hub camera and isolated hub/Mortfall samples were inspected in Play
Mode. Two Play startups and a post-stop capture retained valid sprite materials
with no pink rendering. No dungeon run or player progress was changed. These
checks do not constitute a standalone player build or target-device GPU profile;
the additional transparent background draw still needs device measurement.

Next in the base integration package is V09: multiple background parallax speeds.

Coverage correction (2026-10-25): the height band is now opt-in. Previously, its
entrance-relative lower edge removed the mist on lower dungeon floors and faded
it almost completely at hub ground level. Whole-view coverage keeps the existing
saved amounts, tints and moving world-space pattern; foreground and characters
still draw above it. The focused mist/window suite passed 17/17 checks, including
coverage at distant camera positions and persistence of the height-limit choice.
