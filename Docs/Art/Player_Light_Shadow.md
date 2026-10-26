# Player light shadow — V18 player pilot

Terrain shadow strength and softness remain unchanged, as requested. The player
now partially blocks nearby torch/forge light. Enemies do not cast these shadows
in this pilot. The existing contact shadow under the feet remains unchanged.

## Editing

Open **World Appearance > Lighting > Player light shadow**, choose Hub or
Mortfall and adjust:

- **Enable player shadow** — enables the effect for this location.
- **Light blocked (%)** — the portion of a source's light removed behind the
  body; default 24%, range 0–100%.
- **Shadow reach** — distance from the body in world units; default 2.5, range 0.1–30. The
  shadow fades out over the outer portion of that distance.
- **Shadow width (%)** — 10–400%, default 100%.
- **Shadow height (%)** — 10–300%, default 100%. The shape stays anchored at the feet.
- **Edge softness (%)** — 0–200%, default 25%. Zero gives a firm edge; larger
  values spread the transition across the silhouette.
- **Ground shadow opacity (%)** — 0–100%, default 35%. This is independent of
  wall light-blocking strength; zero disables the separate floor projection.
- **Ground shadow length** — 0–8 world units, default 1.
- **Ground shadow thickness** — 0.03–1 world units, default 0.18.

For an obvious comparison, try 80% blocking, 130% width, 100% height, 15%
softness and reach 4. Higher reach only matters within a source's light radius.
At 100% blocking the source contribution can disappear, but ambient light still
remains. These are percentages of light removed, not black-overlay opacity.
Existing saved settings are retained; newly exposed shape controls start neutral.
The separate ground projection deliberately adds surface contrast on a brightly
ambient-lit floor. Try ground opacity 50–70%, length 1–2 and thickness 0.18–0.25
to make it more visible without changing the wall shadow.

Use the existing Save, Compare and Discard controls. Each location keeps its own
values, which follow the same hub/dungeon profile transitions as local lighting.
The room preview includes this shadow on the player sample only.

The body shape is deliberately simple and stable. It follows position, scale and
jumps, while weapon swings and individual animation frames do not redraw it.
Crossing a source changes the direction; overlapping the flame fades the shadow
away to avoid a giant cone. The body's own area remains lit. This is a soft 2D
light obstruction, not a detailed 3D silhouette projected along a floor.

The original body-lighting exemption incorrectly extended 15% below the feet,
which protected nearby platform pixels from the shadow. It now ends above the
soles and uses the original body bounds, independently of the shaped caster.
Increasing width/height therefore cannot enlarge that exemption over the floor.

## Rendering

`PlayerMovement` adds `PlayerLightShadow` beside the existing grounding visual.
It caches body bounds from the movement collider and uploads one stable ellipse
for its own scene/camera. The isolated appearance preview uses the player sample's
initial bounds instead. It does not add or alter gameplay colliders.

`PlayerShadowLight2D.shader` is adapted from the installed URP 17.3
`Shaders/2D/Light2D.shader`: the original lighting code is retained, with one
world-position varying and a partial-transmission calculation in the point-light
fragment path. `Assets/UniversalRenderPipelineGlobalSettings.asset` references
this shader through its Renderer2DResources light-shader field. The original
terrain-shadow textures, normals, cookies, attenuation, and blending remain in
place. An inactive pilot is a neutral transmission of 1.

Only registered local source lights near the player participate, capped at the
nearest eight per camera. Broad room fills and unrelated lights are excluded.
Each source's light is reduced separately before lights are combined. Another
torch can illuminate the shaded area; ambient/global illumination is untouched.
Native terrain blocking still applies to this light-transmission path. It does
not darken UI, allocate a render texture, or create extra lights.

The ground projection reuses the existing `Shadow_Medium` artwork, with an
explicit unlit material so a nearby light cannot brighten the shadow itself.
It is a separate, optional soft surface layer extending away from the strongest
visible nearby torch. Runtime support, slopes and platform-edge clipping reuse
`GroundingVisualController`; a clear ray to the body is required. Its opacity
fades with source influence and height above the support. The original contact
oval remains independent. Visual-only room previews use the saved tile support
span and terrain visibility without adding gameplay colliders.

Shader globals are cleared between cameras and on disable. Scene ownership and
the existing paused-rendering guard prevent preview players from shadowing the
live game or another room preview. No new raster artwork was needed: source-light
transmission uses the existing native path, and the floor projection reuses the
existing grounding sprite. Torch/glow artwork and ceiling beams are unchanged.

The shader adaptation is pinned to the installed URP code. Review it alongside
the upstream Light2D shader on a URP upgrade. This pilot does not add blocking
to decorative ceiling shafts or glow sprites, which are separate visual layers.

## Verification

Unity compilation and 45 focused EditMode checks passed across player light
shadows, existing contact shadows, local lighting, glow, ambient particles and
appearance saving. GPU checks verify partial blocking, the unchanged lit body,
unaffected ambient illumination, the opposing light, jump positions, disable
cleanup and camera-global reset. The body-geometry check also covers changing
side, overlapping a source, limited reach and scale. Added regressions first
reproduced the missing shadow on pixels immediately below the feet, then passed
with the fix. Expanded controls and ground settings are checked through profile
save/reload. Floor projection checks cover visible darkening, direction reversal,
platform edges, high-jump fade and renderer cleanup.

Mortfall Room 001 was compared on/off at 480x270 across nine horizontal and
airborne positions, in Edit Mode and Play Mode. These use the isolated visual
preview, not recorded player input or a full gameplay traversal. Saved room and
scene placement were preserved. No standalone build or target-device performance
profile was run. The only global-settings change is the 2D light shader reference.

Follow-up ground comparisons used frozen lighting/particles at 480x270 in
Mortfall Room 001. Saved and stronger settings both visibly darkened platform
pixels. The live hub player was also captured next to the forge using the real
grounding support and clipping path, then returned to the original position and
velocity; the camera/profile were restored. Existing raster artwork, scene
placement and room prefabs were preserved.
