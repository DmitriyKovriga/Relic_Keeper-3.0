# Анализ инвентаря: перенос, свап, экипировка — баги и исправления

## 1. InventoryManager

### 1.1 Якорь vs любая ячейка (критичный баг)

**Проблема:** `TryMoveOrSwap(fromIndex, toIndex)` получает `fromIndex` — это может быть **любая** ячейка многоклеточного предмета (например, ячейка 1 у предмета 2x2 с якорем 0). Далее вызываются:
- `TryEquipItem(fromIndex, ...)` и внутри `ClearItemAtAnchor(fromIndex, itemToEquip)` — очистка идёт от `fromIndex` как от якоря, т.е. очищаются неправильные ячейки.
- `HandleBackpackMove(fromIndex, toIndex, itemA)` — там тоже `ClearItemAtAnchor(fromIndex, itemA)` — та же ошибка.
- `TryUnequipItem` вызывает `TryEquipItem(backpackIndex, ...)` — `backpackIndex` может быть не якорем второго предмета.

**Исправление:** В `TryMoveOrSwap` один раз вычислить якорь: `GetItemAt(fromIndex, out int fromAnchor)`. Во все операции «удалить из источника» передавать `fromAnchor`, а не `fromIndex`. В `TryEquipItem` первый параметр трактовать как якорь. В `TryUnequipItem` при вызове `TryEquipItem` передавать `anchorIndex` (якорь предмета в рюкзаке), а не `backpackIndex`.

### 1.2 Дублирование предметов в сохранении (критичный баг)

**Проблема:** В `GetSaveData()` для рюкзака делается:
```csharp
for (int i = 0; i < Items.Length; i++)
    if (Items[i] != null && ...) data.Items.Add(Items[i].GetSaveData(i));
```
Для предмета 2x2 в ячейках 0,1,10,11 одна и та же вещь сохраняется 4 раза с разными SlotIndex. При загрузке создаётся 4 копии предмета.

**Исправление:** Сохранять предмет только если текущая ячейка — его якорь: `GetItemAt(i, out int anchor)` и добавлять в `data.Items` только когда `anchor == i`.

### 1.3 Проверка границ для экипировки

**Проблема:** В `TryEquipItem` используется `localEquipIndex = equipGlobalIndex - EQUIP_OFFSET`. Если `equipGlobalIndex` некорректен (например, отрицательный после вычитания), возможен выход за границы массива. На практике вызывается только при `toIndex >= EQUIP_OFFSET`, но явная проверка `localEquipIndex >= 0 && localEquipIndex < EquipmentItems.Length` повышает надёжность.

**Исправление:** В начале `TryEquipItem` проверить границы `localEquipIndex`.

---

## 2. StashManager

### 2.1 Своп внутри склада: неверный слот при размещении

**Проблема:** В `TryMoveStashToStash` при свопе двух предметов выполняется `PlaceItem(toTab, toSlotIndex, item)`. Здесь `toSlotIndex` — слот, по которому кликнули; он может быть любой ячейкой области второго предмета. Размещать нужно по **якорю** освободившейся области, т.е. по `otherAnchor`.

**Исправление:** Заменить `PlaceItem(toTab, toSlotIndex, item)` на `PlaceItem(toTab, otherAnchor, item)`.

### 2.2 GetSaveData

В StashManager уже используется проверка якоря (`isAnchor`) при сохранении — дубликатов нет.

---

## 3. Сводка изменений

| Место | Что сделано |
|-------|-------------|
| InventoryManager.TryMoveOrSwap | Вычисление fromAnchor, передача якоря в TryEquipItem и HandleBackpackMove |
| InventoryManager.TryEquipItem | Параметр трактуется как fromAnchor; проверка границ localEquipIndex |
| InventoryManager.TryUnequipItem | Вызов TryEquipItem(anchorIndex, ...) вместо backpackIndex |
| InventoryManager.GetSaveData | Сохранение только по якорю (GetItemAt(i, out anchor) && anchor == i) |
| StashManager.TryMoveStashToStash | PlaceItem(toTab, otherAnchor, item) вместо toSlotIndex при свопе |
