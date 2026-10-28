# Step 04 — brief confirmed-hit highlight

Implemented 2026-10-27: the C07/C10 target-flash slice. The receiver highlights
its current sprite pixels briefly after a positive direct hit, with light,
normal, heavy and critical strength. This uses the shared confirmed-hit path
for melee, projectiles and spells; the focused live comparisons below start
with Cleave, The Weight of a Giant and Simple Bow Shot.

## Comparison

Left: flash disabled. Right: flash enabled. Body motion, recoil, skill VFX,
weapon path, scene and attack settings are enabled identically in both halves.
These are actual Unity camera frames, cropped and enlarged 3× with nearest
sampling. GIF playback follows the 60 fps capture with a short final hold.

- [Normal strike comparison](HitFlash/normal-comparison.gif)
- [Heavy strike comparison](HitFlash/heavy-comparison.gif)
- [Bow shot comparison](HitFlash/bow-comparison.gif)
- [Peak comparison sheet](HitFlash/comparison-sheet.png)
- [Critical, frozen, tinted/emissive, locally lit and Animator-root targets](HitFlash/status-and-lighting.png)
- [Capture summary and source fingerprints](HitFlash/audit.json)

## Behavior and tuning

`CombatHitFeedback.Confirm` sends the same immutable receiver result to the
new `EnemyHitFlash` component. `EnemyEntity.Setup` initializes it on the actual
animation source renderer. Hits with zero final damage, misses/evades and DoT
ticks do not flash. Lethal hits clear an existing pulse and leave death
presentation in control. Frozen targets can flash while keeping their freeze
state and tint.

`SpriteFxController` owns a separate `_FxHitFlash` property channel. Combat
does not call its existing full-color `Flash()` method or replace that timer.
Tint, emission, fade, dissolve, legacy flash, lighting and world color values
remain with their existing owners. Each write reads the current property block
first. Recoil copies that block to its existing display renderer after the flash
samples, so the pulse follows whichever body sprite is visible.

The existing World Color Lit, World Color Unlit and Relic Sprite FX shaders
apply a small contrast-preserving highlight after their normal color/lighting
work. Sprite coverage and alpha do not change. There is no overlay renderer,
new contact art, particle burst, audio or material replacement. Unknown custom
shaders are left intact and simply do not display this optional channel.

Settings are in the existing
`Assets/Resources/Visuals/CombatPoseProfile.asset`, under **Enemy hit flash**:

| Setting | Default |
|---|---|
| Enable Enemy Hit Flash | On |
| Color | Warm white `(1, 0.95, 0.82)` |
| Light / normal / heavy strength | 0.18 / 0.28 / 0.40 |
| Critical strength bonus | +0.08 |
| Duration | 0.075 scaled seconds |
| Minimum interval per enemy | 0.18 scaled seconds |

Attack weight comes from the captured authored hit multiplier, never raw damage
or target HP. A normal critical hit peaks at 0.36; a heavy critical hit at 0.48.
The pulse decays quadratically, does not stack or restart during its interval,
and preserves the cooldown after becoming invisible. Runtime safety caps
strength at 0.5 and duration at 0.12 seconds and enforces at least 0.05 seconds
of quiet time. This still holds if Inspector values are outside their ranges.
Scaled time holds the pulse during hit-stop/pause. Disable, hide, death and
enemy reinitialization clear the pulse without clearing status effects.

## Verification

- Unity compilation passed. **112 EditMode tests passed, 0 failed, 0 skipped**
  across EnemyHitFlash, CombatHitFeedback, CombatPose, RelicVisualFx,
  RelicVisualFxRender and WorldColorProfiles.
- GPU tests rendered all three affected shaders at 480×270: the flash increased
  brightness while preserving red/green texture detail, transparent coverage,
  neutral output after clearing and the original material reference.
- State tests cover all three weights and their critical bonuses; huge damage
  with normal/missing metadata; zero damage/DoT; material/color/property-block
  preservation; recoil display copying; frozen targets; hide/disable/profile
  switches; lethal/reuse reset; unknown shaders; pause; and rapid-hit bounds.
- **12 live cases passed**, including flash-only on/off normal, heavy and bow
  pairs; critical, frozen, tint/emission, local lighting and Animator-root
  enemies; and a rapid attack sequence. Every case ended with zero flash.
- On/off pairs had identical contact frames, recipe phases and final damage.
  Recorded hand and weapon transform differences were exactly zero.
- Visible peak strengths were 0.28 normal/bow, 0.40 heavy and 0.36 normal crit.
  Normal/heavy/bow highlights lasted 7/8/5 rendered frames including the
  existing hit-stop. Eight rapid casts confirmed eight hits but produced only
  four separated flashes; gameplay damage was not delayed or filtered.

The status preview uses a temporary material with the same neutralization as
`StatusAuraPresenter`; the shared demonstration material's existing static green
flash is not mistaken for combat feedback. The source material asset was not
edited. The live fixture keeps physical roots constrained, uses a stationary
target, and adds a temporary warm point light for the local-light case.

Raw camera frames and detailed traces remain in ignored `Logs/CombatHitFlash/`.
The review is a focused comparison, not a crowded-room performance benchmark
or a standalone player build. C09 hit-stop tuning and full crowd validation
remain later work.
