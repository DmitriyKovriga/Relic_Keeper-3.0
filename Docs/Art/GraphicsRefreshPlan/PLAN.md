# Relic Keeper — graphics refresh implementation plan

Date: 2026-10-30. Inspected revision: `8b383bc0`.
Status: **planning only; art direction is not selected**.
No new concepts, textures, prefabs, runtime changes or Unity checks are delivered
by this document. The steps and output paths below describe future work.
See [handoff prompt](HANDOFF.md) and [inspected-source snapshot](source-audit.json).

## 1. Objective and accepted scope

Make the existing game look more deliberately authored through architecture,
tile artwork, composition and a coherent UI treatment. Start with one Mortfall
room; choose a concrete visual direction before producing a large asset set.

The user considers current combat/movement adequate and rejected another pass
over weapon rhythm. Do not turn this project into an attack-timing or game-feel
redesign. The atmosphere, combat and whole-game feedback passes already exist.
They are the baseline, not a new checklist of missing features.

Work order:

1. Three visual alternatives of the **same existing room and camera view**.
2. Selection of a direction, followed by a small modular architecture set.
3. Tile-quality refinement and real Unity integration in Room 001.
4. Reuse/authoring check in two other existing room shapes.
5. A separate UI art pass using the established visual language.

Large new structures are visual backdrop or wall-face treatments. They do not
add walkable platforms, traversable holes or solid obstacles. Keep playable
geometry, camera framing, spawn points, portals and loot-container behavior.
Room composition/enemy pacing and boss staging remain user-owned (former
J07/J08). Audio remains deferred. New authored normal-attack body poses, generic
contact bursts, material-hit catalogs and reactive-prop systems are outside scope.

## 2. Verified project baseline

Source inspection and existing delivery reports support the following; none is
a new Play Mode or performance verification for this plan:

| Area | Existing implementation / implication |
|---|---|
| Runtime | Unity 6000.3.11f1, URP 2D. Keep the project pipeline. |
| World art | 24 PPU; Mortfall detail tiles are 24x24 on a one-unit grid. Room 001 Grid has cell size `(1,1,0)`. Match existing imports and local transforms. |
| Display | 480x270 logical reference. UI PanelSettings also uses 480x270; its UI PPU values are a separate system, not a reason to set UI sprites to world PPU. |
| Rooms | Ten `MortfallRoom_*.prefab` assets under the existing MineDungeon resource folder. Shared tiles/materials mean edits can affect all ten. |
| Walls | `MortfallWallRuleTile` / `MortfallWallSupport` already handle support from Ground/Platforms, exposed undersides and normalized diagonal neighbors. Existing tests cover 47 canonical shapes and the retained shared rule GUID. |
| Detail painting | WallDetails exists in all ten rooms. The documented Room 001 sample uses a palette of seam-following moss and fine cracks. It has no collision. |
| Atmosphere | Color groups/profiles, depth mist, parallax, local lights, contact shading and source halos exist. Stone-relief normals are a bounded pilot tied to current diffuse art. |
| Background | Existing parallax already includes sparse distant stone columns. A larger pillar is not automatically new depth; its scale, framing and material treatment must improve the composition. |
| Preview | World Appearance uses a temporary visual-only scene. It copies art and remaps wall-support references; it does not run normal AI, physics or save systems. |
| Feedback | The GameJuicePlan status records delivery and ordinary-play review. Preserve its exit cache, UI transactions, attack warnings and status effects. |

The user rejected earlier damp streak/floating moss samples and removed V14
forge/winch prop pilots. Do not silently restore those assets or make decorative
clutter the main improvement. Keep accepted wall details and their authoring.

Read these focused references before implementation:

- [Mortfall smart walls](../Mortfall_Smart_Walls.md)
- [Wall details and rejected/accepted artwork](../Surface_Variations.md)
- [World color profiles and preview](../World_Color_Profiles.md)
- [Layered parallax](../Layered_Parallax.md)
- [Local lighting](../Local_Lighting.md), [contact shading](../Environment_Contact_Shading.md), [stone relief](../Stone_Relief.md)
- [Completed game-feedback status](../GameJuicePlan/STATUS.md)

## 3. Art direction alternatives — not three biomes to implement

