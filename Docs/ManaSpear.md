# Mana Spear / Копьё маны

Wand primary skill. Zero cooldown and mana cost.

- One straight, narrow spear. Initial coefficient: 100% weapon damage, converted to lightning.
- Every complete 75 Max Mana contributes 5 flat lightning damage before normal damage modifiers. Current mana is irrelevant. The link is skill-only and reads modified Max Mana.
- Projectile Pierce adds extra victims: a value of N permits 1+N distinct targets. Compound colliders cannot double-hit the same enemy.
- Projectile Count, Fork and Chain are ignored to preserve one precise line. No homing, innate pierce or splash damage.
- Base speed 18, lifetime 2 seconds; Projectile Speed scales travel. Solid terrain stops the spear.
- Fixed world hit radius 0.1 metres, independent of art length, animation and Area of Effect. Other projectiles retain their existing sprite-derived collision radius.

The existing stepped stat-link system supplies the Max Mana bonus. Tooltips now describe complete source steps accurately instead of displaying an equivalent continuous percentage. Examples: 74 mana adds 0, 75 adds 5, 150 adds 10, and 750 adds 50 lightning damage.

Projectile sweep processing now continues after a successful pierce. Previously the scan stopped at its first enemy, allowing a fast projectile to skip later enemies crossed during the same frame. The pierce budget still stops it at the final allowed victim.

## Assets

`ManaSpearBuilder.Apply` creates the skill/recipe and registers the wand primary pool, item database, EN/RU names and descriptions. Assets live under `Assets/Resources/Skills/1HWeapon/Wand/ManaSpear/`.

All new art is PixelLab-authored: Point filtering, 24 pixels per unit, no compression or mipmaps. Animated art is a child of the stable projectile/collision root; pooling resets its animation. Lightning contact effects appear only on accepted damage.

- Icon: gallery `616d3ac3-b37f-5777-99a1-21c7ed03fb77`, job `ceed829b-4b62-42d4-8e74-4f35cc1b2934`. Enlarged faceted mana spearhead with an electric wake.
- Flight: effect `3feb0a30-17be-4d8f-8f11-56c177997ee4`, step `c72046db-a5dd-4ddc-a065-4fc90cc59554`; eight 64×24 frames at 24 fps. Counter-moving electric filaments, tapered trail and drifting sparks remain attached to the narrow spear.
- Glow: effect `66187605-c75b-42c8-b4da-247fe6f3a9e6`, step `60fde159-a521-45aa-9e2f-f06d23cd52ad`; eight 64×24 frames at 24 fps. A separate additive blue halo pulses behind the spear, follows attack opacity and sorting, and resets on pool reuse. No global bloom change or additional collision geometry.
- Pierce contact: effect `51e49327-c999-46cf-9407-b317cfc89e53`, step `bcaa7341-0eb5-489f-8332-f5f69b9bc475`; eight 32×32 frames at 40 fps.

## Verification

`ManaSpearTests` covers mana thresholds and modified maximum mana, current-mana independence, channel conversion, spell scaling, preview/runtime agreement, precise swept collisions, compound hurtboxes, 0/1/2 pierces, off-axis misses, single-shot exclusions, wall blocking, animation/collider independence, pool reuse, registration and both locales.

Related filters: `SkillProjectileHitboxTests`, `PrismaticCycleTests`, `SkillDpsPreviewTests`, `LuckyShotTests`, `CircularSawTests` and `ArrowArtilleryTests`. Combined result: 88 passed, zero failures/skips.

Live Editor verification: three aligned Mold enemies each received 20 damage (10 weapon + 10 from 150 Max Mana); the off-axis enemy received zero. Flight and contact effects were rendered at 480×270. English and Russian tooltips fit within the reference viewport (135×120 and 135×127). Temporary preview objects and helper were removed; play mode and autosave were restored.

Visual upgrade verification: 28 focused ManaSpear/SkillProjectileHitbox tests passed, including additive shader support, animation reset on pool reuse and stable narrow collision. Updated icon, electric wake and glow were reviewed in a live 480×270 render; aligned/off-axis damage remained 20/20/20/0. No console errors. Preview helper removed and autosave restored.
