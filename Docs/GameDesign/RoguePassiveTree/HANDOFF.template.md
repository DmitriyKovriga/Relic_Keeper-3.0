# Rogue passive tree - design v4 / implementation handoff

Status: **proposed**, 2026-10-23. This is a complete content specification for review,
not a claim that the tree has been implemented or balanced in combat.

## Files and authority

- `rogue-passive-tree.html`: offline interactive Russian review, including hover,
  pinned details, search, node table, core routes and defense alternatives.
- `rogue-passive-tree.json`: canonical node-by-node specification; exact stat names,
  enum values, modifier types, geometry, source/proposed adjacency and effects.
- `rogue-passive-tree.csv`: the complete list, UTF-8 with BOM, suitable for tools.
- `NODES.md`: human-readable list of all 130 nodes, effects, resolved coordinates
  and proposed neighbor IDs. English names accompany Russian names.
- `validation.json`: structural audit and route cost evidence.
- `build_design.py`, `content_design.py`, `design_audit.py` + the two `.template` files: reproducible authoring sources.
  Regenerate with the bundled Python runtime from the project root. The builder
  writes only this documentation directory, never Unity assets.

If prose, preview rounding or a CSV display differs from JSON, use JSON and the
current source code's stat semantics. Do not infer formulas from flavor names.

## Source snapshot and preservation

Source: `Assets/Resources/PassiveTrees/RoguePassiveSkillTree/RoguePassiveSkillTree.asset`.

- Git revision: `__REVISION__`.
- SHA-256 of the actual working file: `__SHA256__`.
- The source already had an uncommitted modification before this task. This
  design reads that working version and preserves it. It does not restore HEAD.
- 130 nodes: 96 Small, 25 Notable, 8 Keystone, 1 Start. **Do not add extra nodes,
  change types, renumber IDs, move nodes, or replace the layout.**
- Existing graph: 148 undirected edges, all reciprocal. n73 and n74 are isolated.
  Proposed graph: 150 edges, all 130 nodes reachable. The only topology changes
  proposed for implementation are reciprocal n30-n74 and n33-n73. These connect
  two existing small leaf nodes; no existing edge is removed.
- Check the source SHA before implementation. If it changed, compare ID, type,
  placement and adjacency differences; preserve newer user changes and reconcile
  this plan rather than overwriting the whole asset.

Coordinates are **tree canvas coordinates**, not world metres or screen pixels.
Positive X is right; positive Y is down. `position` is resolved by the same formula
as `PassiveNodeDefinition.GetWorldPosition`. For `placementMode=1`, preserve
`rawPosition`, `clusterId`, `orbitIndex`, `orbitAngle` and the source cluster radius
and centre. Do not write the resolved position into an orbit definition or
recalculate angles from the HTML. Six decimal places in NODES.md are display only;
JSON/CSV carry full numeric precision. All original Bezier definitions remain
visual-only; proposed connections are dashed in the review.

## Accepted design direction and layout v4

User correction: matching damage archetypes must not occupy one local sector.
Spread **whole coherent feeder groups** across the existing graph; do not move
node geometry or randomly mix individual small effects inside a group. A player
reads one small node and then the notable(s), as in the Warrior tree.

- Attack speed: NW n55 and SE n178. More-speed keystone is west n85.
- Crit: NE junction n60 and SW inner orbit n149. Crit keystone is south n4.
- Poison: west upper orbit n87 and south inner orbit n118. Poison key is NE n9.
- Physical: NE junction n63 and west upper orbit n96. Physical key is north n8.
- Range: north-right orbit n161 and south outer orbit n117. Range key is NW n7.
- Conversion: cold north-left n160, lightning east-lower n128, fire SW n168.
  Elemental More key is east n2. The southern double ring has no conversion.
- Life, evasion, recovery and resistance groups stay interleaved. Defensive
  repetition is intentional: each offensive destination needs local alternatives.
- n3 is now Dance of Blades (evasion-to-crit scaling); n5 is Borderland Blade.
  All eight key concepts are retained but relocated. Their old IDs from v3
  are superseded; do not combine old and new effects.