All three alternatives must use the same baseline screenshot, platform edges,
camera position/zoom, actor scale and HUD. Keep lighting/profile settings fixed
for the primary comparison; otherwise brighter grading can masquerade as better
architecture. A second explicitly labeled lighting study may follow.

| ID | Direction | Large forms / materials | Avoid |
|---|---|---|---|
| A | Monumental ruins | Heavy stone piers, deep recessed bays, broad lintels, restrained carving. Large quiet masonry masses with a few strong structural accents. | A forest of narrow columns, ornate trim on every edge, hiding landable surfaces. |
| B | Collapsed fortress | Layered exposed masonry, broken decorative arches, chipped faces and incomplete wall structures. Retain a strong underlying construction rhythm. | Painting apparent holes through solid gameplay terrain, rubble that looks walkable, random noise everywhere. |
| C | Burial complex | Repeated shallow arches, sealed niches, restrained funerary stone bands. Distinctive cadence and recess depth without increasing visual density. | Bright fake doorways, implied new interactions, new religious/lore claims presented as accepted canon. |

These are art hypotheses. The user has not selected a favorite. After producing
the concrete A/B/C comparison, ask which direction to develop. This question is
about a missing creative choice, not permission to begin the preparatory work.
Complete the comparison and asset feasibility notes before asking; do not build
all three directions into the live game or choose silently.

## 4. Implementation sequence

### G00 — Capture and freeze the comparison baseline

Use `Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab`.
Inspect its current instance rather than relying on the old atmosphere screenshots.
Select one view containing a platform edge, wall mass and some backdrop; select
a second view near the exit for the pilot validation. Record camera positions
and projection, room transform, current appearance profiles and light settings.

Deliver under `Docs/Art/GraphicsRefreshPlan/Baseline/`:

- Native 480x270 PNG and nearest-neighbor enlarged review image.
- Camera/framing manifest and source GUIDs.
- Baseline semantic room snapshot: Ground/Platforms occupied cells, transforms,
  tile collider types and collision configuration, existing wall occupancy,
  player/enemy spawn transforms, portals and one-way platform settings.
- Relevant serialized/source hashes. Preserve a reversible pre-change snapshot
  if the working tree already contains user edits; never reset unrelated files.

The current ordinary-run evidence is context only, not a matching before image:
[review contact sheet](../GameJuicePlan/FinalReview/OrdinaryRunEvidence.jpg).

### G01 — Produce three comparable concepts

Deliver `Concepts/A.png`, `B.png`, `C.png`, `Comparison.png` and a short design
note. Use native-resolution or pixel-aware mockups, showing the actual hero,
enemy and portal scale. Label image edits/paintovers as concepts, not rendered
Unity implementation. Do not flatten a painted room into the shipped backdrop.

For each direction identify:

- One dominant architectural motif and one secondary motif.
- Playable ledge treatment and background value separation.
- A list of reusable pieces versus unique illustration work.
- Likely authoring effort per additional room and any material/normal-map risk.

Concept edits must keep the actual traversable silhouette legible. AI imagery
does not prove geometry preservation: compare an overlay of original terrain
edges and the unchanged actor/HUD locations. Reject attractive concepts that
depend on a new camera, bigger hero, moved platforms or painted-in walkable art.

Use relevant image-generation/editing skills for concept images when appropriate.
For production pixels, prefer native pixel-art workflows such as the installed
PixelLab tools. Inspect current tool schemas, dimension support and export data;
do not change the 24-pixel grid to fit a generator's 16/32-pixel defaults.

### G02 — Lock the selected room target and module specification

Record the user's selection in `DECISIONS.md`. Polish one target composition
and create an asset manifest before bulk generation. If the user mixes motifs,
write a single coherent target rather than shipping three incompatible kits.

Starter families below are provisional, not a mandatory asset count:

