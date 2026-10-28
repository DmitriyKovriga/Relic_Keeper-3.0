# Step 12 — optional camera follow comparison (M04)

Implemented 2026-10-28 as an Editor preview. The saved camera keeps its existing
direct follow. Candidate: Cinemachine horizontal look-ahead 0.12 seconds,
prediction smoothing 3 and horizontal damping 0.05. Vertical look-ahead and
vertical damping are off. No new camera mover, follow proxy, runtime component,
scene or prefab edits are needed.

## Try it

Open **Relic Keeper > Camera Follow Preview** in Unity, then press Play. Click
**Look ahead** and move around the Hub or Mortfall. Click **Current** to compare.
The window has Look ahead (seconds) and Horizontal softness sliders. Closing
the window or stopping Play restores the exact original composer settings.
It also restores before script reload and Cinemachine's optional Save During
Play capture. Switching the live camera releases the old preview session.

The preview does not save a preferred mode. Choosing a permanent default is a
separate decision after player review. Existing target offsets, composition,
confiner, lens, camera impact and pixel-render settings are not edited.

## Verification

Compilation and **14 EditMode tests passed**, zero failures/skips:
CameraFollowPreviewTests and CombatCameraImpulseTests. New cases cover exact
settings restoration, repeat previews, disposal, clamped experiment values,
preserved target offsets and the Cinemachine hot-save callback.

**Eight live cases passed**: off/on pairs for straight travel, reversal, jump
and confined room edges. The actual Cinemachine composer, brain and confiner
ran at native 480x270 with a supplied target path. Both modes stayed inside
the room bounds; their vertical camera paths were identical. Horizontal camera
differences peaked at 0.58 units during straight travel, 0.59 on reversals and
0.80 near edges (about 14, 14 and 19 source pixels at 24 PPU).

[Recorded comparison](CameraFollow/camera-follow.gif),
[measurements](CameraFollow/audit.json). Raw frames/traces are ignored under
`Logs/CameraFollow`. Reproduce in Play Mode with
`RelicKeeper.Editor.Combat.CombatBaselineCapture.BeginFollowReview`.

The comparison uses a flat review floor and controlled target positions, with
the C11 camera extension installed but no hits. It is not physical input
playback, a full Hub/Mortfall art traversal, a teleport test, combat-plus-follow
validation or a standalone build/GPU profile. Existing pixel-perfect settings
remain authoritative; the capture alone does not establish background shimmer
quality on every display. Try the preview in the actual rooms before adopting
it. The small lead gives more forward view, while reversals still carry some
prediction lag; there is no automatic recommendation to replace direct follow.

Next independent plan item: M05, validate the existing jump-buffer, coyote and
drop-through input windows and fix only observed failures.
