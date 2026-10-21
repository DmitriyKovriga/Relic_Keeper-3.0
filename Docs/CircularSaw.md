# Circular Saw

Implemented chakram special skill: 5-second cooldown, 10 mana, one disk regardless of Projectile Count, Fork or Chain.

- Initial hit: 250% weapon damage.
- First accepted enemy contact anchors the disk in world space for 2.5 seconds. It does not follow the enemy.
- Hidden repeat damage: every 0.25 seconds, 50% of the skill's damage (125% weapon damage), capped at ten repeat passes. Compound colliders count once per pass. A moving enemy can leave and re-enter the disk.
- Effect Duration scales the pause through the shared duration calculator. It does not increase the repeat budget; a shorter pause can yield fewer passes.
- Projectile Speed affects outbound and return travel. The outbound limit is 8 metres. A miss or a wall starts return flight without a sawing pause.
- Return homes toward the moving character, uses the existing 50% return-damage rule plus Returning Projectile Damage, and can hit each target once without pausing again.
- Fork and enemy Chain are disabled to preserve one disk. Lifetime includes the resolved pause plus a return allowance. Pooled initialization resets pause, return and contact state.
- EN/RU automatic tooltips show current pause duration and repeat damage. Damage preview includes the initial strike only, since repeat and return hits depend on position.

## PixelLab assets

All new art comes from PixelLab. Sprites use Point filtering, 24 pixels per unit, no compression or mipmaps.

- Icon: gallery `b9e34f21-20f2-55df-a834-f3236482e080`, job `0d399440-ca04-41fd-bb1b-93b41ba9a2e9`.
- Disk: gallery `01f5d634-5ed5-5711-b573-b8ac014fc87d`, job `5c3f5f3c-713e-47cd-84fa-000ee0d74b02`.
- Cutting sparks: effect `7b7bc9d2-1cfd-45f3-ae62-03b99461f34f`, step `97cff87d-f666-4706-b95a-c0bbfaf28629`; eight 32×32 frames at 24 fps.

Assets and repeatable builder: `CircularSawBuilder`, skill folder `Assets/Resources/Skills/2HWeapon/Chakram/rb/CircularSaw/`.

## Verification

`CircularSawTests` covers coefficients, quarter-second repeats and cap, duration scaling, escaping and returning targets, compound colliders, fast swept contact, Projectile Count/Fork/Chain exclusion, speed scaling and zero-speed timers, missed throws, wall returns, return damage, pooled reuse, registration and both locales.

Related regression filters: `SkillProjectileReturnTests`, `PrismaticCycleTests`, `SkillProjectileHitboxTests`.

Play Mode review used the actual Mold prefab and runtime projectile at 480×270. Travel logs confirmed ten repeat passes and return to the owner; captures include a departing target and both tooltips (EN 135×176, RU 135×182).
