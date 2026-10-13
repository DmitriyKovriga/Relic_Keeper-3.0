# Warrior movement animation refresh

Warrior now uses new sprites under `Heroes/WarriorResources/Refined`. The original run, jump, fall sheets and portrait remain available. Character ID, starting stats, movement physics, equipment and passive tree are unchanged.

- Run: nine poses with calmer grounded steps, lower knee lift and a one-pixel upper-body/hand settle on alternate contacts. The upper body and hands settle together by at most one native pixel. Held items follow exactly the same frame offset. No isolated hand cutouts or erased rectangles. The calmer cycle lasts 0.72 seconds; gameplay movement speed is unchanged. The first pose supplies the resting pose.
- Jump: five coherent poses transitioning into mildly bent, complete legs with visible shins and boots. The presentation is 6% narrower during jumping and 3% narrower while falling; normal width returns on running or resting. Width affects only MovementSprite, preserving physics and weapon sockets. Other characters default to normal width.
- Fall: five poses starting on the exact jump-apex image, then unfolding the legs toward the landing-ready pose. Both airborne animations play once and hold their last pose until the movement controller switches state.
- Every sprite keeps a 28 x 28 FullRect canvas, centred pivot, 24 PPU, point filtering and uncompressed alpha. Uniform frame bounds retain the existing foot-alignment and weapon-socket behavior.
- Warrior run weapon offsets are 0 or -1/24 unit and follow the sprite frame without interpolation, keeping equipment aligned with the subtly moving grip. The optional weapon-offset system remains available for other animations. The bowstring remains unchanged.

`Tools > Visuals > Apply Refined Warrior Movement` reapplies imports and CharacterData bindings. `WarriorMovementVisualBuilder.AssembleGeneratedFrames` packs accepted individual frames from `%LOCALAPPDATA%/Temp/codex-warrior-refresh` using Unity's image decoder/encoder before applying them. It does not require runtime network access.

The run builder imports the accepted PixelLab native FixedGripRun-sheet.png. Jump and fall remain complete individual frames. Earlier handless sheets are not used.

Accepted PixelLab sources:

- Run: `35fb63c0-37c6-4b1a-80d6-b9e64307284b; one-pixel grip pass 455b8a9c-9612-4bd6-bcae-3dcfc919b387; cleanup f901accd-58c8-4aa0-b0e3-dc333ca82fa6; right-hand repair e86e6961-eead-4ff9-bae3-733c8acb009e; full left cuff repair bc55cbf3-d6dd-4520-9f66-3fa940540fe3`.
- Jump: `99cb40e4-25f4-42b5-9869-b545e5e49b95`, ending on frame 2 of `e3dca59f-78dc-4d01-b16a-56eb400433a3`.
- Fall: `89ef9b03-d621-41ae-bfdb-8d3d64ae7bd6`, starting on the same airborne pose.

Validation: WarriorMovementAnimationTests (6) covers frame geometry, resource loading, run cadence, final-pose holds, frame-aligned weapon offsets, one-pixel hand tracking with animated upper body and legs and presentation width resetting after airborne states. PlayerRenderAnchorTests (6) checks shared pixel alignment and restoration of attack sockets after bobbed rendering. The Play Mode preview exercises the runtime animation controller and render anchor with a held bow or axe in both facing directions. It samples a jump/fall arc and writes PNGs and a review GIF to `%TEMP%/codex-warrior-refresh`. It is a controlled animation preview rather than a recorded player-input session.

Both hands are preserved through their full fingertip and dark cuff/outline extent below the waist, in front of the leg pixels. The previous right-only guard missed the left cuff. The regression check now covers both complete contours across all nine frames, with their existing one-pixel settling motion. Runtime uses one complete sprite per frame; the offline compositing boundary was responsible for the disappearing contour.
