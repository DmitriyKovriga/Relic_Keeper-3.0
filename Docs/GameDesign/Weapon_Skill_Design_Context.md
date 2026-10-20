# Relic Keeper: weapon skill design context

Snapshot: 2026-10-20, source revision `a8db9e59`. English working reference for
weapon-skill discussions. Evidence: serialized assets + runtime code; no live
combat or balance validation was performed for this snapshot. Numbers below are
current content, not recommended balance targets. Recheck affected sources after
gameplay changes; do not re-audit the whole project for every idea.

## 1. Product and loadout

- 2D action roguelike with platforming, facing-based attacks, equipment-driven
  skills, loot/affixes, and separate class passive trees. Unity 6000.3.11f1.
- Six active slots: 0 main weapon; 1 offhand weapon/shield OR 2H special;
  2 helmet; 3 body armor; 4 gloves; 5 boots.
- Weapons grant skills when items are generated. A 2H item rolls one primary
  from `SkillPool` and one special from `SecondarySkillPool`. A 1H item uses its
  ordinary pool. Pool alternatives are loot outcomes, not a freely selectable
  action bar of all skills in that pool.
- Dual wield uses the active hand's local damage, crit and speed. Global affixes
  on the other hand remain applicable. Do not add both weapons' local damage to
  a single ordinary hit. Slot 1 also matters for shields and dual wield when
  discussing `SpecialSkillCooldownRecovery`.
- Switching weapons preserves cooldown state by item instance, skill and index.
  Existing launched projectiles retain their own lifetime; do not assume a swap
  removes all persistent offense or resets cooldowns.
- Classes have distinct starting overrides and trees. The inspected skill data
  has no class requirement field: weapon access should not be assumed class-locked.
- Runtime UI uses 480x270 reference resolution. Skill silhouettes, telegraphs and
  effects must remain readable at this size. Content supports English/Russian.

Sources: `Assets/Scripts/Systems/ItemGenerator.cs`,
`Assets/Scripts/Skills/PlayerSkillManager.cs`,
`Assets/Scripts/Combat/WeaponHandStatScope.cs`,
`Assets/Scripts/Combat/WeaponHandStatsProvider.cs`.

## 2. Stat rules that change design decisions

### Layers, damage and tags

- Ordinary stat layers: `(base + Flat) * max(0, 1 + net Increase/100) * product(More/Less)`.
  Increases add together; More/Less multiply separately. `Flat +25 CritMultiplier`
  adds 25 percentage points; `25% increased CritMultiplier` scales its current base.
- Crit chance is in percent units: 5 means 5%. Crit multiplier starts at 150
  (a critical hit deals 1.5x damage). Accuracy is an unused compatibility enum.
- Four hit channels: physical, fire, cold, lightning. Poison is an ailment, not
  a fifth weapon damage channel. No chaos damage channel was found.
- Current generic hit pipeline: active-hand flat damage/weapon roll -> recipe
  conversions -> character conversions -> channel/context modifiers -> skill
  multiplier -> crit -> target defenses. This differs from some older prose in
  `Docs/Stats/Gameplay_Stat_Semantics.md`; use runtime as authority.
- Context tags: `Attack`, `Spell`, `Melee`, `Projectile`, `Area`,
  `DamageOverTime`, `Ailment`, `Range`. `Range` is explicit; a projectile does not
  automatically receive it. `MeleeDamage`, `SpellDamage`, `RangeDamage` require
  matching contexts; `ElementalDamage` targets elemental channels without a tag.
- Untagged ordinary damage falls back to `Attack|Melee`; projectile steps fall
  back to `Attack|Projectile`, or add `Projectile` to explicit skill tags.
  Action-speed mode and damage tags are independent authoring choices.
- Some elemental spell outputs use a dedicated path: elemental flat contributions
  only, converted to the selected element, excluding physical weapon damage.
  Check the actual step's damage mode before claiming physical-to-spell scaling.
- Skill `StatLinks` derive private modifiers from pre-link stats, so links do not
  feed each other. Steps support private modifiers, conversions and bonuses per
  target ailment stack. These do not mutate the shared character or skill asset.

### Speed, cooldown, area and duration

- Attack speed uses active weapon APS with attack modifiers. Spell speed uses
  weapon flat speed with cast modifiers; Universal chooses the faster result.
  `SkillSpeedMultiplier` applies afterward; final action speed is clamped 0.05-12.
  Faster animation/cast timing does not itself reduce authored cooldown.
