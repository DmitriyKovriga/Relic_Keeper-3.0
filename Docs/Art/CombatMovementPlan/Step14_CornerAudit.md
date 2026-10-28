# Step 14 — conditional corner correction audit (M06)

Reviewed 2026-10-28. No persistent snag, lost ascent at an open corner or wall
traversal was reproduced in the controlled cases. Automatic corner correction
has not been added. Gameplay code, collider shapes and movement values remain
unchanged by this step.

Added CornerTraversalTests: four fixtures, speeds 5/10 units per second, both
directions, **16 physical trajectories**. The fixture uses the Hub player's
serialized box dimensions/offset, its actual zero-friction physics material,
PlayerMovement acceleration/gravity/support code and Physics2D simulation.

- Held movement into a tall wall: ascent continues; the body falls away rather
  than clinging, without crossing the wall or remaining embedded.
- Low solid ceiling: upward motion is blocked as expected; descent continues.
- Clear ceiling lip, about one source pixel of clearance: moving away clears
  the corner and preserves the normal jump arc.
- One-way platform: the body passes upward through it, then lands on its top.

At double speed, discrete collision briefly overlaps the wall's near face by
about 0.045 world units before resolving on the next step. The test permits
that transient solver contact, rejects sustained embedding and checks that
the body never traverses the wall. Falling below the finite wall is also
allowed; the wall test only checks obstruction while their vertical bounds
overlap. Earlier fixture assertions incorrectly classified those two normal
physics behaviors as traversal; those assertions were corrected after reading
the per-step traces. No gameplay fix was inferred from them.

Compilation and **63 EditMode tests passed**, zero failures/skips:
CornerTraversalTests and PlayerMovementTests. Existing movement tests also
retain tiny platform-edge support, ceiling rejection, drop-through and jump
input coverage. [Simulation summaries](CornerReview/audit.json); ignored CSV
traces are in `Logs/CornerReview/`.

This is scripted EditMode physics, not live keyboard playback or a full
Mortfall room sweep. Each trajectory advances real physics for 90 steps at
0.02 seconds; Update/FixedUpdate logic is invoked explicitly. Since manual
EditMode simulation does not advance Time.time, the launch probe lock is
expired explicitly after launching; upward velocity still rejects ground
contact. No rendered animation, mixed Tilemap/CompositeCollider seams, dash
corner trajectory or display performance claim is made by this fixture.

If a specific room corner catches the player in play, reproduce that geometry
and approach before changing movement. M06 remains conditional; the general
M01–M05 movement/input pass can now be evaluated through normal playtesting.
