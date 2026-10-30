# J04 continuation — Shock buildup and active electricity

Implemented and checked 2026-10-30. This continues the accepted Ignite work with
Shock's actual buildup/activation/expiry, without text popups or new mechanics.

Refined after user review: the initial smoothly crawling paths read as flowing
energy. Current Shock uses fast, held-shape discharges with new jagged topology
per snap, a sharp attack/short tail and irregular paired flashes separated by
quiet gaps. At default speed the two normal snaps occupy brief windows within
a 0.28-second cluster; reduced mode uses one broader, softer snap per 0.48-second
cycle. The threshold accent is now 0.1 seconds. These are presentation timings,
not changes to Shock duration or proc mechanics. Earlier captures remain in the
parent folder; current evidence is in Snaps.

## Three-chain audit

| Chain | Existing implementation and decision |
|---|---|
| Ignite | Already refined through user review: body-attached fire, overlapping flame lives, blended emission sites and breathing light/glow. Keep that treatment and its ownership. |
| Shock | Successful lightning procs accumulate a hidden enemy threshold; partial buildup had no visual. Full Shock only alternated the whole body's tint. Add a quiet buildup trace and a distinct active arc layer. |
| Mystic Shield | `MysticShieldController` already raises charge-consumption/changed events. `PlayerDefenseFeedback` provides a bounded charge response; `WorldStatusBarsManager` displays current/max charges and recharge progress, and Character Window reads the actual count. Keep these cues rather than adding another consumption flash. Ordinary-play review can still identify a later gap. |

Source audit: `AilmentController.TryApplyShock/UpdateShock/ResetShock`,
`IgniteVisualController`, `MysticShieldController`, `PlayerDefenseFeedback`,
`WorldStatusBarsManager` and `CharacterWindowUI`.

## Delivered behavior

Only a successful lightning proc updates the buildup trace. A short cyan zigzag
crosses part of the body while the enemy is below its existing threshold; it
does not claim an active Shock or damage amplification. Crossing the existing
30%-of-maximum-health threshold produces one short discharge and longer crawling
arcs that snap between shapes rather than scrolling. The active state uses longer
paths and an additional branch, not color alone.
This is a visual distinction rather than a numeric buildup meter.

Refreshing active Shock keeps one reusable arc renderer and extends the existing
status as before, without replaying the activation discharge. Ailment-owned
visuals follow the authoritative ailment expiry/reset; their cosmetic timer
cannot finish a frame early and make a refresh replay activation. The existing
standalone `Play(duration)` behavior remains available when there is no active
ailment owner. Partial buildup retains the existing mechanics' lifetime until
activation or owner reset; no decay rule has been introduced.

The arc is a separate sprite-masked shader layer. Brief quiet gaps are intentional
while the ailment remains active; they do not reset buildup or the status. The
layer follows the current body
frame, flip, alpha, sorting and visibility without writing the source color,
material or hit-flash property block. Freeze continues to own its tint. Ignite's
fire/glow hierarchy remains independent. Reduced effects lowers opacity/width,
removes the second persistent branch and reduces the activation accent. Expiry,
disable, reinitialization and dead-owner cleanup remove the visual.

Damage, proc chance, avoidance, threshold math, effect magnitude, duration,
refresh priority and partial-buildup reset rules remain unchanged. No camera
effect, local light, sound, collision, input change, new HUD element or popup
was added. Existing serialized tint/intensity/speed fields and script identity
remain intact. The Resources material references its shader for player inclusion.

## Verification and limits

Unity compilation succeeded. **68 focused EditMode tests passed, 0 failed/skipped**:
ShockPresentationTests, ShockThresholdTests, IgniteBurnVisualTests,
DefenseFeedbackTests and EffectDurationTests. Tests cover successful versus
rejected procs, partial versus active state, unchanged threshold/damage/duration,
no refresh replay, authoritative expiry, body-renderer ownership, frame/flip,
visibility, resource inclusion, reduced mode and cleanup. The timing regression
forces the visual timer to finish while the actual ailment is still active and
verifies that the visual stays active and refresh remains quiet.
Additional cases check bounded fast flashes with quiet gaps, held topology
within a snap and fewer/wider discharges in reduced mode.

The live check used real Mortfall Room 001 geometry, authored zombie walk sprites
and disposable stats/health owners. It called the real `TryApplyShock` method:
100/300 buildup, then 200/300, then threshold crossing. It checked one activation,
20 quiet refreshes, active arcs, explicit authoritative expiry/reset, unchanged
health and damage multiplier, actual freeze tint, Ignite visual coexistence,
reduced effects and ten shocked actors with one arc layer each. The preview scene
advances presentation explicitly. Expiry is advanced through `UpdateShock(dt)`;
these are controlled fixtures, not an ordinary combat playthrough or timing benchmark.

- [Completed runtime report](J04/Shock/Snaps/RuntimeReport.txt).
- [Native 480x270 comparison](J04/Shock/Snaps/ShockComparison.png).
- [Integer 4x snap/gap details](J04/Shock/Snaps/Detail.png).
- [Sampled active animation](J04/Shock/Snaps/ShockPreview.gif), an integer 5x crop;
  [capture timestamps](J04/Shock/Snaps/AnimationTimes.csv) and
  [preview timing](J04/Shock/Snaps/PreviewTiming.json). Sampling rate is not gameplay FPS.
- [Buildup visibility check](J04/Shock/Snaps/BuildupVisibility.json): the finalized
  partial trace changes visible cyan pixels inside the native body region versus
  the quiet capture. The first, overly faint treatment was revised before handoff.

The updated preview records 96 samples at their actual timestamps, including
each discharge's shape seed and pulse strength. Bright and quiet samples are
both required by the live check. Still state comparisons select actual bright
samples; a separate quiet-gap capture shows the state between snaps. The GIF
uses measured capture timing without invented or interpolated frames.

No scenes, prefabs, combat data or project settings were edited by this task.
Pre-existing build/project/URP edits and generated Addressables linker files
were left intact; the three settings-file hashes remained unchanged during the
lightning-snap refinement. Diff/metadata checks passed. The initial implementation
recorded save hash `d91a39da4baa1798d744651510d149d83190421701aad224dd1102dfd2da321f`.
The subsequent snap refinement matched its current save before/after checks:
`df3ee83c407ae06d35f3a887effbcecd7fc0f1fbc7421f60ae88ee6cde321044`.
The fixture restores presentation settings, background policy and random state.
Graphify was refreshed with its existing namespace-collision discovery warnings.
No new standalone build, ordinary-run A/B or performance benchmark was performed.

## Test by hand

1. Use a lightning setup that needs several successful procs to shock a sturdy
   enemy. Before full Shock, look for the short cyan trace; the damage bonus must
   not begin early. A failed proc should not create a trace.
2. Cross the threshold. Look for one discharge, followed by longer crawling arcs.
   Keep attacking: regular lightning snaps continue, but refreshes must not
   restart the threshold accent or create extra layers.
3. Let Shock expire, then build it again. The old buildup should not survive the
   expiry; a fresh threshold is required, with a fresh activation when reached.
4. Combine it with Ignite or freeze. Check body poses, hit feedback and the frozen
   tint. Try a crowded fight and reduced combat effects.
5. Leave the room or kill the enemy. No detached arcs should remain.

The three J04 chains have now been audited; Ignite and Shock have delivered
treatments, while Mystic Shield retains its existing presentation. Review these
in ordinary combat. The next planned candidate is J05: audit anticipation and
recovery for two enemy families against their actual attack windows. J07/J08
remain user-owned and excluded; J09 remains later.
