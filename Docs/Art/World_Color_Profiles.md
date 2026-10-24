# World color profiles

World color profiles adjust rendered colors without changing sprite textures,
sprite imports, renderer colors, sorting layers, or physics layers. This is the
foundation for V01/V02/V03 of the visual atmosphere plan. It does not add lights,
mist, parallax, bloom, or contact shadows.

## Tune and save

Open **Tools > Relic Keeper > Visuals > World Appearance**:

1. Choose **Location**: Current game, Hub, or Mortfall.
2. Choose **Colors** to adjust Characters, Environment, and Background, or
   **Mist** to adjust background haze directly beside the preview.
3. Press **Save** to keep both colors and mist for that location.

Changes appear immediately. **Compare with saved** temporarily displays the saved
appearance without losing your edits. **Discard changes** restores both colors
and mist. Unsaved drafts survive switching locations within the window; closing
or reloading the window discards them and restores the assigned game profiles.

The window connects to MainCamera automatically. Start by moving **Colors >
Background > Saturation (%)** down to 60, or change **Mist > Amount (%)** to see
an obvious change. The inline view excludes HUD and keeps native pixel scaling
when the window is large enough. Maintenance and diagnostic commands are under
**Tools > Relic Keeper > Developer**, outside this everyday editing workflow.

Window sliders show brightness, contrast, and saturation as percentages: 100%
means unchanged. Brightness at 50% halves linear brightness; 200% doubles it.
The underlying profile asset stores brightness in exposure stops (0 = unchanged,
+1 = twice the linear brightness), contrast/saturation as multipliers, and a tint.
Neutral settings preserve RGB and alpha. Grading keeps HDR values and never
changes coverage, sprite silhouettes, or animation geometry.

The starter assets in `Assets/Visuals/ColorProfiles/` are:

| Profile | Use |
| --- | --- |
| Neutral | Original colors; useful for comparisons |
| HubDay | Very small actor brightness lift and quieter, slightly cooler background |
| Mortfall | Same readable actors with a more subdued dungeon backdrop |

These are restrained starting points, not a completed art pass.

## Membership and environment assignment

The existing **Atmosphere Controller** on MainCamera holds **Hub Color Profile**
and **Default Dungeon Color Profile**. Each **Dungeon Data** asset can override
the latter through **Color Profile**. Entry selects the dungeon override or
default; return restores the hub. A dungeon with no override and no default is
neutral, rather than accidentally inheriting hub colors. Preview overrides take
precedence until the preview window closes.

Add **World Color Group** to a renderer or parent object for explicit membership:
Characters, Environment, Background, or Unaffected. The closest enabled group
wins. An actor's PlayerStats/EnemyEntity boundary prevents a room-wide group
from recoloring the actor as environment; put explicit actor overrides on the
actor itself or below it. Influence controls how much of the profile to apply.
Combat Effect additionally multiplies this by the profile's Effects Influence.
Existing temporary VFX and skill projectiles are recognized automatically.

The hub prefab explicitly assigns Ground/Buildings/TreesAndPlants to Environment
and the distant art/fog to Background. The hub builder recreates these groups.
Existing render categories supply defaults for actors, weapons, remains and
combat VFX; legacy sorting layers provide a fallback for otherwise unclassified
content. These defaults do not change drawing order. HUD canvases and grounding
shadows are excluded. Current world status bars use an overlay canvas and remain
ungraded.

New room renderers are registered on room creation. Existing sorting and
temporary-VFX initialization paths register newly spawned content without a
scene scan every frame. A custom spawn path that bypasses both should call
`WorldColorGrading.RefreshRoot(instance.transform)` after assigning its materials.
The window refreshes scene groups automatically when reconnecting. A single
combined sprite remains a single grading region: split the art
or author a mask if it needs independent adjustments within that sprite.

## Rendering and ownership

The system caches shared temporary material variants by original material,
group, influence, and effect flag. It preserves original material assets and all
MaterialPropertyBlock values. Disabling the last atmosphere controller restores
original materials and releases variants. Editor save/reload guards remove
temporary variants before serialization, then recreate them for preview.

Grading uniforms are uploaded for each camera. A camera without an enabled
Atmosphere Controller sees neutral colors; Scene view previews MainCamera's
profile. Screen Space Overlay UI is unaffected. Custom sprites under a Canvas
are excluded from material binding as well.

Supported shaders: URP Sprite Lit/Unlit Default, legacy Sprites/Default (mapped to
graded URP Unlit), Relic Sprite FX, Hub Building Wind, and Hub Fog. The URP shader
adaptations retain 2D lighting, normals, sprite flips, vertex colors and skinning;
review them when upgrading URP. Relic Sprite FX applies grading before hit flash,
emission and dissolve. Unknown custom shaders (including AuraHalo) are deliberately
left unchanged so their behavior is preserved. The preview does not substitute
an incompatible shader.

