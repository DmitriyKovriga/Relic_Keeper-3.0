# План: Склад (Stash)

## Цель
Склад с вкладками слева от инвентаря, как в Path of Exile: те же ячейки для предметов, но сетка больше; несколько вкладок (изначально 1), добавление новой по кнопке «+» справа от вкладок; сохранение/загрузка в сейв; drag & drop между инвентарём и складом.

## Данные

- **StashTabSaveData**: имя вкладки (опционально), список `ItemSaveData` (те же структуры, что в инвентаре; `SlotIndex` = индекс ячейки сетки 0..(Cols*Rows-1)).
- **StashSaveData**: список вкладок `List<StashTabSaveData>`.
- **GameSaveData**: поле `StashSaveData Stash`.

## Логика

- **StashManager** (singleton):
  - Константы: STASH_COLS = 12, STASH_ROWS = 20 (или 15) — размер сетки одной вкладки.
  - Данные: список вкладок, каждая вкладка — массив `InventoryItem[]` размером STASH_COLS * STASH_ROWS; текущий индекс вкладки.
  - Методы: AddTab(), GetItem(tabIndex, slotIndex), CanPlaceItemAt(tab, slot, item), TryPlaceItem(tab, slot, item), TryMoveFromInventory(invSlotIndex, toTab, toSlot), TryMoveToInventory(fromTab, fromSlot, toInvIndex), GetSaveData(), LoadState(StashSaveData, ItemDatabase).
  - Событие OnStashChanged для обновления UI.

## UI

- **Расположение**: склад слева от текущего контента (Equipment + Inventory). Окно инвентаря расширяется или слева добавляется колонка: [ Stash | Equipment + Inventory ].
- **Вкладки**: горизонтальная полоса сверху склада. Слева направо: вкладка 1, вкладка 2, …; справа — полупрозрачная «вкладка» с текстом «+». Клик по «+» создаёт новую вкладку. Выбранная вкладка визуально выделена.
- **Сетка**: под полосой вкладок — сетка слотов того же размера (24px), STASH_COLS x STASH_ROWS. Иконки предметов рисуются так же, как в рюкзаке (позиция по anchor-слоту).
- **Drag & drop**:
  - Начало перетаскивания: из инвентаря (рюкзак/экипировка/крафт) или из склада (текущая вкладка, слот). Запоминаем источник (inventory vs stash, индекс/вкладка/слот).
  - Отпускание: если курсор над слотом склада — перемещение/своп из инвентаря в склад или внутри склада; если над слотом инвентаря/экипировки — из склада в инвентарь или в слот экипировки (с проверкой типа).

## Порядок реализации

1. **Сейв**: StashTabSaveData, StashSaveData; GameSaveData.Stash; сохранение/загрузка в GameSaveManager (при отсутствии Stash — пустой склад с одной пустой вкладкой).
2. **StashManager**: класс, Awake/Instance, одна вкладка по умолчанию, AddTab, сетка (CanPlaceItemAt, IsSlotOccupied, GetItemAt по аналогии с InventoryManager), TryMoveFromInventory / TryMoveToInventory, GetSaveData / LoadState.
3. **UI**: в UXML добавить слева колонку «Stash» (табы + сетка); в USS стили для stash-tabs, stash-tab, stash-tab-add, stash-grid; в InventoryUI построение вкладок и сетки склада, отрисовка иконок в текущей вкладке.
4. **Drag & drop**: при PointerDown различать слот инвентаря vs слот склада (userData: отрицательный или специальный маркер для склада — например tab*1000+slot). При PointerUp определять, над чем отпустили (инвентарь или склад), вызывать StashManager.TryMoveFromInventory / TryMoveToInventory или InventoryManager.TryMoveOrSwap + StashManager.

## Замечания

- Размер одной вкладки: 12x20 = 240 слотов (StashManager.STASH_COLS, STASH_ROWS). При необходимости можно вынести в настройки.
- Имена вкладок в сейве: «Tab 1», «Tab 2». Переименование можно добавить позже.
- Миграция сейва: если Stash == null при загрузке, передаётся new StashSaveData(), LoadState создаёт одну пустую вкладку.
- **В сцену** нужно добавить компонент **StashManager** (на тот же GameObject, что и InventoryManager, или отдельный). Без него склад не будет сохраняться/загружаться и панель склада будет пустой.
- **Склад не открывается по I**: по I открывается только инвентарь. Склад открывается по отдельному бинду **B** (Player/OpenStash) или в будущем при взаимодействии с сундуком. Компонент **StashPanelToggle** на объекте с инвентарём: задать InventoryUI, InputActionReference = OpenStash (из InputSystem_Actions), при желании — окно инвентаря, чтобы при первом нажатии B открыть и окно, и склад.
- **Размер сетки склада**: 6×10 слотов на вкладку (можно увеличить в StashManager.STASH_COLS / STASH_ROWS).
