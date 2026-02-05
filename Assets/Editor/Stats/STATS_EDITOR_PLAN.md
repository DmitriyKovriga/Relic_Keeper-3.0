# План: редактор характеристик (Stats Editor) и поддержка системы статов

## 1. Анализ текущей системы

### 1.1 Ядро статов

| Компонент | Расположение | Назначение |
|-----------|--------------|------------|
| **StatType** | `Scripts/Stats/StatType.cs` | Enum со всеми характеристиками (~100+ значений). Единственный источник истины «какие статы существуют». Добавление/удаление = правка кода. |
| **StatModifier** | `Scripts/Stats/StatModifier.cs` | Рантайм: Value, StatModType (Flat / PercentAdd / PercentMult), Order, Source. Не сериализуется в asset. |
| **SerializableStatModifier** | `Scripts/Stats/StatDataStructs.cs` | Структура для инспектора: Stat (StatType), Value, Type. Конвертируется в StatModifier с source. |
| **CharacterStat** | `Scripts/Stats/CharacterStat.cs` | Один стат: BaseValue + список модификаторов, расчёт финального значения по формуле (Base+Flat)*(1+Sum%)*Mult. |
| **PlayerStats** | `Scripts/Stats/PlayerStats.cs` | В Awake создаёт `Dictionary<StatType, CharacterStat>` по всем значениям enum. Раздаёт статы, вешает модификаторы с предметов/пассивок. |

### 1.2 Где используется StatType

| Система | Файлы | Как используется |
|---------|--------|-------------------|
| **Предметы и аффиксы** | `ItemAffixSO` (AffixStatData.Stat), `InventoryItem` (модификаторы по StatType), `ItemTooltipController` (отображение статов) | Аффиксы задают Stat + ModType + Scope + Min/Max. При экипировке модификаторы вешаются на PlayerStats по StatType. |
| **Дерево пассивок** | `PassiveNodeTemplateSO.Modifiers`, `PassiveNodeDefinition.UniqueModifiers`, `PassiveTreeManager` (применение модификаторов) | Нода = список SerializableStatModifier (Stat, Value, Type). При прокачке модификаторы добавляются в PlayerStats по StatType. |
| **Характер / класс** | `CharacterDataSO` (StatConfig: Type + Value) | Стартовые значения статов для класса. |
| **UI персонажа** | `CharacterWindowUI` | Перебор `Enum.GetValues(StatType)`, для каждого стата — строка с именем (локализация `stats.{type}`) и значением. Логика форматирования (%, время, урон) захардкожена: IsDamageStat, IsTimeStat, IsPercentageStat, ShouldShowStat. |
| **Тултипы предметов** | `ItemTooltipController` | Отображение статов вещи и аффиксов; имя стата через локализацию `stats.{type}` (таблица MenuLabels). |
| **Бой и урон** | `DamageCalculator`, `EnemyHealth`, `CleaveSkill` | Чтение конкретных StatType (DamagePhysical, Armor, FireResist и т.д.). |
| **Движение** | `PlayerMovement` | MoveSpeed, JumpForce. |
| **Враги** | `EnemyStats`, `EnemyHealth` | Словарь по StatType, инициализация из данных. |

### 1.3 Локализация

- **Статы**: таблица **MenuLabels**, ключи вида `stats.{StatType}` (например `stats.MaxHealth`). RU/EN в LocalizationTables (MenuLabels_ru, MenuLabels_en).
- **Аффиксы**: таблица **Affixes**, ключ = `ItemAffixSO.TranslationKey`. Описание аффикса с подстановкой значения.

### 1.4 Проблемы при добавлении/изменении/удалении стата

