# Combat and movement feel — research and implementation plan

Date: 2026-10-27. Source revision: `0283601f`.
Revision 2: user feedback applied. Status: **planning only**. Existing facilities below are verified by source and
serialized-data inspection. Proposals are not implemented or combat-balanced.
No new Play Mode capture, GPU profile or runtime test run was performed for this
task. The HTML demonstration is an abstract explanatory diagram, not game footage.
The user's report that atmosphere work is complete is accepted as context; the
previous atmosphere plan and its modified documents are not rebuilt or overwritten.

## Recommendation

Implementation follow-up (2026-10-28): first-slice work is implemented through
**crowd validation (steps 1–5)**; focused checks and their limits are linked below.
See [the observed baseline](Step01_Baseline.md) and
[confirmed enemy hits and the shared hit-stop gate](Step02_HitConfirmation.md).
The original research status above describes the planning task. C01's enemy-hit
path and the initial C18 per-cast hit-stop bound are implemented. C02/C04 now
add [body follow-through and enemy visual recoil](Step03_BodyAndRecoil.md), with
shared tuning and on/off previews. Broader C18 settings remain later work.

The [custom melee expansion](Step03_MeleeExpansion.md) also covers the ten
previously excluded melee skills, with 163 passing regression tests and twelve
live presentation cases. Ranged/casting body motion remains a separate slice.

The [C07/C10 target-flash slice](Step04_HitFlash.md) is also implemented, with
normal/heavy/bow flash-only comparisons, status/lighting preservation and
bounded rapid retriggers. [C09 hit-stop tuning and controlled crowd validation](Step05_CrowdAndHitStop.md)
now include real-time duration, a shared interval, a true zero-disable setting,
pause/cancellation/lifetime checks, 174 passing tests and 15 final live cases.
[C11 restrained heavy-hit camera feedback](Step06_CameraImpulse.md) is implemented:
saved EN/RU strength/off control, one attempt per cast, shared quiet interval,
whole-reference-pixel translation before the confiner, and pause/room/owner cleanup.
115 focused tests and eleven final live camera cases passed; live Settings previews
also passed in English and Russian.
[C12 player damage response](Step07_PlayerHurt.md) is implemented with a bounded
warm highlight, foot-anchored body recoil and defense/DoT filtering. 100 focused
tests and seven controlled live cases passed.
[C13 distinct successful-defense feedback](Step08_DefenseFeedback.md) is implemented
with PixelLab parry/evade/shield accents, bounded reusable renderers and separate
defense versus hurt semantics. 121 focused tests and twelve live cases passed.
[C14 fatal-hit/death handoff](Step09_LethalHits.md) is implemented: immutable fatal
direction, bounded heavy fragment impulse, living-feedback cleanup and rejection
of terminal repeat/reentrant hits. 142 focused tests and seven live death cases
passed. The C11–C14 presentation wave is complete.
[M01 ground movement weight](Step10_MovementWeight.md) is implemented with bounded
start/brake leans and turn settling, foot anchoring, existing action priorities
and no movement-physics changes. 110 focused tests and nine controlled live
presentation cases passed.
[M02/M03 landing strength and planted-foot dust](Step11_LandingContact.md) are
implemented after reviewing the existing class art and surface effects. Small
falls blend down the existing class landing; larger falls reach its authored
limit. Existing dust now uses the same physical contact and authored run-foot
markers, retaining surface density/stride limits. 151 focused tests passed,
followed by a final 15-test landing/surface rerun and eleven live cases.
The moving-landing follow-up removes the stationary recovery hold during
horizontal travel; 102 focused tests and fourteen final live cases passed.
M04 camera follow/look-ahead remains an optional comparison, not an automatic
change to movement or camera settings.
Full player-build GPU profiling is not claimed by the Editor measurements.

User review (2026-10-27): **remove the automatic generic contact effect**.
The extra mark overlaps existing skill VFX and adds visual clutter. Keep the
confirmed-hit signal and zero-damage hit-stop fix; use the skill's authored VFX
for the visible strike. Do not add a universal contact burst by default in C05.

Prioritize a **coherent impact**, not simply more particles: attacker preparation
and follow-through, a readable contact point and target visual recoil.
The first slice preserves current damage, movement,
hitboxes, attack windows, stun rules and cancellation rules.

Movement already has substantial mechanical and presentation work. Audit it in
motion and add selective start/stop/turn presentation before considering another
trail, landing cloud or jump mechanic. The user specifically identifies the
ordinary attacking body's lack of response; that is the highest-value gap.

## User decisions — revision 2

- Preserve independent weapon animation. The weapon/recipe remains authoritative;
  **C02 is procedural body-follow**, not a body clip driving the weapon.
- C03 authored attack key poses are excluded from the recommended path. The old
  idea remains archived under its stable ID, not an automatic future phase.
- C08 audio is explicitly deferred. No audio assets, dispatcher, voice budget,
  footstep sounds or mandatory placeholder audio hooks are in the current scope.
- C06 target-material variants are optional and unproven in return on maintenance.
  First use one universal contact package with direction and light/heavy intensity.
  Do not add a material enum, enemy catalog or material-by-element matrix now.
- Current packages must work without C03/C06/C08. Their records remain for discussion,
  with visible disposition; default HTML filtering shows the 21 current proposals.

## Source findings: what exists and what is missing

### Player body and weapon

- PlayerCharacterAnimationController has Idle/Run/Jump/Fall/Land state selection
  and explicit neutral-dodge and skill-spin ownership. It has no generic normal
  Attack body state. This does not mean the entire character is frozen during a
  cast: locomotion animation can continue, while ordinary attack body motion is absent.
- SkillHandAnimation samples weapon windup, strike and recovery. SkillStepRunner
  already supplies normalized phase and a speed-scaled TotalDuration. Use that
  clock for body presentation instead of a new unrelated animation timer.
