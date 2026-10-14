# Dash jump identity and visuals

The launch cloud now uses a generated 12-frame cloud sheet within the existing 650 ms duration. Large rolling lobes shift outward and upward, open into hollow billows, then separate into drifting remnants. Import preserves a shared frame origin instead of grounding or recentering every fragment. Late frames receive a small additional outward/upward drift and a short final fade.

Art was generated with the built-in imagegen tool using the four user-provided motion references. Source: Docs/Art/MovementVfx/DashJumpCloudSource.png. Runtime sheet: Assets/Resources/VFX/Grounds/DashJumpCloud-Sheet.png. Reimport entry point: DashJumpVfxBuilder.ImportCloud. Prompt: 12 frames in a 4x3 transparent grid, a powerful white ground-anchored takeoff cloud leaning opposite launch, coherent rolling lobes shifting upward/outward, expanding hollow spaces, detached thinning wisps; crisp pixel art, fixed world anchor, no characters or background. Import maps the source to the movement VFX palette and filters colored export speckles.

Successful TryPerformDashJump now sets IsDashJump inside ApplyJumpForceCore before raising Jumped / OnJumpStarted. The existing two-argument ApplyJumpForce entry point remains and clears the flag for ordinary ground/air jumps. IsDashJump also returns false on the ground. Movement speeds, jump force and carry duration remain unchanged.

GroundingVisualController handles dash jump separately: a three-pronged upward launch burst with a broad floor-hugging base, 650 ms duration, 95% opacity and width 2.5 times the collider width. Converting a dash into a dash jump immediately removes that actor's preceding dash cloud and scrape; late dash notifications cannot respawn them. A coyote launch uses the feet instead of a distant floor. The regular ground-jump and air-jump clouds remain separate.

DashJumpTrail emits small white world-space streaks during the first 320 ms of the launch. Each is about 4.5 pixels long and one pixel thick, lasts 180 ms and fades behind the body. Emission stops on landing or a normal jump; existing streaks finish fading. Disabling the owner clears all streaks and releases its generated sprite. No trail objects follow the moving character after emission.

Controlled Play Mode capture: WarriorMovementPreview.CaptureDashJumpRuntimeViews. Tests: PlayerMovementTests checks jump identity during event delivery; JumpPuffTests checks exclusive cloud selection, world-space streaks and cancellation.