1. **Добавить стат**: (1) добавить значение в enum StatType; (2) добавить ключ в MenuLabels (stats.NewStat); (3) при необходимости обновить CharacterWindowUI (ShouldShowStat, IsDamageStat, IsTimeStat, IsPercentageStat); (4) нет центрального места, чтобы увидеть «какие аффиксы/пассивки используют этот стат».
2. **Удалить/переименовать**: риск сломать аффиксы, пассивки, CharacterDataSO, формулы урона. Поиск по проекту по имени enum.
3. **Метаданные стата** (категория, формат вывода, показывать ли в окне персонажа) размазаны по CharacterWindowUI и AffixGeneratorTool (GetStatCategory), а не в одном месте.

---

## 2. Связи с другими системами (кратко)

```
                    ┌─────────────────┐
                    │   StatType      │  (enum)
                    │   (источник     │
                    │   истины)       │
                    └────────┬────────┘
                             │
     ┌───────────────────────┼───────────────────────┐
     │                       │                       │
     ▼                       ▼                       ▼
┌─────────────┐      ┌──────────────┐      ┌─────────────────┐
│ ItemAffixSO │      │ PassiveTree  │      │ CharacterDataSO │
│ .Stats[].   │      │ NodeTemplate │      │ .StartingStats  │
│ Stat        │      │ .Modifiers   │      │ .Type           │
└──────┬──────┘      └──────┬───────┘      └────────┬────────┘
       │                    │                      │
       │                    │                      │
       ▼                    ▼                      ▼
┌─────────────────────────────────────────────────────────────┐
│  PlayerStats  Dictionary<StatType, CharacterStat>            │
│  + модификаторы с source (item / passive node)              │
└─────────────────────────────────────────────────────────────┘
       │
       ├──► CharacterWindowUI (список статов, формат по типу)
       ├──► ItemTooltipController (статы вещи + имена stats.{type})
       ├──► DamageCalculator, EnemyHealth, PlayerMovement, CleaveSkill…
       └──► HUDController (Health/Mana из StatResource)
```

Редактор статов не должен менять рантайм-логику (PlayerStats, CharacterStat, StatModifier). Он только даёт удобный обзор и правку **определений** и **метаданных** статов и быстрый доступ к аффиксам/пассивкам по стату.

---

## 3. Цели редактора характеристик

1. **Удобно просматривать** все статы в одном месте: имя (из локали), категория, где используется.
2. **Видеть «кто использует»**: по каждому стату — список аффиксов (ItemAffixSO), пассивок (ноды/шаблоны с этим StatType), CharacterDataSO (стартовые статы).
3. **Редактировать локализацию статов прямо в окне** — без перехода в Unity Localization Editor. Для выбранного стата: поля EN и RU с текущими значениями из таблицы MenuLabels; сохранение записывает в StringTable (en/ru). Так проще добавлять и править переводы.
4. **Быстро переходить** к аффиксу/ноде из редактора (Ping/Selection, открытие ассета).
5. **Опционально** — централизованные метаданные стата (категория, формат для UI, показывать в окне персонажа) без правки enum и CharacterWindowUI на первом этапе; при желании — вынести в SO позже.
6. **Не ломать** существующий код: enum StatType остаётся; редактор только читает enum и ассеты, при необходимости пишет локализацию или SO-метаданные.

---

## 4. Функциональность редактора (предложение)

### 4.1 Окно Stats Editor

- **Меню**: `Tools / RPG / Stats Editor` (или `Tools / Stats Editor`).
- **Раскладка**: слева — список статов (или дерево по категориям), справа — панель деталей выбранного стата.

### 4.2 Список статов (левая панель)

- Источник списка: `Enum.GetValues(typeof(StatType))`.
- Для каждого стата показывать:
  - **ID** (имя enum).
  - **Отображаемое имя**: из локализации `MenuLabels` → `stats.{StatType}`; если ключа нет — показать сам enum и пометку «нет перевода».
  - **Категория**: из метаданных (если введём SO/базу) или вычисленная по имени (как в AffixGeneratorTool: Vitals, Defense, Damage, Ailments, …).
  - **Счётчики**: число аффиксов (ItemAffixSO), число нод пассивок (шаблоны + ноды с UniqueModifiers), использование в CharacterDataSO (да/нет).
