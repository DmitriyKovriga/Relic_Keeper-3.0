# Piercing Shot

A secondary bow attack using the same Bow Draw stance, windup, release and recovery as the current Simple Bow Shot recipe. Adventurer's Bow now uses BowSecondarySkillPool; newly generated bows grant Piercing Shot as their secondary attack.

- Base mana cost: 25; base cooldown: 6 seconds.
- Damage multiplier: 3 (300% weapon damage).
- Exactly one base projectile, ignoring the additional-projectile stat.
- IgnoreFork and IgnoreChain enabled; infinite enemy piercing enabled.
- Projectile speed, lifetime, movement-lock timing and world collision are inherited from the current basic-shot recipe. Infinite enemy piercing does not disable wall collision.

The projectile keeps the ordinary physical arrow on its root renderer. A child renderer carries an eight-frame, 24 FPS pale-green energy loop with a tapered trail and flickering highlights. The effect is cosmetic: it does not convert damage to another element or enlarge the arrow's collision bounds. The nocked arrow is visibly green during windup; energy appears on the launched projectile. The icon has a fully opaque dark brown-purple background matching Simple Bow Shot.

Resources: `Skills/2HWeapon/Bow/PiercingShot/`. `Tools > Skills > Apply Piercing Bow Shot` applies the authored assets and requested balance. The first application clones the current basic recipe; subsequent applications preserve its copied timing and update the requested projectile flags. Name localization is provided in English and Russian.

PixelLab energy effect: https://pixellab.ai/create-vfx?effect=475cb5e9-ec82-4ddb-877b-7b5dfe1a3020

Energy export: `9a16ad01-e517-46a4-bbb6-4e2588597cf2`; editable step: `210d6dc8-855a-48db-ba70-00832492aa1b`. Icon job: `6d7515ba-cf8b-41cf-87d2-82894b0484e2`; background revision: `0ded6479-182a-425b-be60-f2d00ec0c2f6`.

Validation: PiercingBowShotTests covers costs, cooldown, secondary pool, preserved timing, actual launch with high additional-projectile/fork/chain stats, repeated piercing with no pierce budget, mirrored release origin and core-only collision. BowDrawTests protects the basic shot. `BowShotPreview.CapturePiercingRuntimeViews` captures the runtime draw and launch in both directions and samples the animated projectile's travel in Play Mode; PNGs are written to `%TEMP%/codex-piercing-bow-shot`. This is an isolated visual check, not a full dungeon combat cast.
