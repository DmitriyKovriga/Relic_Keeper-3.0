# Step 03 — body follow-through and enemy visual recoil

Follow-up: [custom melee coverage](Step03_MeleeExpansion.md) extends this pilot
to ten additional skills. The original pilot scope and measurements follow.

Completed 2026-10-27. Implements the C02/C04 slice using existing sprites.
The player prepares backward, follows through forward and returns to the
current locomotion pose. Enemies show a small directional recoil while their
existing animation continues. No generic contact spark, particle burst, sound
or camera effect is added.

## Preview

These are actual Unity camera frames. The side-by-side clips use the same
recipes with the new body/recoil layers disabled on the left and enabled on
the right. They are cropped and enlarged 3× with nearest-neighbor sampling;
the individual clips remain at the native 480×270.

- [Ordinary Cleave comparison](PoseComparison/cleave-comparison.gif)
- [The Weight of a Giant comparison](PoseComparison/heavy-comparison.gif)
- [Attacking Mold](PoseComparison/attacking-target-normal.gif)
- [Animator-root Zombie](PoseComparison/zombie-recoil-normal.gif)
- [Left-facing Rogue](PoseComparison/rogue-left-normal.gif)
- [Airborne Mage miss](PoseComparison/mage-air-normal.gif)
- [Preparation/contact/recovery sheet](PoseComparison/contact-sheet.png)

Each individual case also has a 4× slowed clip and a pose CSV in
`PoseComparison/`. The slowed clips change playback only, not simulation.

## How it works

`SkillStepRunner` forwards the existing WeaponWindup, WeaponStrike and
WeaponRecovery phases to `AttackBodyFollow`. The weapon recipe remains the
clock and authority. Misses still follow through. Only ordinary melee recipes
with those three phases and a supported direct-damage step acquire this body
layer. Channeling, equipment utility, projectile recipes, parallel recipes,
SpinAround and the special Falling Anvil pipeline remain neutral in this pilot.

`PlayerMovementVisual` composes the pose on its existing display sprite, over
the current run/jump/fall/landing image. Small rotation and compression use a
compensated foot pivot. The hands, weapon transforms, owner root, Rigidbody,
colliders, reach, damage windows and cancellation rules are not modified.
Latest accepted valid cast ownership prevents older/offhand cleanup from
clearing a newer pose. Dodge, spin and stasis take presentation priority;
cancel, disable, death and character changes clear ordinary body follow.

`CombatHitFeedback` sends a confirmed hit to `EnemyHitRecoil` on the receiver.
`EnemyEntity.Setup` initializes this layer for both sheet-driven and root
Animator enemies. Recoil copies the visible sprite, material, color, flips,
sorting and property block into one reusable display child while briefly
hiding the source renderer with `forceRenderingOff`. The original renderer
and animation continue to own their normal frame/foot registration. No root
Animator transform is moved or reparented, and no collider is deformed.

The recoil offset is converted from world direction into source-local space,
so left-facing enemies still move visually away from the blow. Rotation and
compression preserve the source foot baseline. A bounded pulse replaces its
current displacement on retrigger, blending from that current pose instead
of adding offsets. Death, hidden sprites, freeze, disable and reuse restore
the source and clear the pulse. A legitimate gameplay stun can still interrupt
an attack under the pre-existing rules; cosmetic recoil adds no interruption.

There is no separate timer that restores Time.timeScale. Body phase and recoil
use scaled time, so pause and the existing hit-stop hold the presentation.
One lazy display renderer per hit enemy is reused; idle hidden displays are
not repeatedly rewritten. There is no particle allocation per confirmation.

## Shared tuning

Select `Assets/Resources/Visuals/CombatPoseProfile.asset` in Unity's Project
window to inspect the shared motion settings. No new runtime screen or
maintenance button is introduced.

Default ordinary preparation/strike angles are 2° backward and 2.5° forward,
with a small height compression. A heavy hit uses a 1.4 multiplier, capped at
6°. Normal enemy recoil is 1.2 source pixels, about 2° of lean, and 0.16 scaled
seconds. Recoil retrigger interval is 0.05 seconds; its displacement is hard
capped at 2.5 source pixels. Boss response uses 35% strength. The two enable
switches allow body-only, recoil-only or disabled comparisons. Profile settings
are shared data; no fields were copied into each skill recipe.

## Verification

- Unity 6000.3.11f1 compilation passed.
- The combined relevant EditMode run passed **133/133**, with no failures or
  skips. It includes 26 new combat-pose cases, player render anchors, both hands,
  spin, neutral dodge, ground dash, all three class animation suites and prior
  hit-confirmation/baseline regressions. The final focused pose run also passed.
- Pose tests check fixed feet and physics, identical weapon render paths with
  body follow on/off, run/jump/fall composition, facing, old/new cast cleanup,
  special-state priority, source material/property-block copying, sheet and
  Animator enemies, busy attacks, boss scaling, hidden/frozen/dead/reused sources
  and capped rapid retriggers.
- **Eight live cases passed**: normal and heavy on/off pairs, an attacking Mold,
  a root-Animator Zombie, a left-facing Rogue and a stationary airborne Mage miss.
  New recoil is visible in damaging enabled cases; the miss produces none.

The recorded hand/weapon position and rotation differences between the on/off
pairs were **zero** at matching recipe phases. Damage confirmation remained at
phase **0.3333334** for Cleave and **0.6999996** for The Weight of a Giant, with
the same hit-stop counts in each pair (3 and 4 rendered frames respectively).
See [weapon comparison measurements](PoseComparison/weapon-comparison.json) and
[capture summary/source fingerprints](PoseComparison/audit.json).

The fixture constrains physics roots to isolate presentation. Base physical
damage is 100, base APS 1, crit 0, and target HP/stun threshold 10000. Skill
speed multipliers, stat links and the existing global hand-stat scope remain
active. In particular, the heavy skill's Max Health damage link boosts damage
in this high-HP fixture; individual weapon rolls also vary. These are motion
comparisons, not new balance targets. The Mage case is a controlled airborne
miss pose, not a full airborne traversal test. Native PNG frames remain in
ignored `Logs/CombatPoseComparison/`; export with `build_baseline.py --poses`.

This is a first shared pose pilot, not a new authored attack animation catalog.
Full gameplay traversal across every room/class/weapon combination and crowd
performance profiling remain outside this verification. No standalone build
or full project test-suite result is claimed. Existing scenes, skill balance
assets and EN/RU UI were not edited for this feature.