- Сортировка: по категории, затем по имени; опция поиска/фильтра по строке.
- Выбор стата → обновление правой панели.

### 4.3 Детали стата (правая панель)

- **ID**, **категория**.
- **Локализация (редактирование в окне)**:
  - Ключ: `stats.{StatType}` (только для справки).
  - Два текстовых поля: **EN** и **RU** — текущие строки из таблицы MenuLabels (таблицы `en` и `ru`). При изменении и нажатии «Сохранить локаль» — запись в StringTable через API Unity Localization (GetEntry/AddEntry, установка Value), SetDirty, SaveAssets. Так не нужно заходить в Unity Localization Editor.
  - Опционально: кнопка «Открыть таблицу в Unity Localization» (ping по ассету коллекции MenuLabels).
- **Использование**:
  - **Аффиксы**: список (имя ассета, GroupID, Tier). По клику — Selection.SetActiveObject(affixSO), Ping.
  - **Пассивки**: список (PassiveNodeTemplateSO или ноды дерева с этим статом). По клику — открыть ассет/дерево.
  - **CharacterDataSO**: список персонажей/классов, у которых этот стат в StartingStats.
- Кнопки: «Сохранить локаль», «Проверить локализацию» (отчёт по всем статам).

### 4.4 Дополнительно (по желанию)

- **Проверка локализации**: для каждого StatType проверить наличие ключа `stats.{StatType}` в MenuLabels; отчёт «каких не хватает».
- **Экспорт/отчёт**: какие статы нигде не используются (ни в аффиксах, ни в пассивках, ни в CharacterDataSO) — кандидаты на удаление или доработку контента.
- Позже: **редактируемые метаданные** (StatMetadataSO или один StatsDatabaseSO) — категория, формат (number / percent / time), показывать в окне персонажа. Редактор тогда даёт их править; CharacterWindowUI при рефакторинге может читать оттуда вместо захардкоженных IsDamageStat/IsTimeStat и т.д.

---

## 5. Этапы реализации (to-do)

### Фаза 1: Окно и список статов (без зависимостей)

1. Создать папку `Assets/Editor/Stats/` (если ещё нет).
2. Реализовать окно **StatsEditorWindow** (EditorWindow): меню `Tools / RPG / Stats Editor`.
3. Левая панель: список всех StatType (из enum). Колонки: ID (имя), категория (вычисленная по имени, как в AffixGeneratorTool), опционально — плейсхолдер для счётчиков.
4. Выбор строки → запомнить выбранный StatType, подготовить правую панель под детали.

**Результат**: открыл окно — видишь все статы и категорию. Детали пока пустые.

---

### Фаза 2: Подсчёт использований и панель деталей

5. Сервис/хелпер **StatsUsageFinder** (или статические методы в окне):
   - Найти все `ItemAffixSO`: `AssetDatabase.FindAssets("t:ItemAffixSO")`, загрузить, для каждого проверить `Stats` — есть ли нужный StatType. Вернуть список ассетов.
   - Найти все `PassiveNodeTemplateSO`: проверить `Modifiers` на StatType. Опционально — обход деревьев `PassiveSkillTreeSO` и нод с `UniqueModifiers`.
   - Найти все `CharacterDataSO`: проверить `StartingStats` на StatType.
6. В правой панели для выбранного стата вывести: ID, категория, ключ локализации; счётчики и списки «аффиксы», «пассивки», «CharacterData». По клику на элемент списка — `Selection.SetActiveObject`, `EditorGUIUtility.PingObject`.

**Результат**: по каждому стату видно, где он используется, и можно перейти к аффиксу/ноде/персонажу.

---

### Фаза 3: Локализация — просмотр и редактирование в окне

