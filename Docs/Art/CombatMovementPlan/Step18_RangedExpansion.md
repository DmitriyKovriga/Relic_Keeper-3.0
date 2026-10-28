# C02 — ranged body expansion

Implemented 2026-10-29, following the four-skill pilot. **30 skills supported,
26 newly opted in.** See [preview](RangedExpansion/ranged-expansion.gif) and
[case measurements](RangedExpansion/audit.json).

Coverage: all current bow, wand, chakram and elemental staff skill assets,
plus Horizon Cross-section and the older one-handed Weapon Throw. Wave Cut
retains its existing melee response. Utility skills and spins retain their
existing presentation.

The skill's Body Follow Style selects bow, wand, staff, throw or sword-wave
motion. Existing enum values are preserved; SwordWave is appended. The shared
CombatPoseProfile still supplies Enable Ranged Body Follow and Ranged Body
Strength. New skills default to None; unsupported recipes, parallel groups and
channel loops are rejected rather than guessed.

The recipe clock now samples one ranged envelope per cast: first preparation,
first release, then the last valid recovery. Intermediate volley windups and
recoveries cannot restart the visible response. Double Discharge and Icy
Palisade hold a small brace through their later discharges/eruptions, then
settle. Orbiting discs, Winter Storm, Frost Wall and Lightning Rod animate only
their cast, independently of their continuing effect. Recipes without valid
recovery ease out over the rest of the cast. Horizon uses a bounded 3-degree
sword preparation/follow-through; the other styles retain the pilot values.
Fast casts retain the pilot's reduced amplitude.

No recipe timings, damage, stats, projectile rules, hand animation, physics
movement or localization strings changed. No new sprites or VFX were needed.

## Verification

- Unity compilation passed.
- RangedBodyFollowTests and CombatPoseTests: **112 passed, 0 failed, 0 skipped**.
  Every supported recipe is sampled through its complete timeline with repeated
  hand callbacks: exactly one release response, bounded angles, neutral finish,
  stale-owner rejection and cancellation cleanup. Recipe JSON stays unchanged.
  All 30 skills also cover feet, root, collider and hand registration in both
  facings, grounded and airborne.
- **30 distinct real Play Mode casts passed**, using the existing controlled
  capture fixture: 29 in the expansion audit and a separate Weapon Throw case.
  No case errors; all finish with neutral pose and released body ownership.
  The six-skill comparison contact sheet was visually inspected.
- Console error check and diff whitespace check passed. Local Graphify refreshed.

These are controlled Editor casts with disabled targets and constrained actor
roots. Existing weapon-family sprite proxies are recorded per case. They do not
establish hardware-input traversal, a complete equipment matrix, hit/damage
balance or standalone GPU performance. The three previously documented Mana
Spear finite-pierce test/data disagreements remain outside this change.

Reproduce in Play Mode via
RelicKeeper.Editor.Combat.CombatBaselineCapture.BeginRangedExpansionReview.
BeginWeaponThrowBodyReview runs the isolated axe case; BeginRangedReview retains
the original four-skill on/off, running, air, fast and cancellation fixture.
Raw frames/traces are ignored under Logs/RangedExpansion. The preview is a
nearest-neighbor enlargement, slowed three times; projectile edges are cropped
to keep the body visible. No scene edits are saved by the capture fixture.
