# Dodge cover feedback

Stationary ground dodges and all air dodges use a bright five-frame burst: immediate solid circle, opening crescent, separated ribbons, thinning streaks and drifting remnants. There is no buildup. Playback lasts at least 280 ms; the final frame fades to zero. Directional air dodges retain light body and weapon afterimages and no longer emit legacy black overlays.

The full sprite rectangle is 1.8 times collider height, reduced from 2.1 (about 14%). Ground and air attach at collider feet plus 45% of collider height, centered on the torso. Directional ground dash behavior is unchanged.

Built-in imagegen prompt: exactly five transparent frames in one horizontal strip; bold opaque ivory circular coverage immediately in frame one, then four distinct dissolution poses: opening bite, separated curved ribbons, thin outward drifting streaks, tiny curved remnants. Crisp broad pixels, minimal pale gray accents, no black, characters, floor or glow. Stable center and scale, simple readable evasive twist rather than detailed smoke or shield outline.

Source: Docs/Art/MovementVfx/StationaryDodgeSource.png. Runtime: Assets/Resources/VFX/Dodge/StationaryDodge-Sheet.png, five 40x40 sprites at 24 PPU. Reimport with DashJumpVfxBuilder.ImportStationaryDodge. The importer uses a shared square sampling window to preserve circular proportions and fragment positions, maps the palette and removes export speckles.

Validation: exact GroundDashVisualTests filter (13 cases), including stationary ground/air and directional air selection, immediate opaque coverage, five distinct silhouettes, torso anchor, reduced scale, final fade, legacy opacity compatibility and body/weapon afterimages. Controlled runtime previews use WarriorMovementPreview.CaptureStationaryDodgeRuntimeViews and CaptureAirDodgeRuntimeViews.
