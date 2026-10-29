# Step 02 — J01 intake prototype

Follow-up: the user-requested rare/unique wave-and-stars accent is now implemented
in [Step 03](Step03_J01_Rarity.md). The scope below records the initial intake slice.

Implemented 2026-10-29. This delivers the first J01 treatment: a brief arrival
response on the existing exit loot sphere. The baseline remains recorded in
[Step 01](Step01_BaselineReview.md).

## Result

The existing sphere compresses slightly and draws its existing purple glow inward
when a tracked loot flight finishes. It settles in 0.38 seconds. Arrivals during
that response merge without restarting it, adding strength, or queuing further
animations. The question mark, idle glow, position and localized interaction
prompt remain the existing ones. No new art, icons, particles or player-facing
strings are introduced.

Only the new `Visual` child scales. The container host, interaction collider and
portal placement do not animate. Items still enter the real grid before any
flight starts. The completion callback changes no ownership and raises no grid
`Changed` event, so it does not refresh or move an open inventory window.

Each production flight tracks its item. Taking/hiding that item cancels its
flight and prevents intake; hiding/removing all active contributors immediately
settles the pulse. Flights and trails are children of the cache, so destroying
the cache or room removes them. Inventory discards carry a runtime-only origin
flag through both centralized delivery and the fallback loose-drop route and do
not start a flight/intake celebration. Revealing old filtered loot stays quiet.
Save formats and item identities are unchanged.

[Play Mode preview](J01/IntakePreview.gif) — 480x270 room view plus a 4x detail,
sampled at nominal 0.06-second intervals. This is a visual preview, not timing or
performance evidence.

## Verification

Compilation succeeded. The combined focused filters passed **58 tests, 0 failed,
0 skipped**:

- `RelicKeeper.Tests.EditMode.LootCacheIntakeTests`
- `RelicKeeper.Tests.EditMode.RoomLootCollectorTests`
- `RelicKeeper.Tests.EditMode.WorldDroppedItemTests`
- `RelicKeeper.Tests.EditMode.LootFilterTests`

The new cases cover completion-triggered intake, immediate access, twenty-arrival
merging, fixed collider/host, return to rest without a queue, tracked-item removal
and filtering, filter reveal, pause, disable/reset, cache-owned cleanup and the
discard flag. The existing filtering test now checks all sphere/glow renderers
under the cache rather than assuming its sprite lives on the collider host.

Real Play Mode checks used the existing Mortfall room 001 prefab with enemy
spawners disabled in an owned temporary instance; the ordinary player inventory
and save were not used as test storage:

- Twenty new items were stored immediately, flew over real frames, produced one
  merged intake envelope, settled, and left no flight tail. A synchronous check
  used a disposable full inventory: rejected pickup left the original item and
  count accessible in the cache. [Report](J01/IntakeBurstReport.txt).
- A player discard stored its item with no flight or intake.
  [Report](J01/DiscardReport.txt).
- The real cache window opened while two flights were in progress. A third item
  was delivered while it stayed open and appeared in the actual grid. Existing
  menu pause held the flights; closing the window resumed them and they settled
  normally. [Report](J01/OpenUiIntakeReport.txt), [grid capture](J01/OpenUiGrid.png).

The original save still matched SHA-256
`442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db` after live checks.

Repro helpers, from the hub in Play Mode with game windows closed:
`RelicKeeper.Editor.VFX.RoomLootRuntimeCheck.CaptureIntakeBurst`,
`CaptureDiscard`, and `CaptureOpenUiArrival`. These are Editor checks, not new
buttons or controls in the game. Raw frames/reports use the existing temporary
`room-loot-check` directory; selected artifacts above are saved with the plan.

## Scope and follow-up

This is the intake slice. Rarity-qualified accents, accumulation states and
limiting/grouping the existing flight streams remain optional and are not
implemented here. A twenty-item burst still creates the existing flight visuals;
the container response is what is merged. No combat feedback or balance changes.
Reduced combat effects retains its combat scope; no new setting is implied for
this small reward cue.

A full repeated 11-minute ordinary-run A/B and a new standalone build have not
been performed after this change. The recorded live checks above are targeted
Play Mode checks. The previous isolated baseline executable still contains the
baseline until explicitly rebuilt.

To inspect by hand in the updated Unity project: enter Mortfall, defeat enemies
and watch the exit sphere as the golden flight reaches it. Try several drops,
open the sphere before arrival, hide/reveal common loot, and discard your own
item. The sphere stays usable throughout; discards and filter reveal stay quiet.

The next planned feature slice is J02, confirmation of committed equipment,
crafting and passive-allocation results using the existing UI.
