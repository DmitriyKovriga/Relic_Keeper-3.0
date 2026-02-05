# План: Items Editor — предметы, пулы аффиксов, аффиксы

## 1. Текущая система (кратко)

### 1.1 Предметы (Items)

| Тип | Файл | Поля |
|-----|------|------|
| **EquipmentItemSO** | `Scripts/Items/EquipmentItemSO.cs` | ID, ItemName, Icon, Width, Height, Slot, DropLevel, ImplicitModifiers, **SkillPool**, SkillCount |
| **ArmorItemSO** | `ArmorItemSO.cs` | + DefenseType, BaseArmor, BaseEvasion, BaseBubbles |
| **WeaponItemSO** | `WeaponItemSO.cs` | + IsTwoHanded, InHandSprite, Min/Max damages (Phys/Fire/Cold/Lightning), APS, BaseCritChance, **SecondarySkillPool** |

- **Slot**: Helmet, BodyArmor, MainHand, OffHand, Gloves, Boots.
- **SkillPool** (SkillPoolSO): пул скиллов, которые могут выпасть на предмете; **SkillCount** — сколько роллить.
- У оружия может быть **SecondarySkillPool** (для правой кнопки, 2H).

### 1.2 Пул аффиксов (Affix Pool)

| Компонент | Файл | Назначение |
|-----------|------|------------|
| **AffixPoolSO** | `Scripts/Items/AffixPoolSO.cs` | **Slot** + **DefenseType** (Armour, Evasion, Bubbles, Hybrid, None), список **Affixes** (List&lt;ItemAffixSO&gt;). |

- **ItemGenerator** (в сцене) хранит список пулов и при генерации предмета выбирает пул по правилу: `pool.Slot == item.Slot && pool.DefenseType == (armor ? armor.DefenseType : None)`.
- Предметы **не ссылаются** на пул напрямую — связь идёт через пару (Slot, DefenseType).

### 1.3 Аффиксы (Affixes)

| Компонент | Файл | Поля |
|-----------|------|------|
| **ItemAffixSO** | `Scripts/Items/ItemAffixSO.cs` | UniqueID, GroupID, Tier, RequiredLevel, TranslationKey, **Stats** (AffixStatData[]: Stat, Type, Scope, MinValue, MaxValue) |

- Аффикс может входить в несколько пулов (вручную добавляется в список каждого пула).
- **ItemDatabaseSO**: AllItems, AllAffixes, AllSkills; подтягивается через ItemDatabaseEditor «Auto-Find».

### 1.4 Связи

```
ItemDatabaseSO.AllItems (EquipmentItemSO[])
         │
         ├── EquipmentItemSO.Slot, (ArmorItemSO.DefenseType)
         │         │
         │         ▼
         │   ItemGenerator._affixPools (AffixPoolSO[])  →  pool.Slot + pool.DefenseType
         │         │
         │         ▼
         │   AffixPoolSO.Affixes (List<ItemAffixSO>)
         │
         ├── EquipmentItemSO.SkillPool (SkillPoolSO)
         └── WeaponItemSO.SecondarySkillPool (SkillPoolSO)
```

---

## 2. Цели Items Editor

1. **Предметы:** удобно просматривать все предметы (базы), фильтровать по типу/слоту, редактировать поля (включая SkillPool, ImplicitModifiers), создавать новые (Armor/Weapon), удалять. Видеть, какой пул аффиксов используется для этого предмета (Slot + DefenseType) и переходить к нему.
2. **Пулы аффиксов:** просматривать все AffixPoolSO, фильтровать по Slot/DefenseType, редактировать список аффиксов пула (добавлять/удалять аффиксы), создавать пулы, удалять. Видеть, какие предметы используют этот пул.
3. **Аффиксы:** просматривать все ItemAffixSO, фильтровать по GroupID/Tier/RequiredLevel, редактировать поля аффикса, создавать/удалять. Видеть, в каких пулах состоит аффикс; добавлять/удалять аффикс в выбранный пул.

---

## 3. Предлагаемая структура окна

- **Меню:** `Tools / Items Editor` (рядом с Stats Editor и Passive Tree Editor).
- **Режимы (вкладки):** **Items** | **Affix Pools** | **Affixes**.
- **Раскладка:** слева — список (с поиском и фильтрами), справа — панель деталей выбранного элемента (или создание нового).

---

## 4. Фазы реализации

### Фаза 1: Окно + вкладка Items