| Family | Purpose | Starter contents / constraints |
|---|---|---|
| M01 Support | Large structural rhythm | Repeatable shaft and compatible base/capital, normally aligned to 24-pixel increments. Use asymmetric variants only when they add visible value. |
| M02 Arch/lintel | Recognizable construction | Left/right termination and repeatable span. Positioned behind existing gameplay; it must not suggest a new path through a solid wall. |
| M03 Recess/niche | Controlled depth | One restrained inset family compatible with the selected stone. Keep its interior quieter than threats and interactables. |
| M04 Wall-face variation | Break obvious repetition | A few coherent broad-block variations or patches, not a unique random texture per cell. Match mortar continuity. |
| M05 Broken termination | Controlled damage | A small compatible set of chipped/broken decorative ends. Preserve the existing gameplay cap and collision silhouette. |
| M06 Junction/trim | Connect the kit | Minimal transitions where supports meet spans and existing masonry. Add only joints needed by the pilot. |

Prefer an initial kit of roughly 8–14 reusable sprites, adjusting after the
selected concept. This is a scope target, not a production estimate. Record each
sprite's actual width/height, PPU, pivot, material, layer, edge compatibility,
repetition axis and intended role. Multi-cell modules may be larger than 24x24;
single-cell RuleTile sprites must continue to match their 24x24 contract.

### G03 — Create the small production kit

Suggested new asset locations (create only when this phase begins):

- `Assets/Visuals/Environment/MortfallArchitecture/` — selected native art.
- `Assets/Prefabs/Visuals/MortfallArchitecture/` — reusable static modules.
- `Assets/Resources/Sprites/Pallets/Mortfall/Architecture/` — optional palette
  assets only if the chosen pieces benefit from tile painting.
- `Docs/Art/GraphicsRefreshPlan/Assets/manifest.json` — provenance, dimensions,
  GUIDs, pivots, material/layer roles and export/generation identifiers.

Use 24 PPU for world sprites, Point filtering, no mipmaps and uncompressed
alpha-preserving imports consistent with current art. Review at 1x before 3x.
Preserve source PNGs and existing `.meta`/sprite identities; add new art as new
assets before replacing anything shared. Retain editable sources/provenance.

Avoid per-cell GameObjects for basic masonry, unique materials for each module,
new physics, Update loops or a new shader framework. Reuse existing sprite
lighting/color-group paths. A single large sprite must still tile/join without
filtered seams. Check asymmetrical lighting before relying on mirrored pieces.

### G04 — Refine tile quality without breaking smart walls

Architecture and base-tile changes have different blast radii. Keep them separate.
First try art-side broad masonry variation and decorative end/junction pieces.
If actual wall/ground sprite changes are needed, prepare an alternate sheet and
test it in an isolated preview or a temporary scoped tile mapping. Do not edit
the shared rule's sprite references merely to obtain a one-room comparison.

The installed shared `MortFallWallRuleTile.asset` has retained GUID/rule contracts.
Do not rebuild it as a plain RuleTile, replace its GUID, delete its 47 supported
shapes or change fixed-rule semantics. Shared promotion is a deliberate later
scope decision with all-room verification; there is no free per-room replacement
when every room references the same asset.

If diffuse art is replaced, inspect secondary-texture/normal bindings and the
existing stone-relief pilot. Old normal maps may no longer match the new pixels.
Regenerate/rebind appropriate normals, or explicitly preserve the old art in
that patch while integrating new pieces elsewhere. Do not silently ship mismatched
lighting or disable accepted features globally to make the comparison pass.

`MortfallSpriteAtlasSetup.Apply()` recreates its packable list from source paths.
An ad-hoc Inspector addition may be lost on the next rebuild. Integrate approved
new sources into the existing setup only if atlas packing is needed, preserving
its padding/rotation/filtering constraints and existing entries. Avoid a second
competing atlas-maintenance tool.

### G05 — Integrate the Room 001 pilot

Add a clearly owned visual-only architecture group/layer under the existing
room hierarchy. Its name/location must be included in the placement manifest.
Use reusable modules with semantic roles and stable pivots. Do not alter existing
Ground/Platforms cells, Grid origin/scale, wall occupancy or collision. A dark
painted recess is not permission to cut a real hole into the room.

Respect the existing render stack and WorldColorGroup membership. Use the
current sorting/category facilities; avoid enormous sorting-order overrides.
Keep actors, attack warnings, portal/cache cues and floor boundaries readable.
Architecture behind terrain must not cover a surface that catches the player.
Foreground silhouettes are optional and must stay away from combat/landing paths.

Freeze current profile/light settings for the main before/after. If later
lighting adjustments help the selected artwork, record those separately so the
user can distinguish the art gain from grading changes.

