# Stationary dodge feedback

Stationary dodges use a large bright ivory evasive burst on both ground and air takeoff. PlayerAttackInput.SpawnDodgeFeedback selects it exclusively when _isStationaryDodge is true. Directional feedback and afterimages keep their existing behavior. Dodge movement, cooldowns and invulnerability are unchanged.

Eight 40x40 frames at 24 PPU: a closing crescent, solid-volume artwork across two peak frames, then opening crescents and thinning remnants. The importer removes isolated export speckles without cutting out the centre. Effect height stays 2.1 times collider height and playback is at least 400 ms. Opacity now uses a short steep S-curve flash instead of a constant translucent fade: zero on creation, rise to 85% by 12% of playback (48 ms), brief hold through 40% (160 ms), sharp quintic fall to zero by 60% (240 ms), then remain invisible. SpriteSheetOverlayVfx opacity pulses are opt-in; other overlays retain their prior opacity. The effect attaches to the actor outside the compressed weapon Visuals hierarchy. Both ground and air centre at collider feet plus 45% of collider height, anchoring the swirl to the torso.

GroundingVisualController suppresses legacy dash dust/scrape for IsStationaryDodging, preventing a dark secondary burst from layering over the new effect.

Built-in imagegen prompt: eight transparent frames in a 4x2 grid, much stronger white stationary-dodge burst inspired by the three user panda dodge references; broad closing crescent, fully opaque ivory spinning volume in frames 3 and 4 covering the entire hero, then huge peeling crescents and fading curved remnants. Organic hooked silhouette rather than a geometric shield, crisp native40x40 pixels, minimal pale shadow, no characters/ground/background. Peak shape fills 80-90% of the cell with consistent centre and scale; no transparent hole at the peak.

Generated source: Docs/Art/MovementVfx/StationaryDodgeSource.png. Runtime: Assets/Resources/VFX/Dodge/StationaryDodge-Sheet.png. Reimport: DashJumpVfxBuilder.ImportStationaryDodge. Previous stand_dodge-Sheet remains for directional legacy feedback.

Validation: GroundDashVisualTests (12 cases), including ground/air selection, single-effect emission, torso anchor, zero initial opacity, bright pulse peak, steep fall and early zero opacity, opt-in compatibility for legacy overlays, solid centre in the peak artwork, suppressed legacy dust, and body/weapon afterimage ownership. Controlled Play Mode preview: WarriorMovementPreview.CaptureStationaryDodgeRuntimeViews, ground left and air right.
