# Step 15 — attack input audit and loss/stale-input fixes (C16)

Implemented 2026-10-29. This slice audits the current input contract and fixes
specific lost/stale requests. It adds no timed recovery buffer and changes no
attack/cancel windows, damage, cooldown values or mana costs.

## Findings and changes

PlayerAttackInput kept only the held state. A press and release delivered before
its next Update left that state false, losing a valid quick tap even while the
skill was ready. Each of the six slots now also remembers its press edge until
the first input Update. That update makes one attempt and clears the edge even
when casting, cooldown, mana, dodge or UI prevents the action. A held button
continues to retry normally. Released presses never wait for recovery to end.

Menu pause previously allowed direct skill casting, and held attack/dodge input
could survive a paused menu. SkillBehaviour.TryCast and PlayerSkillManager.UseSkill
now reject menu-time casts. PlayerAttackInput clears attack edges, held state
and queued dodge input on pause; starts while paused are ignored. Existing
casts are suspended by the existing pause clock, not cancelled. Combat freeze
is distinct from GamePauseService and does not discard attack intent.

CancelAllSkills now notifies the input component to clear old attack intent.
Weapon preset swapping already calls this cancellation API, so an old held
press cannot automatically operate a replacement weapon. Updating an individual
skill source clears that slot only. Existing dodge-start cancellation and
source/class changes share the cleanup; subscriptions are released on disable.

## Observed contract

| Input | Result |
|---|---|
| Quick tap between updates, skill ready | One attempt on the next Update |
| Hold through an active cast | Retries after the cast becomes available |
| Released tap during recovery | Rejected; does not become a delayed cast |
| Rejected tap, then mana/cooldown becomes ready | No hidden queued cast |
| Two hand slots held | Existing independent per-skill eligibility remains |
| Dodge/full cancellation | Clears old held and pending intent |
| Skill source replaced | Clears only that slot's old intent |
| Menu pause | Blocks new casts; resume requires fresh input |
| Combat freeze | Input remains eligible under existing casting rules |

No saved Resources/Skills recipe was found enabling generic IsChanneling.
That generic duration/cancellation loop was inspected, not redesigned as
hold-to-channel. Existing Electric Beam, Winter Storm and Frost Anabiosis
controller behavior remains covered by their regression suites. No channel
release semantics or recovery cancellation rule was introduced.

## Verification

The initial audit reproduced three failures with a valid resource fixture:
stale intent after full cancellation and menu-time casts for pause/inventory.
Three hold/release/resource cases already passed. A later quick-tap regression
failed while the other eight audit cases passed, before adding the press edge.
An earlier invalid fixture omitted PlayerStats.Awake and had no Mana resource;
those fixture failures were corrected and are not counted as gameplay defects.

Compilation and **127 EditMode tests passed**, zero failed/skipped:
AttackInputAuditTests, WeaponSwapSkillLifetimeTests, ElectricBeamTests,
WinterStormTests, FrostAnabiosisTests, PlayerMovementTests, GamePauseServiceTests,
SkillCooldownRecoveryTests, DefaultSkillBindingsTests, GroundDashVisualTests
and NeutralDodgeAnimationTests.

The new input cases invoke the actual registered start/cancel delegates and
PlayerAttackInput.Update/PlayerSkillManager APIs against an instrumented
SkillBehaviour. They cover quick taps, held repeats, recovery rejection,
resources, both hands, source changes, cancellation, menu blocking, dodge
rejection and a zero-scale combat-style freeze. These are EditMode behavior
checks, not new live keyboard/gamepad playback, full recipe timing captures or
a standalone build. Existing skill suites provide the adjacent gameplay checks.

Remaining C16 proposal: deciding whether a tap late in recovery should queue
the next attack for a short time. That changes accepted input timing and needs
a chosen window/cancellation policy. It is not required for the one-update tap
fix. Broader C18 effect limits/reduced-effects settings also remain separate.
