# Stats System Architecture

## 1. Зачем нужен semantic layer

В проекте один и тот же `StatType` может означать разные вещи:

- итоговый scalar персонажа
- боевой scalar канала урона
- контекстный модификатор для расчёта удара
- utility/counter stat

Раньше это было смешано в одной модели, из-за чего:

- UI мог врать про `%`
- некоторые статы существовали в контенте, но не участвовали в расчётах
- разработчику приходилось помнить скрытые правила вручную

Теперь источник истины — `StatsDatabaseSO`.

## 2. Основные semantic kinds

Используется enum `StatSemanticKind`.

### FinalScalar

Итоговые свойства персонажа.

Примеры:

- `MaxHealth`
- `MaxMana`
- `Armor`
- `CritChance`
- `CritMultiplier`
- `MoveSpeed`

Это то, что можно показывать игроку как “готовую характеристику”.

### CombatScalar

Боевые числовые каналы, которые участвуют в формуле урона, но не всегда являются отдельной “характеристикой для билда”.

Примеры:

- `DamagePhysical`
- `DamageFire`
- `DamageCold`
- `DamageLightning`

### ContextModifier

Модификаторы, которые не имеют смысла сами по себе без контекста удара.

Пример:

- `MeleeDamage`

Такие статы:

- не должны трактоваться как готовый финальный value игрока
- должны применяться только если `DamageContext` подходит

### Utility

Счётчики, лимиты, количество целей, скорости особых сущностей и другие “служебные” числа.

Примеры:

- `ProjectileCount`
- `ProjectileFork`
- `ExtraTargetsForMeleeHits`

### Derived

Производные или вспомогательные величины, которые не должны редактироваться как обычный stat-entry в балансе.

## 3. Что хранится в metadata stat-entry

В `StatMetadataEntry` теперь важны не только:

- `Category`
- `Format`
- `ValueUnit`

но и:

- `SemanticKind`
- `ShowInPrimaryStatsEditor`
- `DisplayAsPercentWhenFlat`
- `AllowNegativeFlatGeneration`
- `ContextTags`
- `DamageChannels`

## 4. Как читать stat correctly

### Для UI

UI не должен догадываться по имени enum.

Он должен смотреть на metadata:

- `Format`
- `ValueUnit`
- `DisplayAsPercentWhenFlat`

Пример:

- `CritMultiplier`
- modifier type = `Flat`
- value = `25`
- `DisplayAsPercentWhenFlat = true`

В UI это должно показываться как:

- `+25%`

а не как:

- `+25`

## 5. DamageContext

`DamageContext` нужен, чтобы применять context-модификаторы только там, где это имеет смысл.

Сейчас в коде уже есть:

- `StatContextTagFlags`
  - `Attack`
  - `Spell`
  - `Melee`
  - `Projectile`
  - `Area`
  - `DamageOverTime`
  - `Ailment`

### Пример

Если скилл ударяет в `Melee`:

- в формулу урона должны попасть модификаторы `MeleeDamage`

Если скилл не melee:

- `MeleeDamage` должен игнорироваться

## 6. Почему ContextModifier нельзя считать через обычный GetValue

Для `ContextModifier` важно сохранить разделение:

- flat/additive percent pool
- multiplicative more/less pool

Если мы превращаем stat в одно число заранее, мы теряем структуру расчёта.

Поэтому `DamageCalculator` теперь:

1. находит подходящие context-модификаторы через metadata
2. достаёт raw modifiers из `CharacterStat`
3. отдельно собирает:
   - additive percent points
   - multiplicative factor
4. применяет их к каналу урона

## 7. Что считается стабильной основой и не должно без нужды переписываться

Следующие части системы считаются хорошей базой:

- `CharacterStat`
- `StatModType`
- local/global обработка предметных модификаторов

Проблема была не в них, а в отсутствии semantic layer и context-aware runtime path.
