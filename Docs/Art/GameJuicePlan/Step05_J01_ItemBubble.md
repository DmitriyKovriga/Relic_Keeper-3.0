# J01 — restore the familiar item bubble

Implemented 2026-10-29 following the user's request to see the item bubble that
existed before room loot was collected into the exit sphere.

The flight now displays the actual item icon on the original rarity-colored
circle during rise, hover, reveal and travel. It reuses WorldDroppedItem's circle
sprite and icon-fitting routine: an 18-pixel circle with the icon fitted within
14 pixels at 24 PPU. Icons retain their original artwork colors. Rarity-colored
trails, the short reveal, and the larger orange-red unique arrival remain.

Only the presentation is reused. Flights have no WorldDroppedItem component or
pickup collider, and the real item is immediately available in its cache. The
icon and circle hide together upon arrival; taking/filtering the item or removing
its cache still cancels the whole visual. The legacy no-item flight retains its
small mote fallback. No new art assets or user-facing controls were introduced.

[Motion preview](J01/ItemBubble/ItemBubblePreview.gif),
[hover and enlarged icon detail](J01/ItemBubble/ItemBubbleHover.png).
These are native 480x270 Mortfall Play Mode captures at nominal 0.06-second
intervals; the detail is nearest-neighbor enlarged. They are visual samples,
not performance measurements.

Unity compilation succeeded. **73 focused EditMode tests passed, 0 failed/skipped**
in LootCacheFlightTests, LootCacheIntakeTests, RoomLootCollectorTests,
WorldDroppedItemTests, LootFilterTests and PickupFlightTests. The four rarity
cases now compare the flight's plate sprite/color and icon bounds with an actual
legacy drop, verify the item remains accessible, and check that the complete
bubble retires at arrival without leaving a loose drop.

Five fresh Play Mode checks passed: [rare](J01/ItemBubble/RareArrivalReport.txt),
[unique](J01/ItemBubble/UniqueArrivalReport.txt),
[20 rare drops/full inventory](J01/ItemBubble/RareBurstReport.txt),
[open-cache arrival](J01/ItemBubble/OpenUiIntakeReport.txt),
and [quiet unique discard](J01/ItemBubble/UniqueDiscardReport.txt).
All non-discard checks observed a visible item icon/bubble, all flight phases,
intake, fixed interaction and automatic cleanup. Discard remained silent.
The bounded console-error query was empty, and the original save SHA-256 remained
`442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db`.

Manual check: defeat enemies in Mortfall and observe the item icon inside its
rarity bubble before it reaches the loot sphere. Opening the sphere before the
bubble arrives should already show the item. Taking it cancels its remaining
flight. This refinement is verified in the Editor; the standalone baseline
executable was not rebuilt.