7. Загрузка таблицы MenuLabels: найти ассет `StringTableCollection` для MenuLabels (например по пути `Assets/Localization/LocalizationTables/MenuLabels.asset` или через FindAssets), получить `StringTable` для локалей `en` и `ru` (GetTable).
8. В деталях стата: ключ `stats.{StatType}`; два поля **EN** и **RU** — читать значения из соответствующих StringTable (GetEntry(key)?.Value или пустая строка). Показывать отображаемое имя в списке слева (из текущей локали или EN как fallback).
9. **Редактирование локали**: при нажатии «Сохранить локаль» — для ключа `stats.{StatType}` в таблице `en`: если запись есть (GetEntry), установить `entry.Value = textEN`, иначе `table.AddEntry(key, textEN)`; аналогично для `ru`. Вызвать `EditorUtility.SetDirty(table)`, `AssetDatabase.SaveAssets()`. Обновить отображение в списке.
10. Кнопка «Проверить локализацию»: пройти по всем StatType, проверить наличие ключа в EN (и при желании RU), вывести в консоль или в окно список «нет перевода для: …».
11. Опционально: кнопка «Открыть MenuLabels в Unity Localization» — `Selection.SetActiveObject(menuLabelsCollection)`, `EditorGUIUtility.PingObject`.

**Результат**: в редакторе видно человекочитаемое имя стата, можно править EN/RU прямо в окне и сохранять в таблицы без перехода в Unity Localization Editor.

---

### Фаза 4: Удобство и полировка

12. Поиск/фильтр по списку статов (по ID или по отображаемому имени).
13. Сортировка по категории, по количеству аффиксов (опционально).
14. Сохранение состояния окна (какой стат выбран) не обязательно, но можно сохранять выбор в SessionState.
15. Краткая справка в окне: «Статы берутся из enum StatType. Локализация: MenuLabels, ключ stats.{StatType}; EN/RU можно править здесь. Аффиксы/пассивки сканируются по проекту.»

**Результат**: удобный ежедневный инструмент для расширения и поддержки статов.

---

### Фаза 5 (опционально): Метаданные статов — **ВЫПОЛНЕНО**

16. Введён **StatsDatabaseSO** (`Scripts/Stats/StatsDatabaseSO.cs`): для каждого StatType — категория, тип форматирования (Number / Percent / Time / Damage), показывать в окне персонажа (bool). Хранится в Resources/Databases (StatsDatabase.asset).
17. В Stats Editor: секция «Metadata» — поле базы, кнопка «Create metadata for all stats», для выбранного стата редактирование Category, Display Format, Show in Character Window; при отсутствии записи — «Create metadata for this stat». Кнопка «Create new Stats Database» создаёт ассет в Resources/Databases.
18. CharacterWindowUI: загрузка `Resources.Load<StatsDatabaseSO>("Databases/StatsDatabase")` в Awake; ShouldShowStat и формат значения (UpdateValues) берутся из метаданных с fallback на прежнюю захардкоженную логику.

**Результат**: добавление нового стата в enum + запись в метаданных + ключ локализации; меньше правок в CharacterWindowUI.

---

## 6. Технические заметки

- **Поиск аффиксов**: `AssetDatabase.FindAssets("t:ItemAffixSO", new[] { "Assets/Resources/Affixes" })` (или без ограничения пути), затем `AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path)` и проверка `affix.Stats` на выбранный StatType.
- **Namespace ItemAffixSO**: в коде указан `Scripts.Items.Affixes`; при FindAssets используется имя типа `ItemAffixSO`.
- **Пассивки**: `FindAssets("t:PassiveNodeTemplateSO")` и `FindAssets("t:PassiveSkillTreeSO")`; у дерева обход `Nodes` и проверка `UniqueModifiers` и ссылок на шаблоны с Modifiers.
- **Локализация в редакторе**: таблицы — `StringTableCollection` (MenuLabels.asset), для локалей `en`/`ru` — `GetTable("en")` и `GetTable("ru")` как `StringTable`. Чтение: `table.GetEntry(key)` → `entry?.Value`. Запись: если `GetEntry(key)` есть — присвоить `entry.Value = newText`; иначе `table.AddEntry(key, newText)`. Затем `EditorUtility.SetDirty(table)`, `AssetDatabase.SaveAssets()`. Аналогично реализовано в `AffixLocalizationSync` (AddEntry). Используются `UnityEditor.Localization` и `UnityEngine.Localization.Tables`.

