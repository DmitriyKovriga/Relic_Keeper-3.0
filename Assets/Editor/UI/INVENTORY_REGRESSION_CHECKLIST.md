# Чеклист Регрессии UI Инвентаря

Дата: 2026-02-23  
Гейт сборки: `dotnet build Relic_Keeper.slnx -nologo` должен проходить до и после проверки.

## Drag & Drop
1. Взять предмет из рюкзака и бросить в пустую область рюкзака.  
Ожидаемо: предмет ставится под целевой указатель с корректным якорем.
2. Бросить предмет на занятую область, где пересекается ровно один предмет.  
Ожидаемо: срабатывает swap, без дюпов и потерь.
3. Бросить предмет в невалидную область.  
Ожидаемо: предмет возвращается в исходный слот.
4. Перетащить предмет из склада в инвентарь и обратно.  
Ожидаемо: перенос атомарный, ghost-копии не остаются.

## Ctrl+Click Переносы
1. Ctrl+Click по предмету рюкзака при открытом складе.  
Ожидаемо: предмет уходит в текущую вкладку склада.
2. Ctrl+Click по предмету склада при открытом инвентаре.  
Ожидаемо: предмет уходит в рюкзак/экипировку, если валидно.
3. Ctrl+Click по предмету в craft-slot.  
Ожидаемо: перенос выполняется через общий quick-transfer endpoint.

## Craft Slot / Орбы
1. Открыть вкладку крафта и положить редкий предмет в craft-slot.  
Ожидаемо: предмет рендерится и остаётся после refresh.
2. ПКМ по орбе с положительным количеством и применить к редкому предмету.  
Ожидаемо: количество орбов уменьшается на 1, предмет рероллится.
3. Нажать `Escape` в режиме применения орбы.  
Ожидаемо: режим отменяется, ghost-орба под курсором исчезает.

## Компоновка Окон
1. Открыть инвентарь без склада.  
Ожидаемо: layout инвентаря остаётся на фиксированной позиции.
2. Открыть склад вместе с инвентарём.  
Ожидаемо: оба окна видны, независимы, инвентарь не сдвигается.
3. Закрыть инвентарь, удерживая предмет в drag.  
Ожидаемо: предмет безопасно возвращается в source/fallback контейнер.

## Save/Load
1. Сохранить игру с предметами в рюкзаке/экипировке/craft-slot/складе.
2. Перезагрузить сцену или состояние игры.  
Ожидаемо: состояние предметов полностью восстановлено, без потери якорей.

## Журнал Результатов
- [+] Drag & Drop
- [+] Ctrl+Click Переносы
- [+] Craft Slot / Орбы
- [+] Компоновка Окон
- [+] Save/Load

## Карта Автопокрытия (EditMode)
- `Drag & Drop возврат в источник`:
  - `Assets/Tests/EditMode/Editor/InventoryUIDragCloseTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryStashIntegrationTests.cs` (atomic rollback)
- `Swap-if-one`:
  - `Assets/Tests/EditMode/Editor/InventoryManagerPlacementTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryStashIntegrationTests.cs`
- `stash <-> inventory transfer`:
  - `Assets/Tests/EditMode/Editor/InventoryTransferServicesTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryStashIntegrationTests.cs`
- `craft slot + орбы`:
  - `Assets/Tests/EditMode/Editor/InventoryManagerCraftOrbsSaveLoadTests.cs`
- `save/load целостность`:
  - `Assets/Tests/EditMode/Editor/InventoryManagerCraftOrbsSaveLoadTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryStashIntegrationTests.cs`
