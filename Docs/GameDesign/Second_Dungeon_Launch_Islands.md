# Second dungeon — launch platforms and combat islands

Date: 2026-10-31. Status: development of the user's proposed direction, not an
approved implementation specification. Earlier furnace, core and alternative
mechanic lists remain rejected/unselected history. The current design topic is
enhanced dash-jump launch surfaces, chasms and compact connected combat spaces.

## Experience target

The player launches across a visible gap, chooses an approach to an enemy
formation, lands with usable control and can launch again to change the fight.
The enjoyable action is traversal integrated with combat: flanking, intercepting,
attacking on approach and choosing the next island. Avoid reducing this to a
mandatory jump followed by an unrelated sealed arena.

## Provisional visual setting

The Hanging Bastions: broken fortress/work terraces over an enormous ravine.
Old transport plates connect surviving platforms. These mechanisms once served
the structure rather than existing solely for the player. This is proposed lore.

Strong stone silhouettes, bronze/iron launch plates, taut architectural supports,
distant vertical structures and a quiet deep backdrop. Launch direction and
destination share readable shape motifs. Use open space to frame fights; a large
gap should make a launch feel powerful without requiring an enlarged camera zoom
that makes actors unreadable. No new audio dependency.

## Launch contract to prototype

- Suggested input: ordinary Jump while grounded on a marked launch surface.
  No separate ability slot or new button. Landing/running over it does not
  automatically relaunch or interrupt a fight.
- Each surface has an authored direction and core trajectory. Start with one
  standard profile and a few authored variants only if necessary.
- Preserve bounded airborne correction and a normal remaining aerial action
  budget; do not grant unlimited double jumps or reset defensive cooldowns.
- Straight traversal with neutral input must reach a generous receiving zone.
  Controls can adjust entry position and timing without making every crossing
  a precision challenge. Tune across class/stat extremes instead of increasing
  global JumpForce or MoveSpeed to make the map work.
- Existing PlayerMovement has TryPerformDashJump, launch carry, air control and
  fast fall. They are reuse points, not an already implemented launcher. Audit
  movement locks, impulses and each representative airborne attack: attacks must
  not unexpectedly erase launch velocity and drop the player into a gap.
- First validate basic attack/movement combinations. Do not promise compatibility
  with every blink, channel or custom movement recipe without checking it.
- No automatic damage burst or landing shockwave is required. A powerful movement
  tool can support the player's existing skills without becoming a free AoE skill.

## Arena grammar

A combat room initially contains two or three visible/legible islands. These
belong to one RoomController encounter: cross-island travel is not a room load or
an enemy reset. More islands are not automatically better.

Each receiving island needs:

1. A generous arrival apron, not occupied by an enemy body at encounter start.
2. A compact but functional fighting area: room for a heavy swing, enemy warning
   and a real dodge/retreat. Measure this from actual actor/hitbox/attack extents.
3. One simple LOS break or elevation feature when needed to prevent sniping the
   entire island from the departure point.
4. An intentional outgoing launch or ordinary safe return route. The fight is
   not allowed to trap a melee build beside a ranged enemy on an unreachable isle.

Use roles: broad staging island, pressure/frontline island, elevated support
island, and an occasional optional detour. A micro-platform is transit, not a
default fighting arena. Geometry must offer choices rather than decorative
copies of the same narrow ledge.

## Representative encounter

From a broad starting ledge the player can see a Guard/Worker formation on the
middle island and a ranged attacker on the upper island. Readable launch paths
offer a direct entry to the middle fight and a longer approach toward the upper
side. Reveal destinations before commitment; do not shoot at the player from an
unrevealed screen. If both cannot be legible at normal framing, simplify the room.

Route A: enter the middle island, bait the Guard's committed attack, then use its
recovery to reach the outgoing launcher and attack the ranged support above.
Route B: take the upper approach first, fight the ranged enemy, then return from
above/behind the frontline. Both routes expose different risks and remain viable.

A later pursuer can follow via the same launcher after a readable preparation,
giving the player a reason to intercept it or leave again. Introduce this only
after basic island combat works. Do not spawn a new pack on every landing or
make every departure trigger unavoidable attacks at its destination.

## Enemy roles

