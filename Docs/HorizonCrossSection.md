# Horizon Cross-section / Сечение горизонта

Two-handed sword special in `2HSwordSkillRBPool`: 20-second cooldown, 25 mana, one wave dealing 300% weapon damage. The complete attack takes 1.25 seconds at Attack Speed 1; the first 0.75 seconds is a committed windup. Normal Attack Speed scaling applies to windup and recovery. Dodging before release cancels the wave; after release the wave continues independently.

The skill-local stat link adds 5 flat physical damage per 1 current Movement Speed before the 300% coefficient. This is continuous, including fractional values and movement-speed modifiers, and does not modify the character's other attacks. For example, 100 physical weapon damage and 5 Movement Speed gives (100 + 25) × 3 = 375 physical damage before ordinary combat modifiers. Other weapon damage channels retain the 300% coefficient. The link is editable through the existing skill StatLinks data.

The wave uses Attack + Projectile context, travels straight at base speed 11 for 1.4 seconds (roughly 15.4 metres), pierces enemies and hits each target once, including compound hurtboxes. Projectile Count, Fork and Chain cannot create additional waves. Projectile Speed applies normally. Walls stop the wave; the wide enemy probe checks line of sight so it cannot reach targets through a wall.

Area of Effect scales the sprite and enemy hit radius using the existing horizontal/full and vertical/half-strength scaling. The narrow 0.08-metre world probe remains separate from the 0.72-metre base enemy probe: the broad wave can travel above a floor instead of immediately colliding with it. Both the wide target probe and AoE scaling are opt-in launch data fields; existing projectiles keep their prior behavior. Pool reuse restores animation, area scale and target hit history.

## PixelLab artwork

| Asset | Source |
|---|---|
| Opaque 32×32 icon | job `8fd48edf-6c33-4dab-942f-8eef99a2f04e`, gallery `4a99a9a8-e4e0-502e-a0fd-1ab88965b3b0` |
| Flight: eight 48×48 frames, 24 fps | effect `9aaae5a6-4fe3-41e9-af41-eabf4c8cba34`, step `d4162152-38d1-4f9c-a79b-80586d567a36`, export `694b1c75-5240-4357-b399-403e4a02fd76` |
| Impact: eight 40×40 frames, 30 fps | effect `065c648b-0516-4396-a6dc-8345ccad5789`, step `cba249b6-1b74-4e97-8470-277e87ac85cf`, export `a1240a64-0c4a-4d4a-81d4-3bef29ae0b62` |

The pressure wave is an ivory/steel crescent with desaturated blue-grey wakes. Point-filtered sprites use 24 PPU, uncompressed FullRect meshes and no mipmaps. Unity centers the flight sprite pivot on the canvas so the enemy probe aligns with the visible wave rather than the effect author's construction origin. `WeaponWaveVisual` resets on launch, animates the flight and plays the short impact cut per successful hit. All shipped icon and effect art was created through PixelLab.

`HorizonCrossSectionBuilder.Apply` imports art, builds the prefabs/recipe, registers the skill and updates EN/RU name and description entries without changing existing asset GUIDs.

## Verification

HorizonCrossSectionTests cover exact configuration, opaque icon, registration, continuous Movement Speed physical scaling, damage preview parity, retained elemental damage, extra-projectile suppression, piercing/deduplication, AoE target reach, separate wall probe, floor clearance, wall occlusion, pool reuse, cancellation and both locales. Regression checks also cover Mana Spear, Lucky Shot, generic AoE/preview behavior and opaque skill icons.

Live Play Mode verification passed with the real recipe: no early wave during windup, one projectile despite +20 Projectile Count, three enemies hit exactly once, and a fourth enemy behind a wall untouched. Preview source and captures remain in the ignored bridge preview folder; the temporary Editor helper was removed, play mode stopped, and autosave/background/time settings restored.

The combined run passed 55 checks and failed three existing ManaSpearTests. The committed Mana Spear recipe already has InfinitePierce enabled, while those tests expect finite piercing; this feature does not change that asset or its setting.