- PlayerMovementVisual creates MovementSprite, copies source sprite/material/MPB,
  aligns feet, composes authored scales, jump/landing shaping, ground-dash shape,
  airborne lean and weapon grip offsets. Casting suppresses airborne lean,
  locomotion grip offsets and dash shaping. Adding a competing transform writer
  will lose to this LateUpdate or cause visible jitter.
- PlayerRenderAnchor moves body and weapon presentation together only during a
  camera render, with pixel rounding and Restore. Its rotation centre is the
  actor transform, **not a universally guaranteed foot pivot**. Grounded attack
  poses need an explicit foot/grip anchor decision; air poses must not snap to floor.
- WeaponHandAnimationAnchor already handles per-hand ownership and rest-pose
  restoration. Two hands can have separate presentation owners; the body needs
  one deterministic owner policy as well.
- SpinAround is a useful implemented precedent: one normalized step samples body,
  real weapon orbit and trail; it restores state on finish/cancel. Do not apply a
  new ordinary attack overlay on top of spin/neutral dodge/stasis.

### Enemies and impact events

- EnemyHealth.TakeDamage calculates mitigation and raises DamageTaken with
  **final positive Amount** and a DamageSnapshot. It also raises DamageDealt;
  subscribing to both would duplicate ordinary impact feedback.
- TakeDamage returns true after processing even when finalDamage is zero. Evasion
  returns false. Thus the bool is not sufficient for deciding to spawn flesh
  particles or a strong hit-stop. Some existing callers use that bool for hit-stop.
  Treat this as a feedback-semantics audit, not permission to rewrite damage rules.
- OnDamageReceived contains only an amount and also fires in ApplyPureDamage.
  Pure damage sets IsDirectHit=false. Use explicit direct/DoT/outcome classification;
  do not attach full physical-impact feedback to every damage notification.
- GameplayEventContext has Source/Target/Damage/Amount/Position. Default Position
  is target.transform.position, **not a collision contact**. DamageSnapshot.HitOrigin
  is used for horizontal pushback and is not a surface normal or an exact hit point.
  Per-target contact data should not be written into a mutable snapshot shared by
  several targets. Add optional feedback context or a presentation-only payload.
- EnemyHealth.TryPlayHitReaction skips busy attacks and special actions;
  EnemyAnimationBridge also rejects busy attacks and active transient states.
  Therefore damage can be accepted while the enemy shows no hit clip. A short
  cosmetic recoil can fill that gap without turning every hit into a gameplay stun.
- Hit-clip references differ by enemy. Mold and SkeletonScorp have explicit
  sprite-sheet paths. Knight and Dummy have empty hit paths and null controllers.
  Zombie uses an Animator controller and a declared Zombie_Hit state; runtime
  availability of its actual state was not validated in this planning task.
- EnemyEntity.VisualRoot is a child for sheet-driven enemies but can be the
  **physical actor root** for Animator-driven enemies. Never blindly shake or
  scale VisualRoot. EnemyAnimationBridge also writes visual localPosition for foot
  registration, so offsets need composition after that baseline, not competing writes.
- PushbackResolver/EnemyLocomotion2D and EnemyStunController already exist.
  Cosmetic recoil must not change them, colliders, targeting or attack impact timing.

### Hit-stop, death effects, movement and camera

- SkillHitStopGate is normally shared by hits/projectile clones of one cast.
  HitStopService currently clamps values to **1–5 rendered frames**, globally
  sets Time.timeScale=0 and restores it with pause checks. It is not a missing feature.
- Raw serialized asset inventory: 72 assets; 27 with value 0, 26 with value 1, 3 with value 2, 14 with value 3, 1 with value 4, 1 with value 5. These counts cover assets
  containing the field, not proof that every recipe is in an active skill pool.
  Zero is currently clamped to one, not an off switch. HitStopTests explicitly
  encode that behavior. Other tests expect authored values in 1–5; verify loaded
  content before a future rollout rather than claiming this suite is currently green.
- Rendered frames have different wall-clock durations at 30/60/120+ FPS. The
  project's FrameRateLimiter enables VSync and sets a 60 FPS fallback; VSync does
  not establish a universal 60-Hz display. Persistent-hit paths can request the
  service separately from the common per-cast gate. Audit crowd/high-APS behavior.
- EnemyDeathEffectSpawner already emits flying source chunks, blood/remains,
  ground decals and wall spatter. EnemyDeathVfxPool/EnemyDeathRemainsSheet supply
  room-owned reuse and budgets (96 active fragments, 180 decals in checked code).
  This does **not** establish an affordable living-hit particle system: per-hit
  effects occur far more often and should not spawn these physical death chunks.
- Existing movement includes acceleration/deceleration/reversing, jump buffering
  (default 0.12 s), coyote time (0.10 s), two jumps, dash-jump, fast fall, fast-fall
  bunny-hop and drop-through intent handling. Defaults are source values, not a
  guarantee of live per-prefab values. Do not propose implementing these again.
- Existing movement visuals include authored class animations, air lean, dash
  shaping, dodge poses, shadows and surface-specific earth/stone effects. The
  surface system already scales landing dust by downward speed and bounds it.
  A new body landing accent should reuse that signal rather than duplicate dust.
- In the saved camera composer, Damping is zero and Lookahead is disabled.
  Cinemachine 3.1.5 is installed. An existing generic combat-shake or hit-audio
  dispatch path was not found in the inspected combat/skills/visuals code. This
  is a bounded search result, not a claim that the entire game has no audio.

## Research synthesis and limits

