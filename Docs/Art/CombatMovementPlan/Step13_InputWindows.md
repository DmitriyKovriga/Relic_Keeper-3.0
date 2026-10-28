# Step 13 — existing jump input windows (M05)

Verified 2026-10-28. The existing ledge grace, early jump buffer and one-way
platform drop-through worked in the reviewed cases. Their timing and jump
physics remain unchanged. One pause-boundary bug was reproduced and fixed.

## Fix

Jump requests survived opening pause/inventory/choice windows, and the jump
callback accepted fresh requests while those menus stopped game time. Those
requests could execute after resuming. PlayerMovement now clears queued jump
and Down+Jump intent when GamePauseService begins a pause, rejects jump input
while that service is paused, and unsubscribes on disable. Enabling while
already paused also clears pending intent.

Combat hit-stop does not use menu pause ownership. Jump input remains accepted
during its short freeze; the existing scaled-time buffer survives until the
next physics update. No timing extension, extra coyote system or movement
mechanic was added. Existing in-progress platform collision ignores remain
owned by the drop-through controller and finish normally after resuming.

## Results

| Scenario | Observed result |
|---|---|
| Jump shortly after walking off a ledge | Ground jump used; one air jump remains |
| Jump after ledge grace expires | Air jump used; no extra ground jump |
| Early jump before landing, air jump already spent | Waits for contact, jumps once |
| Expired early jump | No automatic jump on landing |
| Down + Jump on one-way support | Drops through; collision restores after the body clears it |
| Down + Jump on solid ground | Normal jump; ground collision stays active |
| Jump during real hit-stop | Buffered during freeze, executes once afterward |
| Jump while menu-paused | Does not queue a gameplay jump |
| Queued jump followed by menu pause | Cleared; no jump after resuming |

The configured jump buffer remains **0.12 seconds**, ledge grace **0.10 seconds**,
and forgiving drop-through intent window **0.30 seconds**. The live ledge cases
pressed at approximately 0.04/0.14 seconds after observing departure; EditMode
tests also sample 0.08/0.12 seconds around the ledge boundary.

Compilation passed. Four pause regressions failed before the fix (38 other
PlayerMovement tests passed). Final **91 EditMode tests passed**, zero failed or
skipped: PlayerMovementTests, GamePauseServiceTests, HitStopTests and
LandingContactTests. Coverage includes expiry, one-time consumption, grounded
and one-way support probes, collision restoration and all four menu-pause
reasons.

**Nine live Editor cases passed** using actual Rigidbody2D simulation,
ground probes, menu pause handles and HitStopService. The fixture invokes the
real jump callback with a default context and injects fresh Down intent for
the platform combinations. It does not replay physical keyboard/gamepad events,
measure device latency or validate saved rebindings. No scenes/prefabs were
saved and no character/input settings were persisted by the fixture.

[Compact results and source fingerprints](MovementInput/audit.json).
Ignored raw recordings: `Logs/MovementInput/`. Reproduce in Play Mode with
`RelicKeeper.Editor.Combat.CombatBaselineCapture.BeginInputReview`.

M06 corner correction remains conditional on a reproducible corner snag. This
review does not justify changing collision geometry or adding automatic corner
movement. The initial movement presentation/input review is ready for player
playtesting; M04's optional camera preview remains a separate preference.
