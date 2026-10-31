# Graphics refresh decisions

## Established constraints

2026-10-31: follow the supplied plan from G00–G01. Mortfall Room 001 is the pilot;
24 PPU world art, existing collision/terrain/spawns/portals, atmosphere and game
feedback remain the baseline. Room pacing/boss staging remain user-owned. UI art
follows a selected and integrated environment pilot.

## Art direction — B selected

2026-10-31: the user selected **B — Collapsed fortress** in the comparison.
Develop one coherent B kit and Room 001 pilot. A/C remain alternatives only.

The concrete alternatives are in [the comparison](Concepts/Comparison.png):

- A: monumental ruins — large piers, broad lintels and deep rectangular bays.
- B: collapsed fortress — broken decorative arch and layered exposed construction.
- C: burial complex — repeated shallow sealed niches and restrained plain bands.

The selected architectural reference is [B](Concepts/B.png). Keep its broken arch
and restrained supporting masonry. Its painted cave backdrop is not a replacement
target; the user's background-preservation clarification below takes precedence.
The G02 specification is
[here](G02_ModuleSpecification.md); its twelve planned native sprite slots and
connector contracts are in [the asset manifest](Assets/manifest.json).

The twelve-piece list is the initial implementation specification, with assets
explicitly marked as not yet created. The choice does not silently promote a
shared wall-sheet/rule replacement, change existing relief maps, repaint all
ten rooms or begin the separate UI theme branch.

## Preserve the artist's background

2026-10-31: the user likes B's direction and explicitly wants to keep the current
background made by their artist. Keep
`Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-background.png`
and its existing sprite identity, import settings, dungeon reference and parallax
presentation. The original cave shapes and painted texture remain the backdrop.

B supplies separate architectural sprites placed over that backdrop, with opaque
stone and transparent openings. Use sparse placements that leave substantial
areas of the original art visible. Do not regenerate, repaint, replace or bake
architecture into the artist's background. No full-screen generated cave layer,
replacement silhouette or opaque wall filler is authorized by B's selection.

The earlier B concept remains historical evidence of the architecture choice,
not approval of its altered background. The next native-piece preview must
composite the new arch/supports over the retained original artwork. See the
manifest's `backgroundPreservation` block for the original asset GUID/hash.