- Mana is paid and cooldown starts when a cast is accepted. Primaries currently
  mostly cost 0 mana and have 0 cooldown; attack timing still limits repetition.
- Cooldown recovery applies slot-specific recovery first, then all-skill recovery.
  Each stage: subtract Flat seconds, multiply by `max(0, 1 - net Increase/100)`,
  then each More multiplies by `max(0, 1 - value/100)` and each Less by
  `max(0, 1 + value/100)`. This is not the common `base/(1+recovery)` formula.
  Cooldown progress responds to current recovery stats during recovery.
- `EffectDuration` applies separately to each eligible effect:
  `(raw duration + Flat seconds) * increased/reduced * More/Less`, clamped >=0.
  Duration is snapshotted on application; refresh recalculates it. Untimed recipe
  buffs remain bound to cleanup, rather than gaining a timer.
- Area scaling is anisotropic: for scale A, X grows by A and Y by
  `1 + 0.5*(A-1)`, before axis-specific multipliers. Do not equate +100% area
  scaling with doubling both axes, or assume every special controller uses it.
- Projectile count, speed, pierce, fork and chain exist, but individual recipes
  can opt out. Ground projectiles force one projectile and disable fork/chain.
- `ReturningProjectileDamage` adds Flat percentage points to the recipe's return
  damage percentage (default 50). It does not multiply outbound damage.
- `ExecutionThreshold` uses base/Flat only, summed and clamped 0-100%. A positive
  accepted direct hit executes an enemy left strictly below the threshold.
  Equality does not execute; DoT does not execute; bosses use the same rule.
- `ExtraTargetsForMeleeHits` has metadata, affixes and description support, but no
  gameplay consumer was found in the inspected runtime. Do not promise it works.

Sources: `Assets/Scripts/Stats/StatType.cs`, `StatModifier.cs`, `StatLayerMath.cs`,
`StatsDatabaseSO.cs`, `StatLinkModifiers.cs` (all under `Assets/Scripts/Stats/`);
`Assets/Scripts/Combat/DamageCalculator.cs`;
`Assets/Scripts/Skills/Logic/SkillBehaviour.cs`, `SkillStepRunner.cs`;
`Assets/Scripts/Skills/SkillCooldownRecovery.cs`, `SkillAoeScale.cs`,
`ReturningProjectileDamageResolver.cs`; `Docs/Execution.md`.

## 3. Ailments, control, sustain and shield economy

Configured baseline below comes from
`Assets/Resources/Databases/DefaultGlobalBaseStats.asset`; runtime fallbacks can
differ. Class overrides replace baseline values, rather than adding to them.
Warrior overrides HP/MP to 75/50 and the AreaOfEffect stat to 100; Rogue starts with 100
Evasion; Mage overrides mana regen to 5. Shared HP/MP baseline is 100/75,
regen 1/1 per second, crit multiplier 150. Actual equipped stats can differ greatly.

| Mechanic | Current behavior and design consequence |
|---|---|
| Poison | Requires physical hit damage and a chance roll. Baseline 10% of physical hit per 1s tick, 2s duration. Damage and stack count accumulate without an authored cap; each application refreshes the shared timer. Repeated hits can sustain the entire accumulated poison pool. |
| Bleed | Requires physical hit damage and a chance roll. Baseline 50% of physical hit per 1s tick, 6s duration. Independent stacks; cap is `1 + MaxBleedStack`. New applications compare against the weakest at cap. Large hits and extra stacks matter. |
| Ignite | Requires fire >=30% of total hit; chance is at least 25% before avoidance. Configured baseline 50% of fire hit per tick, 8s, max stacks 1. Runtime missing-stat fallback is 20%/4s. Spreads to a nearby enemy (2.5 units), default every 2s, prioritizing unignited/under-cap targets. |
| Freeze | Requires cold >=30% of the hit and a chance roll; configured chance/duration 25%/3s. Uses enemy freeze controller and its restrictions. |
| Shock | Configured chance 25%, duration 2s. Successful lightning procs build an enemy threshold equal to 30% max HP using pre-mitigation lightning damage * application effectiveness/100. No 30% lightning-share requirement. At threshold, default +50% increased damage taken from all sources, including DoT. Partial buildup persists; expiry resets threshold and discards overflow. |
| Stun | Enemy stun meter, separate from freeze and shock. Physical hit damage feeds buildup; `StunBuildUp`, `StunThreshold`, duration and enemy configuration matter. Explicit timed-stun/launch effects also exist. Do not assume every stun bypasses boss restrictions. |
| Pushback | Rating-based, enabled per skill; skill rating adds Flat. Existing opted-in attacks often use 200. `PushbackResist` reduces displacement. |
| Sustain | Flat HP/MP on direct hit and on block, plus flat/% regen. Multi-target and repeated hits can increase sustain as well as DPS. This is on-hit recovery, not necessarily percentage lifesteal despite some node names. |
| Mystic Shield | Charge defense, not an extra HP bar. Incoming eligible damage consumes a charge for partial mitigation; charges recharge. Offensive consumption competes with defense. Consumption alone grants no universal damage bonus. |

