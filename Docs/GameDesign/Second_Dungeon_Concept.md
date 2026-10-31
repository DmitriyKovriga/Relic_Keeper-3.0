# Second dungeon concept — The Ashen Foundry

Current discussion: [launch platforms and combat islands](Second_Dungeon_Launch_Islands.md),
developing the user's subsequent movement proposal. The foundry text is history.

Date: 2026-10-31. Status: **original furnace-circuit mechanic rejected by user**.
The original text below is retained as discussion history, not an implementation
brief. Its Keeper-dependent roster and boss proposal are also on hold. No visual
theme or replacement mechanic has been selected.
Supersedes graphics-refresh work as the current discussion topic; previous
graphics documents/assets are retained, not deleted or applied by this task.

## Grounding

### User feedback and revised design criteria

The vent/Keeper loop adds pressure to move and prioritize enemies, overlapping
the role Mold already performs in Mortfall. Increased demands alone are not a
compelling player-facing payoff. A signature mechanic should offer a desirable
new action or consequence and work with the game's ordinary combat rather than
exist simply to distinguish the biome.

SO_Mold confirms a stationary ranged threat. This supports the user's comparison;
do not repackage stationary pressure as a unique second-dungeon feature.

Replacement exploration, not accepted design:

1. **Resonant cores (preferred prototype candidate):** selected relic-bearing
   enemies leave one inert core on death. A fresh player direct attack activates
   its short, clearly drawn blast; nearby cores can chain. An inactive core does
   not harm the player or demand attention. Reward is deliberately arranging a
   satisfying pack clear, not stopping an environmental punishment.
2. **Borrowed techniques:** defeating selected enemy roles grants one visible,
   consumable addition to the next attack. Reward is briefly turning enemy power
   against the formation. Risks: overriding build identity, unwanted consumption,
   UI/skill compatibility and substantially larger implementation scope.
3. **Risk-for-power relics:** an optional room relic grants a strong local offensive
   advantage together with a clearly chosen combat complication. Reward is player
   agency and a temporary power fantasy. Risk: this may duplicate current dungeon
   modifier choices; insufficient novelty unless its interaction is distinct.

For the core candidate, test one carrier and three ordinary enemies first.
Kill carrier -> choose whether to hold or activate the core -> bait enemies into
range -> attack core -> visible burst and limited chain payoff. Use ordinary
attacks without a new input or item slot. It must be understandable for melee,
ranged and spell builds; persistent damage must not silently consume newly
created cores. Exact direct-hit activation semantics are new design/runtime work.

Core constraints: no spawning on every enemy; no carrier focusing mandate or
blanket immunity; bounded lifetime/cap with harmless expiry and clear feedback;
safe placement near the original fight; no chain triggering through solid terrain;
bounded damage/activation identity; no on-hit sustain, ailments, extra cores or
loot recursion from the environmental blast. Specify XP/kill attribution before
implementation. Its payoff must matter without clearing every elite in one hit
or removing the value of actual AoE builds. Do not nerf ordinary player damage
to force interaction. No damage-to-player timer, compulsory detonation or exit gate.

Reject the candidate if normal AoE always triggers everything accidentally, all
players ignore it, players wait for long kiting setups, or successful use is only
an automatic death explosion. A useful prototype must show both a clear voluntary
choice and an immediately satisfying consequence. This is a hypothesis to test,
not a guarantee that chain explosions are intrinsically fun.

### Original grounding

MortFallDungeonSO currently defines levels 1–10 and ten rooms. The requested next
band is 10–20; treat level 10 as the suggested entry boundary, not a confirmed
overlap/scaling rule. No authoritative world-lore bible was found in the focused
documentation search. Existing ruins, undead, magical equipment and relic items
support the proposed tone, but do not prove an ancient foundry is existing canon.

The player wants a substantially harder dungeon with a new mechanic, cohesive
visual identity and enemies that cooperate. 'An order harder' is an experience
target, not authorization to multiply damage/health by ten. Combat/movement
redesign and sound are not prerequisites.

## Pitch and provisional lore

Beyond the ruined outer structures lies a facility once used to encase unstable
relic fragments in metal. Its bound workers and guardians still maintain a
broken refining cycle. This is a possible connection to Mortfall, not a required
physical exit from its current map or a claim about existing history.

## Visual identity

- Large furnace doors, layered masonry, iron braces, extraction shafts and
  cooling channels. Human-scale work structures contrast with oversized relic
  machinery; remain medieval dark fantasy, not a modern factory.
- Cold charcoal/blue-gray stone and soot; restrained copper and ember accents.
  Recessed active channels are warm; inactive machinery remains dark. Keep enemy
  warnings distinct from ambient fire and the player's existing Ignite visuals.
- Large isolated backlit silhouettes; sparse ash behind actors. Avoid full-screen
  orange fog, fire on every surface, heat distortion or foreground sparks that
  obscure platform edges at 480x270.
- Damage zones must match actual playable surfaces. Background lava and pipes
  do not become hazards without a distinct readable outlet/ground marker.

## Signature mechanic — furnace circuits

A marked Furnace Keeper enemy powers a small authored set of vents on selected
platforms. Each circuit cycles through quiet -> visible charging -> one short
eruption -> cooldown. Killing its Keeper disables future cycles for that circuit.
An eruption already clearly committed finishes; no delayed new eruption follows
its death. The keeper does not resurrect or receive invulnerability.

The decision is to fight nearby enemies in safe sections or cross their formation
to remove the source of environmental pressure. The same readable eruption can
damage the player and ordinary enemies; foundry creatures are not universally
fire-immune. This permits luring/positioning without requiring pushback builds.

