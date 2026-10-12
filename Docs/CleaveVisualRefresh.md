# Cleave visual refresh

Cleave now has a broad, compressed axe sweep with an ivory cutting rim,
muted steel-gray wake, inner afterimage and restrained follow-through streaks.
Twelve frames draw the arc rapidly, then peel the trail away behind the blade.
The last frame is transparent. Native 36×36 frames, 24 PPU, centered pivots,
Point filtering and uncompressed FullRect sprites preserve the visual scale.

`Assets/Resources/VFX/CleaveSFX/CleaveRefined-Sheet.png` replaces the frames in
the existing controller's default `CleaveSFXAnim2` clip. The clip remains
non-looping and retains its original approximately 0.583-second duration.
`VFX_Cleave.prefab` uses the first new frame for its preview. Original sheets
and the unused alternate clip remain available. The recipe, player animation,
hitbox, damage, prefab/controller GUIDs and lifetime settings are unchanged.

Authored and reviewed through PixelLab MCP VFX Workbench:
final step `0782f243-a515-4b59-962a-6be58349f801`,
export image `3c237903-873e-4a86-817a-36cc9054b2b6`.
[Editable PixelLab effect](https://pixellab.ai/create-vfx?effect=d2cc711c-8c86-44d4-b6a5-32b73246e0ab).
The export is imported at its original pixel size.

Tools → Skills → Apply Refined Cleave VFX rebuilds slicing and frame wiring.
CleaveVisualBuilder.CaptureRuntimeViews reuses the Cold Slash preview helper
for isolated 480×270 URP renders in Play Mode. It checks fitted animation
speed and both facing directions, and samples the actual AutoDestroyVFX fade.
Temporary objects are cleaned up without starting a dungeon or changing saves.

Validation: Unity compilation succeeded, both SkillAssetRegressionTests passed,
and Play Mode renders confirmed the brighter sweep, mirrored facing and fade.
No console errors were reported. Preview checks the VFX module independently
of a full combat cast.
