# Shock

Enemies have a hidden shock threshold equal to 30% of maximum HP. Successful
shock procs contribute the hit's lightning damage before mitigation, multiplied
by Shock Application Effectiveness / 100. Other damage channels, failed procs,
and avoided procs contribute nothing. Lightning no longer needs to account for
30% of the hit. Partial buildup persists until the threshold is depleted.

Shock Application Effectiveness has a base value of 100%. Shock Effect Magnitude
has a base value of 50% and determines increased damage taken from all sources,
including damage over time. This increase adds to other increased damage taken
before More/Less modifiers. Both stats support ordinary global stat modifiers.

Shock uses the existing Shock Duration (fallback: 2 seconds). Successful procs
while shocked refresh duration immediately and retain the strongest magnitude.
On expiry the threshold is restored in full; excess buildup is discarded.
Disabling or reinitializing an enemy clears buildup and shock. Player targets
retain immediate proc application without an enemy HP threshold.

Both stats have EN/RU labels and T1-T5 affix families for Flat, Increase,
Decrease, More and Less, each in Light/Medium/Strong strengths. Positive Medium
Flat and Increase variants are included in the existing pools offering ShockChance.
Use `AffixContentMaintenance.GenerateShockFamiliesFromCommandLine` to generate
missing content without replacing existing assets.
