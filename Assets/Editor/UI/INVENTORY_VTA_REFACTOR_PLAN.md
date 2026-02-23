# План Рефакторинга Inventory UI (VTA-first)

Дата обновления: 2026-02-23

## Цель
- Визуал и компоновка задаются в UXML/USS.
- Код C# управляет состоянием, биндингами, событиями и геймплейной логикой.
- Окна UI связываются через общий контракт переносов (`quick-transfer`, `drag-drop`), без жёсткой взаимозависимости.

## Текущая Фаза
- **Фаза 8 (завершена 2026-02-23)**.

## Область
- `Assets/UI/Inventory/InventoryLayout.uxml`
- `Assets/UI/Inventory/InventoryStyles.uss`
- `Assets/UI/Inventory/InventoryUI*.cs`
- `Assets/UI/Inventory/Logic/*.cs`
- `Assets/UI/Tavern/TavernUI*.cs`

## Фаза 1. VTA-переход
Статус: завершена

- Статические слои слотов в UXML для рюкзака и склада.
- Runtime-генерация оставлена только как fallback.
- Арт-элементы вынесены в VTA.
- Биндинг слотов выполняется к существующему VTA вместо сборки основного дерева в рантайме.

## Фаза 2. Общий слой переносов
Статус: завершена

- Введён `ItemQuickTransferService` для `Ctrl+Click`.
- Введён `ItemDragDropService` для drop по курсору.
- Inventory/Stash/Craft подключены как endpoint-ы этих сервисов.
- Основные layout-мутации убраны из C# в USS-классы.

## Фаза 3. Декомпозиция крупных классов
Статус: завершена

- `InventoryManager` разбит на модули:
  - `InventoryManager.MoveLogic.cs`
  - `InventoryManager.Placement.cs`
  - `InventoryManager.Craft.cs`
  - `InventoryManager.Orbs.cs`
  - `InventoryManager.SaveLoad.cs`
- `InventoryUI` разбит по зонам ответственности:
  - `InventoryUI.LayoutBinding.cs`
  - `InventoryUI.Rendering.cs`
  - `InventoryUI.Crafting.cs`
  - `InventoryUI.DragDrop.cs`
  - `InventoryUI.QuickTransfer.cs`
  - `InventoryUI.Stash.cs`
- `TavernUI` декомпозирован на presenter/actions/localization/cards-модули.

## Фаза 4. Регрессия и автопокрытие (EditMode)
Статус: завершена

- Ручной чеклист:
  - `Assets/Editor/UI/INVENTORY_REGRESSION_CHECKLIST.md`
- EditMode-тесты:
  - `Assets/Tests/EditMode/Editor/InventoryTransferServicesTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryManagerPlacementTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryManagerCraftOrbsSaveLoadTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryUIDragCloseTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryStashIntegrationTests.cs`
- Гайд по тестам:
  - `Assets/Editor/UI/UNITY_AUTOTESTS_GUIDE.md`

## Фаза 5. Контракт для будущих окон
Статус: завершена

- Централизованы endpoint id/priority:
  - `Assets/UI/Inventory/Logic/Transfers/ItemTransferEndpointIds.cs`
- Добавлен helper парной регистрации endpoint-ов:
  - `ItemTransferEndpointRegistration` (в том же файле)
- `InventoryUI` переведён на общий helper регистрации:
  - `Assets/UI/Inventory/InventoryUI.QuickTransfer.cs`
- Добавлен гайд интеграции нового окна:
  - `Assets/Editor/UI/TRANSFER_ENDPOINTS_INTEGRATION_GUIDE.md`
- PlayMode-набор отложен по решению команды (используем EditMode + ручной UI-чеклист).

## Фаза 6. Подготовка к отделению Stash UI
Статус: завершена

- В stash-части `InventoryUI` выделены presenter-слои:
  - `StashTabsPresenter`
  - `StashGridPresenter`
- Снижена связность `InventoryUI` с деталями рендера вкладок и иконок склада.

## Фаза 7. Отдельный stash UI-компонент
Статус: завершена

- Вынесен контроллер склада `StashWindowController`.
- `InventoryUI` переведён на работу с контроллером (tabs/icons orchestration).
- `StashTabsPresenter` и `StashGridPresenter` вынесены в top-level классы модуля stash.

## Фаза 8. Вынос stash-input в контроллер
Статус: завершена

- Логика stash-input (`take`, `ctrl-transfer`, `resolve drag source`) перенесена в `StashWindowController`.
- `InventoryUI` оставлен с тонкими обёртками применения drag-state:
  - `BeginStashDrag(...)`
  - обработчики `OnStashIconPointerDown`/`OnStashSlotPointerDown` делегируют решение контроллеру.
- Добавлены структуры контрактов stash-input:
  - `StashPointerAction`
  - `StashPointerActionKind`

## Критерии готовности трека
- Основной визуал инвентаря редактируется в UXML/USS.
- Поведение inventory/stash/craft сохраняет совместимость.
- Переносы между окнами идут через общий endpoint-контракт.
- Новые окна подключаются без жёсткой связки с `InventoryUI`.
