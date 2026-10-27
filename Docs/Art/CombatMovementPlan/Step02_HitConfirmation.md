# Step 02 — confirmed hits without extra contact effects

Updated 2026-10-27 after user visual review. C01's enemy-hit confirmation path
and a shared per-cast hit-stop gate remain implemented. The automatic generic
contact mark has been removed: it overlaps skill VFX and adds visual clutter.
The next slice is still C02 body-follow and C04 enemy visual recoil.

## Current behavior

- Skills use their existing authored VFX for the visible strike. No automatic
  generic spark, contact mark or additional burst is created by confirmation.
- Zero final damage, evasion and damage-over-time ticks do not request skill
  hit-stop. Processed zero damage still returns true to existing gameplay callers.
- One accepted cast shares one hit-stop gate across melee steps, chains,
  projectile siblings, forks and returns. Intentional repeated damage and its
  confirmations remain intact; they do not repeatedly freeze the same cast.
- Positive direct hits use the existing frame-count service and authored frame
  values. Lethal hits confirm before the existing death sequence.

Damage calculation, hitboxes, attack timing, conversions, ailments, gameplay
on-hit effects, knockback, execution and cancellation retain their existing
rules. The zero-damage fix changes presentation acceptance, not gameplay rules.

## Single presentation signal

`EnemyHealth.TakeDamage` confirms after mitigation and execution resolution,
before gameplay notifications, hit animation and death can alter the target.
`CombatHitFeedback.HitConfirmed` is independent of `GameplayEventBus` and is
not subscribed to either `DamageTaken` or `DamageDealt`. Those existing events
and their consumers remain intact. One receiver invocation therefore cannot
be duplicated through the paired gameplay notifications.

`ConfirmedHit` captures final post-mitigation damage, crit/lethal flags,
target identity, position and direction. Damage is the resolved amount used
by the existing notifications, not capped to remaining HP. No receiver's final
damage or contact position is written into a shared `DamageSnapshot`.

`AttackImpactContext` is created only after cast acceptance. It captures a
unique transient token, skill ID, weapon asset and slot. Projectiles retain
this identity through launch-data clones and runtime reuse, instead of reading
current equipment at impact. Separate launch families without previously
configured hit-stop do not gain an extra freeze by default.

Position remains an explicitly marked fallback: root collider bounds center,
otherwise visible sprite bounds center, otherwise root position. It is not a
claimed physics collision point or normal. Projectile direction uses current
travel direction; melee uses attack origin toward the receiver. Coincident
or unknown origins have a deterministic rightward fallback. Values survive
receiver destruction and remain available for later body/recoil presentation.

## Visual decision and cleanup

The first pilot reused a dagger spark. A later six-frame PixelLab directional
contact was also tried. User review rejected the extra effect itself because
it overlaps authored skill VFX and creates clutter. Both generic variants are
out of the runtime. The contact presenter, shared mark profile, renderer pool,
art importer and their obsolete tests were removed. Draft new artwork is kept
only in ignored `Logs/RejectedContactEffect/`, outside Unity's Assets folder.

Future C05 work must not reintroduce a universal contact burst by default.
Improvements should first come from character motion, enemy visual recoil and
coherent existing skill effects. Broader particle, flash and camera budgets
remain relevant only if those specific effects are later approved.

## Verification

After removal, Unity 6000.3.11f1 compilation passed. The focused EditMode run
passed **29/29**, with zero failures and skips: confirmation/baseline tests plus
the relevant hit-stop gate and frame-clamping cases. Coverage includes paired
notifications, mitigation, evasion, DoT, lethal execution, shared snapshots,
repeated damage, accepted-cast identity, projectile clones and real projectile
reinitialization. No full-suite or standalone player-build result is claimed.

The refreshed live recording uses the same controlled stationary Warrior,
Cleave recipe and Mold target as step 1 at 480×270 / 60 FPS. Its native captures
show the existing skill VFX without a generic contact overlay. The original
`Baseline/` remains unchanged; current recordings are in `HitConfirmation/`.

| Case | DamageTaken / DamageDealt | Confirmations | Frozen frames |
|---|---|---|---|
| Miss | 0 / 0 | 0 | 0 |
| Positive direct hit | 1 / 1 | 1 | 3 |
| Evaded | 0 / 0 | 0 | 0 |
| Zero final damage | 0 / 0 | 0 | 0 |
| Lethal | 1 / 1 | 1 | 3 |
| Ignite tick | 1 / 1 | 0 | 0 |
| Target already attacking | 1 / 1 | 1 | 3 |

- [Direct hit, normal](HitConfirmation/direct-hit-normal.gif) · [4× slow](HitConfirmation/direct-hit-slow-4x.gif)
- [Zero damage, normal](HitConfirmation/zero-damage-normal.gif)
- [Busy enemy, normal](HitConfirmation/attacking-target-normal.gif)
- [All seven contact comparisons](HitConfirmation/contact-sheet.png)
- [Outcome summary and source hashes](HitConfirmation/audit.json)

Export with `build_baseline.py --confirmation` only after all seven live cases
complete successfully. Native PNG frames remain in ignored Logs.

## Scope still ahead

C02 attacker body-follow and C04 enemy visual recoil are next. Player damage
feedback (C12), camera, audio and a material catalog are not implemented here.
The live fixture is a stationary Warrior/Mold pilot, not all classes, air/run
states, room crowds, highest APS, monitor refresh rates or a performance profile.
