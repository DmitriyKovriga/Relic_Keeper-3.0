# Sparse ambient particles — V12

Open **Tools > Relic Keeper > Visuals > World Appearance > Details**.
Choose **Hub** or **Mortfall**, then choose a **Preview area**:

- Hub: Chimney smoke, Forge embers, Tree leaves.
- Mortfall: Ceiling drops, Torch dust. The initial dust source is in Room 001.

**Enable particles**, **Density (%)**, and **Visibility (%)** control the air
particles. Press **Save** to keep the location's settings. **Compare with saved**
and **Discard changes** use the same draft system as colors, mist and lighting.
Small animations (smoke/drops) have their own controls. Density 0 hides all air
particles; leaves intentionally have long quiet gaps.

## Placement and limits

- Forge: small warm flecks rise from the existing flame at layout pixel (715,281).
  Up to three fixed renderer slots; height 0.7 units, width 0.55 units.
  They fade below the forge canopy rather than rising through its roof.
- Tree: one muted olive leaf falls through a clear pocket below a canopy, with a
  2.8-unit fall. No continuous screen-wide leaf layer.
- Mortfall Room 001: up to four faint dust motes next to the first torch. The
  motion pocket and rays to the flame are checked against solid tilemaps during
  placement. Dust fades toward the edge of the light and disappears if its
  torch or location lighting is disabled. It follows the torch hierarchy.

Default density is 50%, visibility 70%. Existing profile settings are preserved.
Effects use the Environment color group and sort behind characters. All motion
is snapped to the 24-PPU grid. There are no new colliders, gameplay effects,
per-frame spawns, material instances, or camera-wide particle textures.
Each source owns at most 3/1/4 SpriteRenderers respectively. Room destruction
and preview disposal remove the source with its hierarchy. Renderer changes
exist only during the owning camera's render and are restored immediately.

## Assets and maintenance

All three clips were created and exported through **PixelLab MCP**. Recipes,
effect IDs and image IDs are in `Ambient_Particles_PixelLab.json`; no external
pixel editing was used. Atlases are in `Assets/Visuals/EnvironmentParticles`,
8 frames each, point filtered, uncompressed, no mipmaps, 24 PPU.
Reusable prefabs live in `Assets/Prefabs/Visuals`.

`AmbientParticleSetup.Apply` imports the clips and reinstalls only these sources;
the hub builder preserves their anchors on rebuild. The setup helper is for
maintenance, not a required user action. New rooms should receive dust only
where an actual light and a clear pocket justify it. No automatic ambient
emitter is added to every torch.

## Verification

Unity compilation passed. 57 relevant EditMode tests passed (0 failures/skips),
including fixed caps, deterministic pixel-grid motion, clear torch placement,
actual GPU sprite rendering, light/profile switches, transient state restoration,
preview cleanup, and saved appearance settings. Hub effects were captured from
the live Play Mode camera at 480x270; Mortfall Room 001 was checked in the
visual-only preview during Edit and Play Mode. No console errors appeared in
the Play Mode check. Captures are local diagnostics under `Logs/AmbientParticles`.
This is a small pilot placement, not a measured GPU performance improvement.
