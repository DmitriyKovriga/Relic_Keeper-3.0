# EffectDuration

EffectDuration is a modifier container, rather than a single duration shared by
all effects. Each application creates a private CharacterStat with the effect's
already calculated duration as BaseValue, copies EffectDuration modifiers, and
stores the resolved seconds on the effect instance. The shared base value is
ignored and never overwritten.

The order is `(duration + flat seconds) * max(0, 1 + increased/reduced / 100) * More/Less`.
Results are clamped to zero. For example, EffectDurationNode's +45% increased
duration makes a 10-second effect last 14.5 seconds.

The applying character supplies the modifiers for skill buffs, debuffs and
ailments. Self effects without an explicit source use the carrier's stats.
Duration is a snapshot: changing stats does not retime existing effects. A
refresh recalculates from the authored duration using current modifiers. Buffs
that modify EffectDuration affect subsequent applications, without accumulating
duration into the shared stat.

Authored effects, timed quick/stat-based effects, event-created effects, poison,
bleed, ignite, freeze and shock use this calculation. Ailment-specific duration
stats are resolved first. Spread ignite keeps its resolved duration and is not
scaled a second time. Untimed recipe effects remain tied to their cleanup handle;
EffectDuration does not give them a timer. Explicit event extensions are added
as written and are not new effect applications.

Automatic skill descriptions and linked effect tooltips use the same calculation
with the current player's stats in EN/RU. Legacy authored prose cannot dynamically
recalculate embedded numbers. Live effects retain their application snapshot,
while skill previews show the duration of a new application with current stats.
