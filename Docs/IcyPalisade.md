# Icy Palisade / Ледяной частокол

Cold staff special: 10-second cooldown, 25 mana. Three groups erupt at 30%, 45%, and 60% of the spell-speed pipeline (about 0.15 seconds apart at a one-second cast). Each group deals 250% weapon damage converted entirely to cold.

Each complete point of MaxMysticShield capacity grants 20% more spell damage for this skill. The capacity bonuses add together, independently multiplying other increased spell damage: capacity 3 gives 400% weapon damage per group before other modifiers. Remaining charges do not affect damage and are never consumed. The bonus is a private stat link and does not change the character's stats or other skills.

Groups are centered 1.1, 2.4, and 3.7 units ahead in the cast direction. Each contains three spikes, spaced 0.55 units apart, with a base damage box of 0.6×1.65 units per spike. A target can be hit once per group, even when hurtboxes or spikes overlap; a later group may hit it again. Area of Effect scales spike width and height using the standard full horizontal/half vertical contribution, keeping terrain sample locations fixed.

Each spike independently probes solid world terrain and OneWayPlatform surfaces relative to the caster's feet. Upward surfaces with normal.y ≥ 0.55 are accepted, within 0.75 units upward and 2 downward. Trigger colliders, unsupported gaps, steep faces, and columns starting inside tall walls do not produce spikes. Separate probes accommodate steps and slopes without floating an entire group over a gap. The art is anchored at the ground; all spawned effects are temporary and contain no colliders, so no walls or movement obstacles are created.

Uses normal damage, crit, freeze, and ailment rules. Registered in the cold staff special pool and item database. English/Russian names and generated descriptions are included. Rebuild via `RelicKeeper.Editor.Skills.IcyPalisadeBuilder.Apply`, preserving existing GUIDs; `RefreshArt` rebuilds only the art assets.

## PixelLab art

- Opaque 32×32 icon: job `ed6a39af-89a4-4025-8db6-320279d9c732`, gallery `aac8adda-8341-56dd-95bd-d7c16b58f3d4`.
- Spike rise/shatter: effect `42d3d7fd-69f5-4df9-9931-cfed7c9dbc7b`, editable step `20520ffc-81eb-4ed5-b70b-2b9d66b21e19`, export `3d0b6c14-e063-45ed-a039-866e78e33156`.
- Twelve 40×56 frames at 30 fps, one-shot, origin (20,48), 24 pixels per unit. Blue facets with a pale leading edge, a ground crack, and shattering chips. Point filtering, no compression/mipmaps. Each spike disappears after 0.4 seconds.

## Validation

Eighteen skill-specific EditMode cases cover capacity scaling, unchanged remaining charges, multiplicative spell scaling and aggregate preview, group ordering/facing, compound hurtboxes and per-group hit limits, independently stepped ground, gaps/tall walls, slopes/one-way platforms, Area of Effect, registration/costs, opaque icon, collider-free VFX, and English/Russian descriptions.

Final Unity compilation succeeded. All 78 selected EditMode tests passed with zero failures or skips, including the related cold skills, area scaling, damage previews, descriptions/highlighting, and icon backgrounds. The Editor error log was empty.

A live Play Mode preview at 480×270 confirmed three sequential groups with nine independently grounded visual spikes on stepped terrain, 400 cold damage per target at capacity 3, preserved shield charges, and no VFX colliders. Temporary preview code was removed and the prior autosave setting restored.
