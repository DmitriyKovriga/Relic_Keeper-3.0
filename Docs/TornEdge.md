# Torn Edge / Рваная кромка

Special skill shared by two-handed swords and axes. Registered once in each weapon's special pool and the item database. Cooldown 20 seconds, mana cost 25, direct damage 250% weapon damage. Uses Attack/Melee context and a slash weapon animation.

Slow execution: 70% normal attack speed, with impact at 55% of the pipeline and movement locked until recovery completes. One rectangular sweep hits distinct enemies in front once; base area is 3.2×2.5 metres, centred 1.7 metres forward and 0.4 metres above the owner. Normal Area of Effect scaling applies. No additional pushback.

The existing skill-only stat-link system adds 100% of current modified Crit Multiplier as flat Bleed Damage Multiplier. Example: 150 Crit Multiplier plus baseline 50 Bleed Damage Multiplier produces 200 for this skill. A non-critical 250 physical hit then inflicts 500 bleed damage per tick before other bleed modifiers. Critical hits also increase the physical base used by the normal bleed calculation. Source character stats and other skills are unchanged.

Bleed uses ordinary application chance, avoidance, physical-hit requirement, duration and stack rules. The skill does not guarantee bleeding or consume old wounds. The crit-to-bleed bonus applies even when the hit does not crit. This preserves the sword's critical-multiplier and axe's application-chance identities.

`TornEdgeBuilder.Apply` creates/imports the data and animation assets under `Assets/Resources/Skills/2HWeapon/TornEdge/`, including English/Russian localization. No new runtime step or parallel damage calculation is needed: the recipe reuses the standard rectangle damage, stat links and SpawnVFX pipeline.

## PixelLab art

- Icon: job `fcf04212-cc87-4e7a-9983-dd9b94ce01a3`, gallery `44cda806-768e-55a1-8789-a2eae6116769`; chipped steel edge with a crimson tear.
- Sweep: effect `c9b5d656-66c8-40dc-b1a1-1032e7ab1829`, step `c32739fe-43f4-410d-a688-3233d557132f`, export `6501f8ab-1db2-4f79-93f8-a125b5311875`. Eight 96×96 frames at 24 fps; silver cutting rim, crimson wake and blood flecks. Point filtering, 24 pixels per unit, no compression/mipmaps. Playback follows the cast's VFX timing and attack opacity.

## Verification

48 tests passed across TornEdgeTests, ExtractionTests and SkillDpsPreviewTests. Coverage includes both pools, cost/timing, mirrored front sweeps, multiple victims and compound hurtboxes, range exclusions, noncritical and critical bleed math, modified-source and target modifier layers, zero application chance, source-stat isolation and EN/RU descriptions.

Live Editor render at 480×270: the two front enemies took 250 damage and one bleed each; the distant and rear enemies took zero damage and no bleed. Sweep animation and localized icon/tooltips inspected; EN/RU tooltips fit at 135×111 and 135×118. Temporary preview helper removed and autosave restored.