`MysticShieldSkillEffectiveness` starts at 1. It is captured only after successful
shield consumption/generation in a cast. Eligible subsequent base damage scales
with E; eligible timed buffs scale both magnitude and duration. Cleanup-bound
instant buffs keep their authored modifiers. Explicit `MysticShieldDamageBoost`
also scales its bonus (no current recipe uses that step). Charge count, capacity,
mitigation and recharge do not scale. Combined damage/bonus or magnitude/duration
can compound; budget them together. See `Docs/Stats/MysticShieldSkillEffectiveness.md`.

Armor and evasion use a diminishing piecewise curve: 500 rating ->50%, 1000 ->60%,
2000 ->70%, 5000 ->80%, 10000 ->90%. Physical resistance/caps can constrain armor
mitigation further. Evasion applies to direct hits. Default configured resistance
and block caps are 75%. The global asset configures shield mitigation/cap as
40%/75%; 50%/90% are missing/nonpositive-stat fallbacks. Despite its name,
`PlayerStats.EnsureMinStat` only replaces nonpositive bases, not smaller positive
values. Do not substitute fallback numbers for configured values.

Sources: `Assets/Scripts/StatusEffects/AilmentController.cs`,
`EffectDurationCalculator.cs`; `Assets/Scripts/Enemy/EnemyStunController.cs`;
`Assets/Scripts/Stats/PlayerStats.cs`, `MysticShieldController.cs`;
`Assets/Scripts/Combat/DefenseRatingCurve.cs`, `ArmorMitigation.cs`,
`EvasionMitigation.cs`; `Docs/Stats/Shock.md`.

## 4. Existing weapons and skills

Base stats are unrolled weapon assets before local/global modifiers. APS = base
attacks per second. CD/MP = authored cooldown seconds / mana. Skill names preserve
existing spelling. All listed ordinary primaries have CD/MP 0/0. Skill coefficient
examples are not final damage or DPS; verify shared step defaults and runtime too.

| Weapon | Base damage; APS; crit | Implicit | Primary pool | Special pool (CD/MP) |
|---|---|---|---|---|
| 1H sword | Phys 10-15; 1.2; 8% | +50 crit multiplier | Dexterous Lunge | None |
| 1H axe | Phys 5-10; 1.2; 6% | +50 bleed chance | Weapon Throw / Extraction | None |
| Dagger | Phys 5-10; 1.6; 8% | 50% increased crit chance | Venom Strike / Ninja Stub / Execution | None |
| 1H hammer | Phys 6-12; 1; 5% | 100% increased stun buildup | Stunning Blow | None |
| Wand | Fire/Cold/Lightning 2-4 EACH; 1; 5% | +1 Mystic Shield capacity | Seeker Skill / Electric Beam | None |
| 2H axe | Phys 15-20; 1; 6% | +100 bleed chance | Cleave (weight 100), Cold Slash (weight 1) | Sweep (5/10) |
| 2H sword | Phys 12-24; 1; 8% | +100 crit multiplier | Wave Cut / Cold Slash | Lunge Slash (3/10) |
| 2H hammer | Phys 12-24; 1; 5% | 100% increased stun buildup AND stun duration | Ground Shake | Tremor (8/10) / Falling Anvil (10/25) |
| Bow | Phys 12-24; 0.7; 8% | +2 projectiles | Simple Bow Shot | Piercing Shot (6/25) / Arrow Artillery (8/20) |
| Chakram | Phys 8-16; 1; 5% | None | Homing Weapon Throw | Orbital Weapon Throw (5/10) |
| Fire staff | Fire 14-24; 1; 5% | +25 ignite chance | Fire Ball | Big Fire Ball (7/50) |
| Cold staff | Cold 12-24; 1; 5% | +25 freeze chance | Icecle Shot | Big Cold Ball (7/50) / Winter Storm (15/50) |
| Lightning staff | Lightning 5-48; 1; 5% | +25 shock chance | Lightning Arc | Big Lightning Ball (7/50) |

