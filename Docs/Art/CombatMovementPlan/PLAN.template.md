# Combat and movement feel — research and implementation plan

Date: 2026-10-27. Source revision: `__REVISION__`.
Revision 2: user feedback applied. Status: **planning only**. Existing facilities below are verified by source and
serialized-data inspection. Proposals are not implemented or combat-balanced.
No new Play Mode capture, GPU profile or runtime test run was performed for this
task. The HTML demonstration is an abstract explanatory diagram, not game footage.
The user's report that atmosphere work is complete is accepted as context; the
previous atmosphere plan and its modified documents are not rebuilt or overwritten.

## Recommendation

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
- Raw serialized asset inventory: __HITSTOP_INVENTORY__. These counts cover assets
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

__OPTIONS_TABLE__

## First slice and dependency order

1. **Observe, do not retune everything.** Capture one ordinary melee attack at
   normal speed and slowed inspection, with a miss, a valid hit, an evasion,
   zero final damage, a lethal hit and a DoT tick. Include an attacking enemy.
   Log the accepted-cast token, recipe phase, hit resolution and body/weapon pose.
2. **C01 semantics + initial C18 bounds.** Pick one post-mitigation confirmation
   path; define direct hit/contact outcome and a fallback position. Prove that
   one event cannot double-spawn through DamageTaken + DamageDealt. Add a neutral
   universal visual fallback and a small shared attack-intensity profile instead of copying fields
   into every recipe. Existing active VFX should not double up.
3. **C02 body + C04 target.** One normal slash and one heavier strike. Compose
   with current movement/weapon ownership. Keep all gameplay transforms and
   recipe hit windows unchanged. Validate standing, running, airborne, facing
   both ways, offhand overlap, cancel, dodge, stasis and spin before adding more art.
4. **C05/C07/C10 contact.** Add one universal contact shape, a small directional
   burst and a brief flash. No sound assets, dispatcher or target-material catalog.
   Use neutral shared shapes with bounded light/heavy variation; Extend to one
   ranged hit so the abstraction is not melee-only. C09 tunes the existing stop.
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

__OPTION_DETAILS__

## Reproducibility

plan.json includes the inspected revision, file hashes, raw serialized recipe
inventory, proposal metadata and source list. RESEARCH.md distinguishes reviewed
sources from optional videos/catalogs. The offline HTML filters and schematic
are verified separately; browser QA is not Unity gameplay validation.
