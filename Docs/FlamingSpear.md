# Flaming Spear / Пламенное копьё

Fire staff special skill: 10-second cooldown, 25 mana, 250% weapon damage converted entirely to fire. Uses spell action speed and launches one narrow, straight spear at 15% of its casting sequence.

At impact, each existing Ignite on that particular target grants 20% additional direct damage. Bonuses add together: three stacks give 250% × 1.6 = 400% weapon damage. The stack count is read before this hit applies normal ailments. Existing Ignites are neither consumed nor modified by the bonus. A hit may still apply or refresh Ignite under the ordinary ailment rules.

MaxIgniteStacks increases the number of Ignites that can be prepared. ProjectilePierce uses the ordinary additional-target budget; each pierced target gets its own bonus. ProjectileCount, Fork, and Chain do not duplicate or redirect the single spear. World geometry stops it. Base speed: 18 units/second; safety lifetime: 2 seconds; narrow world probe: 0.1 units. Target-independent damage previews show the unconditional 250% damage, while the generated English/Russian tooltip explains the conditional bonus.

Registered in the fire staff special pool and item database. Rebuild through `RelicKeeper.Editor.Skills.FlamingSpearBuilder.Apply`, preserving existing GUIDs. The bonus is authored in the projectile step's `IgniteHitDamagePercentPerStack` parameter; other projectile skills default to zero.

## PixelLab art

- Opaque 32×32 icon: job `2120f955-99f9-4eb2-b5ce-d102b453332e`, gallery `d7ddee07-7d20-59cd-b99f-1fca06ef1ad4`.
- Flight: effect `ea044be6-e624-41b6-9b69-d8e7e5ab8318`, editable step `8f6fb3f5-f497-40a3-87b4-928c662b0eeb`, export `9541d5a5-f271-40fa-8cb4-6a94dd0853a5`. Six 64×24 frames at 20 fps, looping.
- Impact: effect `9992b0a6-a55d-4607-bd15-52c3d0e3b5f2`, editable step `b891287b-f33c-4b21-a37e-adf668be37aa`, export `a124fdf6-9f81-410d-ab65-e15a72359891`. Six 32×32 frames at 30 fps, one-shot.

Point-filtered, uncompressed sprites without mipmaps. Reuses the existing spear animation/impact component with independent fire art and material; no changes to Mana Spear assets.

## Validation

Eighteen skill-specific EditMode cases cover costs and registration, opaque icon, conversion and unconditional preview, additive stack scaling, preservation of existing stack damage/duration, stacks gained or expired during flight, independent pierced-target bonuses, MaxIgniteStacks, crit/spell scaling, geometry, narrow collider, pool reuse, and English/Russian descriptions. An Ignite applied by the spear cannot amplify that same hit; projectile steps without the bonus parameter keep their ordinary damage.

Unity compilation succeeded. The focused regression run passed 69 tests with no failures or skips, covering Flaming Spear, Prismatic Cycle, Ignite spread, projectile hitboxes, damage previews, descriptions/highlighting, and icon backgrounds. The Editor error log was empty.

The wider run including Mana Spear passed 84 tests and failed three existing Mana Spear expectations: `CountForkAndChainDoNotMultiplyOrBendTheSpear` and the zero/one-pierce cases of `PierceBudgetHitsExactlyTheAllowedNumberAlongTheLine`. Its committed `Recipe_ManaSpear.asset` already has `InfinitePierce` enabled, while those tests expect it disabled. Neither that recipe nor those tests were changed.

An isolated live Play Mode preview using the mage and staff confirmed a single spear pierced three targets for 400/300/250 damage with 3/1/0 existing Ignites, preserving all stacks. Its visuals were inspected at 480×270. The temporary harness was removed and the prior autosave setting restored.
