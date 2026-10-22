# A Fan of Sickles

Chakram primary skill: no cooldown, no mana cost, 100% weapon damage per outward hit. Uses the normal attack-speed pipeline and releases at 20% of the attack.

- Three base discs launch simultaneously within a fixed 50° fan. ProjectileCount adds discs within that fan.
- Base speed is 10 units/second. After 0.4 seconds each disc turns and continuously homes toward the owner's current position. ProjectileSpeed changes travel distance; moving the owner changes return trajectories.
- Each disc can hit each enemy once outward and once returning. Return hits use the existing rule: 50% base damage plus ReturningProjectileDamage percentage points.
- Discs pierce enemies and pass through world geometry, matching the existing homing chakram behavior. Fork and Chain do not add branches. They disappear when caught, with a 4-second safety lifetime.
- English and Russian names, descriptions, and generated mechanical tooltips are included. Registered in the chakram primary pool and item database.

## Assets

All new art was created through PixelLab. Point filtering, no compression or mipmaps, 48 pixels per unit. The icon has an opaque dark background. The disc uses a separate animated rim accent at 20 frames/second.

- Icon image job: `4673eabf-8e83-4895-9890-fc5defaa7752` (gallery `994799c9-54b4-5e12-88dc-c55665b44eea`).
- Disc image job: `29738d9f-c548-4e0f-a789-2ef6e9162b3a` (gallery `a99f4065-b4e0-5932-a853-18a705637acb`).
- Wind effect: `aa88fcc5-02e6-4a0d-baf5-5e96e674fb8d`; editable step `c8a5a012-32a1-488c-b891-62bc8fe38a7b`; export `3ab818dd-5ba0-4cd5-a5ca-7660a714efbb`.

Rebuild with `RelicKeeper.Editor.Skills.FanOfSicklesBuilder.Apply`; refresh only the visual assets with `RefreshArt`. Both preserve existing asset GUIDs.

## Validation

Seventeen skill-specific EditMode cases cover registration, costs, fan count/angles and facing, timing independent of frame size, moving-owner return paths, outbound/return hit limits and damage, projectile speed, geometry, cancellation, localization, and opaque icon background. The existing Pendulum tests also pass after extending the fixed-turn path to support owner homing.

A live Play Mode preview confirmed three discs turn once, return toward a moved owner, hit a target missed on the outward flight, and disappear when caught. The temporary preview harness was removed after verification.

Final verification: Unity compilation succeeded; 112 relevant EditMode tests passed with no failures or skips, including returning projectiles, Pendulum, Circular Saw, Near Fan, projectile speed/hitboxes, damage previews, descriptions, and icon backgrounds. The Editor error log was empty.
