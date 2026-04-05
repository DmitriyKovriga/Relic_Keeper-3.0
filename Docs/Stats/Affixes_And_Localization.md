# Affixes And Localization

## 1. Общая модель

Аффикс состоит из:

- stat target
- modifier type
- диапазона значений
- scope
- localization keys

Главный runtime type модификатора остаётся прежним:

- `Flat`
- `PercentAdd`
- `PercentSub`
- `PercentMult`
- `PercentLess`

## 2. Что значит flat для “процентных” stat-ов

Некоторые итоговые scalar-статы по смыслу являются процентными величинами, даже если их runtime-модификатор — `Flat`.

Примеры:

- `CritChance`
- `CritMultiplier`
- `MoveSpeed`

Если такой stat получает `Flat +25`, это означает:

- `+25%`

а не:

- `+25 units`

Это регулируется через metadata:

- `DisplayAsPercentWhenFlat`

## 3. Какие kinds разрешены metadata

`AllowedAffixKinds` задаёт, какие authoring-kind доступны стату.

Важно:

- не каждый stat должен поддерживать все пять kinds
- metadata должна ограничивать лишнее

Пример production-решения:

- `CritMultiplier` — flat-only
- `MeleeDamage` — additive/multiplicative context modifier

## 4. Negative flat

Система теперь поддерживает negative flat generation на уровне metadata:

- `AllowNegativeFlatGeneration`

Если stat это разрешает, генератор может создать отдельные flat-negative families.

Это полезно для статов вроде:

- `CritMultiplier`
- в будущем других scalar-статов, где штраф должен быть “минус процентные пункты”, а не `reduced`

Важно:

- runtime всё ещё использует обычный `StatModType.Flat`
- отрицательность задаётся самим значением

То есть:

- `Flat -25` для `CritMultiplier`

в UI будет:

- `-25% to Critical Strike Multiplier`

## 5. Локализация аффиксов

Автогенерация аффиксов и локалей живёт в:

- `Assets/Editor/Affixes/AffixSetGenerator.cs`

Критичные правила:

1. Имена и value-lines не должны опираться только на `StatModType`
2. Они должны смотреть на:
   - `ValueUnit`
   - `DisplayAsPercentWhenFlat`
3. Signed flat values должны форматироваться со знаком

Теперь value templates используют signed placeholders, поэтому:

- `+25`
- `-25`

обе формы корректно рендерятся через один шаблон.

## 6. Legacy trap, который больше не стоит использовать

Старый `AffixGeneratorTool` переведён в режим deprecated-wrapper.

Он больше не генерирует аффиксы сам, а открывает актуальный `Affix Editor`.

Это сделано, чтобы в проекте не осталось двух конкурирующих путей генерации.

## 7. Что repair/upgrade делает с legacy CritMultiplier affixes

Production upgrade:

- конвертирует `CritMultiplier PercentAdd` -> `Flat`
- конвертирует `CritMultiplier PercentSub` -> `Flat` с отрицательным диапазоном
- обновляет localization keys
- регенерирует авто-локализацию для незалоченных affixes

Это нужно, потому что `CritMultiplier` теперь считается final percent scalar, а не stat с additive percent-kind семантикой.
