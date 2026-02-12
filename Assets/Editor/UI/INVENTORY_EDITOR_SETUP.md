# Настройка инвентаря и склада в Unity Editor

Чтобы инвентарь и склад корректно отображались и работали (перетаскивание, своп, подсветка), в сцене и префабах должно быть следующее.

---

## 1. Иерархия и компоненты

### Вариант A: всё на одном GameObject (окно инвентаря)

```
[InventoryWindow]  ← GameObject с WindowView + (опционально) Canvas/корень окна
├── UIDocument          ← Source Asset = InventoryLayout.uxml
├── InventoryUI         ← висит на том же объекте или на дочернем с UIDocument
├── ItemTooltipController  (опционально, на том же или дочернем)
└── (дочерние элементы окна, если есть)
```

- **UIDocument**: один на окно. Panel Settings — твой общий UI (например PixelArtPanelSettings). Source Asset — `Assets/UI/Inventory/InventoryLayout.uxml`.
- **InventoryUI**: должен видеть этот UIDocument. Если UIDocument на том же объекте — поле `_uiDoc` можно не заполнять (подтянется через `GetComponent<UIDocument>()`). Если на другом — перетащи UIDocument в `_uiDoc`.
- **WindowView**: если окно открывается через WindowManager, на тот же объект вешается WindowView. Его нужно передать в `InventoryUI._windowView` (или оставить пустым — подтянется `GetComponent<WindowView>()`).

### Вариант B: менеджеры данных отдельно (рекомендуется)

Часто **InventoryManager** и **StashManager** висят на отдельных GameObject’ах (например «Gameplay» или «Managers»), чтобы не уничтожаться при закрытии окна:

```
[Managers или Gameplay]
├── InventoryManager    ← один на сцену (Singleton)
└── StashManager       ← один на сцену (Singleton)

[InventoryWindow]
├── UIDocument
├── InventoryUI        ← только отрисовка и ввод; данные берёт у Instance
├── ItemTooltipController
└── WindowView
```

- **InventoryUI** не хранит данные — только подписывается на `InventoryManager.Instance` и `StashManager.Instance` в OnEnable. Важно: к моменту открытия окна оба Instance уже должны существовать (поэтому менеджеры часто на «раннем» объекте сцены).

---

## 2. Обязательные поля в Inspector

### InventoryUI

| Поле | Обязательно | Описание |
|------|-------------|----------|
| **Ui Doc** | Нет (auto) | UIDocument, чей rootVisualElement — корень UI. Если пусто — берётся `GetComponent<UIDocument>()`. |
| **Window View** | Нет (auto) | Для событий OnOpened/OnClosed и сброса режима орбы. Если пусто — `GetComponent<WindowView>()`. |
| **Orb Slots Config** | Да (для крафта) | ScriptableObject с конфигом слотов орб. Иначе подтягивается из Resources. |

### InventoryManager

| Поле | Значение | Описание |
|------|----------|----------|
| **Capacity** | 40 | Число слотов рюкзака. Должно быть равно `ROWS * COLUMNS` в коде (4×10). |
| **Cols** | 10 | Столбцов в сетке рюкзака. Должно совпадать с `InventoryUI.COLUMNS`. |

### StashManager

- Константы заданы в коде: `STASH_COLS = 8`, `STASH_ROWS = 9`. Менять только в коде, не в Inspector.

### StashPanelToggle (если используется)

| Поле | Описание |
|------|----------|
| **Inventory UI** | Ссылка на InventoryUI (тот же объект или дочерний). Можно оставить пустым — ищется через `GetComponentInChildren<InventoryUI>(true)`. |
| **Open Stash Action** | InputActionReference (например Player/OpenStash, клавиша B). |
| **Inventory Window** | WindowView окна инвентаря — чтобы при открытии склада открывать и окно. |

### InventoryWindowToggle (открытие по I)

| Поле | Описание |
|------|----------|
| **Inventory Window** | WindowView окна инвентаря. |
| **Input Action** | InputActionReference (например Player/Inventory, клавиша I). |

### ItemTooltipController

| Поле | Описание |
|------|----------|
| **Ui Doc** | Обычно тот же UIDocument, что и у инвентаря. Тултип должен быть в том же panel, что и инвентарь (иначе координаты WorldToLocal не совпадут). Если пусто — берётся root у `InventoryUI.RootVisualElement`. |

---

## 3. Контракт UXML: имена элементов

Код ищет элементы по **name** в `InventoryLayout.uxml`. Не переименовывать без правок в `InventoryUI.cs`.