Deliver `Pilot/Before.png`, `After.png`, a native contact sheet, a short camera-pan
and ordinary-combat capture, plus `Pilot/placements.json`. Each placement record
contains room GUID/path, parent path, module GUID, local position/rotation/scale,
sorting role and color membership. Record actual coordinates; do not invent them
in this planning document before the concept is selected.

### G06 — Make the kit practical to author and reuse

Verify that modules can be placed, moved, removed and undone through ordinary
Prefab/Tile Palette workflows. No maintenance button should be required to keep
saved art visible. Use the existing WallDetails layer for compatible face details;
do not turn it into a second collision or support-detection layer.

Try the kit in two additional existing rooms with different proportions. Select
them after inspecting the current room list, not by assuming that a particular
room number has a desired layout. Reuse the kit and keep local quiet areas; do
not give every room the same central arch. Record any genuinely missing connector
before expanding the asset set. Initial pilot scope is not an automatic mandate
to repaint all ten rooms; follow the implementation chat's authorized rollout.

### G07 — Separate UI art pass

Begin after the environment language is selected and the Room 001 pilot works.
Keep UI architecture, controls, layout, hit targets and localization intact.

Produce a small shared component sheet: panel edge/corner, inset slot, button
normal/hover/pressed/disabled, selection and focus, title/divider. Materials may
echo the selected environment, but text and semantic colors retain contrast.
Do not decorate every border equally or replace stable state cues with ornament.

Start with a static inventory screen and recruitment screen at 480x270 in EN/RU.
After reviewing the coherent treatment, wire one screen, then the other; HUD is
the final subset, retaining its current readable footprint. Use nine-slicing only
where the sprite's borders and pixel alignment support it. Avoid changing input
logic, prices, party rules, window ownership or data presentation during reskin.

The named `relic-keeper-pixel-ui` skill was not found at the documented user-skill
path or in the searched project/plugin skill folders during planning. Rediscover
it if available in the implementation session; otherwise use current project UI
as the concrete fallback. This does not block the environment phase or require
an unsolicited plugin installation. Inventory has existing UXML/USS; Tavern is
a partial C# UI Toolkit controller. Inspect actual builders before assuming one
stylesheet can reskin every screen or converting everything to a new UI system.

Preserve the circular passive-node transaction feedback corrected in the latest
playtest. UI is outside world grading/camera effects. A new UI theme must not
erase the implemented inventory/crafting/skill state distinctions.

### G08 — Verify and hand off

The definition of done is visible improvement in the actual game plus preserved
behavior, not the existence of generated textures or an attractive mockup.

1. **Structure:** compare the semantic room snapshot. Ground/Platforms cell
   occupancy, local transforms, collider types/configuration, one-way rules,
   spawns and portal transforms remain equivalent. Decorative modules add no
   gameplay colliders, blockers or interactions. Review all unexpected prefab
   diffs before saving/promoting shared assets.
2. **Tile/asset checks:** imports, sprite rectangles/pivots, palette discovery,
   connections, holes, hanging blocks, one-cell gaps, inner/outer corners and
   floor/platform contact. If shared tiles change, inspect all ten rooms.
3. **Native rendering:** exact-camera before/after at 480x270, plus integer-scale
   enlargement. Inspect a camera pan for seams, shimmer, mirrored-light errors,
   parallax artifacts and coverage gaps. Examine both local light and quiet areas.
4. **Actual gameplay:** normal combat, jumping/landing, enemy projectiles/status
   effects, cache access before/after portal unlock, and room transition. World
   Appearance is useful for art checks but cannot validate physical traversal.
5. **UI branch:** EN/RU, long names, empty/full inventory, hover/focus/disabled
   states, crafting/selection, tooltips, keyboard/gamepad, pause and window reopen.
6. **Cost:** compare texture memory, added renderers/materials, batches and
   frame timing in an equivalent scene. Set an explicit budget after measuring
   the pilot; do not invent performance guarantees or compare different builds.

Use the existing Unity bridge skill and narrow relevant checks. Candidate
EditMode classes already present (run only those affected by the actual change):

