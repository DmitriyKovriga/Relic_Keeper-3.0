# Needle Prick / Игольный укол

Dagger primary skill. Cooldown: 0. Mana cost: 0.

## Initial tuning

- One 75% weapon-damage stab against the nearest enemy in front.
- Fixed 0.9-metre reach and 0.5-metre width. Area of Effect does not extend it; projectile stats do not multiply it. Walls block the strike.
- Attack speed multiplier: 2. Impact occurs at 20% of the action, followed by recovery. No hit stop or forced movement.
- This hit gains +100 flat percentage points of Crit Multiplier.
- A skill-only stat link adds 200% of current Crit Chance as flat Crit Multiplier: each 1% Crit Chance contributes 2 percentage points. It reads the hand-scoped, modified chance and does not change Crit Chance or buff other skills.
- The full Crit Chance value contributes, including chance above 100%. Critical probability itself follows the normal damage calculation. Existing Crit Multiplier modifier layers apply normally.

With 150% ordinary Crit Multiplier, Needle Prick has 250% at zero Crit Chance, 350% at 50% Crit Chance, and 450% at 100% Crit Chance. A 100-damage weapon deals 75 on a noncritical hit, or 337.5 on a critical hit at 100% Crit Chance. Base attack speed 1 yields two casts per second.

Damage, speed, range and crit tuning are initial values; the requested primary role and free/zero-cooldown use are preserved.

## Integration

`NeedlePrickBuilder.Apply` creates the repeatable setup, registers the dagger primary pool and item database, and writes EN/RU text. Assets live under `Assets/Resources/Skills/1HWeapon/Dagger/NeedlePrick/`.

Runtime uses the normal weapon damage, hand-specific stats, ailments and on-hit pipeline. The new step has no persistent target state, so each use can hit the same target again. Damage preview includes one hit per cast with the same expected crit formula.

## PixelLab art

All new art comes from PixelLab. Point filtering, 24 pixels per unit, no compression or mipmaps.

- Icon: close-up stiletto puncturing an armor gap, gallery `d3e9b798-16d1-5288-b9d5-1d7afccdd668`, job `6f72a7b4-c33b-4755-9e57-69b3f04a4680`.
- Stiletto thrust: effect `99facb3f-3ce9-43b6-af92-b4a0a7994e34`, step `af20908a-8078-40f8-8824-1d124554922e`; seven 48×32 frames at 42 fps, with a shaded physical blade, bronze guard and retraction. Playback follows attack timing.
- Normal puncture: effect `2a8d60b6-3f02-4282-854f-e21b54a14fde`, step `d390589b-e34b-48c8-b8b7-442c331a3866`; seven 24×24 frames at 40 fps.
- Critical puncture: effect `6265797f-a2c3-4338-9b54-16dea8073380`, step `65daefa1-b890-4953-85db-819a8ae9c1bd`; nine 32×32 frames at 40 fps. A distinct ivory-gold precision glint emphasizes critical hits.

Contact effects spawn at the selected hurtbox's near surface only when the target accepts the hit. The actual delivered damage snapshot selects normal/critical presentation; no second critical roll is made. `NeedlePrickBuilder.RefreshArt` updates visual assets/references while preserving authored gameplay data.

## Verification

`NeedlePrickTests` covers primary registration, zero cost/cooldown, one nearest target in both directions, compound hurtboxes, range and projectile exclusions, wall/trigger behavior, normal damage, linked crit coefficients and modifier layers, source-stat isolation, guaranteed and over-cap crits, preview/DPS agreement, attack-speed scaling, repeat use, no forced movement and both locales.

Related filters: `SkillDpsPreviewTests` and `RetreatingThrustTests`.

The combined focused run passed 48 tests with no failures or skips, including accepted/rejected contact presentation and delivery of the actual critical snapshot. Live 480×270 review used two actual Mold prefabs: a 10-damage test weapon dealt 7.5 at zero Crit Chance and 33.75 at 100% Crit Chance to the close target; the farther target took no damage. Normal and critical contact effects were reviewed separately. EN/RU tooltip bounds were 135×102 and 135×109.
