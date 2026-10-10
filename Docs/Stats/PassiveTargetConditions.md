# Conditional enemy damage taken

Use the node's **Conditional Stats** section:

- Condition: **Distance to enemy**.
- Distance comparison: **at least** for distant enemies, **at most** for nearby enemies.
- From Player: distance in **cells (24 pixels each)**, measured in the 2D XY plane.
- Apply to: **Enemy**.
- Modifier: **DamageTaken**, **Increase**, **25**.

PushbackNode uses this configuration with an **8-cell cutoff** and keeps
its +100 flat Pushback. Its former empty projectile-flight scaling rule was
removed. The distance is configurable in the node; no combat code change is
needed to tune it.

The current Hub camera uses 24 pixels per unit and the grid uses 1-unit cells,
so 8 cells equals 192 world pixels or 8 Unity world units. `DistanceUnits` keeps
its serialized field name, but the editor and EN/RU descriptions display cells.

Distance is checked from the allocating player's current position to the enemy
when damage is received. Both hits and damage over time from that player qualify.
Projectile travel distance and elapsed flight time are separate mechanics.
The boundary is inclusive, and each enemy is evaluated independently.

These modifiers apply only to damage originating from the node owner. They are
not persistent enemy stat modifiers or a global vulnerability for other sources.
Increased DamageTaken adds to shock and the enemy's other increased damage taken,
then More/Less modifiers multiply the result. Enemy destination currently accepts
DamageTaken only; the editor warns about unsupported stats or a distance condition
configured for the Player destination.

Existing conditional groups default to **Player**, preserving their current
resource and event-recency behavior. Those conditions also work with the new
enemy destination. Rebuilding or removing allocated nodes removes the bonus
immediately. Player-facing descriptions and tree search support EN/RU.
