# Retreating Thrust / Укол с отходом

Two-handed sword special skill. Cooldown: 5 seconds. Mana: 10.

## Initial tuning

- One 250% weapon-damage thrust in a 3.2-metre by 0.45-metre line ahead of the character. Each enemy is hit once, including compound hurtboxes. Solid terrain blocks the thrust.
- Immediately after damage resolves, a backward/upward physics impulse is applied. Base force is 8 at 150 degrees relative to forward (about 6.93 horizontal and 4 vertical at mass 1). Facing is preserved. A miss still retreats.
- Attack timing uses 1.5× attack speed: impact at 30% of the action, then recovery. The movement lock preserves the retreat impulse during recovery; completion/cancellation releases the lock. Faster action speed shortens the recovery commitment.
- Skill-only stat link: 1% of current Evasion is added as flat Crit Chance. Thus 100 Evasion contributes 1 base percentage point before the existing Crit Chance modifier layers. Evasion is not consumed, and other skills receive no bonus.
- Critical hits use the ordinary weapon/character Crit Multiplier. No additional critical-damage bonus is granted.
- Normal gravity, colliders, damage reception and wall collision remain active throughout the retreat. No dodge or invulnerability window is opened. The skill is usable in the air.

The 250% coefficient matches the existing Lunge Slash starting coefficient. Damage, range and conversion are initial tuning, while the requested cooldown and mana cost are fixed at 5 seconds and 10 mana.

## Integration

`RetreatingThrustBuilder.Apply` creates the repeatable data setup, registers the sword secondary pool and item database, and writes EN/RU localization. Assets live in `Assets/Resources/Skills/2HWeapon/Sword/Adventurer/RetreatingThrust/`.

Runtime uses the existing damage pipeline, hand-specific stats, skill-scoped stat links, player impulse movement and movement-lock cleanup. Automatic descriptions and the damage preview recognize the new step.

## PixelLab art

All new icon and VFX art is authored through PixelLab. Point filtering, 24 pixels per unit, no compression or mipmaps.

- Icon gallery: `42d78bcc-808e-5379-9268-87cbefac24e7`; job `7dd9ff23-35be-41aa-9a76-feff18618661`.
- Thrust effect: `a7d4f881-6350-4df4-a63f-fd5aec7b81e7`; step `78de2ce8-0c10-4f2d-90f0-45f0a3f18a03`; eight 96×32 frames at 30 fps.
- Retreat dust: `16b80914-3b41-4779-824e-5d04833ae0f2`; step `595b5e8d-ab5c-405e-bfe7-14b354087807`; eight 48×32 frames at 24 fps.

## Verification

`RetreatingThrustTests` covers narrow front-line targeting in both directions, wall/trigger blocking, compound colliders, coefficients, skill-only Evasion conversion and modifier layers, guaranteed critical hits and critical multiplier, immediate hop and preserved facing/gravity, missed thrusts, cancellation cleanup, no invulnerable dodge, physical rear-wall collision, registration, preview and EN/RU text.

Related regression filters: `SkillDpsPreviewTests` and `PlayerMovementTests`.

Live 480×270 review used the actual Mold prefab with aligned footing: the strike dealt 25 damage from a 10-damage test weapon, followed by a backward/upward hop and a stop at the rear wall. EN/RU tooltip bounds were 135×147 and 135×154.
