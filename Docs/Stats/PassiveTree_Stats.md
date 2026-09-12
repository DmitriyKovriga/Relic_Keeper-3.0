# Passive Tree Stats

## 1. Что хранит passive node template

`PassiveNodeTemplateSO` содержит:

- `Name`
- `Description`
- `Icon`
- `Modifiers`

Где `Modifiers` — это `List<SerializableStatModifier>`.

## 2. Как ноды должны форматироваться

Ни tooltip, ни editor-summary не должны вручную угадывать:

- когда ставить `%`
- когда писать plain number

Теперь они должны форматироваться через central stat presentation layer:

- `StatPresentation.FormatModifierLine(...)`

Это уже подключено в:

- runtime tooltip дерева
- editor summary нодов

## 3. Почему это важно

Если нод даёт:

- `CritMultiplier +25`

а metadata для stat говорит:

- `ValueUnit = Percent`
- `DisplayAsPercentWhenFlat = true`

то игрок и дизайнер должны видеть:

- `+25% Critical Strike Multiplier`

а не:

- `+25 Critical Strike Multiplier`

## 4. Что upgrade делает для дерева

Production upgrade:

- ищет legacy modifiers в `PassiveNodeTemplateSO`
- ищет legacy modifiers в `PassiveSkillTreeSO -> Node.UniqueModifiers`
- для `CritMultiplier` конвертирует:
  - `PercentAdd` -> `Flat +value`
  - `PercentSub` -> `Flat -value`

Это защищает дерево от тихого semantic drift, когда stat уже поменял модель, а старые ноды остались на старой.

## 5. Правило для будущих нодов

Если новый нод использует stat:

1. stat должен иметь корректную metadata-entry
2. tooltip/editor summary должны показывать modifier через central formatter
3. если stat является `ContextModifier`, это должно быть осознанным дизайнерским решением

Пример:

- `MeleeDamage +20%`

Это нормальный нод, если он поддерживает melee-tagged damage context.

Но такой нод не должен интерпретироваться как “увеличение глобального `DamagePhysical` без условий”.
