# Jump takeoff clouds

GroundingVisualController subscribes to successful PlayerMovement.OnJumpStarted events and unsubscribes on disable. Normal ground jumps select a cloud at takeoff: horizontal speed above 0.15 units/sec determines direction; if slower, movement input above 0.1 supplies the direction. Without either, the jump uses the vertical cloud. Facing alone does not choose directional art. The source sheet leans left; rightward movement mirrors it so the plume follows the jump direction. Turning afterward does not change an existing world-space puff.

Directional JumpPuffSprites uses a broad ivory base and curved plume with soft asymmetrical lobes, stretching and separating into rounded remnants. VerticalJumpPuffSprites uses balanced base lobes and a central plume rising straight upward. Both use eight 24x24 frames at 24 PPU, point sampling, 320 ms duration, 90% opacity and width 1.25 relative to the collider. The previous observed grounded state supplies the takeoff contact because movement clears IsGrounded before delivering the event. Both sheets preserve a shared floor anchor across frames; later remnants may lift naturally.

Air-jump cloud art is unchanged: a larger 32x32 ring cushion below the feet, short downward jet, 480 ms duration and width 1.65 relative to the collider. Dash jumps retain their separate burst/trail and cancellation of the preceding dash cloud and scrape.

Generated with the built-in imagegen tool. Sources: Docs/Art/MovementVfx/JumpCloudSource.png and VerticalJumpCloudSource.png. Runtime: Assets/Resources/VFX/Grounds/JumpCloud-Sheet.png and VerticalJumpCloud-Sheet.png. Reimport methods: DashJumpVfxBuilder.ImportJumpCloud and ImportVerticalJumpCloud. The original symmetric procedural puff remains the missing-art fallback.

Directional prompt: eight frames in a transparent 4x2 sheet, medium-simple 24x24 pixel-art ivory cloud, wide grounded base, curved rising plume with 2-3 soft asymmetrical lobes, minimal pale shading, no dark outlines or internal rings; bloom, stretch, lift and separate into rounded remnants; fixed anchor, 65-75 percent peak cell occupancy.

Vertical prompt: eight frames in a transparent 4x2 sheet matching the directional cloud's style/scale, broad ground base and balanced side lobes, dominant central rounded plume rising straight up, no lateral lean; expand, stretch, lift and shrink into vertically drifting remnants; clean ivory/pale gray, crisp 24x24 pixels, fixed anchor and consistent scale.

Controlled Play Mode captures: WarriorMovementPreview.CaptureJumpPuffRuntimeViews and CaptureVerticalJumpPuffRuntimeViews. Regression filter: RelicKeeper.Tests.EditMode.JumpPuffTests checks selection/mirroring, horizontal dead zones, cancellation, event lifecycle and world-space effects. GIF/contact sheets live in Docs/Art/MovementVfx.
