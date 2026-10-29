# Step 08 — J06 room resolution and exits

Implemented and checked 2026-10-30.

The exit now gives one short expanding echo of its existing animated art when
the kill threshold opens it. The portal itself and its trigger never grow;
interaction becomes available immediately. Reduced combat effects uses a smaller,
quieter, shorter echo. No new art, full-screen flash, sound or reward was added.

Unlock, full clear and character level-up share one announcement. New information
merges into the visible message without restarting its fade-in. Level-up remains
on a second line when a room message has priority. Duplicate notices do not
extend it or build a queue. Unlock and clear remain distinct events: killing
half the enemies, rounded up, opens the exit; only defeating the last enemy
clears the room. If these events coincide, the heading combines them.

An offscreen exit can add a short `Exit >`/`Выход >` direction line. The hint
ends when the exit comes onscreen or its brief lifetime expires. It does not
intercept input or move the camera. Leaving or destroying the owning room
cancels its announcement. Re-enabling a portal does not replay its unlock cue.
An unrelated global level-up is not cancelled by destruction of another room.

Empty rooms start open without a fabricated celebration. Modifier rewards and
their existing exactly-once guards are unchanged. There is no persisted revisit
system in this flow: the dungeon creates new room instances. This change checks
disable/re-enable of an existing instance; it does not add room persistence.

## Verification

- Unity compilation succeeded. **85 focused EditMode tests passed, 0 failed,
  0 skipped**, across RoomAnnouncementStateTests, PortalUnlockVisualTests,
  RoomResolutionFeedbackTests, DungeonModifierTests, DungeonOpeningRoomTests,
  RoomLootCollectorTests, DungeonServiceArrivalTests and PlayerLevelUpFeedbackTests.
- Live Play Mode checks used a disposable real Mortfall Room 001 clone, the
  actual portal/animation and UI, and controlled enemy-count fixtures. They
  exercised immediate unlock with an enemy remaining, combined clear/level-up,
  live EN/RU switching, native bounds, an onscreen exit, duplicate deaths,
  cancellation/re-enable, one-enemy and empty rooms, no-reward rooms, reduced
  effects and timed cleanup. [Completed report](J06/RuntimeCheck.txt).
- [Native comparison](J06/RoomResolutionComparison.png): each panel is a
  480x270 capture. UI panels render separately from world panels. World panels
  inspect the owned room geometry/portal in the open hub scene and retain its
  backdrop; they are not an ordinary dungeon screenshot or a biome comparison.
  Separate world/UI images have not been composited into a claimed game frame.
- Diff review, whitespace checks and scene/project-setting checks passed.
  The local Graphify cache was refreshed; its known namespace-collision warnings
  remain discovery limitations, not compile failures.
- The original save matched before and after the final controlled check:
  `5b78bc2758c62579cbcc8d0d776c39fb2da58602b319ab101dee81acbb95b87e`.
  This is the current save, which predates that check, rather than the historical
  baseline hash recorded in Step 01. No save was restored or replaced.

The broader test filter exposed an existing editor-only merchant initialization
problem. Stock initialization now runs only in Play Mode. Existing test fixtures
also explicitly perform their trigger setup and unsubscribe runtime loot objects
before destruction, because EditMode objects do not receive those runtime
lifecycle callbacks. Gameplay stock, loot filtering and rewards are unchanged.

The native check source is
`RelicKeeper.Editor.VFX.RoomResolutionRuntimeCheck.Capture`; run it from hub
Play Mode, outside an active dungeon. It restores locale, presentation preferences
and owned resources. It does not defeat real enemies or change inventory/XP.
These are controlled correctness/layout checks. Ordinary-run A/B evaluation,
performance measurement and a fresh standalone build have not been performed.

## Test by hand

1. Enter Mortfall and leave several enemies alive. Defeat half, rounded up.
   The exit should open immediately with a brief local pulse and “Portal Open”.
   It must be usable while the remaining enemies are alive.
2. If the exit is offscreen, check the short direction line. Move it onscreen:
   the hint should disappear. It should not keep returning while you walk.
3. Defeat the remaining enemies. Check “Room Cleared”. If the unlock message is
   still visible, the heading should merge both facts without a second banner.
4. Gain a level near the unlock/clear event. The level-up line should remain
   readable instead of replacing the room result. Let the message fade out.
5. Enter the next room while a message is visible. The previous room's message
   should disappear. Do not expect extra loot or service NPCs without their
   existing room modifier.
6. Repeat with Russian selected and with reduced combat effects enabled in
   Settings. The text should fit and the portal pulse should be less prominent.

J01/J02/J03/J06 now have delivered first slices. The plan's next phase is to
audit three existing build/status chains for J04 and prototype only a demonstrated
readability gap. J05 and J10 likewise depend on observed gaps. J07/J08 remain
user-owned and excluded; J09 stays last.
