# J10 — Optional early return after death

Implemented and checked 2026-10-30. This extends the existing summary and return
sequence, preserving its appearance, statistics and authoritative death handling.

## Reason and scope

The ordinary baseline contains three deaths and readable summaries followed by
return to recruitment. Source requires the same six-second message interval on
every attempt and offers no continuation control. The recording has no input
telemetry and does not establish an input defect or an attempted skip. This
authorized J10 slice makes the repeated wait optional, without replacing a
summary that already works.

A quiet **Return to hub / Вернуться в хаб** button appears after one second.
Click, Enter, numpad Enter, Space or gamepad South can continue. Confirmation
controls must first be released after the reading gate, protecting against a
held attack/confirm input from before death. Button focus and hover use the
existing muted gold palette; its bottom placement leaves the summary unchanged.
The overlay now catches background clicks so they cannot reach UI underneath.

Ignoring the control retains the existing six-second automatic return. A chosen
continuation brings forward the existing title fade; the button fades with it.
Both routes yield a fully black frame before invoking the existing callback,
keep the half-second transition cover, then fade out. Repeated continuation
cannot invoke the callback directly or twice. Concurrent overlay plays are
rejected, and disposal/disable clears continuation ownership.

No enemy/death animation, sound, killer attribution, retained-reward message,
new progress, save migration or recruitment flow was added. Highest stage still
uses the existing account record; monsters killed still belongs to the deceased
character's lifetime. `GameSaveManager` still supplies those snapshots, removes
the character, returns to the hub and opens recruitment through its unchanged
callback. No gameplay or save code was edited.

## Verification

Unity compilation passed. **16 EditMode tests passed, 0 failed/skipped**:

- `DeathScreenFlowTests` (8 cases): early/repeated/held confirmation, fresh input,
  automatic timing, covered-frame ordering, concurrent play, statistics ownership,
  cleanup/reuse, missing-document fallback and English/Russian table entries.
- `DeathProgressTests` (8 cases): existing lifetime kills, account record,
  character death/removal, other-character preservation and old-save migration.

The native check uses the real overlay, a cloned 480×270 panel and an owned
return callback. It checks actual UI navigation submit, live EN→RU switching,
large stage/kill values, held-confirm rejection/release/fresh confirmation,
repeated input and three successive sequences. It runs with world time paused,
using unscaled time. Each callback also reads the previously rendered texture
and requires a completely black frame with the button/summary hidden.

Early-return callbacks occurred at **2.206 s** and **2.202 s**, with exactly one
callback each. Ignoring input returned at **6.020 s**, once. These are controlled
sequence timings, not input latency or performance measurements. The initial
preview incorrectly reused a stale Play-entry delta; adding two warm-up frames
to the Editor fixture corrected the capture. Delivered runtime timing did not
need changing. Successful evidence replaces that failed diagnostic attempt.

- [Native English/Russian comparison](J10/DeathSummaryENRU.png)
- [Live locale switch](J10/RU-live-switch.png)
- [Rendered cover before callback](J10/CoveredBeforeReturn.png)
- [Recorded callback times](J10/Events.csv)
- [Native report](J10/RuntimeCheck.txt)

Clock/input providers are controlled for the tests. The native checker dispatches
an actual UI submit event but does not exercise physical keyboard/gamepad hardware.
Its callback does not kill/remove a real character or run recruitment. Ordinary
combat/death/return and a fresh standalone build remain separate checks. The
original save SHA-256 matched before/after native checks:
`DF3EE83C407AE06D35F3A887EFFBCECD7FC0F1FBC7421F60AE88EE6CDE321044`.

Only one MenuLabels key was added, with EN/RU entries and the existing shared
table identity. No scene, prefab, panel-settings asset or font was changed. The
pixel-UI skill file was unavailable; implementation follows the existing code-built
death screen and verified logical canvas instead of introducing another UI system.
The opt-in Editor checker is `RelicKeeper.Editor.VFX.DeathScreenRuntimeCheck.Run`
from Play Mode; its separate `EnsureLocalization` method maintains this key only.
It adds no runtime/debug menu and restores locale/preferences, time scale and
background-run state while disposing its own panel/objects.

## Hands-on check

Use a character/run you are willing to lose; death still follows ordinary rules.

1. On death, verify the same highest-stage and lifetime-kill summary. Pressing
   confirm immediately should not dismiss it. After about one second, use the
   button or a fresh Enter/Space/gamepad South press.
2. Watch the transition: summary and button fade away, then a black frame covers
   return and recruitment. Repeated confirmation must not cause two returns.
3. On another death, hold a confirm control through the reading gate. Release it,
   then press again; a held control alone must not skip the summary.
4. Give no input on another attempt: the original automatic return should remain.
5. Check both EN and RU, including a live language change while the summary is up.

Next candidate: optional J09, beginning with one existing prop and a demonstrated
benefit before adding reactions. J07/J08 remain user-owned and excluded.
