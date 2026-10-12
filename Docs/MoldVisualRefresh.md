# Mold visual refresh

Mold retains its antlered moss-caster identity and olive/teal/violet palette. The
refined sprite defines the moss mantle, layered torso, hands, cloth folds and
balanced stance with clearer pixel clusters. Native canvas size remains 36×36
at 24 PPU, centered pivot, Point filtering, FullRect mesh and no compression.
The four original sheets remain available unchanged for rollback/reference.

New sheets live in `Assets/Resources/Enemy/MoldResources/Refined`:

| State | Frames | FPS | Behavior |
|---|---:|---:|---|
| Idle | 5 | 5 | Quiet breathing; stable crown and planted feet |
| Walk | 9 | 8 | Short steps and restrained arm/cloth motion |
| Attack | 13 | 14 | Antler sparks, raised-arm charge, overhead lightning release, dissipating arcs and recovery |
| Hit | 5 | 12.5 | Brief grounded flinch using the existing guarded hit-reaction system |

The old attack had five frames at 10 FPS, with the default final-frame impact:
its projectile therefore released at 0.4 seconds even though the fallback
windup field is 0.7. The new thirteen-frame sequence explicitly signals on frame 8 at 14 FPS
at approximately 0.571 seconds. Playback is 30% slower than the initial 20 FPS cast; the projectile stays synchronized to the flash. The 36-pixel canvas keeps the existing
projectile origin and physical scale stable. Damage, cooldown, projectile resource,
movement, loot, physics fit and enemy identity are unchanged. Hit reactions are
now connected to the existing animation hook; that hook refuses reactions while
an attack is busy and does not introduce a new combat interruption rule.

Tools → Enemies → Apply Refined Mold Visuals imports and assigns the sheets and
prefab preview sprite without replacing prefab/data GUIDs. The builder's
CaptureRuntimeViews method renders an isolated old/new comparison in Play Mode
at 480×270 without starting a dungeon run or changing saved progression.

PixelLab MCP provenance: base edits `fc85af62-53de-466f-ac14-539fa852f807` and
`1e870de5-e8a9-420d-bbd2-a091ee53af90` (seeds 1222–1223, five generations each);
idle `7bf6b8bc-a087-4388-a4eb-ee1fbd53d33c`, walk
`f19b442a-cd28-43b7-9247-1ae545464588`, attack
`8fb23872-33f0-4fbd-8d54-f81354f183fc` (seed 1238), hit
`c4e98cac-eab9-4db3-a48a-c8a02be2ff35` (seeds 1224–1227, one generation each).
Three retained free workbench passes stabilized the changing crown/face from the original
input frame (rows 0–15), plus planted foot rows 33–35 for idle and hit:
`d8df2b56-f550-48dd-82d4-1d0af738f738`,
`b984f59f-94bd-462e-8e22-98fe3a60ba06`,
`1f174a47-cf63-4920-8b48-ee2bf64962f0`.
The attack uses generated frame indices 0, 1, 3, 4, 5, 7, 10, 11, 12, 13, 14, 15, 16:
redundant resets were removed to give one continuous charge, and the main flash
aligns with the release signal. All frames were reviewed and assembled without
scaling or repainting locally.

Validation: compilation and six MoldVisualTests passed. Native-scale Play Mode
renders confirmed the runtime child renderer displays the refined art, including
the release pose with the real projectile sprite positioned using the attack
controller's actual spawn-origin method. This isolated capture previews projectile
travel without firing at players. Console contained no errors during verification.
