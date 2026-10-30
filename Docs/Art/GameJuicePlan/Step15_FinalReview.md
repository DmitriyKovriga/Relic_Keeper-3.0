# Step 15 — ordinary-play review and circular node feedback

Reviewed 2026-10-30. The player described the result as almost the same, a little
better, and singled out the square passive-node selection effect as distracting.
This is modest subjective improvement, not evidence of a large or measured gain.
The concrete tree issue is corrected below; no additional effects were added.

## Actual human route

The reviewed capture is `Logs/GameJuiceBaseline/20261030-144251/`. The handoff
boundary is frame 575 at 151.158 seconds. Frames 576–3304 contain 2,729 samples
over 740.881 seconds (12 minutes 20.881 seconds), ending at 892.302 seconds.
There is one 32.671-second sampling gap between frames 615 and 616, both during
recruitment. The observed span includes that gap; it is not continuous video or
a frame-rate measurement. Earlier failed/idle smoke captures remain excluded.

The player recruited a Rogue, inspected equipment, fought through Mortfall,
opened the passive tree repeatedly, collected loot and passed room-clear/portal
states. Nine next-room save messages corroborate progression through ten rooms.
The final death summary shows highest stage 10 and 206 monsters killed. It shows
the Return to hub control before required recruitment reopens; the log records
one permanent death, return and another hire. Input telemetry is absent, so the
recording does not establish whether return was triggered by a particular key,
click or timer.

[Selected ordinary-play frames](FinalReview/OrdinaryRunEvidence.jpg) show the
route at the actual 480×270 reference size per panel.
[Review summary](FinalReview/ReviewSummary.json) preserves sample bounds, the
gap and log counts. Full contact sheets, frame CSV and player log remain local
under `Logs/GameJuiceFinalReview/20261030-113924/Review/` and its parent.

The final standalone player log has no matched exception, load-error,
missing-reference or unsupported-shader messages. This is a bounded log/readability
review, not proof of all possible runtime paths. Inventory/orb inspection is
visible, but the sampled recording alone does not establish a committed craft,
deliberate failed skill input, locale switch or reduced-effects comparison.
Those controlled cases retain their earlier feature-specific checks. Four
samples per second cannot validate fast lightning timing, input latency or
performance. No controlled baseline/final A/B comparison is claimed.

## Reported tree defect and correction

`PassiveTreeRenderer` reused the inventory transaction cue, which draws a square
border, rectangular tint and corner `+`/`!` badge. That shape disagreed with the
tree's round nodes.

Tree transactions now opt into a thin circular ring, anchored to the node's
existing `Circle` element. The background is transparent and the corner badge is
omitted. A successful allocation briefly fades gold; rejection briefly pulses
red. It follows the existing node geometry while panning/zooming. The actual
icon, authored frame, point cost, allocation rules and node position are unchanged.
Inventory/crafting retain their rectangular slot feedback. No localization text
was introduced.

## Verification

- Unity compilation succeeded. **13 focused EditMode tests passed, 0 failed or
  skipped**: `UiTransactionFeedbackTests` (6) and `PassiveTreeNodeClickTests` (7).
  Coverage includes 24/32/48-unit circular cues, transparent corners/no badge,
  the transformed tree container, nonblocking ownership, cleanup and node clicks.
- The first transform test placed the cue owner outside the scaled content and
  failed a subpixel alignment assertion. The final fixture uses the renderer's
  real arrangement: nodes and cue share the panned/scaled content container.
  The final 13-test run passed; the failed attempt is not counted as a pass.
- The existing `UpgradeTransactionRuntimeCheck.Capture` ran again at native
  480×270 in Play Mode with disposable inventory/tree data. It exercised real
  allocation and no-point rejection callbacks, paused feedback expiry, equipment
  swap, committed craft/reroll/purge and currency rejection. Its fresh
  [report](FinalReview/RoundNodeRuntimeReport.txt) passed. Visually inspected
  [allocation](FinalReview/PassiveAllocationRound.png) and
  [rejection](FinalReview/PassiveRejectionRound.png) captures show circular rings
  without a square wash. These are controlled fixture captures, not a second
  human playthrough; the fixture deliberately has no node icons.
- The final bounded Unity error query was empty. The main save SHA-256 still
  matched `DF3EE83C407AE06D35F3A887EFFBCECD7FC0F1FBC7421F60AE88EE6CDE321044`.
  Project settings still matched
  `7B5D086AB83ACA2C6644D5A640AC20A9A12F497C0D785153858F867F36D8A47C`.
- The isolated development player was rebuilt with the circular correction:
  **Succeeded, 79.007 s, 0 errors/warnings**. Its updated executable is
  `Builds/GameJuiceBaseline/GameJuiceBaseline.exe`; the normal save remains
  isolated. [Build manifest](FinalReview/RoundNodeBuild.json) records the new
  assembly and preserved checksums. No second human playthrough or standalone
  launch after this correction is claimed; the visual recheck above used the
  real Editor runtime. The build's newly generated Addressables link.xml/meta
  were backed up under `Logs/GameJuiceFinalReview/20261030-113924/RoundNodeBuildGenerated/`
  and removed from source changes. No room prefab churn occurred this time.
- Graphify's local code-only cache was refreshed. Its existing namespace-ID
  collision warnings remain; the source and Unity checks above are authoritative.

## Quick manual recheck

Open the tree with T and spend a point on an available node. Look for a short
round gold cue without a square tint or corner symbol. Try a connected node with
no points: the red cue should also be round and spend nothing. Pan/zoom and close
the tree during feedback; reopening must not replay the old cue.

J01/J02/J03/J04/J05/J06/J10 now have a combined ordinary-play review in addition
to their documented feature checks. J09 remains an audit without another pilot;
J07/J08 remain excluded/user-owned. The player's modest assessment does not
justify adding further visual effects without a specific need.
