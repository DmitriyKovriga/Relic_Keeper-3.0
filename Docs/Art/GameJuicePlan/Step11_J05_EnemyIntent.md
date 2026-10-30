# J05 — Enemy anticipation and recovery



Implemented and checked 2026-10-30. Refined after user review: the replacement

melee/caster glyphs were rejected. **The original red overhead mark, placement,

size, pulse and accompanying warning flash are restored.** Its sprite construction

matches the pre-J05 source exactly. The generic warning remains shared by enemies;

no new glyphs or visible-shape anchoring remain.



## Projectile direction follow-up

Added at the user's request, alongside the restored default attack mark. Actual
projectile attacks show a steady, one-pixel dashed red line for three world units
from the real launch origin. It follows the current target until release, using
the same aim calculation as the shot. Melee/direct-target attacks and dashing
charges do not get a fictional projectile path. No manual per-enemy wiring is
required. Reduced effects retains the line at lower opacity.

This is a short direction guide, not a full-range, collision or damage-area
prediction. It does not lock aim, change targeting/timers or stop projectiles.
It hides on release, missing target, stun/freeze, reset and disable. A reused
caster keeps one line renderer; the original overhead sprite/placement/flash
remain unchanged. Shared launch-ray math preserves the former firing calculation.

Compilation and **30 targeted EditMode tests passed, 0 failed/skipped**:
`EnemyAttackIntentTests` (18 cases), `EnemyProjectileMovementTests` (6) and
`MoldVisualTests` (6). New cases compare the actual released projectile direction,
moving targets, coincident-target fallback, reflected/scaled owners, melee/charge
exclusion, missing target, both effect modes and cancellation/reuse.

The controlled native check verifies the launch-ray alignment and line lifecycle
on every frame, with moving targets aimed left/right, solo and ten enemies,
normal/reduced effects. [Preview](J05/ProjectileAim/ProjectileAimPreview.gif)
shows normal/right then reduced/left at captured timing;
[states](J05/ProjectileAim/States.png), [capture times](J05/ProjectileAim/CaptureTimes.csv)
and [runtime report](J05/ProjectileAim/RuntimeCheck.txt) record this follow-up.
The same controlled-scene limits below apply; ordinary combat/HUD review and a
standalone rebuild are separate. The local Graphify source cache was refreshed.

## Timing fix retained



The original warning stopped after the 0.5-second pre-attack phase, leaving the

actual windup unmarked. It now stays through preparation and clears at the real

Active transition, using the existing animation impact signal or timer fallback.

The cosmetic clock cannot expire it early. Its original pulse settles to a visible

minimum at the end of preparation until the strike owns its removal.



Stun, freeze, reset and disable still clear the warning. The flash copies the

current sprite without writing the body's tint/material. Gameplay data, damage,

AI, facing, charge movement and attack/cooldown/recovery windows are unchanged.



## Recovery audit



Knight and Mold already have non-looping attack sheets with anticipation, release

and follow-through. Their last frame holds through recovery, then locomotion

resumes. The controller remains busy and rejects another attack during recovery;

existing attack/hit-recoil ownership is retained. Recovery still takes the maximum

of configured/miss recovery and remaining animation. No new pause, stagger or

vulnerability was added. A released Mold projectile remains dangerous after the

mark disappears.



## Verification



Before the direction-line follow-up, restoring the original mark passed Unity
compilation and **64 EditMode tests, 0 failed/skipped**:

`EnemyAttackIntentTests` (12 cases), `MoldVisualTests`, `KnightBodyChargeTests`,

`EnemyLevelBalanceTests`, `EnemyProjectileMovementTests` and `EnemyHitFlashTests`.

The intent cases cover actual release, timer fallback, continuous preparation,

recovery/follow-through, cancellation, cooldown-preserving reuse, explicit disable

callbacks, original mark in both effect settings, appearance ownership and rejected

input. The superseded padding-anchor test was removed with that rejected feature.



Native 480×270 before/after checks use actual Knight/Mold data and release signals

against Mortfall 001, alone and with ten enemies, in normal/reduced effects.

All **3,440 after phase samples** match warning visibility to actual preparation.

These restoration captures replace the rejected glyph previews; the follow-up
above has its own current preview:



- [Normal-speed preview](J05/EnemyIntentPreview.gif)

- [Windup/recovery comparison](J05/IntentComparison.png)

- [Enlarged detail and native crowd](J05/IntentDetails.png)

- [Phase audit](J05/PhaseAudit.csv), [capture times](J05/CaptureTimes.csv)

- Native reports: [before](J05/BeforeRuntimeCheck.txt), [after](J05/AfterRuntimeCheck.txt)



Preview callbacks advance once per game frame using real delta time; each pass

keeps the same initialized height. The GIF uses recorded game times rounded to

centiseconds. These disposable owners have no damageable player target. This is

not autonomous combat, live HUD overlap, charge-travel physics, a performance

benchmark or a standalone build. Ordinary Mortfall review remains separate.

The user's save SHA-256 matched before/after checks:

`DF3EE83C407AE06D35F3A887EFFBCECD7FC0F1FBC7421F60AE88EE6CDE321044`.



The Editor-only capture entry points are

`RelicKeeper.Editor.VFX.EnemyIntentRuntimeCheck.Before` and `.After` from Play

Mode. They label the currently compiled source; they do not switch versions.

Owned objects, presentation preferences, random state and background-run state

are restored on completion/error/exit/reload. No in-game controls were added.



## Hands-on check



1. Watch a Knight: the original red mark remains through axe preparation and

   disappears when the strike releases. Its existing follow-through stays.

2. Watch a Mold: the same original mark clears when the lightning ball launches.

   The projectile can still hit during the caster's follow-through.

3. Freeze/stun during preparation: the warning clears with the cancelled attack.

   A later attack must have one mark, without duplicates.

4. Repeat in a crowd and with Reduced combat effects, alongside health bars and

   Ignite/Shock. No replacement brackets or diamond should appear.



Next candidate: J10's repeated-attempt death/return audit, only extending feedback

where an observed gap warrants it. J07/J08 remain user-owned; J09 stays last.

