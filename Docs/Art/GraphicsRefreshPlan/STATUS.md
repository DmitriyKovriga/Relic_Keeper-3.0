# Graphics refresh status

Started 2026-10-31. `PLAN.md` remains the proposal. The user selected **B —
Collapsed fortress** after reviewing the actual comparison.
Their subsequent clarification retains the artist's original cave background;
B contributes separate architectural overlays only.

| Step | State | Evidence / next dependency |
|---|---|---|
| G00 | Delivered | Frozen native entrance/exit views, camera/profile/light/source manifest, compressed semantic room snapshot. |
| G01 | Delivered for selection | Three native concepts, original/A/B/C comparison, protected-pixel audit, terrain-edge overlay and reusable-piece notes. |
| G02 | Delivered specification, clarified | B architecture selected; original artist background retained; twelve-piece kit/connector/import specification and planned manifest recorded. |
| G03 | Pending | Production art follows the selected module specification. |
| G04 | Pending | Tile refinement follows selected art; shared rule/GUID contracts remain intact. |
| G05 | Pending | Room 001 architecture integration and actual gameplay verification. |
| G06 | Pending | Authoring/reuse in two inspected additional room shapes. |
| G07 | Pending | Separate UI art treatment after the environment pilot. |
| G08 | Pending | Integrated structural/rendering/gameplay/cost verification and handoff. |

## G00 — frozen current-room baseline

Source revision: `8b383bc046bf44fe4cc643d7772067a0f4139649`. The tracked working
tree was clean at preflight; the new plan folder was untracked. There were no
existing room edits to reset or archive. Source assets were read directly and
the existing `WorldColorPreviewStage` produced disposable visual copies.

- [Entrance](Baseline/Entrance.png), [3x entrance](Baseline/Entrance_3x.png),
  [exit](Baseline/Exit.png), [3x exit](Baseline/Exit_3x.png).
- [Framing and source hashes](Baseline/Framing.json): actual orthographic camera
  settings, source room GUID/transform, static actor transforms, serialized color
  and atmosphere profiles, placed point/shaft source values and dependency hashes.
- [Semantic snapshot](Baseline/RoomSemanticSnapshot.json.gz), lossless gzip JSON:
  every occupied cell's tile GUID/local ID, collider type and tile matrix; map
  transforms/anchors; Grid configuration; collider, Rigidbody2D and one-way
  effector configuration; player/enemy spawn and portal transforms/configuration.
  The [summary](Baseline/Summary.json) lists counts. Ground has 7,647 occupied
  cells, Platforms 8, Background 140, Environment 10 and WallDetails 12. There are
  9 collision-related components, 11 spawn components and one portal.

The primary camera is `(4.81, 6.14, -10)`, orthographic size `5.625`, 16:9,
480×270; exit is `(20.14, 6.02, -10)` with the same projection. Coordinates are
recorded at full precision in the manifest. These are authored comparison views,
not claims about a recorded moving game camera.

The baseline uses actual source sprites at their existing scale, with the preview's
static Rogue/Zombie/Mold samples. The portal is visible in the separate exit view.
The current scene's actual HUD artwork was cloned and initialized as a **staged
reference**, using real Ninja Stub/Venom Strike/Ice Skates icons. The serialized
50/50 resource values and level 30 are fixture text, not a real character build.
No room AI, physics, player progression or save system runs in these copies.
This is an accurate source-art composition reference, not a gameplay test.

## G01 — architecture alternatives

See [the comparison](Concepts/Comparison.png), [2x comparison](Concepts/Comparison_2x.png),
[terrain-edge overlay](Concepts/TerrainEdgeOverlay.png) and
[design/feasibility notes](Concepts/DESIGN.md).

The built-in image-generation tool edited the same frozen entrance image for
each direction. The high-resolution originals remain under `Concepts/Raw/`;
[prompt/provenance records](Concepts/PROMPTS.md) distinguish them from the final
480×270 concepts. Generated HUD/actor drift, including a missing lower HUD in C,
was rejected as comparison content and corrected from the original pixels.

The editor-only preparation helper resamples artwork to native resolution with
nearest-neighbor sampling and restores the exact original Ground/Platforms,
HUD, actor and environment-decoration pixels using separately rendered alpha
masks plus one-pixel clearance. The [audit](Concepts/Preservation.json) reports
**48,888 protected pixels, zero differences in A/B/C**. Cyan contours on the
terrain overlay mark the same source-terrain boundary in all four panels.

Color-profile/light settings are frozen from G00. Paintover architectural shading
is illustrative; it is not a newly simulated Unity lighting result. These are
pixel-aware concepts, not final sprite sheets, seamless tiles, normal maps or
proof of integrated gameplay. The cave gaps and ledges stay legible at native size.

## Verification and limits

Unity compilation passed. The focused `MortfallWallTests` and
`WorldColorProfilesWindowTests` run executed **16 passed, 0 failed/skipped**.
The native captured baseline, all three final concepts, comparison and terrain
overlay were visually inspected. The bounded Unity error query was empty.
Only an editor-only evidence/preparation helper and documentation/concept files
were added. No scene, prefab, tile, shared rule, sprite importer or runtime code
was changed. No production architecture was imported into `Assets`.

The source-room hash matched before/after capture. The handoff
[verification record](Verification.json) checked all 208 source hashes with no
changes, resolving 14 virtual package inputs through the installed package cache.
The snapshot also records GameObject layers/activation and the 32×32 ignored
collision-layer matrix. The original main save remains unchanged. No
standalone build, traversal, combat test, GPU budget or normal-map quality result
is claimed for G00–G01. Those belong to the selected integrated pilot.

## G02 — selected B target and kit specification

The user explicitly chose B in chat on 2026-10-31, then clarified that their
artist's background must stay. The [B concept](Concepts/B.png) and
[3x review](Concepts/SelectedTarget_3x.png) are architectural references, not
approval of their AI-painted cave backdrop. Preserve the original background
asset/import/reference/parallax; add sparse opaque stone modules with transparent
openings over it. The next native-piece preview must show this retained backdrop.
[Module specification](G02_ModuleSpecification.md) records the twelve planned
sprites, sizes, pivots, connector geometry, render/color roles and relief limits.
[Asset manifest](Assets/manifest.json) labels every slot as `specified-not-created`:
there are no invented sprite GUIDs or generation results.

PixelLab's live capabilities were inspected: revision
`pro-flash-presets-48-128-2026-09-29` supports custom native canvases in four-pixel
steps up to 256×256. The proposed 24/48/72/96-pixel dimensions preserve the existing
24-pixel world grid; no generator default forces a 16/32-pixel grid conversion.
Custom-size output still needs actual dimension/export validation in G03.

Graphify's local code-only cache was refreshed after the editor-helper changes.

Next is G03: create and review the selected native kit. Production art, prefab
placement, shared-tile refinements and integrated gameplay checks remain pending.
