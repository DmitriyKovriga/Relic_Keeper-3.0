# J01 — rare and unique arrival waves

Historical first treatment. The subsequent [flight and unique-size follow-up](Step04_J01_LootFlight.md)
supersedes the unique color/size presentation below and contains current evidence.

Implemented 2026-10-29, following the user's request for waves and small glowing
stars when rare loot reaches the sphere, with twice the effect for unique loot.

- Common/magic: existing small intake only.
- Rare: one PixelLab wave-and-stars clip, with a brief warm local light.
- Unique: two staggered clips (the second mirrored), twice the stars/waves and
  twice the peak light strength. It does not double the interaction radius.

The source clip was made and exported with PixelLab MCP's VFX workbench. It has
10 transparent 43x49 frames at 15 fps, imported Point/uncompressed at 24 PPU.
The supplied anchor is retained. [PixelLab provenance](J01/Rarity/PixelLabProvenance.json),
[editable effect](https://pixellab.ai/create-vfx?effect=17daa889-1c20-4ab9-9d83-4836c14f37c1).
The committed sheet and import metadata work without network access. The optional
Editor import helper reads the saved export JSON beside this plan, not a local
cache or credential.

The stars have an additive halo and a brief point light using the existing local
lighting system. It follows the environment's room-fill light enable/strength/
radius settings, uses existing occlusion, and does not cast player shadows.
An environment disabling those lights still displays the emissive sprite clip.

Only actual tracked arrivals admit accents. Filtering, taking the contributing
loot, disabling/destroying its cache or room cancels the appropriate visuals and
light. Inventory refreshes, filter reveal and player discards do not celebrate.
The existing immediate ownership/access and fixed collider are preserved.

Rapid rare arrivals share one batch with a one-second admission cooldown; no
animation queue or additive strength per item. A unique can upgrade a rare batch
once, without resetting its first burst; an immediately following unique also
has priority over the rare cooldown. Further unique arrivals merge. Taking or
hiding a unique contributor removes its extra strength while other valid rare
contributors can retain their ordinary cue. Pause uses the existing game clock.

## Checked

Compilation succeeded; **65 focused EditMode tests passed, 0 failed/skipped** in
LootCacheIntakeTests, RoomLootCollectorTests, WorldDroppedItemTests and
LootFilterTests. The seven added cases cover rarity eligibility, two unique
bursts, rare batch merging/unique escalation, removal/filter reveal, and admission
rate limiting with unique priority.

Real Play Mode checks in an owned Mortfall room 001 instance:

| Case | Result |
|---|---|
| Rare | 1 burst, peak light 0.2799958; settled and cleaned up. |
| Unique | 2 bursts, peak light 0.5598525 (approximately 2x); settled and cleaned up. |
| 20 rare items | All stored immediately; 1 burst; full inventory rejects pickup without losing loot; no queued waves. |
| Player-discarded unique | Stored successfully; 0 waves, 0 intake, 0 light. |

[Rare report](J01/Rarity/RareArrivalReport.txt),
[unique report](J01/Rarity/UniqueArrivalReport.txt),
[batch report](J01/Rarity/RareBurstReport.txt),
[discard report](J01/Rarity/UniqueDiscardReport.txt).

[Recorded comparison preview](J01/Rarity/RareUniquePreview.gif) includes the
480x270 room view and a nearest-neighbor 3x detail. The preview uses sampled real
Play Mode frames at nominal 0.06-second intervals; it is not a performance test.
The capture camera uses Mortfall's depth/light profile for the lighting check.
Synthetic affixes/unique flags are confined to disposable item-data clones; the
original weapon assets are not edited.

The bounded console-error query returned no errors. The original save SHA-256
still matched `442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db`.

This update adds no UI strings or icons, so the existing EN/RU prompts and grid
labels remain in use. A full ordinary-run A/B and standalone rebuild have not
been repeated after this addition; the old baseline executable remains a baseline.

Repro in hub Play Mode, with windows closed: RoomLootRuntimeCheck methods
`CaptureRareArrival`, `CaptureUniqueArrival`, `CaptureRareBurst`, and
`CaptureUniqueDiscard`. Import helper: `LootCacheArrivalArtSetup.Import`.
