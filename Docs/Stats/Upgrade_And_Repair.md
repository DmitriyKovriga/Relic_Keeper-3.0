# Upgrade And Repair

## 1. Зачем нужен production upgrade

После введения semantic layer и `DamageContext` старый контент может остаться в legacy-состоянии:

- metadata у stat-ов живёт по старым правилам
- `CritMultiplier` всё ещё может встречаться как `Increase/Decrease`
- аффикс локализации могли остаться на старых value templates

Чтобы не исправлять это вручную по одному asset, в `Stats Editor` добавлен production upgrade.

## 2. Где запускать

Окно:

- `Tools/Stats Editor`

Секция:

- `Stats System Upgrade`

Кнопки:

- `Analyze Upgrade`
- `Apply Production Upgrade`

## 3. Что делает Analyze

`Analyze Upgrade` ничего не меняет.

Он считает:

- сколько stat metadata отсутствует
- сколько metadata entries нужно нормализовать
- сколько legacy `CritMultiplier` affixes ещё осталось
- сколько passive templates / trees ещё содержат старый `CritMultiplier` modifier type
- сколько context modifiers имеют невалидную metadata

## 4. Что делает Apply Production Upgrade

### Metadata

Нормализует для всех stat-ов:

- `SemanticKind`
- `Format`
- `ValueUnit`
- `AffixGenType`
- `AllowedAffixKinds`
- `ShowInPrimaryStatsEditor`
- `DisplayAsPercentWhenFlat`
- `AllowNegativeFlatGeneration`
- `ContextTags`
- `DamageChannels`

### Affixes

Для `CritMultiplier`:

- `PercentAdd` -> `Flat`
- `PercentSub` -> `Flat` с отрицательным диапазоном

После этого:

- нормализуются localization keys
- для всех unlocked affixes регенерируется auto-localization

### Passive content

Конвертируются legacy modifiers в:

- `PassiveNodeTemplateSO`
- `PassiveSkillTreeSO.Node.UniqueModifiers`

## 5. Ограничения

### Unity must not open the project twice

Headless upgrade через `-executeMethod` не сработает, если проект уже открыт в другом Unity instance.

### LockAutoLocalization

Если у affix стоит `LockAutoLocalization`, upgrade не будет переписывать его локализованные строки автоматически.

Это ожидаемое поведение.

## 6. CLI entrypoint

Для batchmode есть метод:

- `Scripts.Editor.Stats.StatsEditorStatLifecycle.ExecuteProductionUpgrade`

Его можно запускать через Unity batchmode, если проект не открыт в другой Unity instance.

## 7. Recommended workflow

1. Сделать git commit / backup branch
2. Открыть `Stats Editor`
3. Нажать `Analyze Upgrade`
4. Проверить отчёт
5. Нажать `Apply Production Upgrade`
6. Прогнать smoke-test:
   - пассивное дерево
   - item tooltips
   - affix editor
   - melee hit damage
