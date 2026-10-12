# Tremor visual refresh

Tremor now breaks the ground from the centre outward: stone slabs lift, surrounding smaller ruptures follow, and dust settles. A single ground-contact burst throws physical pixel clods away from the impact centre. These particles simulate in world space, collide with Ground/OneWayPlatform and finish after the effect disappears.

## Assets and behavior

- PixelLab effect: https://pixellab.ai/create-vfx?effect=9b38639a-61ec-4c08-9a18-a0e44742615a
- Source export image ID: f8e3ca31-6989-43f2-a32b-acf50927022d; editable step: 53890363-c3ec-4662-a576-dc4dd5375908.
- `TremorRefined-Sheet.png`: thirteen 85 x 19 poses, 24 PPU, FullRect, point filtering, no compression/mipmaps.
- Existing clip, animator controller, prefab and recipe identities remain. Native one-shot duration remains 7/12 seconds; runtime still fits playback to the skill pipeline.
- Recipe impact window remains 0.05-0.75. GroundYOffset is -2/24 world units (two native pixels): one compensates for the transparent bottom row in the opening poses, and one overlaps the floor edge to prevent a visible seam.
- `GroundProjectileDebris` has an opt-in stationary-impact mode. Ground Shake retains the default travelling mode.
- Damage, stun, mana, cooldown and effect footprint are unchanged.

## Tools and validation

`Tools > Skills > Apply Refined Tremor VFX` reapplies import settings, clip keys and prefab references. The builder uses Ground Shake's shared pixel-clod material.

In Play Mode, `RelicKeeper.Editor.VFX.TremorVisualBuilder.CaptureRuntimeViews` captures both directions against an isolated floor using runtime SkillVFX playback, SkillStepRunner ground snapping and actual particle simulation. It writes frames to `%TEMP%/codex-tremor-refresh`. This is a focused runtime preview; a normal dungeon combat cast is still the final art judgment.

Targeted tests: TremorVisualTests (2), GroundProjectileDebrisTests (4), GroundShakeVisualTests (2). They verify stable footprint and clip duration, grounded recipe timing, single stationary bursts with outward velocities, travelling debris and reactivation.
