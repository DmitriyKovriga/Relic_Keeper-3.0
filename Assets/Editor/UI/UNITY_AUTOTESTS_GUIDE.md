# Гайд По Автотестам Unity

Дата: 2026-02-23

## Что Добавлено

- EditMode-наборы тестов:
  - `Assets/Tests/EditMode/Editor/InventoryTransferServicesTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryManagerPlacementTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryManagerCraftOrbsSaveLoadTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryUIDragCloseTests.cs`
  - `Assets/Tests/EditMode/Editor/InventoryStashIntegrationTests.cs`

Текущее покрытие:
- `ItemQuickTransferService`
- `ItemDragDropService`
- `InventoryManager.PlaceItemAt` (swap/overlap)
- craft slot + экономика орбов + save/load инвентаря
- отмена drag при закрытии окна (источник: рюкзак и склад)
- интеграция stash/inventory (move/swap/rollback/save-load)

## Как Запускать Тесты В Unity

1. Открыть проект в Unity.
2. Открыть `Window -> General -> Test Runner`.
3. Выбрать `EditMode`.
4. Нажать `Run All` (или запустить конкретный тест/класс).

Ожидаемо:
- Зелёный тест = passed.
- Красный тест = failed, детали и stack trace в Test Runner.

Важно:
- `dotnet test` не является основным исполнителем Unity-тестов.
- Для реального прогона используй Unity Test Runner (или Unity batchmode с `-runTests`).

## Как Разбирать Падения

1. Открыть упавший тест в списке Test Runner.
2. Проверить:
- текст assertion
- expected vs actual
- первый фрейм из проектного кода в stack trace
3. Перезапустить только этот тест для быстрой проверки фикса.

## Как Добавлять Новые Тесты

Добавляй файлы в:
- `Assets/Tests/EditMode/Editor/`

Шаблон:
- один тестовый класс на подсистему
- один сценарий на тестовый метод
- явная очистка/инициализация статики в `[SetUp]`/`[TearDown]`

Именование:
- `MethodOrFlow_WhenCondition_ExpectedResult` (допустимый тех-формат имени)
- пример: `PlaceItemAt_WhenTargetOccupiedBySingleItem_SwapsToSourceAnchor`

## Приоритеты Для Расширения Покрытия

1. `InventoryManager`:
- переходы craft-slot
- валидация equip/unequip
- edge-cases save/load
2. Интеграция склада:
- fallback-сценарии cross-window drag/drop

Примечание по PlayMode:
- PlayMode-набор для инвентаря/склада сейчас отложен.
- Текущий рабочий стандарт: EditMode + ручной чеклист регрессии UI.

## Правила Поддержки

1. Тесты должны быть детерминированными:
- без случайности без фиксированного seed.
2. Изолируй global/static состояние:
- очищай реестры и singleton-ссылки в `[SetUp]`/`[TearDown]`.
3. Не завязывайся на состояние сцены без явного bootstrap для PlayMode.
4. Каждый фикс бага в inventory-flow должен добавлять/обновлять минимум один тест.

## Безопасный Процесс При Рефакторинге

При изменениях инвентаря:
1. Прогнать EditMode-тесты.
2. Исправить упавшие тесты или обновить expected-поведение, если оно изменилось осознанно.
3. Повторно прогнать весь EditMode-набор.
4. Пройти ручной чеклист регрессии:
- `Assets/Editor/UI/INVENTORY_REGRESSION_CHECKLIST.md`

## Пример BatchMode (CI)

```powershell
Unity.exe -batchmode -quit `
  -projectPath "d:\Work\GameDev\Games\RK\Relic_Keeper" `
  -runTests -testPlatform EditMode `
  -testResults "TestResults_EditMode.xml" `
  -logFile "EditMode.log"
```

Для PlayMode:
- `-testPlatform EditMode` -> `-testPlatform PlayMode`
