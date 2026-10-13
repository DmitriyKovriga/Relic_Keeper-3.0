# Mage movement refresh

Selected outfit: `Docs/Art/MageWizardRobeVariants/Mage-09.png` (PixelLab Pro job `bac1b725-329c-4785-901f-9dd563f56109`). All nine candidates and earlier sets remain preserved.

Matching PixelLab animations: run `9c7d5dfc-3662-40fd-b6e1-193088cf4d8e`, airborne `e58a8a6c-e670-43a7-9358-55d038e3766a`. All nine raw frames per job are retained in `SelectedAnimation/`; no generated pixels are painted or recolored.

The runtime sprites keep the native 32x40 canvas and 24 PPU, point filtering, full rectangles and no compression/mipmaps. Transparent bottom padding is translated above the model to align boot pixels with the physical ground. Opaque pixels are neither resized nor clipped. The hat receives extra canvas height.

Run uses eight frames at 0.08 seconds and loops. Jump uses poses 0, 1, 2, 2, 2 at 0.075 seconds. Fall holds pose 2 to avoid airborne walking. Double jump restarts at frame 3, avoiding the grounded anticipation. Landing uses five rest frames at 0.06 seconds with a small compression and recovery. Existing shared forward lean remains active.

Equipment follows authored front-hand coordinates in every frame, including idle. Coordinates refer to the ground-aligned canvas; offsets use (21,4) as the existing physical grip baseline. Staff and wand previews exercise both facing directions and jump/fall/landing transitions.

Rebuild with `RelicKeeper.Editor.VFX.MageMovementVisualBuilder.AssembleGeneratedFrames`. The existing Mage asset identity, stats and passive tree are preserved. Original movement sheets remain available. Narrow regression filter: `RelicKeeper.Tests.EditMode.MageMovementAnimationTests`.
