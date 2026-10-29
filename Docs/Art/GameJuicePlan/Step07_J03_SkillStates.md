# J03 — skill-state feedback

Completed 2026-10-30 following the roadmap's J01 -> J02 -> J03 -> J06 order.

## Delivered behavior

The HUD already had cooldown overlays/countdowns, input labels and mana costs.
This slice adds the missing transitions rather than another cast VFX layer:

- A fresh denied press briefly outlines its slot and shows one caption beneath
  the skill bar: Cooldown, Not enough mana, Already casting or Cannot cast now.
  Captions include the current input binding and support English/Russian,
  including a live language switch. Different reasons have different colors;
  the words carry the meaning independently of color.
- Accepted casts with an effective cooldown of at least 3 seconds arm one ready
  cue. The tracker must actually observe recovery. Once recovery finishes, it
  waits for enough mana, cast completion and the current shared cast gates before
  showing Ready with a short cyan border. Persistent Winter Storm, stasis,
  manager suppression and the Falling Anvil sequence cannot be labelled ready
  merely because their cooldown reached zero.
- Zero/short cooldown attacks, initially ready slots, restored cooldowns without
  a new accepted cast, empty slots and HUD rebuilds do not arm readiness.
  Replacement runtimes are identified by instance, even if they use the same
  skill data/icon. Events from the previous instance are ignored.
- Held attacks retain their existing retry behavior. Only their accepted uses
  notify presentation; failures on every held frame do not repeat messages.
  Rapid fresh denials of the same reason are limited to once per 0.65 seconds;
  a changed reason, or a failure after a new success, can appear immediately.
- The slot does not move or intercept input. Its icon and existing timer remain
  visible. A rejected attempt does not show an accepted/ready cue, spend mana or
  begin a cooldown. Actual accepted skill VFX remain the existing ones.

There is one caption per HUD, replaced rather than queued, lasting 0.65 seconds.
Normal ready borders fade over 0.45 seconds; rejection borders and reduced-mode
ready borders use 0.3 seconds. Reduced combat effects lowers border intensity to
60% while preserving the semantic caption. Frame lifetime uses unscaled time;
cooldown recovery still uses the existing gameplay clock. Menus hide the caption
and slot flash. Reopening/rebinding does not replay old feedback.

The label is a 180x13 logical-pixel region with a 10-point project TMP font,
placed below the skill row and excluded from layout/raycasting. It follows the
existing HUD scale/opacity settings. No new settings, developer buttons, bitmap
art, camera effects or audio were introduced.

## Runtime boundary

SkillBehaviour retains its existing void TryCast entry and exposes a result-
returning companion plus a shared availability query. The guard order is still
pause -> persistent skill lock/stasis -> already casting -> data -> cooldown ->
owner/mana resource -> mana amount. Accepted casts still decrease mana, start
their cooldown and execute in the same order. The availability query samples
normal cooldown progress but spends no resource and starts no cast.

PlayerSkillManager preserves its entry points and manager-level pause,
suppression and Falling Anvil gate. It forwards committed results to the HUD.
PlayerAttackInput passes its existing fresh-press flag for presentation only;
it changes no held/tap buffering, dispatch, dodge, cancellation or input ownership.
Readiness calls the shared availability query only for an armed slot whose timer
has reached zero, avoiding a full eligibility poll for every idle primary.

The change is shared by the common skill gate; no per-skill gameplay recipe,
balance data, save structure, cooldown duration, mana cost or localization asset
was rewritten. Caption keys use the established EN/RU runtime fallback pattern.

## Verification

Unity compilation succeeded. **84 focused EditMode tests passed, 0 failed/skipped**:
SkillSlotFeedbackStateTests, SkillSlotBindingFeedbackTests, SkillCastFeedbackTests,
AttackInputAuditTests, SkillCooldownRecoveryTests, WeaponSwapSkillLifetimeTests,
HudSkillTooltipTests, CombatBaselineTests, CombatHitFeedbackTests and
DefaultSkillBindingsTests.

The new cases cover accepted versus rejected resource/cooldown behavior, preserved
gate precedence, read-only availability, persistent Winter Storm locking, held
retry silence, a single long-cooldown ready transition, delayed affordability/
lock/cast/pause readiness, no zero/short cooldown cue, no unobserved instant-refund
cue, initialization/restoration/rebinding, stale runtime events, throttling and
nonblocking icon-preserving border cleanup. Existing filters guard input, weapon
swap lifetime, cooldown math, accepted attack context, tooltips and bindings.

