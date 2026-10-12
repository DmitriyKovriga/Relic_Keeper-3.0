# Lunge Slash visual refresh

Lunge Slash now uses a pointed, rising sword cut with an ivory cutting edge,
steel-gray wake, separate upper and lower motion trails, and restrained streaks
breaking away after the cut. Thirteen frames build the stroke, then taper it
from behind and finish transparently. Native 108×25 frames, 24 PPU, centered
pivots, Point filtering and uncompressed FullRect sprites preserve its footprint.

`LungeSlashRefined-Sheet.png` replaces the frames in the existing
`LungeSlashAnimation` clip and `LungeSlashVFXPrefab` preview. Frame spacing is
20 FPS, with a blank hold retaining the original approximately 0.667-second
clip duration. The controller and prefab/clip GUIDs are preserved, and the
original sheet remains available. Dash force, damage window, effect offset,
owner attachment and all gameplay data were not changed.

Authored and reviewed through PixelLab MCP VFX Workbench:
final step `d0cb6ac4-acbc-4659-8471-f73f5b7f34ba`,
export image `ad6f2409-d064-4399-b505-5606a682eb35`.
[Editable PixelLab effect](https://pixellab.ai/create-vfx?effect=ae6c7c79-4db6-4116-a368-b92cc929cef6).
The export is imported at its original pixel size.

Tools → Skills → Apply Refined Lunge Slash VFX rebuilds slicing and frame wiring.
LungeSlashVisualBuilder.CaptureRuntimeViews uses isolated 480×270 URP renders
for both facing directions, at a sample 0.35-second lifetime, with the recipe's
0.5-unit offset and default fade. Like the actual recipe, VFX instances are not
attached to the owner. The helper explicitly cleans up these unattached instances.
This previews the VFX module independently of a full movement/combat cast.

Validation: Unity compilation succeeded, and both tests in
`RelicKeeper.Tests.EditMode.SkillAssetRegressionTests` passed. The isolated
Play Mode capture checked mirrored frames and lifetime fading; the console
contained no errors. A full combat lunge was not exercised.