This is a placement proposal, not proof of equal build power. Physical increases
still do not scale an already converted destination channel. Hybrids use shared
stats such as attack speed, crit, projectiles or deliberately retained physical
hit damage. A road crossing alone does not force buying every nearby cluster.

## Transport and shared templates

Transport is unchanged: **24 Small nodes, each exactly +3 MaxHealth Flat and
+5 Evasion Flat**, using the existing shared asset:
`Assets/Resources/PassiveTrees/Templates/Misc/NewPassiveNode 11/transportRogue.asset`,
GUID `292d6a03e1da8244ebd2b0d361b6dbcd`.

`n10 n11 n14 n15 n16 n17 n18 n19 n20 n21 n43 n49 n58 n59 n61 n62 n64 n65 n66 n68 n70 n76 n80 n111`

There are 72 specialized Small nodes, using only **10 repeated small templates**
across 21 feeder groups. All smalls within a group, including isolated centre
leaves n73/n74, have identical names and effects. The same template is reused
across distant groups where the package matches. There are 25 notable templates
and 8 keystone templates: **__TEMPLATE_COUNT__ new templates total**, not 105
unique assets. The existing transport template is additional and is not copied.
`templateRegistry` in JSON is the canonical list to create once by path; per-node
references must resolve to those shared assets. No unique small-node override.

Source pattern checked: the Warrior mana regen, life regen, maximum-life,
attack-speed and crit feeder groups repeat templates before their notable.
Warrior SmallEvasion grants +50 Evasion Flat / 10% increased Evasion; its
SmallMaximumLife grants +25 MaxHealth Flat. Those exact packages are reused as
Rogue proposals. This is pattern/value evidence, not runtime balance validation.

__CLUSTER_TABLE__

n87 and n96 share the same physical feeders: physical hits support poison as
well as direct damage. On double orbits c122 and c105, each orbit has its own
homogeneous package. This is the deliberate two-topic exception per visual
cluster, never four different small effects on one approach.

## Dispersion and complete-goal costs

__PORTFOLIO_TABLE__

These are **exact minimum costs of a connected allocation from free start**
containing all listed notable goals, with shared prefixes counted once. The
node-weighted Steiner calculation is appropriate here because each set has only
2-3 goals. It considers all graph routes, not only the sample offensive path.
JSON includes reconstructed allocation IDs for both allowed-graph variants.
The unrestricted variant includes all selected keystone bonuses and drawbacks;
it is not equivalent to the safer variant. These are not full endgame build costs.

Full in-tree conversion: **40% cold + 40% lightning + 40% fire**, only on the
three separated notables n160/n128/n168. Feeders grant ElementalDamage, not
conversion. Two goals give 80%; all three are required to reach 100% by this
passive tree alone. A 120% authored share is normalized by the existing runtime.
The exact cost is **23 points without keystones, or 18 through six keystones**
(n2/n3/n4/n5/n8/n9). The shortcut includes their real tradeoffs. Skills and gear
can already supply conversion and can reduce the required tree investment;
this document does not claim to prevent full-conversion builds using those.

Skill conversion executes before character conversion. Full conversion removes
poison's physical hit basis. No new exclusivity or conversion rule is requested.

## Defense accessibility evidence

The named primary route is a first destination, not a mono-archetype sector.
Each option below is measured independently from that allocated route. Costs
count only newly allocated nodes; shared prefixes are paid once. Transport
baseline is never counted as a defense alternative. A real specialized small
node may be a defense target; a notable is not required for a meaningful option.

__DEFENSE_TABLE__

At least two different defense families are accessible within four additional
points on every primary route. Additionally, `allDamageGoalDefenseAudit` checks
**every offensive notable and keystone**, not only the six example goals. Each
has distinct off-route specialized defense targets in at least two families
within four new nodes, without passing through an unallocated keystone.
Armor is residual: +50 on n152, with Evasion remaining its main investment.

## Hybrids

__HYBRID_TABLE__

Examples connect an initial speed group to the west poison notable, cold
conversion to north ranged damage, and southern poison to SE speed. Their shared
attack/physical/projectile basis depends on the equipped skill, not on map color.
All example extensions avoid unallocated keystones. Budget tables do not prove
hybrid DPS superiority; compare actual weapon recipes in combat before tuning.

## Stat semantics - implementation-critical

