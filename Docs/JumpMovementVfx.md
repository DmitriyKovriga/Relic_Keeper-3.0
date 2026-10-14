# Jump takeoff puff

The shared GroundingVisualController subscribes to the successful PlayerMovement.OnJumpStarted event. Subscription is removed on disable. No input polling, landing callback or enemy update creates a jump puff.

Ground takeoff uses the previous observed grounded state because PlayerMovement has cleared its current state before raising the event. Ground puffs start at the contact surface. Air jumps create a smaller, softer puff at the collider feet, never at a floor raycast below. Both clouds remain at their origin and finish after 320 ms.

JumpPuffSprites creates eight cached 24x24 frames at 24 PPU with point sampling. Lobes expand, rise, split and fade; no external art dependency is needed. Runtime rendering reuses GroundingSpriteSheetVfx and existing gameplay VFX sorting. The serialized enable flag, duration, width and air opacity are exposed under GroundingVisualController / Dust.

Controlled Play Mode preview: RelicKeeper.Editor.VFX.WarriorMovementPreview.CaptureJumpPuffRuntimeViews. Preview GIF and contact sheet: Docs/Art/MovementVfx. Regression filter: RelicKeeper.Tests.EditMode.JumpPuffTests; existing shadow checks: GroundShadowTests.
