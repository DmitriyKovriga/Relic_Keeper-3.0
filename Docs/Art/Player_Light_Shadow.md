# Player light shadow — V18 player pilot

The player partially blocks nearby torch/forge light. Existing terrain shadows
and the small contact shadow beneath the feet remain in place. Enemies do not
cast this player-specific shadow.

## Appearance and movement

The floor shadow now removes light from the source itself. The old elongated
oval overlay is no longer rendered: it darkened ambient color as well as the
flame, which made it look painted onto the platform. Ambient floor detail and
illumination from other sources now survive inside a cast shadow.

A soft projection begins at the feet and extends away from each visible nearby
source. Its length depends on lateral distance and the height of the light above
the supporting surface. Moving away from an elevated source lengthens the
projection; raising the source shortens it. Crossing the flame collapses its
length to zero before changing direction. The tip fades and broadens rather than
revealing a full-size oval. Source attenuation naturally fades it at a distance.

This is a stylized side-view projection, not a full 3D character silhouette.
A torch is often below the character's head, where a literal head-height ray
would have infinite length. The lower body supplies a bounded effective caster
height, and a smooth length cap prevents an enormous shadow. Position-based
curves keep the result independent of frame rate and camera render frequency.

## Editing

Open **World Appearance > Lighting > Player light shadow** and choose Hub or
Mortfall. Existing Save, Compare and Discard controls apply to these settings.

- **Enable player shadow** enables the effect for this location.
- **Light blocked (%)** controls the source's wall/body shadow, from 0–100%.
- **Shadow reach** limits that wall shadow to 0.1–30 world units.
- **Shadow width / height (%)** shape the stable body caster. Height stays
  anchored at the feet. These do not change the actual body-lighting exemption.
- **Edge softness (%)** adjusts that caster's edge, from 0–200%.
- **Ground light blocked (%)** controls how much of each source is removed from
  the floor projection. It cannot remove ambient color, even at 100%.
- **Maximum ground length** caps the floor projection at 0–8 world units.
- **Ground shadow thickness** sets the shallow receiving strip below the surface,
  from 0.03–1 world units. Around 0.15–0.3 suits ordinary platform tops.
- **Light height response (%)** controls sensitivity to the light's elevation.
  Zero ignores elevation while retaining directional growth from the feet.
- **Crossing fade distance** softens the transition while crossing the source.
- **Length growth softness** makes the tip extend more gradually at larger values.
- **Overlap darkness (%)** slightly darkens the player's body while it covers
  the visible source. This does not affect weapons, enemies, labels or scenery.

Saved profile values and serialized field names are retained. The old opacity,
approach and growth controls now describe light blocking, light-height response
and length softness; they no longer stretch an oval. No room or scene placement
changes are required. The room preview follows the same rendering behavior.

## Rendering and ownership

`PlayerMovement` adds `PlayerLightShadow` beside `GroundingVisualController`.
The stable body follows position, scale and jumps independently of weapon swings
and animation outlines. Ground support, normal and clipping come from grounding;
visual-only room previews use contiguous saved tile spans and terrain visibility
without adding gameplay colliders.

`PlayerShadowLight2D.shader` adapts the installed URP 17.3 Light2D shader. Its
point-light pass evaluates `PlayerLightShadow.hlsl` before combining sources.
The native body shadow and floor extension combine by maximum occlusion, avoiding
a double-dark seam where they meet. Native terrain shadows, cookies, attenuation,
normals and blending remain in place. Room fills and unrelated sources are excluded.
Up to eight nearby sources are evaluated independently: crossing between two
lights does not switch a single decal abruptly between them.

The floor extension only affects a shallow strip below the support plane, is
clipped to its supported span, requires a clear source-to-body path, and fades
with height above the ground. No overlay sprite, material instance, texture or
extra light is created. `GroundProjection` remains as a compatibility accessor
returning null. The old ground material remains available for existing references
but is no longer loaded by this component. No raster artwork changed.

Body overlap shading uses a receiver flag in the primary renderer's property
block, preserving values owned by other effects. Disabling restores the previous
flag. Source counts and overlap globals reset between cameras and on disable;
scene ownership and binding suspension isolate room previews from the live game.
Ceiling shaft artwork and source glow remain separate visual effects.

The global URP settings retain their existing custom light shader reference.
Review the adapted shader against upstream when upgrading URP.

## Verification

The focused checks cover real GPU floor darkening, ambient preservation beneath
an active shadow, opposing illumination, surface-only shading, platform clipping,
obstructed-source rejection, source crossing, jump fade, body shading, effect
property preservation and camera cleanup. Geometry checks cover continuous growth,
source elevation, mirrored direction and finite length near a low flame. Existing
contact-shadow and appearance-save regression suites are also included.

Unity compilation and all 67 relevant EditMode checks passed. Mortfall Room 001
was rendered with the saved profile at 480×270. A 41-position crossing with the
live hub player was captured in Play Mode using the saved Mortfall ground-shadow
values on a temporary profile. Position, velocity, camera and profile were then
restored, and Play Mode was exited. These are sampled poses, not a recording of
input-driven gameplay. No standalone build or device performance capture was run.