1. Создать **ItemsEditorWindow** (EditorWindow), меню `Tools/Items Editor`, три вкладки (Items, Affix Pools, Affixes).
2. **Вкладка Items:**
   - Источник списка: `AssetDatabase.FindAssets("t:EquipmentItemSO")` (или ItemDatabaseSO.AllItems, если назначена база).
   - Слева: список предметов (отображать ItemName или ID, тип Armor/Weapon, Slot). Фильтры: поиск по имени/ID, тип (All/Armor/Weapon), Slot.
   - Выбор предмета → справа **панель деталей**: отображение полей через SerializedObject/Editor (ID, ItemName, Icon, Slot, DropLevel, ImplicitModifiers, SkillPool, SkillCount; для Armor — DefenseType, BaseArmor/Evasion/Bubbles; для Weapon — IsTwoHanded, damages, SkillPool, SecondarySkillPool и т.д.). Кнопки: **Open in Inspector**, **Ping**, опционально **Delete**.
   - Блок «Used affix pool»: по Slot и (для брони) DefenseType показать подходящий AffixPoolSO (найти по FindAssets и совпадению Slot+DefenseType); кнопка «Open pool» → переключение на вкладку Affix Pools и выбор этого пула.
   - Кнопки **Create Armor Item** / **Create Weapon Item** — создание нового ассета в выбранной папке (например Resources или по умолчанию), добавление в ItemDatabaseSO.AllItems при наличии базы.

**Результат:** открыл окно → вкладка Items → список предметов, выбор → правка полей, переход к пулу аффиксов, создание предмета.

---

### Фаза 2: Вкладка Affix Pools

3. **Вкладка Affix Pools:**
   - Список: все AffixPoolSO (FindAssets). Отображать: имя ассета, Slot, DefenseType, количество аффиксов.
   - Фильтры: Slot, DefenseType, поиск по имени.
   - Выбор пула → справа: редактирование **Slot**, **DefenseType**, список **Affixes** (ReorderableList или свой список с кнопками Add/Remove).
   - **Add affix:** кнопка открывает выбор из всех ItemAffixSO (поиск/фильтр по GroupID, Tier, RequiredLevel); выбор аффикса добавляет его в текущий пул (если ещё нет).
   - **Remove:** удаление выбранного аффикса из списка пула (не удаление ассета).
   - Кнопки: **Create new pool**, **Delete pool** (с подтверждением), **Open in Inspector**, **Ping**.
   - Блок «Used by items»: список предметов (ArmorItemSO/EquipmentItemSO), у которых Slot и DefenseType совпадают с пулом; клик → переключение на вкладку Items и выбор этого предмета.

**Результат:** удобно редактировать пулы, добавлять/убирать аффиксы, видеть связь с предметами.

---

### Фаза 3: Вкладка Affixes

4. **Вкладка Affixes:**
   - Список: все ItemAffixSO (FindAssets или ItemDatabaseSO.AllAffixes). Отображать: имя, GroupID, Tier, RequiredLevel, кратко статы.
   - Фильтры: поиск по имени/GroupID/TranslationKey, Tier, RequiredLevel.
   - Выбор аффикса → справа: редактирование полей (GroupID, Tier, RequiredLevel, TranslationKey, Stats — массив AffixStatData).
   - Блок «In pools»: список AffixPoolSO, в которых этот аффикс присутствует (сканировать все пулы); по клику — переход на вкладку Affix Pools и выбор пула.
   - Кнопки: **Add to pool** (выбор пула из списка → добавление аффикса в выбранный пул), **Remove from pool** (выбор пула из списка, где аффикс уже есть → удаление из списка), **Create new affix**, **Delete affix** (с подтверждением), **Open in Inspector**, **Ping**.

**Результат:** просмотр и правка аффиксов, управление принадлежностью пулам без лазания по папкам.

---

### Фаза 4 (опционально): Полировка

- Сохранение выбранной вкладки и выбранного элемента в SessionState.
- Кнопка «Refresh ItemDatabase» (Auto-Find) из окна.
- Подсказки (HelpBox) в каждой вкладке.

---

## 5. Технические заметки

- **Поиск предметов:** `AssetDatabase.FindAssets("t:EquipmentItemSO")` или `t:ArmorItemSO`, `t:WeaponItemSO`; для единого списка — `t:EquipmentItemSO` (наследники подхватятся).
- **Поиск пулов:** `FindAssets("t:AffixPoolSO")`, путь обычно `Assets/Resources/Affixes/Pools/...`.
- **Поиск аффиксов:** `FindAssets("t:ItemAffixSO")`.
- **Редактирование полей:** через `SerializedObject` и `SerializedProperty` для универсального отображения полей базового и производного типа (EquipmentItemSO → ArmorItemSO/WeaponItemSO).
- **Создание ассетов:** `ScriptableObject.CreateInstance<>()`, `AssetDatabase.CreateAsset()`, при создании предмета — опционально добавить в ItemDatabaseSO.AllItems и вызвать SetDirty.

---

## 6. Итог

- **Items Editor** — одно окно, три вкладки: предметы, пулы аффиксов, аффиксы.
- Предметы: список, фильтры, детали (все поля + SkillPool), связь с пулом по Slot+DefenseType, создание Armor/Weapon.
- Пулы: список, фильтры, редактирование списка аффиксов (add/remove), связь с предметами, создание/удаление пула.
- Аффиксы: список, фильтры, редактирование полей, «In pools» / «Add to pool» / «Remove from pool», создание/удаление аффикса.

Порядок работ: Фаза 1 → Фаза 2 → Фаза 3 → при необходимости Фаза 4.