| Name в UXML | Назначение |
|-------------|------------|
| **InventoryScreenRoot** | Корень документа (rootVisualElement). В USS: 480×270 px. |
| **WindowRoot** | Окно инвентаря/склада. Классы `stash-open` / `inventory-solo` переключает режим. |
| **MainRow** | Горизонтальный ряд: склад + контент. Классы `stash-open` / `inventory-solo`. |
| **StashPanel** | Контейнер склада. Класс `visible` — панель видна. |
| **StashTabsRow** | Ряд вкладок склада. |
| **StashGridContainer** | Контейнер сетки слотов склада (слоты создаются в C#). |
| **ContentRow** | Экипировка + рюкзак (в USS для ширины при stash-open). |
| **InventoryGrid** | Контейнер сетки рюкзака. Слоты и слой иконок создаются в C#. |
| **EquipmentView** | Контейнер слотов экипировки. |
| **CraftView** | Контейнер крафта (CraftSlot + орбы). |
| **Slot_Helmet**, **Slot_Body**, **Slot_MainHand**, **Slot_OffHand**, **Slot_Gloves**, **Slot_Boots** | Слоты экипировки. Имена заданы в `EquipmentSlotUxmlNames` (Scripts/Items/EquipmentSlot.cs). |
| **ToggleModeButton** | Кнопка переключения Экипировка/Крафт. |
| **CraftSlot** | Слот предмета для крафта. |
| **OrbSlotsRow** | Ряд слотов орб. |

Подробнее по раскладке: `Assets/UI/Inventory/INVENTORY_UI_LAYOUT.md`.

---

## 4. Согласование размеров (код ↔ USS)

Чтобы координаты дропа и подсветки совпадали с сеткой:

| Что | Где задаётся | Значение |
|-----|--------------|----------|
| Размер ячейки рюкзака/склада | `InventoryUI.SLOT_SIZE` (константа) | 20f |
| Сетка рюкзака | `InventoryUI`: ROWS=4, COLUMNS=10 | 4×10 = 40 слотов |
| Сетка склада | `StashManager`: STASH_ROWS=9, STASH_COLS=8 | 8×9 на вкладку |
| Рюкзак в менеджере | `InventoryManager`: _capacity=40, _cols=10 | _rows = 40/10 = 4 |

В **InventoryStyles.uss** должно быть:

- `.inventory-grid-container`: ширина = COLUMNS × 20px = **200px**, высота = ROWS × 20px = **80px**.
- `.inventory-row`: высота **20px**, ширина **200px**.
- Слоты рюкзака/склада в этой сетке: **20×20 px** (класс `.slot` без slot-2x2/2x3/2x4).

Экипировка и крафт используют другие классы (slot-2x2, slot-2x3, slot-2x4) — их размеры в USS (48×48, 48×72, 48×96) заданы в `EquipmentSlotSizes` в коде для позиционирования; главное — не менять только одну сторону (только USS или только код).

---

## 5. Порядок инициализации

1. **Awake**: InventoryManager, StashManager (создают Instance).
2. **Открытие окна** (или старт сцены): объект с UIDocument + InventoryUI включается, в OnEnable:
   - читается `_uiDoc.rootVisualElement` → ищутся элементы по name;
   - создаётся сетка рюкзака и слой иконок, подписка на Instance.
3. Если окно показывается по требованию (WindowManager), менеджеры должны жить на объекте, который не выключается вместе с окном.

Если в консоли есть `"InventoryGrid not found"` — UIDocument не назначен или в UXML нет элемента с name="InventoryGrid". Если слоты экипировки не находятся — проверь имена `Slot_Helmet` … `Slot_Boots` в UXML.

---

## 6. Краткий чеклист

- [ ] В сцене есть ровно один **InventoryManager** и один **StashManager** (или они подгружаются до открытия инвентаря).
- [ ] Окно инвентаря имеет **UIDocument** с Source = `InventoryLayout.uxml`.
- [ ] На том же (или дочернем) объекте висит **InventoryUI**; при необходимости в него проставлен **Ui Doc** и **Window View**.
- [ ] В UXML присутствуют все **name** из таблицы выше (в т.ч. InventoryGrid, WindowRoot, MainRow, StashPanel, StashGridContainer, EquipmentView, CraftView, CraftSlot, OrbSlotsRow, ToggleModeButton, Slot_Helmet … Slot_Boots).
- [ ] **InventoryManager**: Capacity=40, Cols=10 (сетка 4×10).
- [ ] В USS размеры сетки рюкзака: 200×80 px, слот 20×20 px; константа SLOT_SIZE=20 в коде не менялась (или изменена и там, и в USS).
- [ ] Тултипы: **ItemTooltipController** использует тот же UIDocument/root, что и InventoryUI (тот же panel).
- [ ] Открытие по I/B: **InventoryWindowToggle** и **StashPanelToggle** имеют ссылки на **WindowView** и при необходимости на **InventoryUI**.

После этого отображение, перетаскивание, своп и подсветка зон должны работать согласованно.
