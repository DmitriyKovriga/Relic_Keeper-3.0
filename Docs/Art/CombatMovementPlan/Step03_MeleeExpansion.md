# Step 03 follow-up — custom melee body motion

Completed 2026-10-27. Adds body follow-through to ten more existing skills:
Crimson Hook, Retreating Thrust, Falling Anvil, Ground Shake, Lunge Slash,
Dexterous Lunge, Stunning Blow, Ninja Stub, Tower Charge and Spectacular Parry.
The catalog now has **23 gameplay skills plus Debug Swing** eligible for this
body layer. Sweep and Conning Tower retain their authored spin. Projectile
attacks, wand/staff spell recipes, equipment utility and channeling remain
outside this melee expansion.

## Behavior

Custom hit steps now qualify alongside ordinary circle/rectangle hits. Their
existing WeaponWindup/WeaponStrike/WeaponRecovery callbacks still drive the
body. Persistent lunge/charge contact windows and Ground Shake's ground wave
also qualify; the latter follows the hammer release, not the remote hit.

Dexterous Lunge and Stunning Blow lack a recovery step. Lunge Slash has a
zero-length recovery. These three use the same normalized recipe clock to
ease the body back after the contact window, through the remaining pipeline.
Degenerate weapon-recovery callbacks cannot snap the body back early. No
weapon steps, hit windows, movement locks or recovery durations were added.

Falling Anvil samples its actual wind-up and recovery coroutine. Its prepared
body pose holds during the fall; forward follow-through begins only after
landing. Attack speed does not become a timer that ends the fall.

Ninja Stub samples the strike inside the successful blink, after selecting a
safe target/destination. If there is no target, preparation eases to neutral
without inventing a forward hit. Recovery interpolates from its captured
starting pose, rather than accumulating changes each frame.

All sampling and cleanup remain bound to the accepted cast owner. An older
cast cannot overwrite or release a newer pose. Dodge/spin/stasis priority and
foot compensation remain shared. Damage calculations, VFX, hand/weapon paths,
physics roots and skill assets are unchanged by the body layer. This adds no
art, particles, UI or localization strings.

## Verification and previews

- Unity compilation passed. **163 EditMode tests passed, 0 failed, 0 skipped**
  across CombatPose, CrimsonHook, RetreatingThrust, FallingAnvil, NinjaStub,
  ShieldParry, TowerShieldCharge, GroundShakeVisual, PlayerRenderAnchor,
  DualWieldHandAnimation and SpinAround classes.
- Ten added eligibility cases first reproduced the gaps: 26 existing pose
  cases passed and all ten new cases failed before implementation. The pose
  class now passes 42 cases, including recipe immutability, missing/zero-length
  recovery, phase ownership, blink misses and landing-controlled progress.
- Three old Ninja Stub assertions expected 100% damage, although its committed
  recipe already uses 125%. Updated those tests to verify 12.5 physical damage
  from a 10-damage fixture and the existing 1.25 multiplier; no balance asset
  was edited.
- **12 live Play Mode cases passed**: all ten skills, both grounded/airborne
  Falling Anvil and Ninja Stub without a target. Every cast showed preparation
  and completed with neutral, unowned body state. All successful attack cases
  showed follow-through and one confirmed enemy hit; the missed blink showed
  no forward hit/recoil. The idle parry review showed its body/guard animation
  without a damage event. Falling Anvil never followed through in the air.

Live cases use real movement against a stationary Mold on a controlled test
platform, with flat lighting and no saved scene edits. The long Lunge Slash
can leave this finite platform after contact. Dexterous Lunge uses the existing
Debug Blade sprite because no matching one-handed sword item with hand art was
found; each actual review weapon is recorded in the capture audit. These are
focused presentation checks, not a full room traversal or crowd/performance
benchmark. No standalone player build was run.

The capture starts these casts in Update, before the body renderer's LateUpdate,
matching normal input timing. Starting after rendering had hidden Dexterous
Lunge's brief preparation frame; the corrected capture shows it.

- [Twelve-case contact sheet](MeleeExpansion/contact-sheet.png)
- [Crimson Hook](MeleeExpansion/hook.gif)
- [Retreating Thrust](MeleeExpansion/retreat.gif)
- [Airborne Falling Anvil](MeleeExpansion/anvil-air.gif)
- [Dexterous Lunge](MeleeExpansion/dexterous.gif)
- [Ninja Stub](MeleeExpansion/blink.gif)
- [Ninja Stub without a target](MeleeExpansion/blink-miss.gif)
- [Capture results and source fingerprints](MeleeExpansion/audit.json)

GIFs use actual 480×270 Unity camera frames at approximately normal playback
speed. The contact sheet crops those frames without scaling the game art.
Raw frames and traces remain in ignored `Logs/CombatMeleeCoverage/`.

## Changed runtime paths

- `AttackBodyFollow`: custom melee eligibility, captured recovery pose and
  fallback sampling on the recipe clock.
- `PlayerMovementVisual` and `SkillStepRunner`: forward the existing pipeline
  phase for that fallback.
- `SkillStepRunner.FallingAnvil`: actual wind-up, landing and recovery callbacks.
- `SkillStepRunner.ExecuteBlinkStab`: forward strike after successful targeting.

The shared enemy recoil path was already broadly covered and was not changed.
