# Cold Slash visual refresh

ColdSlashVFX now uses a continuous downward sweep with a white cutting rim,
blue undertow, a secondary inner streak, angular ice facets and trailing shards.
The twelve frames build the arc, peel it away from its upper end, then finish
with a transparent frame. Native 32×32 frames, 24 PPU, centered pivots, Point
filtering and uncompressed FullRect sprites preserve the existing placement.

`ColdSlashRefined-Sheet.png` is assigned to the existing `ColdSlashVFX.anim`
and prefab. Their GUIDs, animator controller, material and recipe reference
are preserved. The original `ColdSlash-Sheet.png` remains available. SkillVFX
continues to fit animation playback to the recipe's VFX lifetime, including
attack-speed changes; the recipe, hitbox and damage were not changed.

The effect was authored through PixelLab MCP VFX Workbench, using curved slash
layers and deliberately placed ice shapes rather than a looping crescent.
Final step: `dfdbcc08-f86f-4c0e-8524-4da5d1b3f8b1`.
Export image: `36ba5243-7dc8-4edb-8563-e4093e4399df`.
[Editable PixelLab effect](https://pixellab.ai/create-vfx?effect=c0a9c410-a86c-4ca8-b71d-ebd64d5562ac).
All frames were reviewed; export is used at its original pixel size.

Tools → Skills → Apply Refined Cold Slash VFX rebuilds the slicing and clip.
ColdSlashVisualBuilder.CaptureRuntimeViews creates temporary isolated SkillVFX
instances in Play Mode, validates fitted animation speed, and samples the
actual clip for both facing directions into 480×270 URP renders. It cleans up
its objects and does not start a dungeon run or alter player progression.

Validation: Unity compilation succeeded, both SkillAssetRegressionTests passed,
and isolated Play Mode renders confirmed frame wiring and facing. No console
errors were reported. This checks the actual VFX module, not a full combat cast.
