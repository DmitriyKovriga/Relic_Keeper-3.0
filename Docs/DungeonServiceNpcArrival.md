# Dungeon service NPC arrival

Room-clear stash and merchant modifiers still spawn their existing NPC prefabs.
The dungeon spawner places their feet on the floor, sorts them in the World
actor layer above terrain, and uses the existing unlit HubArt material. Previously
these NPCs were assigned the Background layer and could disappear behind tiles.

`Assets/Resources/Dungeons/ServiceNpcArrival.asset` configures the entrance:
the existing dungeon portal fades in for 0.3 seconds, the NPC walks one world
unit over 1.2 seconds, and the portal fades out for 0.3 seconds. Interaction and
the name label become available after the entrance. The NPC's existing idle
Animator then resumes. The temporary portal is visual only and belongs to the
room; it does not trigger room travel or collisions.

The entrance checks both directions for walls and a continuous floor. If neither
direction is safe, the NPC emerges in place rather than walking off a ledge.
Disabling an arriving NPC restores its idle state and removes its portal.
Hub NPC entrances and scene placements are unchanged.

Both walk sheets contain nine 28×28 frames at 24 PPU, with Point filtering and
no compression. PixelLab MCP generated eight movement frames from each existing
idle sprite: market job `3725ea37-eebc-4641-a04e-2cd5cc419adf` (seed 1220),
warehouse job `f617b847-0401-473b-a745-d935b1813684` (seed 1221).
PixelLab's workbench preserved the original head rows 0–13 and removed an
invented market staff. Final workbench results:
`a6540dd5-dd87-4047-8291-5160aad39ab7` and
`5dc1744a-2dca-4f34-a13a-e05cca421c07`. Frames were assembled into horizontal
sheets without scaling. Tools → Hub → Build Dungeon NPC Arrival Assets rebuilds
the import settings and references while preserving the settings asset GUID.

Validation: six DungeonServiceArrivalTests passed. An isolated Play Mode render
of MortfallRoom_001 at 480×270 confirmed both NPCs during the portal entrance and
after returning to idle; a live disable check confirmed portal cleanup. This
visual check did not start a dungeon run or alter saved progression.
