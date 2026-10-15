# Unique items

Enable `IsUnique` on an equipment base and populate `UniqueAffixes` with its
authored affixes. Each generated copy receives every listed affix, with one
value rolled within each stat's bounds. Normal random affix pools are ignored.
Register the base, affixes and granted skill in the item database, as usual.
Existing item saves preserve rolled values, affix tiers and granted skills;
Unique identity is resolved through the saved base ID.

`ItemDatabaseSO.UniqueItemDropChance` defaults to `0.001`: 0.1% of successful
equipment drops before increased rarity. Equipment quantity/drop checks still
happen first. Unique selection precedes Rare and Magic and uses only eligible
Unique bases. Normal rarity rolls exclude Unique bases. With no eligible Unique
base, selection falls back to the existing Rare/Magic/Common selection.
The editor loot-chance HUD uses the same effective probability calculation.

Unique tooltips and loot labels use orange-gold. Affix crafting orbs cannot alter
Unique items. Existing loot filters never hide Unique items at the Rare threshold.

God's Helmet uses a native 48×48 transparent inventory icon for its 2×2 slots
(24 pixels per slot). The skill and buff share a separate native 24×24 icon.
Both use point filtering, no compression, no mipmaps and no power-of-two resizing.

God's Helmet has 25 base armor, 50–150 maximum life, 15% to all four resistance
channels and 30% area of effect. Its equipment skill, God Of Strenght, costs
50 mana, has a 60-second base cooldown and applies a 20-second base buff.
Every full 50 maximum life grants 10 flat physical damage and 5% area of effect.
Maximum life is sampled when activated; reapplication refreshes rather than stacks.

`DerivedStatModifier.SourceStep` supports other stat-scaled effects: zero keeps
continuous scaling; positive values round the source down to complete steps.
For this buff, source step 50 and source percentages 20/10 give 10 damage/5% area
per step. Both status effects and skill stat links use the shared evaluation.

`Tools > Items > Build God's Helmet` rebuilds the authored assets using the
existing artwork and stable asset paths. It is an explicit authoring action.
