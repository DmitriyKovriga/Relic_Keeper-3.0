# The Weight of a Giant / Вес великана

Two-handed hammer special, registered in `2HHammerSkillPoolRKM` and the item database. Cooldown: 15 seconds. Mana: 35. One impact deals 500% weapon damage. Every complete 75 current Max Health adds 5 flat physical damage before damage increases, conversion, critical hits and the skill's 500% effectiveness. Current remaining health does not affect this bonus; modified Max Health counts. The link is scoped to this skill and never changes the player's global physical damage.

Example with 100 physical weapon damage and 750 Max Health: (100 + 50) × 5 = 750 physical damage before mitigation and other modifiers. Other existing weapon damage channels remain intact.

## Timing and area

At Attack Speed 1 the sequence lasts 2.5 seconds (`SkillSpeedMultiplier = 0.4`). The shoulder/overhead animation winds up until 70% (1.75 seconds), then strikes once and recovers. Movement is locked until 92% (2.3 seconds); cancellation releases the lock. Dodging before impact forfeits the hit. No invulnerability, damage reduction or stun immunity is granted. Attack Speed scales the whole sequence.

The impact uses the existing radial damage step with radius 1.35, vertical axis multiplier 0.65, center 1.2 metres forward and 0.15 metres down. The ellipse rejects box corners, deduplicates multiple hurtboxes and mirrors with facing. Area of Effect enlarges both axes. Each affected enemy receives one normal weapon hit, using normal crit, stun and ailment rules. Four hit-stop frames reinforce the impact.

The PixelLab animation uses an ivory/steel downward wake, compact contact flash, low stone pressure traces and falling chips. It has 12 transparent 80×56 frames at 24 fps. The icon has a fully opaque dark background. Assets use Point filtering, 24 PPU, no compression/mipmaps, and a non-looping Animator synchronized to recipe playback. There is no independent VFX damage pulse.

## PixelLab provenance

- Icon job: `17129e1b-cb42-49dd-856d-1b13a0407d61`; gallery: `68ed7118-e351-56f6-abd6-82aefbd7f7aa`.
- Effect: `3107381e-1609-4bd9-ba60-799d7e908189`; editable step: `c5ad3390-b3fd-4822-a773-8da105491c7d`; export: `acd7a396-8a1c-4a23-a745-391d3279fbb8`.
- `WeightOfAGiantBuilder.Apply` creates and registers the skill and both locales. `RefreshArt` rebuilds only its visuals. Existing GUIDs are preserved on rebuild.

## Verification

The 14 focused EditMode cases cover costs and registration, opaque icon, health breakpoints, modified maximum versus current health, critical hits, runtime/preview parity, facing, multiple hurtboxes, ellipse corners, area expansion, attack-speed timing, cancellation releasing the movement lock, and English/Russian descriptions.

Final checks: Unity compilation succeeded. All 57 relevant EditMode checks passed with zero failures or skips (new skill, descriptions/highlights, DPS previews, AoE, icon backgrounds, Thunder Hammer regression). Live Play Mode captures at 480×270 verified the planted wind-up and impact placement. Two front targets received one hit each, with damage inside the equipped weapon's normal roll range around the 750 average preview; behind/distant targets were untouched. The temporary preview helper was removed, Play Mode stopped, and autosave, time scale and background settings restored.
