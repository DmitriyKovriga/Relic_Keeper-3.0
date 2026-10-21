# Near Fan / Ближний веер

Bow special in the existing bow secondary pool and item database. Cooldown: 10 seconds. Mana: 20. The accepted starting values are five simultaneous arrows, each dealing 100% weapon damage, across a 70° cone.

## Combat and scaling

Uses the normal bow stance, draw, release and recovery: release at 55% of the attack-speed-scaled sequence. The arrows share the drawn arrow's release point. Dodging before release cancels the volley. Once released, arrows use their normal independent lifetime.

Projectile Count adds floor(max(0, stat)) arrows inside the same 70° cone. The outer arrows remain at ±35°; extra arrows reduce angular spacing instead of widening the cone. An even arrow count is symmetric without a center arrow. A one-arrow configuration fires straight ahead. The optional `TotalConeAngle` recipe value enables this behavior; existing cones retain `ConeAnglePerProjectile` spacing, and parallel rows retain their existing offsets.

Each original arrow owns its hit history, allowing different arrows to hit one enemy. A single arrow cannot hit the same target again or duplicate damage through extra hurtboxes. At close range the whole volley can connect; distant narrow targets typically receive fewer arrows, while separated enemies can intercept different parts of the fan. Wall and floor collisions still stop arrows.

Projectile Speed, Pierce, Fork and Chain use their normal rules. No automatic pierce, homing or ricochet is granted. Base speed: 18. Lifetime: 3 seconds. Thin world/target probes (0.06/0.09 metres) are independent of the visual trail bounds. Damage previews retain the normal per-arrow value; five arrows hitting one target at 100 physical damage each deal 500 total before mitigation and other modifiers.

## Artwork and localization

PixelLab supplies the opaque 32×32 icon and four-frame looping 32×16 arrow animation at 20 fps. The arrow uses muted wood, steel and ivory feathers with restrained wind strokes. Point filtering, 24 PPU, uncompressed sprites, no mipmaps, and a custom pivot matching the PixelLab anchor (21,8). Existing bow animations supply the draw and release.

- Icon job: `8948f10c-f875-4317-9c6e-993aace331c6`; gallery: `daed6df3-54f7-59af-a176-7d74fe3d51f3`.
- Arrow effect: `66bc6c18-eba1-4ffc-9bb3-c37c50a246fc`; editable step: `c4634109-2775-426a-99f9-832e393f339e`; export: `df5401aa-c0e6-463f-a5ec-23c92fd2ce76`.
- `NearFanBuilder.Apply` rebuilds/registers the skill and EN/RU strings. `RefreshArt` rebuilds only the visuals. Both preserve existing GUIDs.

## Verification

NearFanTests cover registration/costs/art, count rounding, fixed fan density, close-range multi-hits, per-arrow hit history and compound hurtboxes, facing, far targets, multi-target coverage, legacy cone spacing, centered single arrows, ordinary projectile stats, cancellation and both locales.

Live Play Mode captures at 480×270 verified the bow draw/release and the five-arrow 70° volley. A nearby target received five hits with normal projectile collision and disposal. The temporary preview helper was removed and editor autosave/time/background settings restored.

Final validation: Unity compilation succeeded. All 75 checks passed with zero failures/skips across NearFanTests, BowDrawTests, PiercingBowShotTests, LuckyShotTests, SkillProjectileHitboxTests, ProjectileSpeedResolverTests, SkillDescriptionGeneratorTests, SkillDescriptionHighlightTests, SkillDpsPreviewTests and SkillIconBackgroundTests. The final console error query was empty.