- `RelicKeeper.Tests.EditMode.MortfallWallTests`
- `RelicKeeper.Tests.EditMode.MortfallSpriteAtlasTests`
- `RelicKeeper.Tests.EditMode.SurfaceVariationTests`
- `RelicKeeper.Tests.EditMode.WorldColorProfilesTests`
- `RelicKeeper.Tests.EditMode.WorldColorProfilesWindowTests`
- `RelicKeeper.Tests.EditMode.StoneReliefTests`
- `RelicKeeper.Tests.EditMode.RoomLootCollectorTests`

Confirm current discovery before invoking a class. Existing art-path assertions
must not be deleted just to make a new sheet pass; update only assertions whose
contract was deliberately changed and retain the preserved-behavior coverage.
Add focused regression coverage only for actual new integration/logic risks.

Typical sequence, from the project root:

```powershell
agents-unity-bridge get-status
agents-unity-bridge compile
agents-unity-bridge run-tests --mode EditMode --filter RelicKeeper.Tests.EditMode.MortfallWallTests
agents-unity-bridge get-console-logs --limit 20 --filter Error
```

Use separate targeted filters as needed. Record passed/failed/skipped counts and
visual evidence separately. Do not claim a new runtime check from old reports.
Use an isolated run/save where gameplay traversal is needed; preserve the main
save and user edits. A UI-only or art-only proof is not a complete standalone
build check; record exactly which verification was performed.

## 5. Concrete touchpoints

Paths are existing unless explicitly described as proposed new output locations.
Inspect current callers/serialized references before changing them.

| Purpose | Existing source or asset |
|---|---|
| Pilot room | `Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab` |
| Actual room list | `Assets/Resources/Dungeons/MortFallDungeonSO.asset` |
| Shared wall rule | `Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/MortFallWallRuleTile.asset` |
| Ground/platform sheet | `Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-ground-platforms.png` |
| Corrected exposed ends | `Assets/Visuals/Environment/MortfallWalls/ExposedBottoms.png` |
| Detail layer prefab | `Assets/Prefabs/Visuals/MortfallSurfaceDetails/WallDetails.prefab` |
| Detail palette | `Assets/Resources/Sprites/Pallets/Mortfall/WallDetails/` |
| Wall logic/support | `Assets/Scripts/Visuals/Tiles/MortfallWallRuleTile.cs`, `MortfallWallSupport.cs` |
| Existing wall authoring | `Assets/Scripts/Visuals/Editor/MortfallWallSetup.cs` |
| Existing atlas authoring | `Assets/Scripts/Visuals/Editor/MortfallSpriteAtlasSetup.cs` |
| Atlas asset | `Assets/Visuals/MortfallSprites.spriteatlas` |
| Isolated preview | `Assets/Scripts/Visuals/Editor/WorldColorPreviewStage.cs` |
| Render/color membership | `Assets/Scripts/Visuals/WorldRenderSorting.cs`, `Assets/Scripts/Visuals/Atmosphere/WorldColorGroup.cs` |
| Inventory style/layout | `Assets/UI/Inventory/InventoryStyles.uss`, `InventoryLayout.uxml` |
| Recruitment UI | `Assets/UI/Tavern/TavernUI.cs` and its existing partial files |
| HUD | `Assets/UI/PlayerHUD/HUDController.cs` and its existing partial files |
| UI reference settings | `Assets/UI/Inventory/PixelArtPanelSettings.asset` |

## 6. Delivery and completion records

Create `STATUS.md` when implementation starts, listing each G-step as pending,
in progress, delivered or intentionally omitted. Keep `PLAN.md` as the proposal
and `DECISIONS.md` as selected choices. A rejected concept is not an implemented
feature; a reusable palette is not evidence that every room was decorated.

Final handoff includes selected comparison, native art sheet/provenance, palette
or prefab authoring instructions, actual placement manifest, preserved-geometry
results, narrow test outputs and live captures. State pilot/rollout/UI coverage
and remaining choices precisely. Avoid a new catalog of optional effects at
closeout: this phase succeeds through artwork and composition.

## 7. First action in the implementation chat

Read this plan, inspect the current room and make the baseline plus A/B/C
comparison (G00–G01). Present the three actual alternatives and their reusable
asset implications, then obtain the art-direction choice. The author of this
planning document has not created those alternatives or selected the style.