The developer interview about Dead Cells describes particles, stop/slowdown,
animation timing and impact sound as a combined source of weapon weight. It also
describes making repeated animation iteration affordable. This plan takes the
coordination/workflow lesson, not a historical freeze number or that game's 3D
animation pipeline. The interview is from 2017, not documentation of current
Dead Cells balance. [Developer interview](https://www.gamedeveloper.com/design/designing-each-of-the-50-weapons-in-i-dead-cells-i-to-feel-distinctive).

Maddy Thorson's first-person account of Celeste presents timing/position
forgiveness as a deliberate part of responsiveness. Applied here, the immediate
task is to validate existing windows under hit-stop and collision transitions;
corner correction remains a separately scoped mechanics proposal.
[Celeste & Forgiveness](https://www.maddymakesgames.com/articles/celeste_and_forgiveness/index.html).

Official Unity documentation confirms installed-family Impulse source/listener
facilities and the choice between scaled and unscaled particle simulation.
These facilities do not establish this project's performance budget or exact
best tuning. Existing project pooling should be reused where compatible; a pool
does not independently cap active effects.

The Sakurai and Juice It or Lose It talks are listed as optional visual references.
Only available official metadata/catalog was checked: full video review was not
available. No specific frame counts or unseen techniques are attributed to them.
See RESEARCH.md for each source's verification status and project application.

## Option matrix and estimate scope

Gain 1–5 is expected perceptual impact, not a measured user score. Setup and
maintenance 1–5 increase with difficulty. Prototype estimates are working days
for an experienced developer on **one class, one light and one heavy attack,
one projectile, two ordinary enemies and a dummy**. New art/audio acquisition,
full content rollout and platform certification are additional. Rows share
infrastructure and must not be blindly summed.

Maintenance 1: common tuning/tests; 2: reusable family profiles; 3: recurring
integration or mixing; 4: substantial new animation/gameplay variants. CPU/GPU
cost is a different dimension and must be measured. The final column marks
features that intentionally change mechanics, not only presentation.

| ID | Proposal | Gain / 5 | Setup / 5 | Maintenance / 5 | Prototype days | Changes mechanics |
|---|---|---:|---:|---:|---|---|
| C01 | Один подтверждённый сигнал попадания | 4 | 3 | 2 | 1–2 | No intended rule change |
| C02 | Процедурный follow корпуса за атакой оружия | 5 | 3 | 2 | 2–4 | No intended rule change |
| C03 | Нарисованные ключевые позы атаки | 5 | 4 | 4 | 3–6 + существенный арт | No intended rule change |
| C04 | Короткая отдача тела противника | 5 | 3 | 2 | 1–3 | No intended rule change |
| C05 | Направленный выброс частиц из места удара | 5 | 3 | 2 | 1–3 + арт | No intended rule change |
| C06 | Необязательные варианты по материалу цели | 2 | 2 | 3 | 1–2 + арт | No intended rule change |
| C07 | Чёткий короткий flash попадания | 4 | 2 | 2 | 0.5–1.5 | No intended rule change |
| C08 | Слои звука: взмах, контакт, материал | 5 | 3 | 3 | 2–4 + звук | No intended rule change |
| C09 | Настроить уже существующий hit-stop | 4 | 3 | 2 | 1–2 | No intended rule change |
| C10 | Иерархия обычного, тяжёлого и критического удара | 4 | 2 | 2 | 1–2 | No intended rule change |
| C11 | Небольшой импульс камеры для редких сильных событий | 3 | 3 | 2 | 1–2 | No intended rule change |
| C12 | Ясная реакция героя на полученный урон | 4 | 3 | 2 | 1–3 | No intended rule change |
| C13 | Отдельный отклик парирования и защиты | 4 | 3 | 2 | 1–2 + арт | No intended rule change |
| C14 | Согласовать попадание и смерть | 3 | 3 | 2 | 1–2 | No intended rule change |
| C15 | Игровое прерывание/пошатывание по правилам | 4 | 4 | 4 | 3–6 | YES |
| C16 | Буфер следующей атаки и правила отмены | 4 | 4 | 4 | 3–5 | YES |
| C17 | Настоящий перенос веса шагом/микровыпадом | 4 | 4 | 4 | 3–5 | YES |
| C18 | Лимиты эффектов и спокойный режим | 4 | 3 | 2 | 1–3 | No intended rule change |
| M01 | Поза старта, торможения и разворота | 4 | 3 | 2 | 1–3 | No intended rule change |
| M02 | Интенсивность реакции тела на посадку | 3 | 3 | 2 | 1–2 | No intended rule change |
| M03 | Контакт стопы и пыль в одном ритме | 3 | 3 | 2 | 1–2 | No intended rule change |
| M04 | Направление взгляда камеры и спокойное сопровождение | 3 | 3 | 2 | 1–2 | No intended rule change |
| M05 | Проверить удобство уже существующих окон ввода | 3 | 2 | 1 | 0.5–1.5 | No intended rule change |
| M06 | Мягкая коррекция зацепления за угол | 3 | 4 | 3 | 2–4 | YES |

## First slice and dependency order

1. **Observe, do not retune everything.** Capture one ordinary melee attack at
   normal speed and slowed inspection, with a miss, a valid hit, an evasion,
   zero final damage, a lethal hit and a DoT tick. Include an attacking enemy.
   Log the accepted-cast token, recipe phase, hit resolution and body/weapon pose.
2. **C01 semantics + initial C18 bounds.** Pick one post-mitigation confirmation
   path; define direct hit/contact outcome and a fallback position. Prove that
   one event cannot duplicate presentation through DamageTaken + DamageDealt.
   Carry shared attack-intensity hints and a per-cast hit-stop gate. Keep the
   skill's authored VFX; the generic contact mark is excluded after user review.
3. **C02 body + C04 target.** One normal slash and one heavier strike. Compose
   with current movement/weapon ownership. Keep all gameplay transforms and
   recipe hit windows unchanged. Validate standing, running, airborne, facing
   both ways, offhand overlap, cancel, dodge, stasis and spin before adding more art.
4. **C07/C10 skill-aware contact.** Compare a restrained target flash and preserve
   the skill's existing contact art. C05's automatic universal burst is excluded
   after user review. No sound assets, dispatcher or target-material catalog.
   Extend to one ranged hit so presentation is not melee-only. C09 tunes the existing stop.
5. **Crowd validation.** Repeat with the highest plausible APS, several targets,
   persistent pulses and projectile return hits. Enforce active/retrigger
   limits; do not hide real target confirmations by delaying gameplay damage.
6. **Second wave.** C11 camera, C12 player damage, C13 visual defense outcomes
   and C14 death integration. Camera shake is optional and has a zero-intensity
   setting. C03 stays excluded; C08 is a later audio task. C06 is reconsidered
   only after a small comparison proves a worthwhile difference over universal FX.
7. **Movement review.** M01 first, then M02/M03 if the live comparison shows a
   gap. M04 is an A/B camera test. M05 validates what exists. C15/C16/C17/M06 are
   explicit later gameplay changes, not bundled into the cosmetic rollout.

Proposed package membership is in plan.json and the HTML. A subsequent request
authorizes whichever slice the user selects; this plan does not modify gameplay
and introduces no redundant approval gate after an explicit implementation request.

## Attacker body design

### Low-maintenance first version: phase-driven presentation

- Start with one shared body-follow envelope and bounded light/heavy amplitudes.
  Add a style-specific direction/axis only when a slash, thrust or release needs
  it. No body clip, per-weapon key pose or body-animation dependency.
- Sample the same normalized step phase that already drives WeaponWindup,
  WeaponSwing/its specific style and WeaponRecovery. Do not re-time damage to
  match a cosmetic overlay. Attack Speed and actual step durations stay authoritative.
- Keep the existing ground/air locomotion sprite under the overlay. A small
  backwards preparation, forward shoulder motion and bounded return creates
  participation without requiring torso/leg layer separation on day one.
- At 24 PPU, one source pixel is 1/24 world unit. Prototype offsets in logical
  pixels, not display pixels, and inspect native resolution. A few degrees or
  1–2 source pixels can be a starting experiment, not a shipped constant. Avoid
  strong sprite rotation/stretch that shimmers or looks rubbery.
- Keep separate body and weapon output transforms. Weapon windup/swing/recovery
  stays unchanged and does not consume the new body-follow offset. The body samples
  the same phase and a restrained follow-through envelope. Do not drive weapon
  animation from a new body pose or rotate both with the existing shared
  PresentationLeanDegrees just to create the attack-follow effect.
- Compose the body-only contribution in the current presentation owner. Compare
  the visible weapon's pose/path at identical phases with body-follow on/off;
  it must be identical apart from already-existing movement/camera transforms.
- Grounded feet and grip need native-resolution checks. A full rigid sprite
  cannot promise independent shoulder motion with two perfectly fixed anchors.
  Keep amplitudes small; reduce or disable an unsuitable follow profile rather
  than moving the weapon, root or hitbox to conceal an art mismatch. An airborne
  cast keeps its existing air baseline instead of snapping toward the floor.
- One ordinary body owner token: the latest accepted attack with a valid body
  profile may acquire it. Ending an older cast cannot reset the new one. Explicit
  spin/neutral-dodge/stasis states retain priority; unsupported utility skills
  are neutral rather than forcing a weapon strike pose. Per-hand weapon ownership
  remains separate. Test this policy before supporting overlapping equipment casts.
- Projectile recoil is tied to **release**, not to every remote hit afterwards.
  Target impact is tied to accepted contact. Melee misses still have follow-through
  but produce no target burst or target recoil. Audio is deferred.

### Archived alternative: authored poses (C03)

This is no longer part of the recommended plan. The user's intended architecture
keeps weapon attacks independent for flexibility; authoring body-specific strike
poses would introduce grip, class and ground/air variants. Existing SpinAround
frames remain an implemented special-case precedent for ownership/cleanup only,
not a request to expand full-body animation to ordinary attacks. Current work
requires no new body art, rig, torso/leg separation or weapon-driven body clip.

## Enemy response and particle recipe

- First layer is a small **visual-only** receiver response, including while
  EnemyAttackController is busy. It must not cancel the attack, modify its
  impact frame or imply an interruption that did not happen. Heavy/boss responses
  can be small compression/flash without a large translation.
- Preserve EnemyAnimationBridge's foot-registration baseline. For a sheet
  enemy, compose an offset under/after it. For an Animator root sprite, introduce
  a safe display/anchor route before moving the renderer; never scale the root
  collider. A child-only pilot must not be described as complete enemy coverage.
- A contact burst is two parts: a very brief readable mark plus a few secondary
  flecks. Flecks use world-space motion and low-cost ballistic curves/particles,
  not a Rigidbody2D each. Optional floor residue is rare and budgeted.
- Suggested **pilot**, not final limits: light 3–5 flecks, normal 5–8, heavy
  8–12; roughly 0.12–0.25 s for the primary secondary burst. Tune at 480×270.
  Critical/heavy priority changes shape and bounded intensity, not unlimited
  particle count. Screen-wide attacks use reduced per-target density.
- Primary direction comes from actual projectile velocity or authored melee
  sweep/strike direction. HitOrigin can provide a coarse fallback; bounds-centre
  fallback is explicit when contact is unknown. No random radial shower for
  every hit. For this revision all targets use the same universal package.
  Do not author material/element mappings. Mixed damage does not create several
  full bursts. The optional C06 proposal is outside the implementation packages.
- Do not mutate a shared DamageSnapshot with one target's contact or final
  damage. If enriching GameplayEventContext, use optional fields/payload and keep
  event/save contracts stable. No save schema change is required for visual-only
  transient context. Source weapon/cast identity may need explicit forwarding;
  do not infer it from the currently equipped weapon after a projectile was fired.
- Receiver death can destroy its GameObject immediately. A spawned effect owns
  captured pose/palette/direction, not a dependency on the destroyed target.
  Keep ordinary contact and existing death burst from doubling each other's budget.

## Timing, pause, crowd limits and accessibility

Use distinct budgets for target pose retrigger, visible particles, flash and
camera. Audio voices are explicitly outside this revision. Per-cast/per-target policy must preserve intentional gameplay
multi-hits while limiting only feedback density. A pool can expand unless there
is an active budget; use the existing death-budget experience without treating
its long-lived fragments as cheap per-hit particles.

Spawn an initial impact pose/mark visibly on the confirmation frame. By default,
scaled body/particle progression can hold during current hit-stop and resume
with the action. If a specific spark uses unscaled time, explicitly honor the
real pause menu and application lifecycle. Shake pause policy also needs an explicit choice; audio is deferred. Freeze is not permission for background coroutines to restore
Time.timeScale=1 while a menu remains open.

Current hit-stop overlaps extend the longest remaining freeze, not a sum. A high
rate of separate requests may still keep global time frozen. Measure frame-stop
wall time at 30/60/120/144 Hz, not only at a nominal 60 FPS. A true off switch
requires an explicit enable policy because current ClampFrames(0)==1. Changing
to millisecond-based stop is a separate reviewed change; do not silently reinterpret
all existing serialized recipe values. Avoid a second hit-stop/time owner.

Reduced-motion/intensity settings should remove camera shake and reduce flash
while retaining contact-shape confirmation. Reuse the existing settings
system after inspection. UI stays stable; no full-screen flash on every critical
hit. High-crit and high-APS builds should remain readable.

## Small-slice verification and handoff targets

Expected existing touchpoints (not a request to edit all of them):

| Concern | Reuse / affected locations |
|---|---|
| Accepted cast and shared phase | SkillBehaviour, SkillStepRunner, SkillHandAnimation, SkillRecipeSO |
| Body and held equipment composition | PlayerCharacterAnimationController, PlayerMovementVisual, PlayerRenderAnchor, WeaponHandAnimationAnchor |
| Confirmed target outcome | EnemyHealth, PlayerDamageReceiver, GameplayEventContext/Bus |
| Target presentation baseline | EnemyEntity, EnemyAnimationBridge, SpriteFxController |
| Movement and surface events | PlayerMovement, GroundingVisualController, GroundSurfaceProfileSO |
| Existing pause and stop ownership | SkillHitStopGate, HitStopService, GamePauseService, FrameRateLimiter |
| Existing pools and death | PoolManager, EnemyDeathEffectSpawner, EnemyDeathVfxPool, EnemyDeathRemainsSheet |
| Camera | installed Cinemachine Impulse, saved composer/confiner, Pixel Perfect |

Data lives in a small shared attack-intensity/follow profile or existing appropriate
authoring data; not constants in Update and not one new renderer/material per hit.
No external feedback framework, package update, new assembly definition, or save
migration is proposed. Existing serialized field/API identities remain stable.

Observable acceptance cases:

1. Same melee cast with a miss, positive direct hit, evade, zero final damage,
   DoT and lethal hit: exactly the appropriate package, no duplicate strong impact.
2. Body and weapon share preparation/strike/recovery under low/high Attack Speed;
   damage timing is unchanged. Body-follow on/off does not change the weapon
   render trajectory or hand-animation ownership. Airborne and moving casts preserve their baseline.
3. Main/offhand overlap, equipment swap, cancel, dodge, stasis, spin, disable,
   death/respawn and room transitions restore pose once, with no drift/orphan owner.
4. Transform/rotation/scale of player and enemy physical roots and hitbox reach
   remain unchanged by C02/C04. Test a sheet enemy and an Animator-root enemy.
5. An enemy visibly receives a normal hit while completing its own telegraph and
   attack. Only existing real stun/knockback mechanics interrupt or move physics.
6. A projectile fired before a weapon swap keeps its original feedback identity;
   return hits and chains respect their actual per-hit history. New visuals do
   not change hit-once memory, damage rolls, status application or life-on-hit.
7. Repeated high-APS/multi-target/persistent hits stay bounded, and a lethal
   contact does not double-spawn a full death effect. Pools reset sprites, tint,
   orientation, simulation state, owner references and pending callbacks.
8. Hit-stop at different display rates, menu opened during freeze and cancellation
   during freeze do not stick time, replay input twice or lose buffered input.
9. M01/M02/M03 do not add input lag, mandatory landing recovery or duplicate
   surface puffs. Jump can interrupt presentation recovery as current rules permit.
10. Compare clips with effects off, body-only, target-only, contact-only, combined visual
    stack and reduced-motion. Conduct a small blind preference test with the
    same weapon/scene; do not infer success from particle count.
11. Profile a named target machine in sparse and crowded rooms: frame time,
    active renderers/particles, allocations and overdraw. No FPS
    guarantee or universal budget is established by this source-only plan.

Existing relevant tests to select **when implementation begins**: PlayerRenderAnchorTests,
DualWieldHandAnimationTests, SpinAroundTests, HitStopTests, PushbackResolverTests,
EnemyDeathRemainsTests, SurfaceMovementTests, PlayerMovementTests and class
movement-animation tests. Add focused regression cases for actual changed
behavior, use Unity Bridge compile + narrow nonzero EditMode runs, then manual
Play Mode capture. This task does not claim the current suites were run/passed.

## Detailed proposals

### C01 — Один подтверждённый сигнал попадания

Объединить точку контакта, направление, фактический урон и универсальный профиль визуального удара. Звук и каталог материалов в этот этап не входят.

- Example: Промах и уклонение не создают кровь. Ядовитый тик не притворяется новым ударом меча.
- Current state / reuse: частичная основа. GameplayEventBus.DamageTaken уже содержит итоговый Amount и DamageSnapshot. Точная точка/нормаль контакта в общем событии пока отсутствуют.
- Ongoing work: Один адаптер для каждого пути урона: melee, projectile, persistent/beam. Профили общие, без копий на каждый скилл.
- Main risk: TakeDamage возвращает true даже при нулевом итоговом уроне; OnDamageReceived не различает прямой удар и DoT. Эти сигналы нельзя слепо считать подтверждением сильного попадания.

### C02 — Процедурный follow корпуса за атакой оружия

Оружие остаётся главным и анимируется как сейчас. Тело получает небольшой дополнительный наклон/сдвиг по фазе замаха, удара и возврата, с коротким затуханием. Новые кадры тела не требуются.

- Example: В замахе тело чуть отводится, в ударе следует вперёд, после завершения догоняет и возвращается. Промах сохраняет follow. Траектория оружия от этого не меняется.
- Current state / reuse: сохраняем раздельную архитектуру. SkillStepRunner уже знает фазу, SkillHandAnimation — стиль. В PlayerMovementVisual / PlayerRenderAnchor добавить body-only вклад; не использовать общий наклон, который также поворачивает оружие.
- Ongoing work: Начать с одной общей кривой follow и амплитуды лёгкого/тяжёлого удара. Вариант направления по стилю добавлять только при необходимости. Проверять три класса и обе стороны.
- Main risk: Амплитуда мала: проверять стопы и хват. Если на цельном спрайте возникает разрыв, уменьшить follow; не подгонять траекторию оружия и не добавлять ключевые кадры. Root и хитбоксы сохраняют положение.

### C03 — Нарисованные ключевые позы атаки

Отдельные кадры подготовки, удара и завершения для семейств атак. Дают более убедительную работу плеч, таза и рук, чем поворот всего спрайта.

- Example: Воин переносит вес в рубящем ударе, рога собирается в укол, маг сопровождает выпуск заклинания жестом.
- Current state / reuse: есть для SpinAround. Опыт классовых кадров SpinAround и нейтрального уклонения. Сначала один класс и одна атака.
- Ongoing work: Новые классы, скины, движущиеся/воздушные атаки и хват оружия требуют новых проверок или кадров.
- Main risk: Исключено из рекомендуемого пути после обсуждения: отдельные ключевые кадры создают лишние требования к хвату и вариантам оружия. Используем C02; это не обязательный следующий этап.

### C04 — Короткая отдача тела противника

Небольшое смещение, сжатие или наклон видимого тела от направления удара с быстрым возвратом. Это реакция изображения, а не постоянный нокбэк.

- Example: Рыцарь чуть принимает удар плечом, скелет дёргается, плесень сминается. Во время атаки враг продолжает действие, но удар виден.
- Current state / reuse: частичная основа. EnemyAnimationBridge и EnemyEntity.VisualRenderer. Нужен отдельный визуальный слой композиции, не перенос gameplay root.
- Ongoing work: Несколько весовых профилей: лёгкий, обычный, тяжёлый/босс. Повторные удары объединять, а не бесконечно складывать смещение.
- Main risk: У Animator-врага VisualRoot может быть самим физическим root. Вращать его напрямую нельзя. Резкая отдача не должна выглядеть как отменённая атака.

### C05 — Направленный выброс частиц из места удара

Короткий ударный силуэт и небольшой конус осколков/брызг вдоль импульса. Источник находится на поверхности цели, а не всегда в её центре.

- Example: Укол даёт узкий вылет, рубящий удар — веер, тяжёлый — более плотный короткий выброс. Обычное попадание заметно слабее смерти.
- Current state / reuse: нет общего контура. Пул существующих префабов и общие спрайты частиц; рецепты могут иметь свои VFX, их надо учесть без удвоения.
- Ongoing work: Небольшой набор форм и профилей; один профиль обслуживает много врагов и оружия.
- Main risk: Не создавать Rigidbody2D и коллайдер на каждый пиксель. На тиках луча/DoT и множестве целей нужны лимиты.

### C06 — Необязательные варианты по материалу цели

Разделить материал врага и тип урона. Плоть, броня, кость, растение/слизь дают разные частицы и звуки, элемент добавляет небольшой акцент.

- Example: Броня — искры и звон; кость — светлая крошка; плесень — волокна/споры. Огонь не заменяет материал цели огромным взрывом на каждом тике.
- Current state / reuse: выгода пока не доказана. Опциональный общий impact-профиль на EnemyDataSO или переиспользуемой визуальной конфигурации; разумный нейтральный fallback.
- Ongoing work: Один выбор материала при создании врага. Не делать матрицу всех сочетаний враг × оружие × элемент.
- Main risk: Отложено до сравнения с универсальным откликом. Сначала один общий эффект; материал добавлять только при заметном выигрыше на небольшой паре целей. Не обязательная система и не этап по умолчанию.

### C07 — Чёткий короткий flash попадания

Коротко подчеркнуть силуэт цели в момент удара, сохранив исходную палитру между попаданиями. Сильный белый всплеск оставить редким случаям.

- Example: Слабый светлый импульс сразу сообщает, что удар дошёл, даже если враг не прерывает атаку.
- Current state / reuse: шейдерная основа есть. SpriteFxController.Flash существует. Применять к реально видимому renderer и сохранить текущий материал/lighting/status MPB.
- Ongoing work: Общие уровни light/normal/heavy и минимальный интервал повторного flash.
- Main risk: Не заменить lit-материал старым unlit и не перебить freeze/poison/level-up. Долгое белое тело скрывает анимацию врага.

### C08 — Слои звука: взмах, контакт, материал

Разделить звук движения оружия и подтверждённого контакта. Добавить несколько вариантов, ограниченную вариативность и приоритет сильного удара.

- Example: Промах оставляет только свист; попадание добавляет удар; броня — короткий звон. Тяжёлый удар отличается огибающей, а не только громкостью.
- Current state / reuse: общий контур не найден. В проверенных combat/skills/visuals исходниках общий hit-audio dispatcher не найден; сначала небольшой общий проигрыватель с voice limit.
- Ongoing work: Нужны качественные исходники и микс для новых семейств оружия/материалов. В толпе управлять числом голосов.
- Main risk: Пользователь явно отложил звук на поздние этапы. Сейчас не добавляем ни клипы, ни диспетчер, ни обязательные аудиохуки. Карточка сохранена как будущая задача.

### C09 — Настроить уже существующий hit-stop

Проверить силу и частоту остановки для лёгких, тяжёлых и многократных атак; согласовать момент остановки с видимым контактом.

- Example: Один небольшой акцент на удачном тяжёлом попадании; серия быстрых ударов не превращает игру в непрерывную паузу.
- Current state / reuse: уже реализован. SkillHitStopGate и HitStopService уже работают. Основной gate общий на каст, некоторые persistent-пути запрашивают остановку отдельно.
- Ongoing work: Профили семейств и тесты на разные частоты кадров, multi-hit и pause/menu.
- Main risk: Сейчас 0 зажимается в 1, а единица — rendered frame, не фиксированные миллисекунды. Новая схема отключения/длительности требует осознанного изменения, а не ещё одного сервиса.

### C10 — Иерархия обычного, тяжёлого и критического удара

Отличать силу контакта размером силуэта, ритмом и короткой реакцией. Крит не обязан быть самым большим взрывом в комнате. Звук для этой версии не требуется.

- Example: Обычный удар — компактная точка, тяжёлый — более плотная форма, крит — отдельный штрих.
- Current state / reuse: есть crit-данные. DamageSnapshot.IsCrit и авторский вес атаки. Итоговый DPS не использовать как неограниченный множитель числа частиц.
- Ongoing work: Три общих уровня с ограниченной поправкой. Билды с высокой скоростью и критами проверять отдельно.
- Main risk: Большое сырое число урона не всегда означает тяжёлую атаку. Только цвет не должен нести всё различие.

### C11 — Небольшой импульс камеры для редких сильных событий

Короткий направленный сдвиг для тяжёлого удара, парирования или убийства элиты. Мелкие тики оставляют камеру спокойной.

- Example: Удар молота кратко толкает композицию вдоль импульса, после чего камера точно возвращается.
- Current state / reuse: Cinemachine уже есть. Cinemachine 3.1.5 Impulse; не писать второй скрипт, напрямую двигающий Main Camera.
- Ongoing work: Один общий лимит наложения и настройка интенсивности/полного отключения для игрока.
- Main risk: Проверить Pixel Perfect, parallax, confiner и маленькие снаряды. Сильный непрерывный shake снижает точность платформинга.

### C12 — Ясная реакция героя на полученный урон

Короткий визуальный отклик собственного урона. Игрок быстро отличает свой урон от удачного попадания по врагу; звук отложен.

- Example: Лёгкое сжатие/отдача изображения, читаемый flash; уже выполняемое действие не отменяется косметикой.
- Current state / reuse: события урона есть. PlayerDamageReceiver и GameplayEventBus; композиция с PlayerMovementVisual, dodge/spin/stasis.
- Ongoing work: Проверять защитные состояния, три класса и сочетания с собственными атаками.
- Main risk: Нельзя выдавать обычный hurt-эффект за успешное уклонение/парирование. Новый hitstun и потеря управления сюда не входят.

### C13 — Отдельный отклик парирования и защиты

Свой компактный знак и реакция для парирования, уклонения и расхода мистического щита. Аудиослой позже.

- Example: Успешный парри — резкая искра в точке защиты; это не кровь игрока и не обычный hit flash.
- Current state / reuse: механики и события есть. GameplayEventType.Parried, Evaded и MysticShieldConsumed. Сохранить текущие окна и ответный урон.
- Ongoing work: Общие профили защитных исходов; новые щиты назначают профиль, не собственную систему.
- Main risk: Сигнал защиты не должен дважды усиливать один удар через DamageTaken и Parried.

### C14 — Согласовать попадание и смерть

Дать смертельному попаданию ясный финальный акцент и направление, согласованное с текущим разлётом. Избежать двух одинаковых полных всплесков.

- Example: Последний удар запускает короткий контакт, затем существующую смерть; обычный удар не создаёт трупный разлёт.
- Current state / reuse: богатая смерть уже есть. EnemyDeathEffectSpawner, EnemyDeathVisualFactory, EnemyDeathVfxPool и EnemyDeathRemainsSheet уже есть.
- Ongoing work: Два согласованных профиля: контакт и смерть. Сброс и уборка принадлежат текущей комнате.
- Main risk: Не переиспользовать дорогие физические фрагменты смерти на каждом попадании. На убийстве цель может уничтожиться до завершения VFX.

### C15 — Игровое прерывание/пошатывание по правилам

Если это нужно дизайну, отдельно задать настоящие пороги прерывания для лёгких, тяжёлых врагов и боссов.

- Example: Тяжёлое попадание прерывает подходящего лёгкого врага, но быстрый кинжал не удерживает любого босса бесконечно.
- Current state / reuse: стан и pushback уже есть. EnemyStunController, EnemyAttackController, PushbackResolver; не подменять существующие системы косметикой.
- Ongoing work: Потребуется баланс каждого семейства врагов, высокая скорость атаки, multi-hit, freeze и статусы.
- Main risk: Это меняет сложность, окна контратаки и ценность билдов. Не включать автоматически ради джуса.

### C16 — Буфер следующей атаки и правила отмены

При выявленных потерях ввода сохранить одно намерение следующего действия на короткое окно; явно определить отмену.

- Example: Раннее нажатие следующего удара не теряется, но очередь из старых кликов не заставляет героя атаковать после ухода игрока.
- Current state / reuse: нужна отдельная проверка ввода. PlayerAttackInput, PlayerSkillManager.TryUseSkill и текущие касты/каналы. Проверить имеющееся удержание кнопки прежде, чем добавлять очередь.
- Ongoing work: Проверять hold/tap, две руки, смену оружия, канал, mana/cooldown и паузу.
- Main risk: Отмена recovery меняет DPS и безопасность. После уклонения/смены оружия старое намерение должно исчезать.

### C17 — Настоящий перенос веса шагом/микровыпадом

Отдельным решением добавить движение физического тела в некоторых атаках. Это более дорогая альтернатива чисто визуальной подаче.

- Example: Тяжёлый укол продвигает героя на небольшое расстояние; на краю платформы правила движения остаются предсказуемыми.
- Current state / reuse: есть движения внутри отдельных скиллов. SkillMovementControl, PlayerImpulse и конкретные рецепты. Не глобальный сдвиг для любого оружия.
- Ongoing work: Стены, края, one-way платформы, воздух, скорость и дальность атак требуют регрессии.
- Main risk: Меняет досягаемость и позицию хитбоксов. Для первого среза достаточно C02 без физического шага.

### C18 — Лимиты эффектов и спокойный режим

Ограничить одновременно активные частицы, flash и shake. Сохранять важные попадания, удешевлять повторные. Аудиобюджет сейчас не реализуем.

- Example: В толпе каждое попадание читается, но десять целей не дают десять полных вспышек и десять остановок.
- Current state / reuse: есть лимиты смерти. Текущий PoolManager и опыт room-owned death-бюджета; общий небольшой policy для живых попаданий.
- Ongoing work: Два-три уровня качества, один набор пользовательских настроек и stress-проверка новых multi-hit скиллов.
- Main risk: Пул не равен лимиту активных объектов. Unscaled-анимация должна останавливаться в настоящем меню паузы.

### M01 — Поза старта, торможения и разворота

Небольшая визуальная инерция корпуса на резком разгоне и остановке, короткий перенос веса при смене направления.

- Example: При развороте на земле плечи и корпус догоняют направление; коллайдер и действующий ввод не получают задержку.
- Current state / reuse: физическое ускорение уже есть. PlayerMovement сообщает скорость и ввод; добавить вклад в текущую композицию PlayerMovementVisual.
- Ongoing work: Несколько ограниченных кривых; согласовать приоритет с атакой, dodge и freeze.
- Main risk: Чрезмерное запаздывание выглядит как скольжение. Положение оружия должно оставаться связанным с хватом.

### M02 — Интенсивность реакции тела на посадку

Согласовать с силой посадки именно тело; звук отложен. Не добавлять ещё одно облако поверх готовой surface-системы.

- Example: Небольшой спад — почти без акцента; сильное падение — чуть более заметная компрессия и быстрое восстановление.
- Current state / reuse: пыль уже зависит от скорости. OnLanded, сохранённая скорость падения, готовые классовые land-кадры и GroundSurfaceProfileSO.
- Ongoing work: Один ограниченный профиль по классу/весу; проверка быстрого следующего прыжка.
- Main risk: Управление не ждёт завершения картинки. Рывок, падение через платформу и начальный grounded не должны давать ложную посадку.

### M03 — Контакт стопы и пыль в одном ритме

Согласовать читаемый контакт стопы с существующей пылью. При необходимости поправить cadence, без добавления шаговых звуков.

- Example: Пыль не вылетает раньше, чем нога визуально опирается на пол; для камня и земли остаются готовые surface-пресеты.
- Current state / reuse: surface-пыль и run-кадры есть. GroundingVisualController уже использует surface-пресеты и дистанционный stride. Сначала сравнить с фазами run-кадров.
- Ongoing work: Точки опоры в новых run-анимациях и существующие presets поверхности.
- Main risk: Не дублировать готовую пыль. Аудиосистему ради этой проверки создавать не нужно.

### M04 — Направление взгляда камеры и спокойное сопровождение

Сравнить небольшое упреждение по направлению и ограниченное сглаживание. Подобрать поведение отдельно для боя и перемещения.

- Example: Игрок чуть раньше видит путь впереди; при развороте камера не бросает композицию из стороны в сторону.
- Current state / reuse: Cinemachine настроен напрямую. В сохранённом position composer Damping=0, Lookahead выключен. Настройка существующего Cinemachine, без второго camera-follow.
- Ongoing work: Проверка комнат разных размеров, confiner, телепортов, pixel grid и атак за краем экрана.
- Main risk: Сглаживание добавляет визуальное отставание. Это эксперимент для сравнения, не обязательное улучшение.

### M05 — Проверить удобство уже существующих окон ввода

Проверить late jump, ранний jump перед посадкой, down+jump и ввод во время hit-stop. Исправлять только найденные сбои.

- Example: Прыжок после позднего нажатия и раннее нажатие перед землёй работают предсказуемо без новой механики.
- Current state / reuse: уже реализовано. Jump buffer 0.12 с, coyote time 0.10 с и специальные drop-through окна уже есть в PlayerMovement.
- Ongoing work: Небольшая матрица сценариев и существующие PlayerMovementTests.
- Main risk: Не копировать ещё один coyote time из туториала и не увеличивать все окна без выявленной проблемы.

### M06 — Мягкая коррекция зацепления за угол

Только если есть реальные жалобы на углы: минимально корректировать неглубокое касание края при прыжке.

- Example: Незначительное касание головы об угол не обрывает красивую дугу прыжка.
- Current state / reuse: в проверенном контроллере не найдена. PlayerMovement и текущая коллизия; отдельный эксперимент на тестовой полосе.
- Ongoing work: Новые формы тайлов, one-way платформы, dash и скорость требуют проверки.
- Main risk: Может проталкивать игрока сквозь геометрию и менять маршруты. Без конкретного сбоя оставить выключенным.


## Reproducibility

plan.json includes the inspected revision, file hashes, raw serialized recipe
inventory, proposal metadata and source list. RESEARCH.md distinguishes reviewed
sources from optional videos/catalogs. The offline HTML filters and schematic
are verified separately; browser QA is not Unity gameplay validation.
