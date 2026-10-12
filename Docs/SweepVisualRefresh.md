# Sweep visual refresh

Sweep uses a travelling axe edge around a wide ellipse centered on the player.
The arc radii are approximately 20% smaller than the initial refresh. The steel-gray wake is one-third thicker and follows the blade longer. The ivory rim leads the wake and inner afterimage; the wake
peels away behind the blade rather than repeatedly showing a static oval.
Thirteen frames finish with trailing wisps and a transparent final frame.
Native 96×24 frames, 24 PPU, centered pivots, Point filtering and uncompressed
FullRect sprites preserve the active animation's original visual scale.

`SweepRefined-Sheet.png` replaces the frame curve of the existing controller's
default `SweepVFX2` clip. Frame spacing is 16 FPS, with a blank hold keeping the
original approximately 0.833-second clip duration. The clip remains non-looping.
Both `VFX_Sweep.prefab` and the alternate `SweepPrefab.prefab` use the first new
frame for their preview. Original sheets and unused alternate clip remain.
The recipe, player pose animation, hitbox, damage and lifetime settings were
not edited. Existing prefab/controller/clip GUIDs are preserved.

Authored and reviewed through PixelLab MCP VFX Workbench:
final step `48718235-d871-4adb-98c2-3f1b1d2a48bf`,
export image `2240a70e-2fa9-488c-a07a-f9d5f14cd541`.
[Editable PixelLab effect](https://pixellab.ai/create-vfx?effect=4ba3b738-a536-4547-a109-1ebaffff4fd4).
The export is imported at its original pixel size.

Tools → Skills → Apply Refined Sweep VFX rebuilds slicing and frame wiring.
SweepVisualBuilder.CaptureRuntimeViews reuses the isolated 480×270 URP preview
helper, placing separate owners for both facing directions with zero VFX offset.
It checks fitted animation speed and samples AutoDestroyVFX using the recipe's
75% fade start and 0.35 starting-alpha multiplier at a 0.46-second lifetime.
Temporary objects are cleaned up without starting a dungeon or changing saves.

Validation: Unity compilation succeeded, both SkillAssetRegressionTests passed,
and isolated Play Mode renders confirmed the travelling edge, mirrored facing,
clear center and fade. No console errors were reported. Preview checks the VFX
module independently of a full combat cast.
