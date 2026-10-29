# J01 — loot reveal and larger unique arrival

Presentation follow-up: [Step 05](Step05_J01_ItemBubble.md) restores the original
item bubble in place of the small flight mote. Timing and unique arrivals below
remain in use; its newer preview shows the current appearance.

Implemented 2026-10-29 following the user's request to reuse the crafting-currency
presentation rhythm and make unique arrivals twice as large with a different color.

Loot now rises 2.8 world units above its drop position over 0.38 seconds, hovers
for 0.42 seconds, flashes for 0.16 seconds, then follows a curved path to the loot
sphere. Travel time scales with distance from 0.42 to 1.6 seconds. The core, trail
and reveal use the existing item-title rarity colors: white, blue, gold and orange.
The reveal reuses the existing PixelLab wave sprite through an additive material
that recolors its density. The existing crafting-currency behavior is unchanged.

Rare arrivals retain their gold burst. Unique arrivals use a separate orange-red
PixelLab sheet at 2x linear scale (twice the width and height), retain two staggered
bursts, and have a matching orange-red local light with twice the rare radius.
Peak light strength remains twice the rare peak. Only visual children scale; the
cache position, interaction radius and actual item ownership stay fixed.

The real item is stored immediately. Loot can be taken while its cosmetic flight
is still rising or travelling. Taking/filtering it or destroying its cache cancels
the presentation. Discarded items remain quiet; existing batch admission and
pause behavior continue to apply.

[Recorded preview](J01/FlightReveal/LootFlightPreview.gif),
[still comparison](J01/FlightReveal/LootFlightComparison.png).
These are actual Mortfall room 001 Play Mode frames rendered at 480x270, sampled
at nominal 0.06-second intervals. They are visual evidence, not a performance
measurement. Both rows show the same source and cache positions. The capture
camera uses Mortfall's depth/light settings with no color-grading preview profile.

## Verification

Unity compilation and sprite import succeeded. **73 EditMode tests passed,
0 failed/skipped** across LootCacheFlightTests, LootCacheIntakeTests,
RoomLootCollectorTests, WorldDroppedItemTests, LootFilterTests and PickupFlightTests.
The six new cases cover flight phases, bounded distance-based duration, and
rarity-specific colors/reveals with immediate storage. Existing tests cover
cancellation, filtering, fixed interaction, merging and unique escalation.

Five real-frame Play Mode checks passed:

| Case | Observed |
|---|---|
| Rare | Rise, reveal, travel, intake; 1 burst; peak light 0.2799925. |
| Unique | Rise, reveal, travel, intake; 2 orange-red bursts; peak light 0.5599807. |
| 20 rare items | Immediate storage; all phases; 1 merged burst; full-inventory rejection preserves loot. |
| Unique discard | Stored; no flight, intake, wave or light. |
| Open cache | 3 items available immediately; existing UI pause and subsequent arrival/cleanup pass. |

Reports: [rare](J01/FlightReveal/RareArrivalReport.txt),
[unique](J01/FlightReveal/UniqueArrivalReport.txt),
[batch](J01/FlightReveal/RareBurstReport.txt),
[discard](J01/FlightReveal/UniqueDiscardReport.txt),
[open cache](J01/FlightReveal/OpenUiIntakeReport.txt).
The bounded console-error query was empty. The original save SHA-256 remained
`442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db`.
Graphify's code-only cache was refreshed; its existing namespace-collision
warnings do not affect Unity compilation.

The unique sheet was created using PixelLab MCP's VFX workbench, retaining the
10-frame, 43x49, 15 fps geometry and anchor. Import is Point/uncompressed, 24 PPU.
[Provenance](J01/FlightReveal/PixelLabProvenance.json),
[editable PixelLab effect](https://pixellab.ai/create-vfx?effect=e238cc6f-5cf1-488e-8369-785d729fad1d).
Art and metadata are saved in the project; runtime has no network dependency.

Manual check: in Editor Play Mode, enter Mortfall and defeat enemies. Observe
rarity-colored loot rise, pause and flash above the defeat location before flying
to the exit sphere. Compare rare and unique arrival size/color. Open the sphere
before flights finish; items should already be present and taking one should
cancel its outstanding flight. For deterministic rare/unique fixtures use the
existing RoomLootRuntimeCheck capture methods from hub Play Mode.

No new player-facing controls or text were added. This was verified in the Unity
Editor; the older standalone baseline executable has not been rebuilt.
