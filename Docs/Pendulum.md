# Pendulum / Маятник

Chakram primary in `ChacramLBSkillPool`. No cooldown or mana cost. One disc launches at 20% of the normal attack-speed-scaled throw animation. The accepted base damage is 100% weapon damage; turns occur at projectile ages 0.6 and 1.2 seconds, and the disc expires at 1.8 seconds.

## Fixed three-pass path

The disc moves straight forward, reverses backward, then forward again. It never aims at or homes toward the owner. At base speed 10, each 0.6-second pass covers 6 metres, repeating the lane between the release point and the first turnaround. Projectile Speed changes that length; Attack Speed changes release cadence, not an already flying disc's turn interval. Each enemy can be hit once per pass; hit history clears on both reversals. Multiple hurtboxes do not multiply hits within a pass.

Both later passes use the existing return rule: 50% base return damage plus the raw flat percentage points of ReturningProjectileDamage. With 100 physical damage and +25 Returning Projectile Damage, the three hits deal 100, 75 and 75 before mitigation/other modifiers. The second turn does not reset or compound this bonus. The normal damage preview shows the first hit.

Infinite enemy pierce preserves the lane. Projectile Count, Fork and Chain add no discs or branches. Ordinary world/platform collision stops the disc. Multiple casts may coexist, each with independent timing/history. Cancellation before release prevents the throw; released projectiles follow their own lifetime.

`FixedReversalTiming` opts this skill into segmented movement: each update is split at turn and expiry boundaries, with swept collision on every segment. Larger frames therefore cannot turn early, skip a reversal or drop the last segment before expiry. Existing projectiles retain their previous update path. Reuse resets age, turn timing, target history and return-damage state through the existing initialization lifecycle.

## Return-damage correction

The new regression exposed an existing issue: ReturningProjectileDamage was also included in DamageCalculator's generic context-modifier index. Its flat percentage then added base damage to ordinary projectile hits, including the first pass, before being applied again by the return resolver. The generic index now excludes this specially handled stat. Metadata, affix eligibility and the flat-percentage return rule stay intact. Regression cases cover unchanged ordinary hit damage and correct later-pass damage/preview.

## PixelLab artwork and localization

Opaque 32×32 icon, transparent 32×32 steel chakram sprite, and four 32×32 wind-accent frames at 20 fps. The disc has an open center, compact scratched steel rim and restrained moving edge streaks. Point filtering, 48 PPU, no compression/mipmaps; the wind Animator targets a child renderer while the root disc spins. English and Russian descriptions explain the path, timing, speed scaling and both later passes.

- Icon job `1e87c507-a392-45f6-b283-cfa72f0d179b`, gallery `6665c008-65d8-50ce-9307-f8d32d952b77`.
- Disc job `e351a151-2de5-4d03-a688-e5e4286f3a09`, gallery `63822f4b-4dd7-5dcf-99bb-7e6694827219`.
- Wind effect `f2dafc79-8e9a-495b-b568-2e9ad4515c84`, editable step `f7e25b0c-27a2-4c93-b4c5-4edc027fac05`, export `1669e2f4-a7f9-437b-9e6c-9897d676d585`.

`PendulumBuilder.Apply` builds/registers data and both locales; `RefreshArt` rebuilds only visuals. Existing asset GUIDs are retained on rebuild.

## Verification

19 focused skill cases cover registration, fixed turn geometry/facing, moving owner, per-pass hit deduplication, return bonus, fine/coarse frame equivalence, flat/increased projectile speed, one-disc restrictions, reuse, cancellation and both locales. Together with the return-stat tests, all 26 focused checks passed.

Live Play Mode captures at 480×270 confirmed both front targets received three hits each, behind/distant targets received none, the disc reversed twice along its original lane and expired after 1.8 seconds. Preview code was removed and editor autosave/time/background settings restored.

Final validation: Unity compilation succeeded and all 100 relevant regression checks passed with zero failures/skips (Pendulum, return resolver/flight, Circular Saw, elemental/ranged damage, projectile hitboxes/speed, damage previews, descriptions/highlights, icon backgrounds and Near Fan). Console error query was empty. A wider run including InventoryItemLocalDamageTests had 105 passes and three pre-existing affix-data mismatches: EveryWeaponPool_OnlyOffersLocalFlatAndIncreasedCritChance expects seven pools but finds eight; SavedWeaponCritAffixes_BakeFlatAndIncreasedIntoWeaponOnly and GlobalCritChanceAsset_RemainsGlobalAndDistinctFromWeaponLocalCopy expect short IDs while the committed assets already contain path-qualified IDs. No affix assets were changed.