Serialized shader references on the saved hub camera keep the graded Lit/Unlit
shaders reachable in player builds. No extra cameras or intermediate color
buffers are introduced. Performance still needs measurement on target hardware.

## Verification

`WorldColorProfilesTests` covers neutral rendering, independent roles, live
changes, effect influence, flash/dissolve, camera isolation, reversible shared
materials/property blocks, and hub/dungeon fallback/restore. Rendering checks
require a graphics device. Nearby atmosphere, hub fog, and layout checks protect
existing behavior. Manual motion and hub/dungeon inspection supplement EditMode
checks; a shader compiling alone is not proof of visual correctness.

Implementation validation (2026-10-24): Unity compilation passed; the 14 profile
checks passed, including GPU comparisons and real scene/prefab-save checks.
Existing sprite/atmosphere rendering checks passed (5/5), and hub fog checks
passed (3/3). Hub layout checks passed 7/8: the existing NPC placement assertion
expects X = -19.333334, while the saved scene has X = -18.35. This feature changes
only four profile/shader reference fields in HubScene and does not move NPCs.
The mismatch remains for separate correction.

The actual hub camera was inspected in Play Mode at 480x270 with neutral,
assigned hub, and switched dungeon settings. Source-art hub/dungeon comparisons
are in `Logs/ColorProfiles/`; regenerate them with **Tools > Relic Keeper > Developer > Visuals
> Capture Color Profile Examples**. No standalone player build or target-device
performance measurement was performed.

Window usability follow-up (2026-10-24): removed the temporary copy's NotEditable
flag, replaced collapsed inspector fields with expanded percentage sliders, and
added an inline world preview plus automatic scene registration/reconnection.
The combined UI/profile suite passed 16/16 tests. Background saturation was also
changed through the actual window and visually checked, then discarded to keep
the saved profile unchanged.

Play Mode lifecycle fix (2026-10-24): the pink-world failure was reproduced with
the preview active. Temporary materials were still referenced by Unity's scene
snapshot when assembly-reload cleanup destroyed them, leaving missing material
slots in both Play Mode and the restored edit session. The Editor now restores
originals at ExitingEditMode/ExitingPlayMode and blocks all rebinding until the
corresponding Entered mode. SessionState carries this protection across domain
reload. Duplicated renderers that copied a variant before their first sorting
update are restored as well.

For sessions already affected by the old bug, **Tools > Relic Keeper > Developer > Visuals >
Repair Missing Preview Materials** reads saved scenes in isolation and repairs
only missing material slots. It preserves valid materials, transforms, other
unsaved scene edits, and color-profile values; it does not overwrite scene files.

Validation: the 17 profile/UI checks passed, including the new snapshot/rebinding
regression. The actual Save Profile button was used with the user's current
settings, followed by Play with the window open, Stop, and a second startup with
the window closed. Both live captures and the post-stop capture had zero missing
material references. Before/after captures and material reports are in
`Logs/ColorProfiles/transition-*`.

Preview options: use **Location** at the top of the window to choose
**Current game**, **Hub**, or **Mortfall**.
Selecting a sample view loads its assigned color profile. Mortfall also offers a
**Room** selector populated from its dungeon data, in room-name order,
with **Previous** and **Next** buttons for browsing.
**Whole room** switches from the spawn-area composition to an overview;
**Characters** toggles static player/monster poses.

Sample views use isolated, visual-only copies of the artwork. They do not enter a
dungeon, start spawners/AI, create collision, or alter player progress. Sample
lighting is flat and independent of game-scene lights. Stock lit sprite
materials receive temporary unlit adapters that preserve their authored values;
no Light2D objects are registered by the samples. Use Current game for live
lighting and effects. Profiles are applied to the selected preview camera.
The current game camera remains on its own assigned profile while viewing a
sample. Temporary drafts are kept separately per profile when switching views;
Save writes the selected location's color and mist drafts, and closing/reloading the window
still discards unsaved drafts. Play Mode transition protections also apply to
these sample views.

V08 follow-up: hub and Mortfall sample cameras now include the environment's
background depth mist. The **Mist** tab edits it directly using a temporary draft.
See [Background depth mist](Depth_Mist.md) for its controls and lifecycle.

Preview validation (2026-10-24): all 20 focused profile/window tests passed,
including draft separation, visual-only Mortfall scene cleanup, and coexistence
with the game's global light. Room browsing and whole-room framing were checked
through the actual window. With Mortfall selected, entering Play Mode rendered
both the hub game camera and Mortfall sample correctly, with no missing game
material references.

Unified appearance editor (2026-10-25): 14 focused window/depth-mist checks passed,
including persistent saving of both drafts, saved comparison, discard, and close
cleanup. The actual Mist slider, saved comparison and Discard changes were also
checked visually in Mortfall; the user's saved settings were preserved.
The window also recovered its Mortfall preview after entering Play Mode; no
rendering errors were returned by the filtered console check.
