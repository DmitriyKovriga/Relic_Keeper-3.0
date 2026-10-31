# G02 — collapsed fortress target and starter kit

Selected by the user on 2026-10-31: **B — Collapsed fortress**.

## Selected architecture and retained original backdrop

Use [B](Concepts/B.png) and [3x review](Concepts/SelectedTarget_3x.png) as
architectural references. The user subsequently clarified that their artist's
existing cave background must stay. B's painted cave backdrop is not part of the
selected replacement target. The original background PNG, sprite/import identity,
dungeon reference and existing parallax presentation are retained.

One broad broken background arch is
the dominant form. Sparse supporting piers and exposed masonry are secondary.
Retain open cave pockets and the existing muted teal/charcoal stone language.
Foreground terrain caps, actor/HUD pixels and G00 comparison settings are fixed.

The concept was normalized to native resolution and polished by restoring the
protected source pixels, but its background was not among those protected regions.
It therefore illustrates architecture only after the user's clarification.
Reconstruct its construction
rhythm with modules; do not reproduce every AI-painted crack or add walkable art.
The room's existing small decorative opening and large cave gaps remain legible.
New pieces are separate overlays over the hand-painted backdrop: opaque stone,
transparent interiors/gaps, and enough open space to keep the original cave art
clearly visible. Do not generate a replacement cave image, silhouette, full-screen
wall filler or flattened room texture. The next piece preview uses the original
G00 backdrop with only the arch/support modules added.

## Twelve planned pieces

The machine-readable specification is [Assets/manifest.json](Assets/manifest.json).
All dimensions are native pixels; PPU is 24. Pivots below are normalized Unity
sprite coordinates, bottom-left origin. Every item remains **specified, not created**.

| ID | Sprite / size | Pivot | Contract |
|---|---|---|---|
| B01 | Arch left, 96×72 | 0,0 | Left upper quadrant, 4×3 cells; spring at local bottom-left, apex at top-right. |
| B02 | Arch right, 96×72 | 0,0 | Matching right upper quadrant; apex at top-left, spring at bottom-right. |
| B03 | Broken arch left, 96×72 | 0,0 | Same spring and mating coordinates as B01; restrained missing/chipped crown end. |
| B04 | Broken arch right, 96×72 | 0,0 | Same spring and mating coordinates as B02; complementary damaged crown end. |
| B05 | Pier middle, 24×48 | .5,0 | Repeat vertically; compatible stone/mortar bands at both ends. |
| B06 | Pier base, 48×24 | .5,0 | Shaft connects at top center; restrained footing, background only. |
| B07 | Pier capital, 48×24 | .5,0 | Shaft below; support/arch spring above. |
| B08 | Broken pier termination, 24×24 | .5,0 | Compatible shaft bottom; chipped upper end with no floating debris. |
| B09 | Masonry span, 48×24 | 0,0 | Quiet horizontal repeat between supports; never a foreground platform. |
| B10 | Minimal junction, 24×24 | 0,0 | Only the required pier/span interface; no ornamental universal connector. |
| B11 | Wall-face patch A, 48×48 | 0,0 | Broad connected masonry variation; does not change an occupied-cell silhouette. |
| B12 | Wall-face patch B, 48×48 | 0,0 | Restrained chipped-face alternative; same material/grid, no damp streaks/moss clutter. |

Two 96×72 quadrants form an approximately eight-unit-wide, three-unit-high arch
cap. Treat 12 native pixels as the initial ring thickness; it is a production
target to inspect at 1x, not a collision value. Local connector coordinates:
B01 spring `(0,0)`, apex `(96,72)`; B02 apex `(0,72)`, spring `(96,0)`.
Adjacent apex sections share matching 12-pixel material bands, with transparent
interiors. The damaged versions preserve the support endpoints while removing
part of the upper join. Use paired pieces with a controlled gap; no free fragments.

Generate/check the intact pair first. Derive broken variants with exact pixel
editing from approved geometry where practical. Avoid independently generated
joins that almost connect. Mirroring geometry is allowed only after checking
baked highlights and any future normal orientation. Do not rescale curves to fit
arbitrary spans; inspect the two reuse rooms before authoring another radius.

## Rendering, import and placement roles

- New world sprites: Point, no mipmaps, no compression, alpha preserved, 24 PPU,
  no NPOT rescaling. Full documented canvas retained; trim/pivot changes must not
  move connectors. Review alpha, tile edges and export dimensions before import.
- Backdrop arch/support/span modules: existing `RenderDepthCategory.Background`,
  sorting layer `Background`, ordinary local offset relative to existing cave
  layers, `WorldColorRole.Background`, full influence, no combat-effect flag.
  Reuse shared stock URP Sprite-Unlit material with the existing grading path.
  They are distant matte artwork, so the initial modules need no local-light
  normal map. Confirm mist/column ordering in the actual pilot before recording
  numeric placement orders; do not guess them here.
  Keep the original cave renderer and its existing parallax layers intact;
  architectural overlays must not become a replacement background system.
- Wall patches: Environment color role, aligned to the existing wall face and
  rendered below accepted WallDetails, using its current sorting context. They
  remain separate new art, never replacements for the shared RuleTile sprites.
  Do not place a changed diffuse face in the existing stone-relief pilot region
  until matching new normal data is reviewed, or retain the old face there.
- Proposed owned group: `MortfallRoom_001/MortfallArchitecture`, with `Backdrops`
  and `WallFaces` children. These are future G05 paths, not existing placements.
  Record actual coordinates/GUIDs/orders in `Pilot/placements.json` once placed.
- Modules add no colliders, triggers, physics, interactions, animation loops or
  independent materials. Static placement/edit/remove/undo uses ordinary prefab
  authoring. A tile palette is optional only if the selected patches benefit
  from painting; it is not another maintenance workflow.

## Production route and acceptance

PixelLab's inspected Pro Flash capabilities allow native custom canvases in
four-pixel steps from 16 to 256 for creation. All selected sizes fit those
reported bounds, including 96×72 and 24×48. That is a capability check, not proof
that generated output will meet its seam/alpha/size contract. Preserve exports
and generation IDs; review/correct actual pixels before importing them.

The twelve canvases total 38,016 texels (148.5 KiB of raw RGBA8 diffuse pixels).
This excludes atlas padding/pages, GPU/CPU copies, metadata and any future
normals; it is not a measured memory/performance budget. G08 sets the integrated
cost budget from comparable measurements.

G03 deliverables: native transparent source art, one consistent material palette,
exact-size/alpha/pivot/import results, joint/seam sheet at 1x and integer zoom,
prefabs for the selected roles, and real manifest identifiers/provenance. Reject
pieces that depend on smoothing, opacity, noisy tiny detail, changed ledges or
bright grading to resemble the concept. Existing tiles remain installed through
G03; any alternate base-art study is the separate scoped G04 step.
