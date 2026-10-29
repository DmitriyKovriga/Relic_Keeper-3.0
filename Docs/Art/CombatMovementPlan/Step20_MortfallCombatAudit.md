# C18 — Mortfall combat audit

The next verification slice uses real Mortfall rooms rather than the empty
decoration stress stage. It adds an opt-in development-player diagnostic; no
saved skill balance, enemy AI, movement, room geometry or player UI is changed.

## Workload

Room 001 and room 006 each run Near Fan, Circular Saw and Lightning Rod in
Normal and Reduced modes: twelve cases. The diagnostic loads the actual room
prefabs and authored enemy spawners, keeps enemy AI active, installs the dungeon
color/mist profiles, tiled background and parallax columns, and uses the same
white, intensity-one global ambient light as HubScene.

The player is a stationary Warrior visual proxy with large health/mana pools.
Each skill uses a runtime clone with cooldown zero; attack/cast speed is raised
four times, ProjectileCount is 12 and AreaOfEffect is 200. Enemy maximum health
and stun threshold are increased so the crowd survives the repeated casts.
Authored recipe timing, targeting and on-hit code still run. This is a stress
workload, not a normal balanced build or a full equipment/skin presentation check.

The camera explicitly submits a 480x270 offscreen render every frame. Warm-up
frames are excluded from whole-frame percentile samples. The GC counter includes
diagnostic collections/count sampling and cannot by itself identify a gameplay
allocation leak. GPU frame timings are recorded only when the API returns them;
an unavailable counter is stored as -1 with zero samples, not interpreted as a
zero-cost GPU frame.

Each case checks positive damage and accepted casts, a real pause-service
interval that freezes scaled time and rejects casts, then cancellation and the
same SkillProjectile.DespawnAll call used by DungeonController.LoadCurrentRoom.
The actor and room are destroyed; after six seconds both player and enemy active
projectile counts must be zero. This does not audit every custom VFX helper or
replace a full camera/HUD/input traversal and GPU-profiler capture.

## Fixture corrections

The first visual pass exposed missing ambient light and the shared dungeon
background in the diagnostic. Those were added before the final measurements.
The next pass found that a fixed two-unit left offset put the actor below a
platform and against a wall in room 006. Placement now requires clear body space
and a supported surface at the same height as an authored spawner. Candidates
are checked in order of distance from the entry; an unsuitable nearest spawner
is skipped. Four physics regressions cover the wall/platform case, missing
support and valid positions in both actual room prefabs.

The Unity test runner initially waited on a save prompt for an old temporary
audit scene. The build helper now exposes cleanup for that exact generated
scene; it never saves/discards the Hub scene. Audit builds use a separate
company/product preference identity so an interrupted diagnostic cannot change
the real game's saved presentation settings. Build-time identity, preload and
frame-timing options are restored afterward.

## Reproduction

Build through RelicKeeper.Editor.Combat.CombatDecorationAuditBuild.Build, then run
Builds/CombatDecorationAudit/CombatDecorationAudit.exe with:

    -mortfall-combat-audit -screen-width 480 -screen-height 270 -screen-fullscreen 0

The development player runs in the background and exits automatically. It saves
per-case screenshots and a report under Logs/MortfallCombatAudit. It does not
load the player's party/save or persist a run. Diagnostic code is excluded from
non-development builds. The older -combat-decoration-audit comparison remains
available in the same executable.

## Final verification — 2026-10-29

Compilation passed. **54 focused EditMode tests passed, zero failed/skipped**:
MortfallCombatAuditTests, NearFanTests, CircularSawTests, LightningRodTests and
WeaponSwapSkillLifetimeTests. The temporary-scene cleanup was exercised in the
live Editor, including returning from a lone generated scene to the saved Hub.

The Windows development build completed and **all twelve final standalone
cases passed**. Room 001 has ten authored enemies; room 006 has 41. Every case
accepted casts, dealt positive damage, preserved the pause clock and finished
with zero active player/enemy projectiles after room/owner cleanup.

| Room | Skill | Normal p95 | Reduced p95 |
|---|---|---:|---:|
| 001 | Near Fan | 4.989 ms | 4.608 ms |
| 001 | Circular Saw | 4.221 ms | 4.059 ms |
| 001 | Lightning Rod | 3.918 ms | 4.248 ms |
| 006 | Near Fan | 5.801 ms | 7.100 ms |
| 006 | Circular Saw | 7.525 ms | 6.382 ms |
| 006 | Lightning Rod | 6.561 ms | 5.882 ms |

[Six combat screenshots](MortfallCombatAudit/comparison.png) were visually
inspected. [Full measurements](MortfallCombatAudit/audit.json) record p99,
counts, GC observations and hardware; [source fingerprints](MortfallCombatAudit/sources.json)
identify the diagnostic and room data. Maximum recorded p99 was 9.472 ms.
These short, differently timed AI encounters do not establish a consistent
Normal-versus-Reduced speedup or identical damage totals. They are offscreen
whole-frame measurements, not foreground game FPS. No balance changes or
additional culling were justified by this run.

**GPU timing remained unavailable:** every case returned zero GPU samples and
stores -1 for GPU percentiles despite enabling build-time frame timing. A real
GPU-profiler capture is still open work; it is not marked passed. GC peaks
include the diagnostic's sampling and collections; the larger first-room peaks
do not establish a gameplay leak. Hardware-input movement, the production
camera/HUD/equipment pipeline, all rooms and all custom VFX helper cleanup are
also outside this controlled audit.

An intermediate build hit Windows sharing/mapped-file errors. The retry passed;
settings restoration now uses atomic replacement, with generated-scene cleanup
in a finally block. No game saves are used. Task-scoped whitespace checks passed
and Graphify was refreshed. Build-generated settings/linker files were removed;
unrelated room 007/009 prefab changes were left untouched.
