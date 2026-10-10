# Mystic Shield skill effectiveness: implementation review

## Requested scalar

Stat: `MysticShieldSkillEffectiveness`, base value 1. Evaluate ordinary
CharacterStat layers: `(1 + flat modifiers) * increased/reduced * More/Less`.
A +100% increased modifier produces effectiveness 2, rather than effectiveness 3.
Base 1 preserves existing skills. No shared stat or skill asset is modified when
an effect is applied.

For an eligible timed buff, effectiveness 2 changes a +20% modifier for 5 seconds
into +40% for 10 seconds. Global EffectDuration would apply after this duration
scaling: another +50% duration would yield 15 seconds. Each application stores
its own final magnitude and duration. Instant, cleanup-bound attack buffs must
retain their authored values.

## Current runtime findings

- Shield consumption itself grants no universal damage bonus. It removes charges
  and emits MysticShieldConsumed.
- MysticShieldDamageBoost is a separate recipe step. Its default +50% per charge
  produces `1 + 0.5 * consumedCharges`. No current skill recipe uses this step.
- Mystic Impulse consumes a charge, applies a cleanup-bound +50% More SpellDamage
  quick effect, then deals circle damage. This is the actual current instant
  consumption bonus; scaling it would violate the requested exclusion.
- Mystic Haste consumes a charge and applies timed movement speed and mana regen
  bonuses. Mystic Consume applies a timed SpellDamage bonus. These are eligible
  buff candidates.
- Endless Battery fills shield charges. It has no damage or buff output; the new
  stat should not silently change charge capacity, mitigation or recharge.

## Design conflicts

Multiplying buff magnitude and duration by E scales the integral of the extra
buff benefit by roughly E squared until uptime or other caps intervene. E=2
therefore means four times that integral, not twice. EffectDuration adds another
independent multiplier. This is a balance concern rather than a technical conflict.

Automatically identifying shield recipes is possible by traversing recipe steps
and nested groups. Eligibility alone does not answer whether the stat should
work on a cast that failed to consume a shield. Cast state already tracks actual
consumption and generation, so that restriction can be enforced if intended.

Damage needs exactly specified layers. Scaling both base skill damage and the
shield bonus can apply effectiveness twice. For base damage 100, base effectiveness
1 and a shield bonus 50%, total damage is 150. At E=2:

- Scale only the bonus: `100 * (1 + 0.5 * 2) = 200`.
- Scale only base damage, keeping the bonus fixed: `100 * 2 * 1.5 = 300`.
- Scale both: `100 * 2 * (1 + 0.5 * 2) = 400`.

Keeping an instant buff's modifier at +50% does not mean its absolute damage
contribution stays fixed if the base skill damage doubles. The confirmed rule scales the base damage while keeping that modifier unchanged.

## Confirmed implementation

The scalar is captured on the first successful charge consumption or generation
in a cast. Failed use (zero charges consumed/generated) grants no scaling. Outputs
before that step retain their original values; outputs after it use the snapshot.
Extra charges do not multiply effectiveness itself.

Both base skill damage and the explicit MysticShieldDamageBoost bonus scale.
For one charge, the formula is `baseDamage * E * (1 + 0.5 * E)`.
The +50% More SpellDamage instant buff on Mystic Impulse remains +50%:
its 200% weapon damage becomes 400% at E=2, before that unchanged buff.
Timed buffs scale magnitude and duration; untimed cleanup-bound effects do not.
Charge counts, capacity, mitigation and recharge remain unchanged.

EffectDuration is applied to the scaled raw duration through an independent stat
copy. Automatic previews assume successful shield use and state this condition;
linked effect tooltips carry the same effectiveness as the skill description.

This stat is compatible with the runtime, but combines several powerful axes.
Keep modifier values conservative because magnitude and duration compound, and
base damage and an explicit shield bonus can also compound. A narrower alternative
would scale potency only, leaving duration to EffectDuration; the implementation
follows the requested combined behavior instead.
