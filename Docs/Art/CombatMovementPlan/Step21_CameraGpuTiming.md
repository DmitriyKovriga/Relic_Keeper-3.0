# C18 — native camera GPU timing

This follow-up addresses the unavailable GPU field in the Mortfall room audit.
It adds instrumentation only, with no gameplay or art changes.

## Measurement boundary

The installed URP source creates a GPU-enabled profiling sampler named
`UniversalRenderPipeline.RenderSingleCameraInternal: <camera name>` around its
camera command stream. The diagnostic enables that existing sampler's native
Recorder after its first render request. It reads gpuElapsedNanoseconds and
gpuSampleBlockCount, independently of FrameTimingManager and CPU frame time.
Recorder's documented three-frame GPU delay is excluded by a one-second warm-up.

Only positive timings with exactly one GPU sample block per frame are admitted.
Unsupported recording, no blocks, multiple blocks, and nonpositive timings stay
unavailable (-1); there is no CPU-time fallback. A report needs at least thirty
valid samples per case to label cameraGpuTimingStatus as available. Native
recorder state is restored when the diagnostic ends. GPU-only runs temporarily
enable Unity's GPU profiling area and restore that state too; CPU timings from
those runs include profiler overhead and are not baseline game FPS.

This marker measures the camera's GPU command stream at 480x270. It does not
measure display presentation, CPU simulation, the production HUD/equipment/input
pipeline or a complete GPU frame. The separate FrameTimingManager fields keep
their own availability and must not be mixed with camera measurements.

## Reproduction

Build the dedicated development player with
RelicKeeper.Editor.Combat.CombatDecorationAuditBuild.Build. Run
Builds/CombatDecorationAudit/CombatDecorationAudit.exe with:

    -mortfall-gpu-audit -screen-width 480 -screen-height 270 -screen-fullscreen 0

This selects room 006 / Circular Saw in Normal and Reduced modes, reusing the
previous audit's 41 authored enemies, active AI and documented stress settings.
It renders offscreen, records screenshots/data under Logs/MortfallGpuAudit and
exits automatically. The full -mortfall-combat-audit now records the same extra
camera timing fields for its twelve cases; its previous measurement artifact
is preserved.

A separate -mortfall-gpu-presented mode enables normal camera/screen rendering
in a visible interactive replay window, without the manual offscreen requests.
It saves under Logs/MortfallGpuPresentedAudit. Presented and offscreen results
must not be combined. A hidden profiled player keeps its loop running until its
quit request completes, instead of suspending after background state restoration.

Source evidence: the project's installed UnityEngine.CoreModule.xml documents
Recorder's GPU fields/delay; URP UniversalRenderPipeline.cs CameraMetadataCache
defines the marker, and Core ProfilingScope.cs creates its GPU-enabled sampler.
No external API assumptions or render-pipeline package changes are required.

## Final verification — 2026-10-29

Compilation and the Windows development build passed. **60 focused tests passed,
zero failed/skipped**, including six counter-validation cases for absent,
unsupported, nonpositive or ambiguous GPU data and nanosecond conversion.

Hidden Direct3D 11 and 12 runs exposed the native marker and reported hardware
GPU-recorder support but returned zero GPU blocks/timestamps. Those results stay
unavailable; they are preserved as [D3D11](CameraGpuTiming/offscreen-d3d11.json)
and [D3D12](CameraGpuTiming/offscreen-d3d12.json), not used for GPU conclusions.

**Normal screen presentation on Direct3D 11 returned valid samples from both
providers.** Both cases passed damage, pause and projectile-cleanup checks and
the visible replay exited automatically. Its 480x270 screenshot was inspected.

| Mode | Camera samples | Camera GPU median / p95 | FrameTiming samples | FrameTiming GPU median / p95 |
|---|---:|---:|---:|---:|
| Normal | 1364 | 0.413 / 0.716 ms | 1064 | 0.442 / 2.380 ms |
| Reduced | 1376 | 0.413 / 0.716 ms | 1066 | 0.442 / 2.733 ms |

Every admitted camera frame had exactly one GPU block; none were rejected in
the presented cases. [Raw presented measurements](CameraGpuTiming/presented-d3d11.json)
record the exact marker, backend, 41 enemies, counts and timings. Screenshots:
[Normal](CameraGpuTiming/normal.png), [Reduced](CameraGpuTiming/reduced.png).

This confirms that the earlier GPU-data gap was specific to the automated
offscreen path in this setup, rather than proof of unsupported hardware. It
provides a reproducible GPU capture path on this RTX 4070 Laptop GPU / i7-12700H.
It does not prove a Reduced-mode speedup: camera p95 was the same in both short,
differently timed AI encounters. No additional culling, balance or art changes
were justified. Other rooms, skills, resolutions/hardware, full equipment/HUD
and a detailed per-pass GPU-cost breakdown remain outside this targeted capture.

The profiling runs still emit existing diagnostic shutdown warnings concerning
the lazily created InputManager action maps. The temporary scene has no normal
InputManager lifetime owner; this report does not treat shutdown warnings as a
new gameplay regression or a measured per-frame leak.

Build-generated linker/settings files were cleaned, task-scoped diff checks
passed and the local code graph was refreshed. No scene edits were saved.
