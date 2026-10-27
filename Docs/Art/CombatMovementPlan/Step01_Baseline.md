# Step 01 — observed combat baseline

Completed 2026-10-27. This delivers **step 1, “Observe, do not retune everything,”**
from the plan's dependency order. C01 and the presentation changes remain future
implementation work. Damage, recipes, movement and cancellation rules are unchanged.

## What was recorded

Actual Play Mode camera output at **480×270**, using the existing **Cleave** primary
two-handed axe recipe, its overhead weapon style, a Warrior body and a Mold target.
Each of the seven cases has 110 captured frames: **770 native frames in total**.
Normal-speed and 4× slowed GIFs use the same frames; slowing changes playback only.

The controlled fixture is stationary: player physical damage 100, base APS 1,
critical chance 0; target HP and stun threshold 10000, except lethal HP 1.
Stun meter is recalculated after fixture stats, preventing an unintended gameplay
stun from invalidating the busy-enemy observation. Target damage-taken is -100%
for the zero case; high evasion is used only for the evade case. A real Ignite
stack supplies the DoT tick. No rewards or loot are awarded.

The fixture uses the real SkillStepRunner, weapon animation, player presentation,
EnemyHealth, enemy animation/attack and ailment code. Only the fixture's physical
positions are constrained. This is a controlled standing baseline, not a test of
running, airborne attacks, all classes, all enemies or real player equipment builds.

## Outcomes

Every case had exactly one accepted cast. The two event columns describe existing
notifications; they do **not** represent two gameplay hits.

| Case | TakeDamage result | DamageTaken / DamageDealt | Other result | Captured frozen frames |
|---|---|---|---|---|
| Miss | Not called | 0 / 0 | No target confirmation | 0 |
| Positive direct hit | true | 1 / 1 | 100 final damage | 3 |
| Evaded | false | 0 / 0 | One Evaded event | 0 |
| Zero final damage | true | 0 / 0 | HP unchanged | 3 |
| Lethal | true | 1 / 1 | One EnemyKilled event | 3 |
| Ignite tick | Not called by the skill | 1 / 1 | 3 damage, IsDirectHit=false | 0 |
| Target already attacking | true | 1 / 1 | Busy before and after accepted hit | 3 |

The stop observations are from a fixed 60-FPS capture. They are not measurements
across 30/120/144-Hz displays or crowd/high-APS conditions.

## Findings for the next steps

1. **Zero final damage still freezes play.** EnemyHealth returns true for a
   processed zero-damage hit. The melee runner treats that bool as connected and
   requests its existing three-frame stop. Step 2 should distinguish feedback
   confirmation from processing acceptance, preserving existing gameplay on-hit rules.
2. **One positive hit has two bus notifications.** DamageTaken and DamageDealt
   carry the same hit. A presentation subscriber listening to both would double
   the response. No new subscriber or full impact package was added in this step.
3. **The ordinary player body is not following the weapon yet.** In this standing
   capture the body remains on its existing idle image, `WarriorRun_00` (the current
   controller intentionally uses the first run frame for idle). Body local X and
   rotation remain zero while the hand animates windup, strike and recovery.
   The next body-follow implementation must compose with that presentation owner.
4. **A busy Mold keeps its attack rather than taking the hit clip.** The normal
   case includes MoldHit frames; the busy case keeps MoldAttack frames while
   accepting 100 damage. This supports the proposed cosmetic recoil layer without
   adding a gameplay interruption.
5. **The actual hit is deferred by VFX lifetime.** The authored damage step starts
   at pipeline 0.15, but its SourceStepIndex and VfxLifetimePercent=0.5 defer the
   resolution. These recordings resolve at approximately phase 0.333. Use the
   actual confirmation and recipe clock; do not assume the serialized start is
   the impact moment or retime gameplay to match a new body pose.
6. **Current event position is a fallback.** It is the target root, not a collision
   point or surface normal. The observation reads it without changing the shared
   DamageSnapshot. Per-target contact context remains a C01 follow-up.

## Review files

- [Native normal-speed direct hit](Baseline/direct-hit-normal.gif)
- [The same direct hit at 4× slow playback](Baseline/direct-hit-slow-4x.gif)
- [All seven cases: before, contact/strike and recovery](Baseline/contact-sheet.png)
- [Validated outcome/event summary and source hashes](Baseline/audit.json)

Every case has its own normal/slow GIF and pose CSV in **Baseline/**. CSVs record
cast token, actual normalized phase/duration, time scale, casting/busy state,
body image/transform, hand pose, weapon visibility and target HP. Full frame/event
traces and original PNGs remain in ignored **Logs/CombatBaseline/**. Pose samples
are taken after presentation LateUpdate and before camera pixel-rounding.

The stage has neutral light for the existing lit skill VFX. It uses no newly
generated game artwork and does not alter the atmosphere profiles. GIF palette
quantization and screenshot crops are review artifacts; native PNGs are retained.

## Tooling and verification

Editor-only observation fields count accepted casts and sample the real runner
phase. A guarded Editor-only resolution callback records the existing return value;
observer exceptions cannot change the gameplay path. These hooks and the temporary
capture driver are excluded from player builds with UNITY_EDITOR, are not serialized,
add no gameplay UI and write no saves. The driver restores capture rate, random
state, pause state and temporary window focus, removes its listeners and objects.

Unity compilation passed. **25 relevant EditMode checks passed**, including the five
new observation/semantics checks, cooldown recovery, independent hand animation,
hit-stop clamp and shared gate. All seven live outcomes validated. The Editor was
returned to Edit Mode. The full suite, all-content rollout and standalone build
were not run; the separately documented recipe-zero hit-stop inventory remains
unchanged by this audit.

The normal Unity Game view must be visible during recording: the existing hit-stop
service waits for rendered end-of-frame callbacks. A hidden/stepped trial cannot
establish stop behavior and was discarded. The delivered clips use continuous
Play Mode with the real service, without a replacement timer or manual time-scale fix.

Next: **step 2 — C01 confirmed-hit semantics plus initial C18 bounds**, before
body-follow, target recoil or new contact art.