Only the 2H axe primary has unequal positive weights in this table; other shown
alternatives use equal weights. Sweep has serialized weight 0, but is the ONLY
secondary entry. `SkillPoolSO.GetRandomSkill` falls back to its first entry when
no weighted entry is selected, so this pool still returns Sweep. A zero-weight
entry in a multi-entry pool is not normally selected.

### Existing mechanical territory

- **Dexterous Lunge:** moving persistent melee hitbox, one hit per target;
  skill speed x1.5, scoped +100% increased crit chance, pushback.
- **Weapon Throw:** current weapon sprite as infinitely piercing projectile,
  parallel spread; speed x1.5. Not just an ordinary axe melee swing.
- **Venom Strike:** forward hit with scoped +50 poison chance and +10 poison
  damage coefficient; +1 additional coefficient point per existing poison stack,
  with no authored counting cap. Already occupies poison-ramp territory.
- **Ninja Stub:** blink-and-stab step, damage coefficient 1.25.
- **Execution:** ordinary forward weapon hit plus 10 execution-threshold points.
- **Extraction** (added 2026-10-20 after this snapshot's base revision): one
  forward axe hit against the nearest target; consumes the highest-potency old
  bleed before new ailments. Grants +100 flat BleedExtractionChance for its hit;
  global base chance is 0%, and equipment/passives can enable extraction on other
  hits. Burst = stored DPS × remaining seconds × flat BleedExtraction/100
  (player base 50%; separate amount/chance affixes). Cannot crit or inflict
  ailments. Duration/potency are snapshotted when the wound is applied. See
  `Docs/Extraction.md`; initial cost/CD 0/0, damage coefficient 1, speed ×1.
- **Stunning Blow:** pushback and a secondary circle dealing coefficient 0.5 to
  nearby enemies, excluding the primary target; speed x1.25.
- **Cleave / Sweep:** circular/VFX-fitted melee damage, coefficients 1 / 2.5.
  Sweep also reduces its cooldown by 1 second per recorded hit. Dense packs can
  change its repeat rate; evaluate that separately from isolated-target damage.
- **Wave Cut:** VFX-fitted hit, +100 Flat crit multiplier on this step.
  **Cold Slash:** forward rectangle, 50% physical-to-cold conversion and scoped
  +50% increased crit chance. **Lunge Slash:** moving hitbox, coefficient 2.5.
- **Ground Shake:** grounded, infinitely piercing surface wave; one projectile,
  no fork/chain. **Tremor:** forward hit coefficient 2.5; its step authors +250%
  increased stun buildup. However, the stun consumer reads `StunBuildUp` from
  `DamageSnapshot.Source`, which these hits set back to the player, rather than
  from the private step provider. Treat the scoped buildup bonus as a wiring
  concern to verify, not guaranteed additional runtime stun.
- **Simple Bow Shot:** ordinary arrows, release at 55% of attack pipeline;
  supports projectile stats. **Piercing Shot:** coefficient 3, infinite pierce,
  intentionally ignores extra projectile count, fork and chain.
  **Arrow Artillery:** falling arrows around cast position; one shared hit per
  target per barrage. Visual arrow count is not same-target hit count.
- **Homing Weapon Throw:** despite its name, recipe has homing disabled; infinite
  pierce, ignores world stops, reverses once after 2s, return damage 50% + stat.
  **Orbital Weapon Throw:** 1 + projectile-count stat orbiters, maximum 10,
  lifetime 8s, damage coefficient 3.5. Shared orbit count across casts is enabled,
  capped at 10; repeat casts add within that shared limit. Orbiters use the
  runtime default 0.35s per-target rehit cooldown, rather than lifetime-only hits.
- **Fire Ball / Icecle Shot:** projectile spells. Icecle has scoped +25 freeze
  chance and +4 pierce. **Lightning Arc:** chain target selection and chain
  damage; additional chain investment matters.
- **Big elemental balls:** all three already share a pulsing projectile pattern:
  coefficient 0.55, pulse interval 0.35s, lifetime 5.2s, CD/MP 7/50. Another
  recolored large ball would add little mechanical variety.
- **Winter Storm:** dedicated controller/step; converts other hit channels to
  cold, scoped +20 flat cold and +100 freeze chance; Universal speed x5.
- **Seeker Skill:** homing projectile, coefficient 0.65, lifetime 6s.
  **Electric Beam:** instant beam; 50% of physical, fire and cold converted to
  lightning. Both use Spell speed. Beam tags are `Spell|Area|Range`, Seeker tags
  `Spell|Projectile|Range`; neither should be treated as melee because of weapon.

### Offhand and equipment competition

- Defensive shields have no offensive weapon damage/crit contribution, even
  though their asset type is `WeaponItemSO`; skills can derive offense elsewhere.
- Tower shield: +100 armor, +25 HP on block, +50 block chance. Tower Charge
  (5/20) converts 5% of Armor into private flat physical damage, charges with a
  persistent hitbox and pushback. Launcher Tower Toss (8/10), speed x1.5, applies
  explicit 1s stun and 0.55-unit launch on accepted hits.
- Buckler: +35 block, +100 evasion, 10% increased move speed. Spectacular Parry
  (4/10) absorbs direct enemy hits during a window and retaliates against each
  attacker; configured stun damage is 100% of meter, 25% for bosses.
- Mystic shield: +2 charges, +50 mitigation points. Arcane Reprise (10/10)
  reduces other skill cooldowns by 5s. Frost Anabiosis (15/50) uses 5s
  invulnerable/immobile healing stasis and blocks skills/dodges.
- Armor slots already cover haste, defense/sustain, movement and shield spending:
  Might Of Destruction / Evasionary / Mystic Impulse / Plasma Field (helmets);
  Vitality Force / Vail Of Evasion / Endless Battery (body);
  Haste / Archer Focus / Mystic Consume (gloves);
  Warrior Step / Rush / Blink / Mystic Haste / Ice Skates (boots).
  Winter Storm also appears in the Mystic helmet pool: pool assignment and
  `WeaponRole` labels are not always identical.
- Unique God's Helmet grants God Of Strenght (60/50): 20s buff scaling in complete
  50-max-HP steps, +10 physical damage and +5% area per step. HP-to-offense
  conversion already exists outside weapons.

Content roots: `Assets/Resources/Skills/1HWeapon/`, `2HWeapon/`, `Shields/`;
weapon bases in `Assets/Resources/Items/` AND
`Assets/Scripts/Items/Weapon/LowTier/`; shields in
`Assets/Scripts/Items/Shields/LowTier/`. Debug Blade is excluded from the design
roster. Pool references, not folder names or legacy descriptions, establish pairing.

## 5. Actual passive trees and build hooks

Counts are the `Nodes` list only, excluding cluster records. Character assets
reference the corresponding trees below. A node's visual type does not prove
that it contains a mechanical effect.

| Class/tree | Nodes: small / notable / keystone / start | Current authored state |
|---|---|---|
| Warrior: `WariorPTree/WariorPassiveTree.asset` | 113 / 32 / 8 / 1 = 154 | 153 nodes reference templates; start is empty. 62 distinct templates. |
| Mage: `MagePassiveSkillTree/MagePassiveSkillTree.asset` | 111 / 25 / 8 / 1 = 145 | 87 nodes reference 32 templates; 58 have no template or unique modifiers, including all 8 keystones. One assigned template is also empty (see below). |
| Rogue: `RoguePassiveSkillTree/RoguePassiveSkillTree.asset` | 102 / 19 / 8 / 1 = 130 | All 130 nodes have null templates and empty unique modifiers/scaling rules. Layout exists; allocated stat mechanics are not authored here yet. |

Paths above are relative to `Assets/Resources/PassiveTrees/`. Rogue's spreadsheet
`Docs/RoguePassiveTree_Nodes.xlsx` is planning material, not evidence of runtime
effects. Templates elsewhere in the repository are not automatically in a tree.

### Warrior: verified assigned hooks

- Physical/melee damage, area, attack speed, crit, bleed, poison, armor/evasion,
  resistances, HP/MP regeneration and on-hit recovery; some shield capacity.
- Representative nodes: small melee +25% increased; notable melee +75% and
  +25 physical penetration; small attack speed +10%; notable +25%; small area
  +25%, notable +75%; special recovery -2 Flat seconds; notable all-skill
  recovery -5 Flat seconds. These are existing values, not safe new budgets.
- `AttackSpeedLessDamage`: 40% MORE attack speed, 25% LESS melee damage.
- `BigCritKey`: 100% MORE crit chance, 30% LESS attack speed.
- `BigCritMultKey`: +100 crit multiplier, 15% LESS attack speed.
- `BigBloodKey`: +100 extra bleed stacks, 50% increased bleed duration,
  50% LESS bleed damage. Multi-hit bleed ideas must be checked against this.
- Poison tradeoff template `Misc/NewPassiveNode 15/NewPassiveNode.asset`:
  100% increased poison duration, +10 poison coefficient, 40% LESS poison damage,
  20% LESS attack speed.
- `ArmoreAndLifeKey`: 100% MORE armor AND max HP, 40% LESS attack speed,
  25% LESS melee damage.
- `ArmoreToDamageKey`: +10 physical damage per complete 100 Armor; each elemental
  channel suffers 75% LESS damage. Armor-based offense is an existing build.
- `SpeedPerLife`: 25% LESS max HP; continuous scaling of +5% increased attack
  speed and +1% increased move speed per 50 final max HP.

### Mage: verified assigned hooks and gaps

- Spell damage (small +10%, notable +45% increased), cast speed (+8%/+25%),
  fire/cold/lightning damage, duration (+15%/+45%), mana, crit, ailments, chains,
  pushback and Mystic Shield capacity/effectiveness.
- `LigtningNChain`: +2 chains and 30% increased lightning damage.
- `ShockNote`: +25 application-effectiveness points and +25 shock-magnitude
  points. This changes application reliability and payoff separately.
- `WasteSpellsNote`: 30% increased Mystic Shield skill effectiveness.
- `PushbackNode`: +100 Pushback and +25% increased enemy DamageTaken when the
  target is at least 8 cells away. A cell is 24 pixels = 1 world unit. This is
  a target-distance condition, not a permanent global damage bonus.
- `NoteBarrierInDamage` says each shield charge grants +15% damage, but all its
  modifier/scaling/conditional/trigger/hit lists are empty. No matching runtime
  special case was found. Treat this bonus as unimplemented, not build support.
- `SmallCritMultiplie` still authors CritMultiplier as Increase, while most
  crit-multiplier nodes use Flat points. Preserve this distinction in calculations.

### Runtime capacity vs currently assigned content

The passive framework supports conditional stats (recent events, HP/MP thresholds,
target distance), event triggers (cooldown reduction, buffs, resource restoration,
HP/stun damage), chance/internal cooldown, stacking policies, and per-hit scaling
by projectile flight time/distance. These are implementation tools, not proof that
every class currently has those nodes. No assigned trigger or flight-scaling
templates were found in the three current trees; the Mage distance node is an
assigned conditional example. Check graph connectivity and point cost before
claiming a complete reachable build route.

Sources: the three tree assets, templates under
`Assets/Resources/PassiveTrees/Templates/`, class assets under
`Assets/Resources/Heroes/`, `Assets/Scripts/Skills/PassiveTree/PassiveNodeTemplateSO.cs`,
`PassiveSkillTreeSO.cs`, `Effects/PassiveNodeEffects.cs`, `Effects/PassiveEffectRuntime.cs`.

## 6. Implementation palette for future concepts

- Existing recipes: windup/strike/recovery, movement lock, impulses/charges,
  blink/stab, parry, circles/rectangles, one-hit persistent hitboxes, ordinary,
  ground and orbital projectiles, chain targeting/damage, instant beam, arrow
  rain, plasma field, winter storm, ice skates, self stasis, status applications,
  quick/stat-derived buffs, cooldown edits, shield generation/consumption.
- Steps can add scoped stats, damage conversions, ailment-stack scaling and
  on-hit splash or stun/launch. Channeling and nested groups exist. Check each
  controller before assuming generic steps share all scaling and targeting rules.
- Persistent melee hitboxes normally damage each target once per activation;
  persistent visuals do not imply repeated damage. Ordinary piercing projectiles
  have hit memory; reversal can clear it. Orbiters support a per-target rehit
  cooldown, and pulsing projectiles have separate timing.
- Recipe timing is normalized to the action pipeline. New mechanics must account
  for windup, active window, recovery, cancellation and weapon-swap persistence.
- A concept may require code (marks, custom resource meters, trap ownership,
  detonating/consuming ailments, bespoke enemy targeting). Label that honestly;
  the existing palette is a starting point, not a restriction on creativity.
- Automatic descriptions read recipes. New step semantics need description,
  preview and EN/RU support; do not encode gameplay only in legacy tooltip prose.

Sources: `Assets/Scripts/Skills/Steps/StepEntry.cs`, `SkillRecipeSO.cs`;
`Assets/Scripts/Skills/Logic/SkillStepRunner.cs`;
`Assets/Scripts/Skills/Projectiles/SkillProjectile.cs`;
`Assets/Scripts/Skills/SkillDescriptionGenerator.cs`; `Assets/Scripts/Skills/SKILL_EDITOR_GUIDE.md`.

## 7. Design opportunities (hypotheses, not approved content)

- Differentiate poison's sustained ramp from bleed's limited heavy-hit stacks.
  Venom Strike already rewards existing stacks; a second uncapped ramp needs a
  distinct decision or payoff, not just stronger coefficients.
- Add staff variety beyond the three pulsing large balls: different positioning,
  timing or target-selection decisions. Account for existing chains, homing,
  instant beams, orbiters, storm and field skills before claiming novelty.
- Give 1H sword/axe/hammer more distinct primary loot outcomes; each currently
  has one. Dagger and wand already have multiple alternatives.
- Explore long-range payoff vs close-range risk using distance/flight hooks,
  but Rogue passive support would need authoring; it is not already available.
- Explore shield charge spending vs preserving defense, with explicit failure
  behavior at zero charges and joint potency/duration budgets for effectiveness.
- Ground-based hammer skills can emphasize terrain, commitment or interruption,
  while specifying airborne and boss behavior. Another straight ground wave
  would overlap Ground Shake.
- Returning and orbital chakram behavior supports positioning decisions. Any
  new repeated-hit design must state hit memory, return leg rules and caps.
- The playtest backlog asks for more mage skills, more passive choice, and clearer
  poison/bleed identities. Treat backlog entries as requests, not completed work.

For a proposed skill, describe its weapon/slot, player decision, contrast with an
existing skill, damage tags/channel/conversions, timing, cost/CD, hit cadence and
target cap, 2-3 relevant build hooks, weakness, boss/airborne behavior, and whether
existing steps suffice. Keep initial numbers provisional. Compare single-target
and pack outcomes, sustain, ailment buildup, cooldown loops and visual clarity;
useful novelty can be spatial or temporal rather than another damage multiplier.

Source: `Docs/Playtest_Feedback_Backlog.md`. These opportunities are analysis,
not user-approved direction or shipped features.

## 8. Discussion memory and refresh rules

- Confirmed request: build an English factual foundation and reusable
  `game-designer` skill before collaboratively inventing new weapon skills.
- 2026-10-20 — Extraction implemented at the user's request, including the
  follow-up decision to make its portion a flat BleedExtraction stat (base 50%)
  with affixes. The numbers in `Docs/Extraction.md` are initial tuning, not a
  completed balance validation.
- 2026-10-20 — Follow-up implemented: separate flat-only BleedExtractionChance
  (base 0%) controls the shared hit mechanic. Extraction grants +100 chance for
  its own hit; BleedExtraction remains the damage portion with base 50%.
- Add accepted design decisions here with date, scope, rationale and status
  (`proposed`, `accepted`, `implemented`, `rejected`). Keep unaccepted ideas out
  of factual sections; record rejection reasons when useful to avoid repetition.
- When the user asks to retain a decision, update this file. After relevant game
  changes, refresh affected facts and source revision/date. Do not silently turn
  a snapshot into an always-current claim or perform unrelated automatic writes.
- Resolve disagreements using current assets + their runtime consumers, then
  behavior tests/live Editor evidence where needed. Names, comments, old design
  notes, spreadsheets and visual node types are weaker evidence.
- This snapshot is not a balance audit or a passing-test report. Future code or
  Unity asset changes require project verification; documentation-only updates
  require reference/structure checks instead.