---

## 7. Итог

- **Сейчас**: статы = enum; добавление/изменение неудобно; нет обзора «кто использует стат».
- **После редактора**: одно окно — список статов, категория, использование (аффиксы, пассивки, персонажи), **редактирование локали EN/RU прямо в окне** (без Unity Localization Editor), проверка локализации, быстрый переход к ассетам.
- **Без обязательного рефакторинга**: enum и вся игровая логика остаются; при желании позже можно ввести метаданные в SO и постепенно перенести форматирование/видимость из CharacterWindowUI в данные.

Порядок работ: Фаза 1 → 2 → 3 → 4; Фаза 5 — по необходимости.

---

## 8. Пошаговое тестирование (все 5 фаз)

### Подготовка

1. Открыть проект в Unity.
2. Убедиться, что есть ассеты для проверки использования: хотя бы один `ItemAffixSO`, один `PassiveNodeTemplateSO` или дерево `PassiveSkillTreeSO`, один `CharacterDataSO` (в `Assets/Resources/`).
3. Убедиться, что таблица локализации MenuLabels существует: `Assets/Localization/LocalizationTables/MenuLabels.asset` (с локалями `en` и `ru`).

---

### Фаза 1: Окно и список статов

1. В меню Unity выбрать **Tools → RPG → Stats Editor**.
2. **Ожидание:** открывается окно «Stats Editor».
3. Слева — список статов: каждая строка в формате `StatTypeId — Category` (например `MaxHealth — Vitals`, `DamageFire — Damage`).
4. **Ожидание:** отображаются все значения enum `StatType`, категории заполнены (Vitals, Defense, Damage, Ailments и т.д.).
5. Кликнуть по любому стату в списке.
6. **Ожидание:** строка подсвечивается, справа появляется панель «Details» с полями ID, Category, Localization key (и далее секции локализации, использования и т.д.).

---

### Фаза 2: Использование (Usage)

1. В Stats Editor выбрать стат, который точно используется в проекте (например `MaxHealth` или `DamagePhysical`).
2. В правой панели найти секцию **Usage**.
3. При необходимости нажать **Refresh**.
4. **Ожидание:** под заголовками «Affixes», «Passive node templates», «Passive trees», «Character data» отображаются списки ассетов (или «none», если стат нигде не используется).
5. Кликнуть по имени ассета в одном из списков (если список не пуст).
6. **Ожидание:** в Project окне выделяется и пингется выбранный ассет.
7. Выбрать стат, который нигде не используется (если такой есть).
8. **Ожидание:** во всех блоках Usage отображается «none».

---

### Фаза 3: Локализация

1. В панели деталей найти секцию **Localization (EN / RU)**.
2. **Ожидание:** поле «MenuLabels Table» либо уже заполнено (путь к MenuLabels.asset), либо пусто.
3. Если поле пусто — перетащить в него ассет `MenuLabels` из `Assets/Localization/LocalizationTables/MenuLabels.asset`.
4. **Ожидание:** под полем Key отображаются два поля: **English** и **Russian** с текстом из таблицы для ключа `stats.{выбранный стат}` (или пустые строки, если ключа ещё нет).
5. Ввести в поле English, например, `Test Stat EN`, в Russian — `Тест стат RU`.
6. Нажать кнопку **Save Localization**.
7. **Ожидание:** в консоли сообщение вида «Stats Editor: Saved localization for stats.XXX».
8. Открыть таблицу MenuLabels в Unity Localization (Window → Localization → String Table Collection, выбрать MenuLabels) и проверить локали `en` и `ru` для ключа `stats.{тот же стат}`.
9. **Ожидание:** значения совпадают с введёнными в редакторе статов.
10. (Опционально) Перезапустить Stats Editor или выбрать другой стат и снова вернуться к отредактированному — поля EN/RU должны показывать сохранённые значения.

