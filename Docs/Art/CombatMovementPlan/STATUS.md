# Combat and movement — current delivery status

Updated 2026-10-29. The approved presentation and input-audit pass is implemented
within the coverage below and ready for player playtesting. No additional
mandatory implementation step is selected. Player approval of how it feels and
looks remains a playtesting decision.

## Current coverage

| ID | Result | Evidence / boundary |
|---|---|---|
| C01 | Implemented: confirmed-hit signal | [Step 2](Step02_HitConfirmation.md); positive direct-hit semantics |
| C02 | Implemented: body follows attack phases | [Melee](Step03_MeleeExpansion.md), [30 ranged/casting skills](Step18_RangedExpansion.md); channel loops excluded |
| C03 | Excluded by user decision | No new authored attack-body clips selected |
| C04 | Implemented: enemy visual recoil | [Step 3](Step03_BodyAndRecoil.md); existing gameplay movement stays authoritative |
| C05 | Generic contact effect removed by user decision | Existing skill VFX provide contact; no automatic universal burst |
| C06 | Optional, unselected | No material-by-element effect catalog added |
| C07 | Implemented: target hit highlight | [Step 4](Step04_HitFlash.md) |
| C08 | Deferred by user decision | Audio assets, dispatch and voice budgets remain outside this pass |
| C09 | Implemented: hit-stop tuning | [Step 5](Step05_CrowdAndHitStop.md); pause and shared limits |
| C10 | Implemented within authored presentation | [Step 4](Step04_HitFlash.md), [fatal-hit handoff](Step09_LethalHits.md); no generic contact burst |
| C11 | Implemented: restrained camera impact | [Step 6](Step06_CameraImpulse.md); saved strength/off setting |
| C12 | Implemented: player hurt response | [Step 7](Step07_PlayerHurt.md) |
| C13 | Implemented: distinct defense response | [Step 8](Step08_DefenseFeedback.md) |
| C14 | Implemented: lethal-hit/death handoff | [Step 9](Step09_LethalHits.md) |
| C15 | Optional gameplay change, unselected | Real stagger/interruption rules would need enemy and build balancing |
| C16 | Input audit/fixes implemented | [Step 15](Step15_AttackInputAudit.md); a timed attack-recovery buffer remains a separate decision |
| C17 | Optional gameplay change, unselected | Physical attack steps/lunges would change reach and platform-edge behavior |
| C18 | Implemented within explicit effect paths | [Settings/flash limits](Step16_ReducedEffects.md), [decoration limits](Step19_DecorationBudgets.md), [room audit](Step20_MortfallCombatAudit.md), [targeted GPU capture](Step21_CameraGpuTiming.md) |
| M01 | Implemented: start/brake/turn body weight | [Step 10](Step10_MovementWeight.md) |
| M02 | Implemented: landing body strength | [Step 11](Step11_LandingContact.md); moving-landing slide follow-up included |
| M03 | Implemented: planted-foot dust timing | [Step 11](Step11_LandingContact.md) |
| M04 | Camera comparison delivered | [Step 12](Step12_CameraFollow.md); saved default remains direct follow |
| M05 | Existing input windows audited and fixed | [Step 13](Step13_InputWindows.md); existing timing values preserved |
| M06 | Corner audit completed; correction not justified | [Step 14](Step14_CornerAudit.md); sixteen controlled trajectories reproduced no persistent snag |

## Final regression check

**369 passed, 0 failed, 0 skipped**, run together through the live Editor bridge.
The 23 selected classes cover confirmed hits, melee/ranged body follow, hit-stop,
camera feedback, hurt/defense/death, movement/landing/surfaces, jump/attack input,
pause, corner traversal, feedback budgets, diagnostic placement/timing gates and
weapon-swap lifetime. [Exact selection and result](FinalAcceptance/test-results.json).

This checks the current working tree, including the previously implemented GPU
diagnostic changes. This closeout adds documentation only. It does not rerun the
entire repository suite or repeat all earlier live recordings. The standalone
room/GPU evidence remains in its linked reports, with its original limitations.

Known separate data/test disagreement: three older ManaSpearTests expect finite
piercing, while the existing Mana Spear recipe enables InfinitePierce. It was
documented in [Step 17](Step17_RangedBody.md), remains untouched and is outside
this regression selection. A whole-repository green result is not claimed.

## What remains

Further work requires choosing a specific scope:

- C15: define real interruption/stagger rules for light enemies, heavy enemies
  and bosses, including protection against indefinite interruption.
- C17: select attacks that should physically step/lunge, then test reach, walls,
  platform edges, one-way platforms and airborne use.
- C16 extension: decide whether early presses during attack recovery should
  queue another attack, and define cancellation rules.
- C18 coverage extension: automatic smoke, persistent trails, other custom VFX,
  additional rooms/skills/hardware and a detailed GPU pass breakdown. The measured
  samples do not justify blindly culling more effects.
- C06/C08 remain optional/deferred until the user changes their scope decisions.

## Player playtest

Use a normal game session to check running turns, jumping/landing while moving,
fast melee/ranged attacks, defensive outcomes and crowded combat. Compare
Settings → Combat effects → Normal/Reduced; pause during an attack, swap weapons
and transition rooms. Report a concrete skill, room and setting when something
looks wrong. Automated checks cannot decide whether a visual response suits the
game's style or whether movement feels good to the player.
