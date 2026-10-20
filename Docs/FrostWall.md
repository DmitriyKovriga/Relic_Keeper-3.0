# Frost Wall

Cold two-handed staff special, available in its secondary skill pool.
Cooldown 5 seconds; mana cost 35. Cast places one stationary wall ahead;
recasting replaces that character's previous wall. Solid terrain rejects
placement before a Mystic Shield charge is consumed.

The wall lasts 5 seconds and intercepts enemy projectiles and melee, charge,
and direct attacks crossing its position. It also responds when an enemy
melee/charge hitbox strikes the wall without reaching the player. It does not
grant invulnerability: attacks from the player's side can still hit. Enemy
movement and friendly projectiles are not blocked.

Each intercepted attack launches one cold shard toward its attacker. Shards
use 50% main-hand weapon damage through the normal hit pipeline, converting
physical, fire, and lightning damage to cold. They use main-hand slot 0 even
though the wall occupies the staff's special slot. Projectile Speed applies;
retaliations do not chain, fork, or create additional arrows.

If available, exactly one Mystic Shield charge is consumed at deployment.
Initial empowered tuning is 2x retaliation damage and 2x duration: 100% weapon
damage and 10 seconds at default Mystic Shield Skill Effectiveness. These
multipliers are editable recipe defaults. Successful consumption snapshots
Mystic Shield Skill Effectiveness for both outputs. Effect Duration then
modifies duration for either version. No charge means no effectiveness scaling.

The wall ends on expiry, owner disable/death, or active character change.
The tooltip explains both versions in English and Russian. Cast-based DPS is
omitted because damage depends on intercepted attacks.

## PixelLab assets

- Icon: gallery `215f3b6f-646b-5daf-b0d7-dd42e851b2aa`, job `3625b225-0ad6-4be6-9996-7d220258b3a4`.
- Barrier: gallery `9e773fdc-d524-5dbf-b607-8cfaa61fa704`, job `157bd364-4724-485a-b5c1-69b76acef4d3`.
- Shard: gallery `c7c95df6-33ab-55b6-b736-b197e28b7ee5`, job `3980ee92-1d68-4165-86c7-a3ee6a28ffb1`.
- Impact: [PixelLab effect](https://pixellab.ai/create-vfx?effect=a0c96406-944c-45cb-bee8-27e85f097422), 10 frames at 12 fps, origin 19,18 in each 37x47 frame; exported JSON retained beside its sheet.

`Tools/Skills/Build Frost Wall` rebuilds imported sprites, material, visual
prefab, step definition, recipe, skill, localization entries, and registrations.

## Hub projectile repair

The stuck blue ball was an active `Mold_ProjectileTemplate(Clone)` with
`HideAndDontSave` and a missing projectile component after Play mode. Enemy
projectiles now have a matching script file, clear inherited hidden flags on
clones, activate outside the pool too, and guard duplicate despawn. The Editor
cleans runtime-only templates when returning to Edit mode. Swept contacts
ensure fast projectiles hit a wall/player in the correct order.

Regression coverage: `RelicKeeper.Tests.EditMode.FrostWallTests`, including
base/empowered math, charge use, conversion, fast projectile ordering,
pooled reuse, attack delivery, placement rejection, and lifecycle cleanup.