---

### Фаза 4: Поиск, фильтр, сортировка

1. В левой панели в поле **Search** ввести часть имени стата (например `Health`).
2. **Ожидание:** в списке остаются только статы, в ID которых есть подстрока (без учёта регистра).
3. Очистить Search. В выпадающем списке **Category** выбрать категорию (например **Vitals**).
4. **Ожидание:** в списке только статы этой категории.
5. Вернуть Category в «(пусто)». В выпадающем списке **Sort** выбрать **By Category**.
6. **Ожидание:** статы в списке отсортированы по категории, затем по ID.
7. Выбрать любой стат, закрыть окно Stats Editor, снова открыть **Tools → RPG → Stats Editor**.
8. **Ожидание:** выбранный ранее стат снова выделен (сохранение в SessionState).

---

### Что такое Stats Database (StatsDatabaseSO) и зачем он нужен

**Что это:**  
Один ScriptableObject-ассет (`StatsDatabase.asset`), в котором хранятся **метаданные** по каждому стату: как его показывать в UI и в каком формате выводить значение. То есть не сами числа статов (их даёт PlayerStats), а «настройки отображения».

**Зачем:**  
Раньше правила «показывать ли стат в окне персонажа» и «выводить как число, процент или время» были зашиты в коде `CharacterWindowUI` (методы `ShouldShowStat`, `IsDamageStat`, `IsTimeStat`, `IsPercentageStat`). Чтобы изменить видимость или формат, нужно было править код. База выносит это в данные: можно скрыть стат, сменить категорию или формат без перекомпиляции, и всё правится в одном месте — в Stats Editor.

**Где используется:**

| Место | Как используется |
|-------|-------------------|
| **Stats Editor** (редактор) | Поле «Stats Database» в секции Metadata. Редактор читает/пишет метаданные (категория, формат, Show in Character Window). Список статов слева и фильтр по категории берут категорию из базы, если база назначена. |
| **CharacterWindowUI** (игра) | В `Awake` загружается `Resources.Load<StatsDatabaseSO>("Databases/StatsDatabase")`. При построении списка статов вызывается `ShouldShowInCharacterWindow(type)` — показывать ли строку. При обновлении значений — `GetFormat(type)` — как форматировать (Number / Percent / Time / Damage). Если базы нет или для стата нет записи — используется старая логика (fallback). |

**Как создать и использовать:**

1. Открыть **Tools → RPG → Stats Editor**.
2. Справа прокрутить до секции **Metadata**.
3. Если поля «Stats Database» ещё нет или оно пустое — нажать **Create new Stats Database in Resources/Databases**. В папке `Assets/Resources/Databases/` появится ассет `StatsDatabase.asset` (пустой).
4. Нажать **Create metadata for all stats** — в базу добавятся записи для всех значений enum StatType с дефолтными категорией, форматом и видимостью (как в старой логике).
5. Дальше для любого стата можно менять в секции Metadata: **Category**, **Display Format** (Number / Percent / Time / Damage), **Show in Character Window** (галочка). Сохранить проект — эти данные подхватит окно персонажа в игре.

**Важно:** ассет должен лежать в `Resources/Databases/` и называться так, чтобы по пути `Resources/Databases/StatsDatabase` его находил `Resources.Load` (имя файла без расширения — `StatsDatabase`). Другой путь/имя в коде не подхватится без правки.

---

### Фаза 5: Метаданные и Character Window

**В редакторе:**

