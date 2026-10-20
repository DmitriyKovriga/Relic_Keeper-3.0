# Extraction

Implemented 2026-10-20 as a one-handed axe primary alternative to Weapon Throw.
The nearest living target in a forward rectangle takes one normal weapon hit.
Every connected direct hit can roll BleedExtractionChance to consume the
strongest **existing** bleed before rolling new ailments. Extraction itself
only grants +100 flat percentage points of that chance to its own hit; it does
not grant additional extraction damage. A miss or dodge does not consume a wound.

Strongest means highest damage per tick, matching the existing bleed stack-cap
comparison. Equal-potency wounds prefer the longer remaining duration. Only one
stack is consumed; weaker stacks and their timers remain intact.

The additional raw damage is:

`stored bleed tick damage × remaining seconds / tick interval × BleedExtraction / 100`

Fractional seconds count proportionally. BleedDuration and EffectDuration enlarge
the stored duration on application; BleedDamageMult and BleedDamage determine
stored potency. Extraction does not resnapshot or multiply those values again.
The burst uses the existing pure bleed damage route: target damage-taken/shock
modifiers still apply, while armor, evasion, crit, hit recovery, execution and
new ailment applications do not. Kill credit belongs to the extracting attacker.
If the burst kills, no new bleed is applied to the corpse.

## BleedExtraction

- Appended to StatType (120), preserving existing serialized values.
- Global player base: **50%**, in DefaultGlobalBaseStats.
- Base + Flat only. Increase/More layers do not scale it. Negative values resolve
  to zero; positive values are not capped at 100%.
- Light/Medium/Strong flat-only affix families, five tiers each. Medium is in the
  physical one-handed weapon affix pool used by Small Adventurer's Axe.
- Initial Medium rolls: T1 5–8 percentage points; T5 20–30. Light/Strong use the
  existing 0.75/1.25 strength factors. These starting rolls remain tunable data.
- EN/RU stat labels, affix names/text and skill name/description are registered.
  Automatic tooltips show the current flat percentage, including equipment.

## BleedExtractionChance

- Appended to StatType (121). Global player base is **0%**.
- Base + Flat only; Increase/More layers do not scale it. Effective probability
  is clamped to 0–100%. A 25% chance succeeds for rolls strictly below 0.25.
- Independent of BleedExtraction, whose base remains 50% of remaining damage.
- Evaluated through the shared hit-ailment path, so equipment/passive chance can
  enable extraction on other melee and projectile hits without a special skill
  step. Damage over time and extraction bursts cannot trigger it.
- Light/Medium/Strong flat-only affixes use the initial 5–8 T1 / 20–30 T5 Medium
  curve, with Medium in the physical one-handed pool. EN/RU labels are included.
- Extraction's +100 chance is a private scoped modifier; it does not leak to the
  character's other skills. Its recipe has no bespoke extraction on-hit rule.

Initial skill tuning follows the existing ordinary-primary cost baseline: 100%
weapon damage, attack speed ×1, zero mana/cooldown. Windup is 0–18%, damage at
34%, recovery ends at 62%. Movement is locked through 62%. The hook-and-yank
visual runs from 16–58%; the target is not physically pulled. No forced bleed
chance is added: the ordinary hit uses the character/equipment bleed chance.

## Art and authoring

All icon/VFX artwork was created through PixelLab MCP:

- 32×32 icon: job `2102247b-2793-415c-892d-5b1035856fdb`, gallery image
  `c170dd81-0fe5-59f0-a5f5-7fc5f1051ae8`.
- Hook-and-rip VFX: effect `bcb1ba44-ad1f-4975-ae75-9ac4a4bdc007`, export
  `5f041ac0-8ca6-4add-93e8-7ed1f513f1cb`. Eleven 56×40 frames at 24 FPS, blank
  final frame, native-pixel origin (4,22), 24 PPU, point filtering, uncompressed.
  Original spritesheet/JSON are beside the skill. The existing runtime retimes
  the one-shot clip to attack speed.

`Tools > Skills > Build Extraction` rebuilds imports, animation, prefab, recipe,
skill, base stat and affix registrations while retaining asset identities and
avoiding duplicate pool entries. The skill editor uses its existing scoped stat
modifiers and exposes MaxTargets on rectangle hits (zero retains unlimited targets).

Regression coverage is in `ExtractionTests`, covering old/new wound ordering,
stack selection, remaining time, scaling, crit isolation, evasion, kill credit,
single-target facing, content registration and English/Russian text.

Verified in Unity on 2026-10-20 after the chance-stat follow-up: 29 Extraction tests, 2 AilmentDuration tests,
and 49 nearby Execution/AffixValueBalance/SkillDescriptionGenerator/
SkillProjectileHitbox tests passed (80 total). Before that follow-up, a live cast
at 480×270 produced one non-critical extraction burst and left a fresh bleed.
Afterward, the updated English/Russian chance-and-amount HUD tooltips were
visually checked at that size. Broader passive checks expose an existing,
unchanged ShockSmall template failure (PercentAdd chance where Flat is expected).
