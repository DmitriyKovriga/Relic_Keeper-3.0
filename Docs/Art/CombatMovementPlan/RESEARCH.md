# Combat/movement research notes

2026-10-27. Primary sources and official documentation; no third-party summary is used as technical authority.

## R1: Dead Cells: weapon feel — developer interview

Source: [Dead Cells: weapon feel — developer interview](https://www.gamedeveloper.com/design/designing-each-of-the-50-weapons-in-i-dead-cells-i-to-feel-distinctive)

Verification: read; direct developer statements, 2017 / Early Access.

The developers describe synchronized particles, stops, timing and impact sound, plus an iteration-friendly animation workflow.

Relic Keeper application: Design a coordinated contact package and family profiles. Do not copy historical frame/slowdown numbers as current Dead Cells constants.

## R2: Maddy Thorson — Celeste & Forgiveness

Source: [Maddy Thorson — Celeste & Forgiveness](https://www.maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

Verification: read; author primary source.

Forgiveness is about slightly widening timing/position opportunities in the player's favor.

Relic Keeper application: Audit existing coyote/buffer first; corner correction is an optional movement-rule change, not mandatory juice.

## R3: Sakurai — Breaking Down Attack Animations

Source: [Sakurai — Breaking Down Attack Animations](https://www.youtube.com/watch?v=LewXWM7HDd8)

Verification: official indexed title/description verified; full video/transcript unavailable in this research.

Official description identifies lead-ins, attacks and follow-throughs as the subject.

Relic Keeper application: Use as optional visual reference. No detailed numerical or frame-by-frame claim is attributed to an unwatched video.

## R4: Unity — Cinemachine 3.1 Impulse

Source: [Unity — Cinemachine 3.1 Impulse](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineImpulse.html)

Verification: read; official family docs resolve to 3.1.7; project remains 3.1.5.

A source emits a directional time-shaped signal; a listener applies camera response.

Relic Keeper application: Use installed Cinemachine facilities after checking local 3.1.5 API. No package upgrade is requested.

## R5: Unity 6.3 — ParticleSystem useUnscaledTime

Source: [Unity 6.3 — ParticleSystem useUnscaledTime](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystem.MainModule-useUnscaledTime.html)

Verification: read; official version-matched reference.

Particle simulation can use scaled or unscaled delta time.

Relic Keeper application: Choose deliberate behavior during hit-stop; separately obey actual menu pause, even with unscaled particles.

## R6: Unity 6.3 — ObjectPool

Source: [Unity 6.3 — ObjectPool](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Pool.ObjectPool_1.html)

Verification: read; official version-matched reference.

Instances can be reused to reduce repeated construction/destruction overhead.

Relic Keeper application: Reuse project pooling where compatible; active effect limits remain a separate requirement.

## R7: Juice It or Lose It — GDC catalog

Source: [Juice It or Lose It — GDC catalog](https://www.gdcvault.com/play/1016487/juice-it-or-lose)

Verification: catalog verified; full page redirects to maintenance; talk not watched here.

Catalog identifies Martin Jonasson and Petri Purho's feedback demonstration.

Relic Keeper application: Optional further viewing, not evidence for exact timing/pipeline recommendations.

## Local evidence

Graphify MCP was used for focused cross-file discovery, followed by direct reads of source, recipes, enemy data and tests. It is not the authority for serialized scene wiring. plan.json records file hashes and source locations.

No new gameplay video was supplied or captured for this task. Current feel and regressions must be assessed in Play Mode before implementation/tuning conclusions. The HTML schematic is authored explanatory motion, not a captured result.

Inference: the generic body/weapon phase split and busy-state hit reaction suppression are plausible contributors to the user-reported lack of body participation. This is a source-supported hypothesis, not a measured causal proof.
