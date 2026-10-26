# Stone relief — V19 Mortfall pilot

The wall and platform near the entrance torch in **Mortfall Room 001** use
PixelLab normal maps matching their existing sprite sheets. The diffuse PNGs,
sprite identities, tile placement and gameplay collision are unchanged. Other
rooms and character artwork are outside this pilot.

## Editing

Open **World Appearance > Mortfall > Room 001 > Lighting**. Adjust
**Stone relief (%)**, then use the existing **Save** button. The initial value
is **40%**. Zero restores flat local illumination; 100% exposes the full map for
comparison. The control follows the existing location profile, live preview,
Compare and Discard behavior. Hub/new profiles default to zero.

The effect is deliberately matte. Edges and cracks respond to the flame's
direction, without specular highlights, added outlines or changing ambient color.
The normal map changes only the real source-light contribution. Terrain blocking,
player shadows, other lights, source falloff and color grading still apply.

`StoneReliefSurface` marks a six-unit patch on the Ground, Background and Platforms
tilemaps around the first torch. Its local region follows room placement and
feathers over 0.35 units at its border. It preserves existing property-block
values and restores its own values when disabled. There are no duplicate tiles,
new colliders, manual maintenance buttons or runtime material allocations.

## Rendering

`StoneRelief.shader` retains the graded sprite albedo path and adds URP's
`NormalsRendering` pass. The source sheets bind the generated textures as
`_NormalMap` secondary sprite textures, so tile UVs, atlas packing and sprite
flips use the corresponding normal pixels. Imports are linear normal maps,
point filtered, uncompressed and without mipmaps or NPOT resizing.

The entrance torch's saved Light2D enables Accurate normals with a normal-map
distance of 0.85. The visual-only room preview copies those Light2D settings as
well as the source component. Its native normals pass therefore matches runtime.
Only this torch has been enabled; the second torch and other rooms retain their
current lighting settings.

URP normally attenuates even a flat surface when enabling normal lighting. Its
normal buffer also clears to an invalid zero-length unpacked normal where an
unlit sprite has no normal pass. The existing custom point-light shader accounts
for both cases: empty normals retain the old light contribution, and tilted
normals are measured relative to the flat facing plane. The response is bounded
to 0–1.6 times the source contribution. It is diffuse shading, not a gloss model.
No normal-enabled shader variant runs for lights with normals disabled.

`WorldColorGrading` uploads relief strength from the active atmosphere profile
before each camera renders, including Scene view. The value resets after the
camera and during suspended bindings. No screen-wide lighting filter is added.

## PixelLab provenance

Both assets use `create_normal_map`, mode `lite`, kind `side`, OpenGL convention
(green up). The input is the original sheet at its native size, without resizing
or repainting. Output bytes are stored directly from the PixelLab result.

| Source | Output | Job | Settings |
|---|---|---|---|
| Mortfall-ground-platforms.png, 288×240 | MortfallStoneNormal.png | 5399f611-1934-431f-be03-14f79f77afe4 | strength 0.6, detail 0.5, part shapes 0.1, outline rounding 0.1, softness 0.65 |
| Mortfall-Walls-tiled.png, 168×192 | MortfallWallNormal.png | 5b580244-f6f1-4828-a7ee-c955bcceb1a8 | strength 0.6, detail 0.55, part shapes 0.1, outline rounding 0.08, softness 0.65 |

The editor-only setup helper creates this pilot and imports the secondary
maps; it has no menu button and is not part of level-design workflow.

## Verification

`StoneReliefTests` covers direction-dependent GPU shading, flat/zero recovery,
ambient preservation, unmarked surfaces, a character in front of the wall,
terrain occlusion, camera reset, translated/scaled regions, property-block
ownership, normal imports and the Room 001-only scope. The existing appearance
save test also verifies that relief strength is saved with the location profile.

Native 480×270 captures at 0%, 40% and 100%, plus a 21-position camera pan, are
written under `Logs/StoneRelief/`. This remains a small visual pilot; broader
normal-map authoring and device performance measurement precede a full rollout.

Unity compilation and all 71 focused EditMode checks passed, including the
existing contact/player shadows, local light, source glow, ambient particles,
color grading, character FX and appearance-save suites. The isolated Room 001
preview was also rendered in Play Mode, with the saved profile and a 21-position
camera pan. The scene/session was returned to Edit Mode. This is visual-preview
validation, not a full dungeon gameplay traversal or a standalone player build.