The opt-in SkillStateFeedbackRuntimeCheck uses real uGUI slot art, the HUD,
shared cast gate and disposable skill/owner data on a 480x270 render target. Its
probe executes the common cast gate and controlled clock; it does not execute
each authored attack recipe. The existing user HUD scale remains in effect
(default 0.75). It asserts:

| Case | Result |
|---|---|
| Initial ready slots | No caption or new ready frame. |
| Accepted long cast | Starts actual mana/cooldown state and arms readiness, without a rejection message. |
| Cooldown rejection | Correct EN caption; 60 held retries stay quiet. |
| Live RU switch | Correct full Russian caption, no truncation. |
| Temporary Winter Storm lock | No Ready until the existing lock ends. |
| Recovery completion | One Ready caption, not refreshed each frame. |
| Mana rejection | No execute or cooldown; correct full EN/RU captions. |
| HUD rebuild / same-data runtime replacement | No false Ready; prior runtime events ignored. |
| Zero-cooldown primary | Twenty accepted uses produce no ready pulse. |
| Reduced effects | Ready still appears with the reduced border treatment. |
| Menu pause / cleanup | Cast denied, caption hidden; no queued replay; borders expire. |
| Layout / input | Slot positions fixed; caption excluded from layout/raycasts; native bounds and text overflow checks pass. |

[Runtime report](J03/RuntimeReport.txt), [validation metadata](J03/Validation.json),
[native comparison](J03/SkillStateComparison.png),
[integer 2x details](J03/SkillStateDetails.png),
[cooldown EN](J03/Cooldown-en.png), [cooldown RU](J03/Cooldown-ru.png),
[mana EN](J03/Mana-en.png), [mana RU](J03/Mana-ru.png),
[ready RU](J03/Ready-ru.png), [reduced ready EN](J03/Ready-reduced-en.png).
These are actual Unity UI frames from controlled operations, not an ordinary-run
A/B video or a performance measurement. Repeated fixture icons demonstrate the
shared slot treatment and do not replace the game's equipped skill icons.

Visual review caught an opaque uGUI Outline covering the icon; it was replaced
with four thin border strips and a regression case. The capture now uses a
constant one-pixel logical scale on its render target and larger captions for
the default reduced HUD scale. Its source selection tolerates a hub whose skill
bindings have not yet loaded. The saved evidence is from the final passing run.

No scene or project-setting changes were introduced. The final bounded console
error query was empty, and the original save hash still matched. Save SHA-256:
`442e728fc515c02ab17d8a6bdafe595926e20b2dec41f7cabe29908edd6b50db`.
The code-only Graphify cache was refreshed, retaining its known namespace-node
collision warnings. No network or runtime package dependency was added.

The project's referenced pixel-UI skill is still absent. As allowed by the ECC
Unity context, the implementation follows the comparable existing HUD, its
480x270 canvas, project fonts and EN/RU localization pattern.

## Manual check

1. Equip a skill with an effective cooldown of 3 seconds or more. Use it once,
   release the key, and watch its slot as recovery completes. With enough mana
   and no active lock, it should briefly show Ready once. Continue waiting: it
   should not repeat. Opening the game or a fresh skill HUD should stay quiet.
2. After the cast animation ends but while the timer is still running, press
   again. Look for Cooldown. During the active cast, Already casting can be the
   correct result because the original gate checks that first.
3. With cooldown finished, lower mana below the skill's cost and press it. Look
   for Not enough mana, with no mana spend/cast. If both cooldown and mana block
   it, cooldown retains the original priority. Restoring mana can release a
   waiting genuine Ready transition.
4. Hold the primary attack and another recovering skill. Casting/retry rules
   should feel unchanged; held frames should not spam failed messages. Rapid
   fresh denied clicks show a bounded cue. Zero-cooldown attacks never flash Ready.
5. Swap away and back during recovery; inspect the restored timer. No new Ready
   should be manufactured by the swap. Open/close menus or rebind a key; old
   cues should not replay. Try a persistent Winter Storm or stasis lock if available.
6. Switch EN/RU and toggle Reduced combat effects in settings. Captions should
   remain legible and complete; the reduced border is quieter.

Ordinary-run A/B, every authored skill's full recipe, gamepad-specific navigation
and a new standalone build were not repeated. The updated Unity project is ready
for that user review. Next planned slice: J06 room resolution and exits.
