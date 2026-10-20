# Lightning Rod

Two-handed lightning staff special: 15-second cooldown, 25 mana. The first rod is placed 0.65 m ahead at a fixed height above the caster's feet; the second is initially 6 m farther ahead. Area of Effect scales that gap only, snapshotted on deployment. Solid walls shorten placement. Electrodes remain fixed when the caster moves or changes weapons; disabling the owner, death or changing the active character removes the field. Recasting replaces the previous pair.

Eight alternating travelling passes, 0.45 seconds of travel and 0.75 seconds between starts (5.7 seconds total). Each target can be hit once per pass, including targets with several child hurtboxes. Each hit uses 60% weapon damage and converts physical, fire and cold damage to lightning. Normal critical-hit, shock and other applicable hit rules remain active. Fixed pass timing does not scale with cast or projectile speed. A newly closed solid obstacle blocks a travelling discharge without moving the rods.

## PixelLab sources

- Icon: job `c1a9c091-888b-4afd-998f-3e7d562b6e52`, gallery `6ca6f47b-acc3-5f93-932d-8acbb11dac75`, 32×32.
- Electrode: revised job `dd3e9b07-f4c5-4d5a-adf3-2954d3478917`, gallery `8bf25e2f-7578-54ed-9a16-2968148d22da`, transparent 32×48. Imported at 48 pixels per unit with a terminal pivot.
- Filament: [PixelLab effect](https://pixellab.ai/create-vfx?effect=52bc9e1f-4e8a-44fc-9e37-6d2092f55ceb), export `d6e7589b-3a11-4d6e-a002-896f7753cd4f`. Eight 64×16 frames at 20 fps. Edge contact is intentional for tiling, with matching left/right heights. Original export JSON is included.
- Runtime tiles the filament without stretching pixel size and advances its leading edge between the stationary rods. All renderers use the existing attack-VFX opacity and world sorting settings.

Gameplay defaults are in Step_LightningRod.asset and may be overridden in Recipe_LightningRod.asset. The skill is registered in the lightning staff secondary pool and item database, with English and Russian labels and automatic descriptions.