Prototype constraints:

- One circuit/keeper per encounter; one active vent at a time initially.
- Later, at most two coordinated vents, retaining a reachable safe area.
- Fixed authored vent positions and clear charge animation; no zone appearing
  under the player at the last instant. Preserve a full escape window after a
  warning enters view; validate the camera/activation bounds.
- First timing hypothesis: roughly 1.2–1.6 s warning, 0.5–0.8 s eruption and
  4–6 s quiet interval. These are prototype values, not current gameplay or final
  balance. Start with a single hit per eruption per actor, not per-frame damage.
- No accumulating global heat meter, permanent stat penalty, compulsory resistance
  threshold or elemental immunity. Avoid softlocks and unavoidable chip damage.
- Vent triggers/damage need explicit new support; current fire VFX alone does not
  implement this. Coordinate damage/status rules, disable/death cleanup and
  circuit reset on room unload. Do not classify vents as extra living enemies.
- Keep existing early portal-unlock behavior. Disabling the circuit is tactical,
  not a new clear/exit requirement; remaining hazards cannot damage menus/cache.

## Enemy roster

| Enemy | Role and action | Relationship to circuit / counterplay |
|---|---|---|
| Ashbound Worker | Low-complexity advancing melee enemy; modest short-range swing. | Occupies attention and safe space. Can be lured into vents; no obligatory death explosion. |
| Crucible Guard | Slow, broad, committed horizontal weapon sweep; recovery stays visible. | Screens approach to the Keeper. Bait the sweep and cross during recovery; no universal projectile immunity. |
| Cinder Slinger | Lobbed, clearly marked projectile at an area, with gaps between shots. | Encourages leaving a comfortable safe platform. Limited persistent effect; coordinate impacts with vent escape lanes. |
| Furnace Keeper | Recognizable support enemy maintaining the circuit; weak close defense and a simple ranged attack, never a heal loop. | Priority choice. Starts in a reachable defended position, not shootable from every room entrance; no untargetable shielding. Killing it stops future vent cycles. |
| Slag Hound | Fast short rush followed by a substantial recovery; low durability. | Punishes waiting indefinitely at range. Rush is not launched into the only vent-safe landing at the eruption moment. |

Start with Worker, Keeper and one vent; add Guard, then Slinger. The Hound is a
later optional roster extension, not a fifth new AI required for the first test.
Existing approach/melee/projectile/charge systems are reuse candidates, not proof
that these exact attacks or enemy art already exist. Avoid assembling every role
in every room. Basic placement and concurrent-attack limits carry much of the
difficulty; an adaptive encounter director is not an MVP requirement.

## Encounter examples

1. Keeper + two Workers + one vent: safely teaches that killing a particular
   enemy changes the room. First demonstration occurs before a dangerous pack.
2. Guard on the approach + Keeper on a reachable side platform: bait, reposition,
   then choose to commit to the source or clear the guard first.
3. Slinger covers a comfortable firing perch while a vent threatens another:
   move vertically, with a third safe landing kept clear during the sequence.
4. Hound + Guard without a circuit: teaches the new rush separately and provides
   visual/mechanical contrast. The mechanic need not appear in every room.
5. Late combined fight: Keeper + Guard + Slinger, two coordinated vents. Their
   attacks must leave an executable escape/attack opportunity, not merely a
   mathematically unoccupied but unreachable pixel.

## Difficulty and progression

Opening segment teaches one circuit. Middle adds defended sources and mixed
horizontal/vertical threat. Final segment combines familiar roles with denser
decisions. These are content stages, not a committed number of generated rooms.

Compare representative level-10 entry builds and level-20 destination builds.
Tune enemy durability, incoming damage and pack composition separately. Avoid
turning every basic enemy into a sponge or stacking baseline difficulty with
existing double-enemy/area modifiers before validating safe routes.

Check melee, ranged, slow heavy attacks, poison/DoT and burst builds. Removing the
Keeper should be valuable without making every pack collapse instantly; ignoring
it should be risky but viable. Environmental damage should not become the fastest
universal farming strategy: keep it bounded rather than automatically scaling
with a target's max HP. XP/loot attribution for environmental kills needs explicit
design and implementation; do not silently grant full player on-hit effects.

## Boss and future setting

Provisional boss: The Crucible Warden, a bound custodian with a relic crucible
inside its armor. Its existing-style weapon attacks combine with the already
learned vent cycle. Interruptible cooling opportunities expose the core; do not
introduce an unrelated final-room puzzle or long invulnerable waiting phase.
Detailed boss attacks, phases and values remain a separate design step.

The foundry introduces one aspect of the world: people once tried to contain and
use relic power, and their infrastructure outlived them. Future dungeons can show
storage, transport or consequences in a different ecosystem, rather than reuse
furnace vents everywhere. Do not lock the entire future game into an industrial
theme or assert a cosmic explanation for every relic at this stage.

## Smallest proof and acceptance

One graybox room, one vent circuit, Keeper + Worker, existing temporary art;
then one Guard formation. Verify the new decision before producing a full tileset
or five complete enemy sprite sheets. Native visual target follows the test.

Success: a player understands the source/vent link without a tutorial paragraph,
can survive a first warning, and makes a meaningful priority/positioning choice.
Reject or revise if Keeper sniping trivializes every room, melee cannot reach it,
safe-space camping is always dominant, or coordinated warnings still allow an
unavoidable hit. Preserve room exits, cache convenience and existing input rules.

Open decisions: user approval of theme/mechanic; actual lore constraints; exact
level-band boundary; final roster size; hazard damage/defense/ailment semantics;
environmental kill rewards; difficulty targets after normal-build testing.