1. В Stats Editor в правой панели прокрутить вниз и найти секцию **Metadata** (она выше секции «Localization»).
2. Если поле **Stats Database** пусто:
   - Нажать кнопку **Create new Stats Database in Resources/Databases**.
   - **Ожидание:** в Project в `Assets/Resources/Databases/` появляется файл `StatsDatabase.asset`, а в окне редактора поле «Stats Database» автоматически заполняется этим ассетом.
3. Нажать кнопку **Create metadata for all stats** (под полем базы).
   - **Ожидание:** в консоли Unity сообщение «Stats Editor: Created default metadata for all StatTypes».
   - Выбрать в списке слева любой стат (например `MaxHealth`). В секции Metadata должны появиться поля: **Category** (текст), **Display Format** (выпадающий список: Number / Percent / Time / Damage), **Show in Character Window** (галочка). Вместо блока «No metadata» и кнопки «Create metadata for this stat» теперь отображаются эти три поля.
4. В списке слева выбрать стат **BlockChance**.
5. В секции Metadata изменить:
   - **Category** — стереть текущее и вписать `CustomDefense`;
   - **Display Format** — оставить **Percent** или выбрать его из списка;
   - **Show in Character Window** — **снять галочку** (выключить).
6. Сохранить проект (Ctrl+S или File → Save Project). **Ожидание:** изменения остаются; при следующем открытии Stats Editor у `BlockChance` по-прежнему Category = CustomDefense, Show in Character Window = false.
7. (Опционально) В выпадающем списке **Category** (в левой панели, над списком статов) выбрать **CustomDefense**. **Ожидание:** в списке статов отображается только BlockChance, т.к. у него теперь категория из базы = CustomDefense.
8. (Опционально) Выбрать стат, для которого не создавали метаданные вручную (если базу только что создали и нажали «Create for all», у всех уже есть записи). Чтобы проверить «Create for this stat»: временно удалить одну запись из базы через Inspector у `StatsDatabase.asset` или создать новую базу и не нажимать «Create for all», затем выбрать любой стат — должна появиться кнопка **Create metadata for this stat**; нажать её — появятся три поля с дефолтами.

**В игре (runtime):**

9. Нажать **Play**, загрузить сцену с персонажем и открыть окно персонажа (статы) — как обычно в твоей игре (например кнопка «Характеристики» или горячая клавиша).
10. Просмотреть список статов. **Ожидание:** статы отображаются, числа в нужном формате (целые, проценты с «%», время с «s», урон — средний урон). Это либо из базы (если она есть и заполнена), либо по старой логике.
11. Найти в списке стат **Block Chance** (или как он подписан в локали). **Ожидание:** его **нет** в списке, потому что для `BlockChance` в базе было выключено **Show in Character Window**. Остальные статы на месте.
12. Остановить Play (Stop). В Stats Editor снова выбрать стат `BlockChance`, в Metadata включить галочку **Show in Character Window**, сохранить проект. Снова нажать Play и открыть окно персонажа. **Ожидание:** Block Chance снова появляется в списке.
13. (Опционально) В Project удалить или переименовать `StatsDatabase.asset` из `Assets/Resources/Databases/`, запустить игру. **Ожидание:** окно персонажа по-прежнему работает: все статы видимы по старой логике (кроме HealthRegenPercent и ManaRegenPercent), формат значений — по захардкоженным правилам. Ошибок из-за отсутствия базы быть не должно.

---

### Краткий чеклист

| Фаза | Что проверить |
|------|----------------|
| 1 | Окно открывается, список статов с категориями, выбор стата → детали справа |
| 2 | Usage: списки аффиксов/пассивок/персонажей, клик → Ping в Project |
| 3 | MenuLabels подтягивается, EN/RU отображаются и сохраняются, в таблице локализации значения совпадают |
| 4 | Search и Category фильтруют список, Sort «By Category» меняет порядок, выбор стата сохраняется между открытиями окна |
| 5 | Создание базы, «Create for all», редактирование метаданных, в игре окно персонажа учитывает Show in Character Window и формат; без базы — fallback без ошибок |
