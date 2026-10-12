# Ground Shake visual refresh

Ground Shake uses a travelling, looping projectile rather than a one-shot slash.
Its eight-frame animation depicts a projectile ploughing through the ground.
The leading edge breaks stone while textured brown soil lifts up and backward
into the supplied reversed-wave silhouette. Heavy clods and smaller fragments
are thrown from the front, and churned dust follows behind. Solid earth and
fractured stone replace the hollow, bright curls that read as wind.
Every layer moves through morphing geometry, scrolling soil texture, tumbling
slabs or ballistic debris; the base remains attached to the ground.

GroundProjectileDebris additionally emits real world-space particles at surface
contact points, spaced 0.4 world units apart along the projectile's movement.
Six shaded pixel clods per burst fly backward and upward, fall under gravity,
bounce against Ground/OneWayPlatform colliders, and fade over 0.45–0.8 seconds.
The independent, bounded 256-particle system stays behind the travelling wave.
Stopping/pooling the projectile stops emission while existing debris finishes;
reactivation creates a fresh cloud. Airborne/contact-free positions do not emit.
GroundShakeDebris.mat and GroundShakeDebrisTexture.asset supply an unlit,
Point-filtered four-pixel clod silhouette. All debris settings are serialized.
Frames use 31×28 pixels, 24 PPU, a centered pivot, and the original
approximately 0.333-second loop duration
(eight frames at 24 FPS instead of four at 12 FPS).

The existing GroundShakeAnimation clip and GroundShakeVFXPrefub prefab reference
the new GroundShakeRefined-Sheet.png. Original artwork and asset GUIDs remain
available. Projectile speed, hit radius and damage are unchanged. The wider
31-pixel axis still determines the circular collision radius despite the taller
debris frames. Ground Shake's recipe explicitly sets GroundYOffset to zero;
the artwork reaches the bottom row in every frame to remove the floating gap.
Other ground projectiles retain their existing offset. FullRect meshes keep
visual bounds consistent for ground snapping.

Authored and reviewed through PixelLab MCP VFX Workbench. Final step:
`c398182e-f545-4fc9-92e7-ffc34a52070e`; export:
`441418d5-f151-4b8a-a3e7-63717cef89cd`.
[Editable PixelLab effect](https://pixellab.ai/create-vfx?effect=130d6472-ea4a-4bd3-b01a-12397acc7b0e).

Tools → Skills → Apply Refined Ground Shake VFX rebuilds slicing and wiring.
GroundShakeVisualBuilder.CaptureRuntimeViews renders isolated 480×270 URP views
in Play Mode. It initializes actual SkillProjectile instances without pooling,
checks facing, samples two animation cycles, moves them at the recipe's base
speed, and calls runtime ground snapping against a visible temporary floor. This checks
visuals and ground alignment independently of a full player skill cast.
It also simulates the world-space particle systems, deactivates the projectiles,
checks their clouds survive, and captures eight more frames of falling debris.

Validation: Unity compilation and both GroundShakeVisualTests passed, verifying
zero ground clearance and preservation of the original collision radius.
All three GroundProjectileDebrisTests passed, covering airborne suppression,
world-space persistence/backward launch, and fresh emission after pooled reuse.
The Play Mode capture passed mirrored facing, loop duration and strict ground
alignment checks across all frames, and rendered both directions successfully.
No console errors were reported. A full player combat cast was not exercised.
