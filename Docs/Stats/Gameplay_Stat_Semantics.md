# Gameplay Stat Semantics

## 1. Final Scalars

### MaxHealth / MaxMana

- обычные итоговые scalar-статы
- `Flat` = прямое добавление единиц ресурса
- `PercentAdd` = additive percent scaling
- `More/Less` допускаются только если есть реальная геймдизайнерская причина

### CritChance

- итоговый scalar в процентах
- `Flat +X` означает `+X% crit chance`

### CritMultiplier

- итоговый scalar в процентах
- базовое значение персонажа: `150`
- это означает `150% crit damage multiplier`
- `Flat +25` означает `+25% crit multiplier`
- `Flat -25` означает `-25% crit multiplier`

Production-решение:

- основная authoring-модель — flat percent points
- legacy increase/decrease значения должны быть мигрированы в flat

### MoveSpeed

- итоговый scalar в процентах к базовой скорости
- `Flat +10` означает `+10% move speed`

## 2. Combat Scalars

### DamagePhysical / Fire / Cold / Lightning

Это каналы hit-урона.

Они участвуют в формуле так:

1. weapon/local item contribution
2. global stat contribution
3. conversion
4. crit
5. context-domain modifiers

Важно:

- контекстные домены не должны жить “поверх уже готового финального урона” как случайный extra-multiplier
- они должны входить в расчёт осознанно по контексту

## 3. Context Modifiers

### MeleeDamage

Это не итоговый stat персонажа.

Это модификатор домена, который применим только если `DamageContext` содержит:

- `Melee`

То есть:

- melee skill получает этот бонус
- projectile/spell skill — нет

### Модель применения

`MeleeDamage` должен поддерживать:

- additive percent points
- multiplicative more/less factors

и применяться через `DamageCalculator` только в подходящем context.

## 4. DamageContext future rules

Каждый боевой расчёт должен иметь явный контекст:

- `Attack`
- `Spell`
- `Melee`
- `Projectile`
- `Area`
- `DamageOverTime`
- `Ailment`

В будущем именно это позволит безопасно добавить:

- `ProjectileDamage`
- `AttackDamage`
- `SpellDamage`
- `DamageOverTime`
- `TwoHandedDamage`
- weapon-class domains

без переписывания базовой stat architecture.

## 5. Regeneration

`HealthRegen` и `ManaRegen`:

- flat regen per second
- тикают раз в секунду
- округляются вверх

`HealthRegenPercent` и `ManaRegenPercent`:

- считаются от max resource
- прибавляются к flat regen на том же секундном тике

Если flat и percent оба нулевые:

- реген не происходит

## 6. Что не должно делаться в будущем

Не нужно:

- плодить новые `StatType` только ради `more` vs `increase`
- делать “псевдо-flat, но UI пусть сам догадывается”
- смешивать final scalars и context modifiers в одном mental bucket

Нужно:

- добавлять stat semantics через metadata
- подключать новые context modifiers через `DamageContext`
- использовать central formatter во всех UI/editor местах
