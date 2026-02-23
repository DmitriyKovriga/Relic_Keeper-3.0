# Интеграция Нового Окна В Перенос Предметов

Дата: 2026-02-23

## Зачем Это Нужно

Чтобы новое окно (крафт, торговец, разбор и т.д.) работало вместе с инвентарём одинаково:
- drag & drop между окнами;
- `Ctrl+Click` быстрый перенос;
- отсутствие жёсткой связки с `InventoryUI`.

Базовые сервисы:
- `ItemQuickTransferService` — для `Ctrl+Click`/быстрого переноса.
- `ItemDragDropService` — для drop под курсором.
- `ItemTransferEndpointRegistration` — helper для регистрации пары endpoint-ов одной строкой.

## Общие Константы

Используй общие идентификаторы и приоритеты:
- `ItemTransferEndpointIds`
- `ItemTransferEndpointPriorities`

Для нового окна рекомендуется стартовый приоритет:
- `ItemTransferEndpointPriorities.CompanionDefault`

Это ниже инвентаря/крафт-слота/склада, чтобы базовый flow не ломался.

## Минимальный Чеклист Подключения Нового Окна

1. Зарегистрировать `quick-transfer endpoint`.
2. Зарегистрировать `drag-drop endpoint`.
3. В `OnDisable`/`OnDestroy` корректно вызвать `Dispose()` регистраций.
4. Реализовать `IsOpen`, чтобы endpoint работал только при реально открытом окне.
5. Реализовать `CanAccept` с жёсткой валидацией правил окна.
6. Реализовать `TryAccept` атомарно:
- успех только после фактического размещения предмета;
- при неуспехе вернуть `false`, без частично изменённого состояния.

## Шаблон Регистрации

```csharp
_transferRegistration = ItemTransferEndpointRegistration.RegisterPair(
    endpointId: "inventory.my-window",
    priority: ItemTransferEndpointPriorities.CompanionDefault,
    isOpen: IsWindowOpen,
    canAcceptQuick: ctx => ctx.Item != null && ctx.Item.Data != null && CanPlace(ctx.Item),
    tryAcceptQuick: ctx => TryPlace(ctx.Item),
    isPointerOver: IsPointerOverWindow,
    canAcceptDrop: ctx => ctx.Item != null && ctx.Item.Data != null && CanPlace(ctx.Item),
    tryAcceptDrop: ctx => TryPlace(ctx.Item));
```

## Правила Приоритетов

- Больше число = выше приоритет маршрутизации.
- Текущая базовая шкала:
  - `InventoryBackpack = 100`
  - `CraftSlot = 95`
  - `StashCurrentTab = 90`
  - `CompanionDefault = 80`

Если новому окну нужно быть приоритетнее склада, подними приоритет > `90`.

## Что Проверять После Подключения

1. `Ctrl+Click` из рюкзака в новое окно (когда окно открыто).
2. `Ctrl+Click` обратно из нового окна в инвентарь/склад.
3. Drag из инвентаря в новое окно и обратно.
4. Закрытие окна во время drag (не должно терять предмет).
5. Save/load (если окно хранит состояние).

## Текущий Подход К Тестам

PlayMode сейчас **не обязателен** для этой задачи.  
Минимум: EditMode + ручной чеклист UI.