All node effects use existing `Modifiers` and, only on n3, `StatScalingRules`.
No new StatType, trigger engine, poison consumption, conditional engine, hit
scaling, status controller, skill or save schema is requested. Empty effect
arrays in JSON mean empty Unity lists, not missing future implementation tasks.

- Resolve `Stat` strings against the current `StatType`. JSON also includes
  `StatValue` from the inspected enum as a cross-check; do not rely on that number
  if the enum has changed. Preserve existing serialized enum identities.
- Types: Flat=100, PercentAdd=200, PercentSub=210, PercentMult=300,
  PercentLess=310. More/Less use positive magnitudes in Value. Never serialize
  a negative value with PercentLess for these proposed nodes.
- CritChance: most nodes use increased chance, not flat percentage points.
  +35 PercentAdd does not mean +35% absolute crit chance. CritMultiplier uses
  Flat percentage points. +25 raises a 150% multiplier to 175% before other layers.
- PoisonChance and PoisonDamageMult use Flat points. PoisonDamage also uses
  **Flat**, because AilmentController consumes `1 + GetValue(PoisonDamage)/100`
  and its initialized base is zero. PercentAdd-only PoisonDamage nodes can have
  no effect without another Flat source. Do not copy the warrior templates' type
  merely because their displayed wording looks similar.
- ProjectileSpeed, MoveSpeed and ReturningProjectileDamage use Flat points here;
  ordinary PercentAdd on a zero-base container can otherwise have no benefit.
  Projectile count/pierce/chain are Flat integer counts. Individual skill recipes
  may ignore those stats; passive allocation does not override recipe opt-outs.
- RangeDamage uses context modifier layers, so PercentAdd/More work without a
  positive scalar base. **Range is not Projectile.** Bow and some wand skills
  carry Range; several thrown/chakram skills do not. Physical damage, projectile
  speed/count and return damage on this route still support those weapons.
  Do not silently change skill tags as part of authoring this tree.
- Converted damage receives destination-channel and applicable context modifiers
  after conversion. Increased DamagePhysical does not automatically scale an
  already-converted elemental destination in this runtime.
- Health/mana maxima and Evasion have ordinary scalar layers. Armor and Evasion
  share a diminishing rating curve: 500/1000/2000/5000/10000 rating corresponds
  to 50/60/70/80/90%. Added Evasion rating is not an equal number of dodge points.
  Evasion does not protect against DoT; life and resistances remain valuable.
- n3 uses `PassiveStatScalingRule`: complete steps of 1000 **final Evasion**
  grant +5 Flat CritMultiplier. UseWholeSteps=true. At 2500 Evasion this grants
  +10 points; at 10000, +50. Static AttackSpeed/Armor effects remain separate.
  No new hard cap is specified. Existing runtime refreshes scaling after ordinary
  modifiers without feeding scaling rules into each other. Do not implement
  a self-reinforcing rating loop or change the Evasion cap.
- HP/MP on hit and More attack speed can compound with multihit skills and the
  current uncapped poison pool. Projectile count/chain can also multiply hit
  opportunities. Values are initial content tuning, not a promise of balanced DPS.

## Template authoring and registration

1. Reconcile the snapshot and confirm this design has been accepted for implementation.
2. Create each entry in `templateRegistry` **once** by its exact path, then assign
   it to every listed node ID. There are __TEMPLATE_COUNT__ new templates, including
   10 shared small packages, 25 notables and 8 keys. For `reuse-existing`, assign
   the existing transportRogue asset to the 24 transport IDs without modifying it.
   `none` keeps start free of a stat template. Do not create one asset per Small ID.
3. Map `nameEn` to Template.Name. Use `nameRu` for localized display through the
   project's established presentation path if supported; inspect that path first.
   Do not assume the template has a native NameRu field. Numeric descriptions
   should use the existing stat formatter rather than diverging manual formulas.
4. Populate Modifiers (`Stat`, `Value`, `Type`) and StatScalingRules. JSON fields
   use lower camel case at the node level; map them explicitly to Unity fields.
   ConditionalModifiers, TriggeredEffects and HitScalingRules remain empty.
   For reused transport, verify the existing two modifiers instead of modifying
   the shared asset. Template.Name may retain `transportRogue`; RU/EN Travel names
   are review labels, not a request to rename the existing shared template.
