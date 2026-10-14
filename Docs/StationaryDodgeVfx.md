# Dodge cover feedback

Stationary ground dodges and all air dodges use a bright five-frame burst: immediate solid circle, opening crescent, separated ribbons, thinning streaks and drifting remnants. There is no buildup. Playback lasts at least 280 ms; the final frame fades to zero. Directional air dodges retain light body and weapon afterimages and no longer emit legacy black overlays.

The full sprite rectangle is 1.8 times collider height, reduced from 2.1 (about 14%). Ground and air attach at collider feet plus 45% of collider height, centered on the torso. Directional ground dash behavior is unchanged.

Built-in imagegen prompt: exactly five transparent frames in one horizontal strip; bold opaque ivory circular coverage immediately in frame one, then four distinct dissolution poses: opening bite, separated curved ribbons, thin outward drifting streaks, tiny curved remnants. Crisp broad pixels, minimal pale gray accents, no black, characters, floor or glow. Stable center and scale, simple readable evasive twist rather than detailed smoke or shield outline.

Source: Docs/Art/MovementVfx/StationaryDodgeSource.png. Runtime: Assets/Resources/VFX/Dodge/StationaryDodge-Sheet.png, five 40x40 sprites at 24 PPU. Reimport with DashJumpVfxBuilder.ImportStationaryDodge. The importer uses a shared square sampling window to preserve circular proportions and fragment positions, maps the palette and removes export speckles.

Validation: exact GroundDashVisualTests filter (13 cases), including stationary ground/air and directional air selection, immediate opaque coverage, five distinct silhouettes, torso anchor, reduced scale, final fade, legacy opacity compatibility and body/weapon afterimages. Controlled runtime previews use WarriorMovementPreview.CaptureStationaryDodgeRuntimeViews and CaptureAirDodgeRuntimeViews.

## Neutral dodge hero motion

Neutral ground and air dodges use PixelLab MCP sprite animation for warrior, rogue and mage. Nine frames (the unchanged input plus eight generated poses) play once over the existing dodge duration, normally 240 ms. No procedural spin or tuck scaling is applied. The body center stays fixed across the padded cells; pose-specific feet alignment would incorrectly bounce the hero around.

Held-weapon SpriteRenderers are hidden only while rendering a neutral dodge, without disabling equipment components or moving gameplay sockets. Their original forceRenderingOff flags are restored after each camera render, on cancellation and on disable. Directional dodge weapon visibility stays unchanged.

Data: CharacterDataSO.NeutralDodgeAnimation. Import: NeutralDodgeVisualBuilder.Apply. Source frames and MCP job IDs: Docs/Art/MovementVfx/NeutralDodge. Native runtime sheets are 40x40 per frame for warrior/rogue, 56x56 for mage, 24 PPU, point filtering and no texture compression. Padding preserves native character size rather than enlarging it. Body pose and equipment visibility restore when neutral dodge finishes; regular jump/run/fall animations continue afterward.

Verified final clips: compilation successful; NeutralDodgeAnimationTests 6/6, PlayerRenderAnchorTests 15/15, GroundDashVisualTests 13/13, WarriorMovementAnimationTests 14/14. Controlled Play Mode previews checked all three heroes on ground and in air. No console errors.
