# G01 — architecture comparison and feasibility

All panels use the frozen Room 001 entrance camera and staged existing HUD.
They are image-generation paintovers with exact source-pixel protection, not
screenshots of implemented architecture. [Comparison](Comparison.png),
[native A](A.png), [native B](B.png), [native C](C.png).

Selection clarification, 2026-10-31: B's architecture is selected, while the user
retains their artist's original background. Background pixels were not protected
in these early concepts, so their generated cave treatment is not the production
target. New arch/support pieces will be shown over the original cave artwork.

## A — Monumental ruins

**Dominant:** broad stone piers and deep rectangular recesses. **Secondary:**
heavy high lintels and plain recessed bands. Large masonry faces read as designed
construction rather than a continuous small-brick texture. Keep the playable
caps untouched and the broad background masses subdued behind actors.

Reusable candidates: shaft middle (one or two variants), base, capital, repeatable
lintel, left/right terminations, recess back and a small broad-block wall-face
patch family. Rough starter scope: 8–12 pieces. The individual illustrated stone
cracks must become a few reusable patches, not a unique painted room backdrop.

Additional-room authoring is likely the simplest: a few support/recess clusters
and joined spans per view. Main risk is overusing the same vertical rhythm or
making every room feel like a column hall. Native joints, cropped capitals and
partial spans need review in short rooms. Matte normal-map response should be
authored for the new diffuse pixels; existing relief maps do not fit these blocks.

## B — Collapsed fortress

**Dominant:** one broad broken decorative arch. **Secondary:** layered masonry
and a restrained family of damaged structural ends. The retained open cave areas
keep the silhouette distinctive without covering the screen with wall art.
Damage belongs to the distant construction, with no foreground rubble or new
landing surface. Existing platform caps distinguish real terrain from the arch.

Reusable candidates: left/right spring sections, a small fixed-radius curved
segment family, crown/junction, two broken ends, pier shaft/base and two wall-face
patches. Rough starter scope: 10–14 pieces, likely several multi-cell modules.
Do not stretch a single flattened arch illustration to fit every room. Background
cave silhouettes must retain the existing artist's art instead of shipping a new
room bitmap. Keep the original background's texture/import/reference/parallax.

Additional-room authoring needs more care than A: place one arch cluster, then
fit its supports/terminations around each room's proportions. A second radius
is only justified if the two reuse rooms expose a real need. Main risks are arch
connectors, curves that lose clarity at 1x, and damage that accidentally implies
a traversable opening. Mirrored directional shading/normals need separate review.

**Selected:** B gives the desired large-form change. The user's selection is
limited by the explicit original-background preservation requirement above.

## C — Burial complex

**Dominant:** repeated shallow arched sealed niches. **Secondary:** plain
horizontal stone bands and inset joints. Filled recess backs keep the rhythm
architectural, without suggesting new doors, interactions or religious canon.
The treatment is denser and more regular than B; preserve occasional quiet bays.

Reusable candidates: paired arched sides/crown, a sealed back, narrow pier, base,
capital, band repeat and junctions, plus one compatible face variation. Rough
starter scope: 8–12 pieces. Entire unique carved panels are unnecessary. The
paintover's continuous back wall must translate into repeatable modules and
selected empty/quiet areas, not one giant image.

Additional-room authoring is moderate: repeat a few bays and adjust their
termination at each room edge. Narrow spaces require partial-bay solutions;
avoid enforcing identical spacing across all rooms. Risks are visual repetition,
too many dark doorway-like shapes, and horizontal bands resembling platforms.
As with A/B, new diffuse pixels need matching native normals if locally lit.

## Shared production implications

These are relative authoring estimates, not measured production time. After
selection, G02 specifies actual dimensions, pivots, connectors, layer/color roles
and source provenance before generation. Multi-cell pieces may use 48/72/96-pixel
dimensions where justified, while RuleTile cells stay 24×24 and world PPU stays
24. Review exact generator capabilities before ordering production art.

No direction requires new collisions, a camera change, a bigger hero, an extra
prop system, brighter grading or new game mechanics. The existing shared base
tiles and normal-map pilot remain installed. G04 can evaluate an isolated
alternate base sheet separately if the selected composition needs it; it must
not silently replace the wall rule's shared sprite references across ten rooms.

The primary comparison freezes source profile/light settings; AI-painted material
shadows are illustrative. G05 must reconstruct the selected idea with real
sprites and current lighting, then verify readability, seams in motion, terrain
equivalence and render cost. No concept proves those integration results.