5. Assign Template on the existing node ID. Keep UniqueModifiers and
   UniqueStatScalingRules empty to prevent a duplicated grant. Do not copy old
   template bonuses on top of the proposed template bonuses.
6. Preserve all geometry, cluster/orbit data, existing visual curves and existing
   connections. Add the two reciprocal proposed leaf connections explicitly.
7. Do not alter skill pools, warrior/mage trees, character starting stats,
   equipment, affix pools, global base stats, damage formulas or save schema.
8. Select icons only as presentation work; do not infer mechanics from an icon
   or use identical names as a reason to merge distinct templates.

Localization is presentation-dependent: `PassiveNodeTemplateSO` currently has
Name/Description/Icon, not a dedicated localization-key pair. Keep the provided
RU/EN authoring strings, inspect actual UI resolution and agree the minimal
localization approach during implementation rather than inventing missing fields.

## Verification for the coding chat

- Recount types/IDs and compare all geometry values and existing edges to this
  snapshot; require exactly the two symmetric leaf connections as the graph delta.
- Validate __TEMPLATE_COUNT__ new templates (10 shared Small, 25 Notable, 8 Keystone) and 24 references to the single existing
  transport template, plus one free start. Require all 24 transports to have only
  +3 MaxHealth Flat and +5 Evasion Flat, no scaling/conditional/trigger/hit effects,
  no UniqueModifiers and no v1 road effects. Validate no duplicated modifiers,
  130/130 reachability, no unresolved templates and valid enum/type serialization.
- Re-run route cost checks and verify at least two different defensive choices
  within four additional points on each named archetype route, counting only
  specialized targets, never transport baseline as a defense option.
- Behavior examples: poison nodes improve zero-base PoisonDamage via Flat;
  n3 gives +0/+5/+10 CritMultiplier at 999/1000/2500 Evasion; refund removes it;
  conversion preserves total channel amount and normalizes excess shares;
  Range-only modifiers do not affect untagged projectiles; resource max changes
  do not permit allocation/refund healing exploits.
- Use Unity Bridge compile and narrow relevant EditMode filters. Existing useful
  suites include PassiveNodeEffectsTests, PassiveAilmentChanceTests,
  PassiveTargetDistanceTests and StatResourceMaxChangeTests. Add a focused asset
  regression for this tree when implementing; do not report zero tests as green.
- Inspect the real tree and tooltips in the Editor. This HTML is not a Unity
  runtime validation or an assurance that every skill has appropriate tags.
- Playtest comparable point budgets with physical, poison, converted and ranged
  weapons. Check multihit poison ramp, +projectiles, evasion/life choices, and
  whether any mandatory route pays for a stat its weapon cannot use.

## Research and artifact validation

Graphify MCP was used for cross-file discovery of PassiveTreeManager,
PassiveNodeTemplateSO, PassiveStatScalingRule, PassiveEffectRuntime and CharacterStat.
Returned source locations were inspected directly. Unity serialized IDs, cluster
placement, links and Bezier data were read from the working asset, not inferred
from the code graph or screenshot. Relevant consumers also include DamageCalculator,
AilmentController, PlayerStats, StatType, DefenseRatingCurve and SkillCooldownRecovery.

Artifact verification covers complete coverage, source hash preservation,
reciprocal/reachable graph, route costs and browser interactions. No Unity code or
imported asset was changed by this design task, so no Unity compile/test pass is
claimed. Final browser results are recorded in `browser-validation.json`.

## Source types and visual review

The working source promotes n60, n63, n67, n87, n129 and n150 to Notable.
All six receive notable-sized effects in this proposal. n60/n63/n67/n129/n150
are specialized, never transport. Counts: 96 Small, 25 Notable, 8 Keystone,
1 Start; 24 transport and 105 specialized node references. Preserve current types.
The HTML uses radius 11 for Small, 22 plus double outline for Notable, 26
for Keystone diamonds. Node symbols/colors indicate effect groups, not type.
Life, evasion, resistance, recovery and mixed defenses have separate styles.
visualStyle is review metadata, not a request for new Unity runtime fields.
The SVG has an accessible aria-label and no native title tooltip.
