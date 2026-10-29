# C18 — selected skill-decoration budgets

Shared admission limits now cover verified decorative paths. They do not cull
gameplay projectiles, hitboxes, telegraphs or skill-step VFX: the latter can
control damage timing and must not be treated as disposable decoration.

| Decorative path | Normal | Reduced |
|---|---:|---:|
| Simultaneous saw/prismatic/ricochet impact accents | 16 | 6 |
| Simultaneous generic projectile ripple rings | 8 | 2 |
| Reserved live manual projectile sparks / air sparks / ground debris particles | 384 | 96 |

Values live beside the existing flash limits in CombatEffectsPolicy and use the
existing saved EN/RU Combat effects setting. No new controls or art are added.
At ordinary load the existing authored effects play as before. Saturated paths
skip decoration before constructing its object or emitting its particles;
denied work is never queued. Successful damage/ailment processing happens
independently of impact admission.

Particle reservations expire on scaled combat time using authored lifetimes.
Ground debris reserves its maximum randomized lifetime conservatively; early
projectile cleanup may also leave reservations until expiry. Pausing freezes
expiry and denies new admission. Burst/ripple admission prunes destroyed or
disabled owners. Lowering the setting immediately constrains new admission;
existing decoration finishes naturally rather than disappearing mid-animation.
Subsystem registration clears static records for a new play session.

Coverage remains deliberate: automatic smoke emission, persistent trails, core
projectile sprites, custom auras/beams and skill-step geometry are outside this
budget. This is not a universal graphics-quality controller or proof that all
skill overdraw is bounded.

## Reproduction

CombatDecorationBudgetTests exercises sustained demand, exact limits,
expiry/pause-clock behavior, lower limits and invalid lifetimes. Nearby
ReducedCombatEffectsTests, GroundProjectileDebrisTests, CircularSawTests,
PrismaticCycleTests and LuckyShotTests verify the affected systems.

The opt-in CombatDecorationStressProbe compares an uncapped reference with
Normal and Reduced using identical batches of actual impact prefabs, emitted
particles and 32 real gameplay projectiles. It records admitted counts,
peak impact counts, hits, total damage and whole-frame median/p95 durations,
plus screenshots. Frame durations are observations of this controlled workload,
not isolated GPU timings or a full dungeon benchmark.

In Play Mode invoke RelicKeeper.Diagnostics.CombatDecorationStressProbe.Begin.
The probe restores preferences, vSync and target framerate, then removes its
objects. RelicKeeper.Editor.Combat.CombatDecorationAuditBuild.Build creates a
temporary additive empty scene and builds a dedicated Windows development
player under Builds/CombatDecorationAudit; it closes/deletes only its own scene.
Run that player with -combat-decoration-audit -screen-width 480 -screen-height
270 -screen-fullscreen 0. It exits automatically. Raw outputs go to
Logs/CombatDecorationStressEditor or Logs/CombatDecorationStressPlayer.
The diagnostic runtime is excluded from non-development player builds.

## Verification — 2026-10-29

Unity compilation and the Windows development-player build passed. The final
focused run executed **58 tests: 58 passed, 0 failed, 0 skipped** across the six
classes above, including the newly imported seven budget cases.

The final standalone run forces a real 480x270 offscreen render request every
frame, so a hidden Windows player cannot skip the rendering workload. All three
modes passed with **32 projectile hits and 320 total damage** each.

| Mode | Impact requests / admitted | Peak active impacts | Particle requests / admitted | Whole-frame p95 |
|---|---:|---:|---:|---:|
| Uncapped reference | 800 / 800 | 160 | 4000 / 4000 | 2.963 ms |
| Normal | 800 / 80 | 16 | 4000 / 1536 | 1.025 ms |
| Reduced | 800 / 30 | 6 | 4000 / 384 | 0.751 ms |

[Rendered comparison](DecorationBudgets/comparison.png) was visually inspected;
[raw final measurements](DecorationBudgets/audit.json) include hardware and
whole-frame medians. This is a short controlled sample on an RTX 4070 Laptop
GPU / i7-12700H, not a general FPS improvement claim, isolated GPU timing or a
full Mortfall-room benchmark. Median frame times were essentially unchanged.

Two diagnostic corrections preceded the final run: running in the background
alone did not make the hidden backbuffer render (black screenshots), so the
fixture now submits an explicit render request. A live Hub Editor comparison
also produced distinct valid randomized damage totals despite 32 hits per mode;
exact total-damage equality is therefore checked in the isolated player scene,
which has no equipped-hand/global character modifiers. The final player report
supersedes the earlier simulation-only measurements and failed Editor equality
assumption. Build logs also contain existing recursive StepEntry serialization
warnings; this presentation change does not alter that schema.

Task-scoped diff checks passed and Graphify was refreshed. Build-generated
preload/settings/linker changes were removed. Unrelated Mortfall room/palette
tile-cache changes observed during the session were left untouched.
