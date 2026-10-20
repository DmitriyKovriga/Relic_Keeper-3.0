# Falling Anvil artwork

Created with PixelLab MCP for this skill.

- Icon: PixelLab job `427ee2c6-77af-470c-9ac3-f4112dcd0c0d`, gallery asset `9d2f76d4-243e-5baa-b45f-2f11e4cdc122`; 32×32 pixels.
- Impact: [PixelLab effect](https://pixellab.ai/create-vfx?effect=194c2c2a-b72d-49e4-9b7e-632f12445b9f), export `8dea1870-6d74-4c32-8cd3-429cfb930f45`.
- Twelve transparent 91×48 frames, four columns; origin at (45, 18) from the top left. JSON retains the original export metadata. Unity plays the impact at 16 fps, without looping.
- Cracks, stone fragments, dust and a brief contact spark. Runtime scales the effect with Area of Effect and applies the player's attack-VFX opacity setting.

Gameplay defaults: 250% weapon damage, 1.6 m radius, +25% multiplicative hit damage per metre fallen, capped at +100%. Fall height is measured from the airborne apex to the first landing; ground casts get no height bonus. Windup and recovery scale with attack action speed; airborne waiting does not. These values are authored in `Step_FallingAnvil.asset` and can be overridden in the recipe.