| Role | Contribution | Fairness and counterplay |
|---|---|---|
| Terrace guard | Holds part of an island with a committed directional sweep. | Bait the attack, use recovery or approach from another launcher. No automatic turn-and-hit on landing. |
| Ordinary melee worker | Makes compact fights tangible; follows locally. | Limited numbers; cannot fill the entire receiving apron or require a new traversal AI in the first prototype. |
| Gallery marksman | Creates a reason to change island/height and close distance. | Visible aim and travel time; reachable by ordinary launcher routes. No blanket projectile immunity or unavoidable fire into arrivals. |
| Leaper/pursuer | Uses designated launch links to follow between islands. | Explicit takeoff warning and stable landing area, no instant pursuit. Its AI link/recovery is new work, not assumed from current navigation. |
| Later air interceptor | A limited flying/suspended enemy contests part of a route. | Optional expansion; do not block the only crossing. Add only after flight attacks and camera coverage are proven. |

Do not combine all roles immediately. Start with Guard + marksman; ordinary
melee is a simple density ingredient. Pursuer and interceptor are later tests.

## Difficulty and build compatibility

The new difficulty is committing to a destination and making decisions on a
compact battlefield while support operates on another island. Increase pack
complexity only after there is room to answer its attacks. Faster enemy actions
and cramped ledges must not collectively remove that answer.

Melee uses launches to close gaps/flank; ranged chooses firing angles but cannot
always clear everything from a safe departure ledge; slow heavy builds retain
enough standing area and recovery space. Poison/DoT may attack then move away;
this should remain a legitimate build strength. Do not suppress existing skills
or use unexplained range immunity to force travel.

Counter idle sniping with actual terrain/LOS, reachable enemy pressure and routes,
not universal projectile cancellation at a chasm. Limit pursuit per island so
all enemies do not stack on the landing point. Simulate fast and slow builds,
left/right travel, reverse routes and airborne movement skills.

## Falling and encounter continuity

Prototype with harmless fast recovery to a validated safe receiving/staging
position; later damage penalties remain an explicit design choice. Permanent
death from the first misread jump would test tolerance for punishment rather
than the movement's appeal. A receiving position must have terrain support and
space, not simply equal the player's most recent grounded pixel.

Recovery must not refill health/mana or restart enemies, rewards or the room.
If a brief protection window is necessary for safe recovery, bound it and prevent
attacking during its protected repositioning; do not add an abusable combat exit.
Resolve multiple failed landings and held inputs without an infinite launch loop.

Specify enemy-fall reward attribution and room kill counts before implementation.
Current RoomController has out-of-bounds cleanup, but that is not proof of the
desired chasm-kill semantics. Ensure launch gaps do not break cache placement:
one loot box stays on a wide safe exit island, reachable before/after unlock.
Do not scatter items across narrow islands or collect them through chasm jumps.

## Content progression

1. Safe first launch: broad receiving platform, no enemy on the apron.
2. One compact fight after a launch; learn braking and attacking after landing.
3. Two connected fighting islands, support on one and frontline on the other.
4. A route choice: direct pressure or a longer flank; introduce the pursuer alone.
5. Late encounters combine known roles and return links, not tighter jump tolerances.

Use calm staging/loot spaces between encounters. Travel is repeatably enjoyable
movement, not a long empty segment before every pack. Keep existing portal-unlock
rules initially; reaching the exit via a skilled route is not a bug to patch by
adding mandatory full-clear locks. Revisit reward/route incentives separately if
everyone trivially skips the room.

Provisional boss arena: a main broad platform and two side launch positions.
Launches let the player change attack angle or cross over a committed sweep.
The boss remains attackable from ordinary footing; do not make the whole fight
waiting for a launcher-only vulnerability. Detailed boss design remains pending.

## Smallest prototype and acceptance

One graybox room, two broad islands, one outbound/return launch profile and safe
fall recovery. First assess control and landing with no enemies. Then add one
Guard and one marksman to test whether crossing creates a satisfying approach.
Only then test a third island/alternate route or one pursuer.

Success: players voluntarily launch during a fight, can explain their destination
choice, land with usable control and enjoy crossing again. Ranged and melee have
different viable routes. Fail if launch is only mandatory transit, landing damage
is unavoidable, the best strategy is endless disconnected sniping, or the flight
requires fighting existing attack/movement locks.

Before producing a full biome, record native-size flight/combat clips, route
choices, landing misses and camera visibility. Final art, numerical trajectories,
enemy count/damage, input details and fall penalties remain tunable proposals.
