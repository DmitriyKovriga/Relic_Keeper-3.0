# Jump takeoff puff

The shared GroundingVisualController subscribes to the successful PlayerMovement.OnJumpStarted event. Subscription is removed on disable. No input polling, landing callback or enemy update creates a jump puff.

Ground takeoff uses the previous observed grounded state because PlayerMovement has cleared its current state before raising the event. Ground puffs start at the contact surface. Air jumps create a larger 32x32 cloud cushion below the collider feet, opening into a ring with a short downward jet. It stays at the takeoff point, never at a floor raycast below. Ground puffs last 320 ms; air puffs last 480 ms with 90% opacity and width 1.65 relative to the body. The top of the cushion aligns with the feet to suggest pushing off it.

JumpPuffSprites creates eight cached 24x24 frames at 24 PPU with point sampling. Lobes expand, rise, split and fade; no external art dependency is needed. Runtime rendering reuses GroundingSpriteSheetVfx and existing gameplay VFX sorting. The serialized enable flag, duration, width and air opacity are exposed under GroundingVisualController / Dust.

Controlled Play Mode preview: RelicKeeper.Editor.VFX.WarriorMovementPreview.CaptureJumpPuffRuntimeViews. Preview GIF and contact sheet: Docs/Art/MovementVfx. Regression filter: RelicKeeper.Tests.EditMode.JumpPuffTests; existing shadow checks: GroundShadowTests.
