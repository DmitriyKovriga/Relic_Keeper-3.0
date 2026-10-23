# Double Discharge

Lightning staff primary skill. Zero cooldown, zero mana. Two instant forward discharges at 15% and 35% of a one-second base cast; Spell Speed scales the full cast. Each deals 100% weapon damage. Range is 8 units, width .35 units before Area of Effect; solid terrain clips each discharge separately. Compound hurtboxes receive one hit per discharge. Interrupting the cast forfeits remaining hits.

- First hit: +50 percentage points Shock Chance (flat), and 50% increased Shock Application Effectiveness (additive increase, not a separate more multiplier).
- Second hit: +50 percentage points Critical Strike Multiplier (flat); it still needs to roll a critical hit.
- All bonuses use existing step-scoped modifiers; owner stats, other skills, and the other discharge do not inherit them. Existing weapon damage channels are retained.

`DoubleDischargeBuilder.Apply` creates the recipe, authored beam prefab, material, primary pool/database entries, and English/Russian localization. Automatic descriptions identify the first and second hit separately. DPS preview includes both hits with their own critical modifiers. No new save fields, stats, or affixes are needed.

## PixelLab artwork

- Opaque 64x64 icon: job `629f2a6d-bcde-4109-a2cc-45ffc16ca164`, gallery asset `516feb90-db19-55b2-8aa8-0fee29485446`.
- Animated filament: [PixelLab editable effect](https://pixellab.ai/create-vfx?effect=6c34d3e0-738b-44db-8013-d39343f737f9), export `eeb6bc65-07d6-4c5e-b332-55b0439aca50`; eight 160x48 frames. PixelLab JSON preserves frame layout. Unity plays each flash over .16 seconds.
- Point filtering, uncompressed sprites, no mipmaps, 24 pixels per unit. Material retains the URP 2D unlit shader in builds. The optional authored-beam path leaves existing procedural Electric Beam visuals intact.

Regression fixture: `RelicKeeper.Tests.EditMode.DoubleDischargeTests`. Existing beam, shock, description, and damage-preview fixtures cover the reused systems.

Verified in Unity on 2026-10-23: compilation succeeded; 55 focused EditMode cases passed across those five fixtures. A Play Mode dungeon capture verified the authored animation at the 480x270 reference, an eight-unit beam span, and a supported URP material. The temporary preview was removed and the Editor returned to Edit Mode.
