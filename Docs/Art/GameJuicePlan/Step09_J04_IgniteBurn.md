# J04 first slice — burning enemies

Implemented 2026-10-30 at the user's request for a shader-based Ignite with light
and glow. This is the Ignite presentation slice, not completion of all J04 chains.

Refined after two visual reviews: the first treatment looked like ground fire;
its silhouette-based replacement read as an aura. The current shader finds
surface sites on the animated sprite and emits separate tapered flame tongues.
Each tongue grows, curls upward and fades on a staggered cycle, with a bright
inner core and red-orange edge. The larger flames stay behind the body; smaller
foreground flames cross the torso/arms and fade before covering the face.
Continuous coordinates replace the previous snapped procedural motion.
The first-pass and BodyBurn evidence folders retain those earlier treatments;
links below point to the current refinement.

The latest refinement smooths the spurts and gives illumination a visible breath.
Two overlapping smooth flame lives per site replace the dark interval between
spurts. Emission positions/presence blend for 0.08 seconds when sprite frames
change, and tip antialiasing is calculated outside divergent flame branches.
Ignite's point light and glow share a slower, stronger brightness pulse, with
subtle radius variation. Reduced effects halves this extra breathing depth.
Existing torch sources retain their previous motion because extra breath defaults
to zero. The older FlameMotion folder preserves the pre-breathing version;
current evidence is in FlameBreath.

The old three small flame sprites and direct body-color writes are replaced by
an animated heat overlay masked to the enemy's current sprite, rising procedural
fire and a small warm glow. The body overlay follows frame changes, mirroring,
sorting and visibility. The source sprite's material, color and hit-flash
property block remain untouched. Shock excludes the owned fire/glow hierarchy
from its color cache. Freeze still owns the normal body tint.

Every burning enemy owns one small flickering local light, using the existing
URP 2D surface-light and terrain-shadow system. Its warm source tint remains warm
under a cold room profile; ordinary torch lights still use the environment tint.
Local-light and source-glow profile switches remain authoritative. Refreshing
Ignite or adding stacks reuses the same visual and light. Actual Ignite spread
activates the same effect on the recipient, with no new damage or spread rules.

Ending Ignite, disabling the owner or hiding its sprite removes the presentation.
A dead owner cannot relight its corpse even if ailment stacks remain briefly.
Reduced effects lowers body emission, fire opacity/height, light and glow.
No popup, new status icon, camera effect or extra hit response was added.

## Preview and tuning

- [Sampled animation preview](J04/Ignite/FlameBreath/IgnitePreview.gif): crop of actual native
  frames, enlarged exactly 3x with nearest-neighbor sampling. It uses enemy walk
  frames and a forced direction flip, with no AI or combat simulation. The actor's
  walk frames advance every four samples; the fire advances on every sample.
- [Native comparison](J04/Ignite/FlameBreath/IgniteComparison.png): 480x270 panels showing
  refined burning, Mold, reduced effects and ten burning actors.
- [Low/high breathing samples](J04/Ignite/FlameBreath/BreathingComparison.png).
  These are separate controlled moments, not identical animation frames.

The new preview uses 96 actual captures requested at roughly 30 samples/second,
instead of the previous 12 captures requested around 8 samples/second. Actual
sampling intervals and game time are recorded in
[AnimationTimes.csv](J04/Ignite/FlameBreath/AnimationTimes.csv); measured capture
rate, GIF timing and light/glow/radius ranges are in
[PreviewTiming.json](J04/Ignite/FlameBreath/PreviewTiming.json).
Playback durations follow recorded capture timing. This is a preview sampling
measurement, not a gameplay performance result or a promised runtime frame rate.

For per-enemy tuning, add **Ignite Visual Controller** to the enemy prefab; the
ailment system reuses it. Its Inspector exposes **Strength**, **Flame Height**,
**Light Strength**, **Light Radius** and **Glow Strength**. The component can be
present while the enemy is not burning. Enemies without it gain the default
component automatically on their first Ignite. Editing a runtime component in
Play Mode is temporary; edit the prefab to retain custom values.
**Light Breath** controls pulse depth and **Breath Speed** controls its pace.

## Verification

Unity compilation succeeded. **29 focused EditMode tests passed, 0 failed/skipped**:
IgniteBurnVisualTests, IgniteSpreadTests, LocalLightingTests and SourceGlowTests.
Coverage includes source-renderer ownership, sprite/flip/sorting/visibility,
refresh reuse, no added colliders, reduced settings, warm tint, shock coexistence,
dead-corpse cleanup, Resources shader inclusion and existing Ignite spread/math.
The refinement also verifies rear-fire sorting, current-sprite alpha/atlas
mapping and alignment under mirroring with an off-center pivot. The procedural
shader requires local coordinates, so dynamic batching is disabled for this
effect. Body materials remain untouched.
The added cases check smooth emission transitions, a bounded continuous light
breath, quieter reduced variation and identity behavior for zero-depth sources.

The live check uses real Mortfall Room 001 geometry in an isolated preview scene,
actual zombie animation sprites plus Mold/Knight silhouettes, and disposable
presentation-only actors. The preview scene's presentation is explicitly advanced
before captures because ordinary actor updates are not dispatched there. It checks
supported rendering with no magenta, frame/flip tracking, a group of ten with
one light each, reduced mode, refresh and extinguishing. The initial additive
blend was too bright in groups; the final treatment preserves the body's color
with alpha-blended fire and lower illumination. These are controlled visual checks,
not an ordinary fight or performance measurement. No fresh standalone build was
made; the shader is referenced by a Resources material for build inclusion.

The paired [light on](J04/Ignite/FlameBreath/Light-on.png) and
[light off](J04/Ignite/FlameBreath/Light-off.png) captures keep fire and the animation frame
unchanged and disable the halo in both. A pixel comparison verifies increased
illumination on nearby terrain outside the actor/fire region. See the completed
[runtime report](J04/Ignite/FlameBreath/RuntimeReport.txt).

No scenes, prefabs, combat data or project settings were edited. The first-pass
save check recorded `5b78bc2758c62579cbcc8d0d776c39fb2da58602b319ab101dee81acbb95b87e`.
After the user's ordinary play, the refinement's final check found
`814554b71116ccf0dc1b02e5ef1c078098ed85c0b3b5459694c743e0e616ed7f`;
its last write was 01:42:10 local time, before the refinement's native checks.
The save was not restored or replaced.
The subsequent breathing check matched before/after save hash
`d91a39da4baa1798d744651510d149d83190421701aad224dd1102dfd2da321f`.
The local code graph was refreshed; its preprocessor parsing/namespace collision
warnings are discovery limits and do not replace Unity compilation.

## Test by hand

1. Ignite one enemy near a stone wall/platform. Fire should follow its animation
   and a small warm pool of light should reach nearby stone.
2. Let Ignite spread to another enemy. It should gain the same burning effect.
   Repeated applications must not multiply fire objects or lights.
3. Watch several burning enemies together and attack them. Check that outlines,
   poses and hit feedback remain readable. Try simultaneous shock or freeze.
   Flame spurts should crossfade without blinking out as an animation frame
   changes; the warm surface light and glow should brighten/dim gradually.
4. Let Ignite expire or kill the enemy. Fire and illumination should stop.
5. Repeat with reduced combat effects enabled. It should remain recognizable
   with less fire, glow and light.

Review this look in ordinary combat before extending J04 to shock or shield cues.
