# Rogue visual refresh

The user selected option 01 (Classic) from the nine PixelLab Pro candidates in
`Docs/Art/RogueVariants`. The rogue retains the brown swept silhouette, burgundy
scarf, gray fitted clothing and original soft, muted pixel style, with clearer
cloth, belt and boot definition. The earlier redesigns were rejected.
The original 32 x 32 candidate is preserved unchanged; only its two-pixel
transparent margin is removed for the game's 28 x 28 frames.
PixelLab generated matching movement frames; Unity packs and
imports them with point filtering, 24 pixels per unit and centered pivots.

## Animation behavior

- Run: eight frames at 0.08 seconds each, with restrained arms and per-frame
  equipment grip offsets. The first frame uses the exact resting source.
- Jump: five frames at 0.075 seconds each, using generated poses 0, 1, 2, 2, 2.
  The compact airborne pose is held rather than using later arm-spreading
  frames. Shape changes are drawn into the sprites, without runtime airborne
  squash or stretch.
- Fall: holds the final jump pose, avoiding a walking cycle in the air.
- Double jump: restarts at frame three, skipping ground anticipation.
- Landing: a 0.3-second compression/recovery using the resting sprite.
- Idle: uses the new model and its resting equipment grip. The shared controller
  resets this offset when changing characters; the warrior retains its old grip.

Existing forward-lean presentation applies to the body and equipment together.
The original rogue sheets remain available. The character asset GUID, ID,
passive tree and starting stats are preserved. Only its portrait and movement
animation references change.

## Source and rebuilding

PixelLab jobs:

- Accepted base: `ea474b02-b7b3-4a9f-9ad4-54d53e8d49dc` (option 01).
- Run: `3ee8e458-276d-4788-9f5a-bd1cb9d13533`.
- Jump: `71c89b04-d672-45a9-841a-97421d3a7dd9`.
- Run frame 1 hand repair: `45bcbaec-6e98-406b-bb93-22f0d5282f3b`.
  The uncorrected generated frame is preserved as `RogueRun-1-original.png`.

`RogueMovementVisualBuilder.AssembleGeneratedFrames` reads `RogueBase.png`,
`RogueRun-0.png` through `RogueRun-7.png`, and `RogueAir-0.png` through
`RogueAir-4.png` from `Docs/Art/RogueVariants/SelectedAnimation`.
These accepted sources are preserved with the project. Use
**Tools > Visuals > Apply Refined Rogue Movement** to rebind the existing sheets.

## Validation

Eight rogue and thirteen warrior EditMode tests passed. Coverage includes
imports, preserved identity, grip connectivity, resting equipment offsets,
character switching, double-jump transitions and landing recovery.
Controlled Play Mode captures checked dagger and bow presentation, both facings,
and double jumping. These captures exercise the real animation controller and
render alignment; they are not a recording of a full gameplay session.
