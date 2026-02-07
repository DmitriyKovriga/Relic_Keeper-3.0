using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Saving;

namespace Scripts.Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private int _capacity = 40;
        [SerializeField] private int _cols = 10; 

        // ИНДЕКСЫ: 0=Head, 1=Body, 2=Main, 3=Off, 4=Gloves, 5=Boots
        public InventoryItem[] EquipmentItems = new InventoryItem[6];
        public const int EQUIP_OFFSET = 100;

        /// <summary> Один слот крафта (предмет сверху в режиме крафта). Сохраняется как инвентарь. </summary>
        public const int CRAFT_SLOT_INDEX = 200;
        public InventoryItem CraftingSlotItem { get; private set; }

        public InventoryItem[] Items;

        private List<OrbCountEntry> _orbCounts = new List<OrbCountEntry>(); 

        // События
        public event Action OnInventoryChanged;
        public event Action<InventoryItem> OnItemEquipped;
        public event Action<InventoryItem> OnItemUnequipped;

        private void Awake()
        {
            if (Instance != null && Instance != this) 
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (Items == null || Items.Length != _capacity) Items = new InventoryItem[_capacity];
            if (EquipmentItems == null || EquipmentItems.Length != 6) EquipmentItems = new InventoryItem[6];
            if (_orbCounts == null) _orbCounts = new List<OrbCountEntry>();
        }

        public int GetOrbCount(string orbId)
        {
            if (string.IsNullOrEmpty(orbId)) return 0;
            var e = _orbCounts.Find(x => x.OrbId == orbId);
            return e?.Count ?? 0;
        }

        public void AddOrb(string orbId, int count = 1)
        {
            if (string.IsNullOrEmpty(orbId) || count <= 0) return;
            var e = _orbCounts.Find(x => x.OrbId == orbId);
            if (e != null) e.Count += count;
            else _orbCounts.Add(new OrbCountEntry { OrbId = orbId, Count = count });
        }

        public bool ConsumeOrb(string orbId)
        {
            if (string.IsNullOrEmpty(orbId)) return false;
            var e = _orbCounts.Find(x => x.OrbId == orbId);
            if (e == null || e.Count <= 0) return false;
            e.Count--;
            if (e.Count <= 0) _orbCounts.Remove(e);
            return true;
        }

        public void TriggerUIUpdate() => OnInventoryChanged?.Invoke();

        /// <summary>Установить предмет в слот крафта (для переноса из склада и т.п.).</summary>
        public void SetCraftingSlotItem(InventoryItem item)
        {
            CraftingSlotItem = item;
            TriggerUIUpdate();
        }

        /// <summary>Вызвать событие экипировки (для внешнего кода, например StashManager).</summary>
        public void NotifyItemEquipped(InventoryItem item) => OnItemEquipped?.Invoke(item);

        /// <summary>Вызвать событие снятия (для внешнего кода).</summary>
        public void NotifyItemUnequipped(InventoryItem item) => OnItemUnequipped?.Invoke(item);

        public InventoryItem GetItem(int index)
        {
            if (index == CRAFT_SLOT_INDEX) return CraftingSlotItem;
            if (index >= EQUIP_OFFSET)
            {
                int localIndex = index - EQUIP_OFFSET;
                if (localIndex < 0 || localIndex >= EquipmentItems.Length) return null;
                return EquipmentItems[localIndex];
            }
            if (index < 0 || index >= Items.Length) return null;
            return Items[index];
        }

        public bool AddItem(InventoryItem newItem)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (CanPlaceItemAt(newItem, i))
                {
                    PlaceItemAtAnchor(i, newItem);
                    TriggerUIUpdate();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Забрать предмет из слота (рюкзак/экипировка/крафт). Предмет больше не числится в инвентаре — только в возвращаемой ссылке.</summary>
        public InventoryItem TakeItemFromSlot(int slotIndex)
        {
            InventoryItem item = GetItemAt(slotIndex, out int anchor);
            if (item == null) return null;

            if (slotIndex == CRAFT_SLOT_INDEX)
            {
                CraftingSlotItem = null;
                TriggerUIUpdate();
                return item;
            }
            if (slotIndex >= EQUIP_OFFSET)
            {
                int local = slotIndex - EQUIP_OFFSET;
                if (local < 0 || local >= EquipmentItems.Length) return null;
                EquipmentItems[local] = null;
                OnItemUnequipped?.Invoke(item);
                TriggerUIUpdate();
                return item;
            }
            ClearItemAtAnchor(anchor, item);
            TriggerUIUpdate();
            return item;
        }

        /// <summary>Поставить уже взятый предмет в слот. sourceAnchorForSwap — куда положить вытесненный предмет при свопе (-1 если не своп).</summary>
        public bool PlaceItemAt(InventoryItem item, int toIndex, int sourceAnchorForSwap = -1)
        {
            if (item == null || item.Data == null) return false;

            // Возврат в тот же слот (например отмена перетаскивания)
            if (sourceAnchorForSwap >= 0 && toIndex == sourceAnchorForSwap)
            {
                PlaceItemAtSlotInternal(item, toIndex);
                TriggerUIUpdate();
                return true;
            }

            // Слот крафта
            if (toIndex == CRAFT_SLOT_INDEX)
            {
                InventoryItem displaced = CraftingSlotItem;
                CraftingSlotItem = item;
                if (displaced != null && sourceAnchorForSwap >= 0)
                    PlaceItemAtSlotInternal(displaced, sourceAnchorForSwap);
                TriggerUIUpdate();
                return true;
            }

            // Экипировка
            if (toIndex >= EQUIP_OFFSET)
            {
                int local = toIndex - EQUIP_OFFSET;
                if (local < 0 || local >= EquipmentItems.Length) return false;
                if ((int)item.Data.Slot != local) return false;
                InventoryItem displaced = EquipmentItems[local];
                EquipmentItems[local] = item;
                OnItemEquipped?.Invoke(item);
                if (displaced != null && sourceAnchorForSwap >= 0)
                    PlaceItemAtSlotInternal(displaced, sourceAnchorForSwap);
                TriggerUIUpdate();
                return true;
            }

            // Рюкзак
            InventoryItem other = GetItemAt(toIndex, out int otherAnchor);
            if (other != null && other != item)
            {
                if (sourceAnchorForSwap < 0) return false;
                if (!CanPlaceItemAt(item, toIndex)) return false;
                if (!CanPlaceItemAt(other, sourceAnchorForSwap)) return false;
                ClearItemAtAnchor(otherAnchor, other);
                PlaceItemAtAnchor(toIndex, item);
                PlaceItemAtAnchor(sourceAnchorForSwap, other);
            }
            else
            {
                if (!CanPlaceItemAt(item, toIndex)) return false;
                PlaceItemAtAnchor(toIndex, item);
            }
            TriggerUIUpdate();
            return true;
        }

        private void PlaceItemAtSlotInternal(InventoryItem item, int index)
        {
            if (item == null || item.Data == null) return;
            if (index == CRAFT_SLOT_INDEX)
            {
                CraftingSlotItem = item;
                return;
            }
            if (index >= EQUIP_OFFSET)
            {
                int local = index - EQUIP_OFFSET;
                if (local >= 0 && local < EquipmentItems.Length)
                {
                    EquipmentItems[local] = item;
                    OnItemEquipped?.Invoke(item);
                }
                return;
            }
            if (index >= 0 && index < Items.Length)
                PlaceItemAtAnchor(index, item);
        }

        public bool TryMoveOrSwap(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return true;

            InventoryItem itemFrom = GetItemAt(fromIndex, out int fromAnchor);
            if (itemFrom == null)
            {
                Debug.LogError($"[InventoryManager] Error: Item at {fromIndex} is null!");
                return false;
            }
            if (itemFrom == GetItem(toIndex)) return true;

            bool success = false;

            if (fromIndex == CRAFT_SLOT_INDEX)
            {
                success = HandleMoveFromCraftSlot(toIndex);
            }
            else if (toIndex == CRAFT_SLOT_INDEX)
            {
                success = HandleMoveToCraftSlot(fromIndex);
            }
            else if (toIndex >= EQUIP_OFFSET)
            {
                success = TryEquipItem(fromAnchor, toIndex, itemFrom);
            }
            else if (fromIndex >= EQUIP_OFFSET)
            {
                success = TryUnequipItem(fromIndex, toIndex, itemFrom);
            }
            else
            {
                success = HandleBackpackMove(fromAnchor, toIndex, itemFrom);
            }

            if (success) TriggerUIUpdate();
            return success;
        }

        private bool TryEquipItem(int fromAnchor, int equipGlobalIndex, InventoryItem itemToEquip)
        {
            int localEquipIndex = equipGlobalIndex - EQUIP_OFFSET;
            if (localEquipIndex < 0 || localEquipIndex >= EquipmentItems.Length)
            {
                Debug.LogWarning($"[Inventory] Invalid equip slot: {equipGlobalIndex}");
                return false;
            }

            InventoryItem currentEquipped = EquipmentItems[localEquipIndex];

            // 1. ПРОВЕРКА ТИПА СЛОТА
            int requiredSlotType = localEquipIndex;
            int itemSlotType = (int)itemToEquip.Data.Slot;

            if (itemSlotType != requiredSlotType)
            {
                Debug.LogWarning($"[Inventory] Wrong slot type. Item: {itemSlotType}, Slot: {requiredSlotType}");
                return false;
            }

            // 2. УДАЛЕНИЕ ИЗ РЮКЗАКА (все ячейки по якорю)
            ClearItemAtAnchor(fromAnchor, itemToEquip);

            // 3. ОБРАБОТКА СВАПА
            if (currentEquipped != null && currentEquipped.Data != null)
            {
                if (CanPlaceItemAt(currentEquipped, fromAnchor))
                {
                    PlaceItemAtAnchor(fromAnchor, currentEquipped);
                    OnItemUnequipped?.Invoke(currentEquipped);
                }
                else
                {
                    Debug.LogWarning("[Inventory] Swap failed (no space). Reverting.");
                    PlaceItemAtAnchor(fromAnchor, itemToEquip);
                    return false;
                }
            }

            // 4. ФИНАЛЬНОЕ ПРИСВОЕНИЕ
            EquipmentItems[localEquipIndex] = itemToEquip;
            OnItemEquipped?.Invoke(itemToEquip);
            return true;
        }

        private bool TryUnequipItem(int equipGlobalIndex, int backpackIndex, InventoryItem itemToUnequip)
        {
            InventoryItem itemInBackpack = GetItemAt(backpackIndex, out int backpackAnchor);

            // Если в целевом слоте рюкзака пусто
            if (itemInBackpack == null)
            {
                if (CanPlaceItemAt(itemToUnequip, backpackIndex))
                {
                    int localEquip = equipGlobalIndex - EQUIP_OFFSET;
                    if (localEquip < 0 || localEquip >= EquipmentItems.Length) return false;
                    EquipmentItems[localEquip] = null;
                    PlaceItemAtAnchor(backpackIndex, itemToUnequip);
                    OnItemUnequipped?.Invoke(itemToUnequip);
                    return true;
                }
                return false;
            }

            // Если там что-то есть — своп: экипируем itemInBackpack, снимаем itemToUnequip в рюкзак по backpackAnchor
            return TryEquipItem(backpackAnchor, equipGlobalIndex, itemInBackpack);
        }

        private bool HandleMoveFromCraftSlot(int toIndex)
        {
            if (CraftingSlotItem == null) return false;
            var item = CraftingSlotItem;

            if (toIndex >= EQUIP_OFFSET)
            {
                int localEquipIndex = toIndex - EQUIP_OFFSET;
                if ((int)item.Data.Slot != localEquipIndex) return false;
                var currentEquipped = EquipmentItems[localEquipIndex];
                CraftingSlotItem = null;
                EquipmentItems[localEquipIndex] = item;
                if (currentEquipped != null)
                {
                    CraftingSlotItem = currentEquipped;
                    OnItemUnequipped?.Invoke(currentEquipped);
                }
                OnItemEquipped?.Invoke(item);
                return true;
            }

            if (!CanPlaceItemAt(item, toIndex)) return false;
            CraftingSlotItem = null;
            PlaceItemAtAnchor(toIndex, item);
            return true;
        }

        private bool HandleMoveToCraftSlot(int fromIndex)
        {
            var itemFrom = GetItemAt(fromIndex, out int fromAnchor);
            if (itemFrom == null) return false;
            var previousCraft = CraftingSlotItem;

            if (fromIndex >= EQUIP_OFFSET)
            {
                int localIndex = fromIndex - EQUIP_OFFSET;
                EquipmentItems[localIndex] = previousCraft;
                if (previousCraft != null) OnItemUnequipped?.Invoke(itemFrom);
                CraftingSlotItem = itemFrom;
                OnItemEquipped?.Invoke(previousCraft);
                return true;
            }

            ClearItemAtAnchor(fromAnchor, itemFrom);
            if (previousCraft != null) PlaceItemAtAnchor(fromAnchor, previousCraft);
            CraftingSlotItem = itemFrom;
            return true;
        }

        private void ClearItemAtAnchor(int anchorIndex, InventoryItem item)
        {
            if (item?.Data == null || anchorIndex < 0 || anchorIndex >= Items.Length) return;
            int w = item.Data.Width;
            int h = item.Data.Height;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    int idx = anchorIndex + r * _cols + c;
                    if (idx >= 0 && idx < Items.Length) Items[idx] = null;
                }
        }

        private void PlaceItemAtAnchor(int anchorIndex, InventoryItem item)
        {
            if (item?.Data == null || anchorIndex < 0 || anchorIndex >= Items.Length) return;
            int w = item.Data.Width;
            int h = item.Data.Height;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    int idx = anchorIndex + r * _cols + c;
                    if (idx >= 0 && idx < Items.Length) Items[idx] = item;
                }
        }

        /// <summary>Удалить предмет из рюкзака по любому слоту (очищает все ячейки многоклеточного предмета).</summary>
        public void RemoveItemAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Items.Length) return;
            InventoryItem item = GetItemAt(slotIndex, out int anchorIndex);
            if (item == null) return;
            ClearItemAtAnchor(anchorIndex, item);
        }

        /// <summary>Поставить предмет в рюкзак по якорному слоту (заполняет все ячейки).</summary>
        public void PlaceItemAt(int anchorIndex, InventoryItem item)
        {
            if (anchorIndex < 0 || anchorIndex >= Items.Length || item?.Data == null) return;
            PlaceItemAtAnchor(anchorIndex, item);
        }

        /// <summary>Подсчитать уникальные предметы в области (targetIndex + размер item).</summary>
        private int CountUniqueItemsInArea(int targetIndex, InventoryItem item)
        {
            if (item?.Data == null || targetIndex < 0 || targetIndex >= Items.Length) return 0;
            HashSet<InventoryItem> uniqueItems = new HashSet<InventoryItem>();
            int w = item.Data.Width;
            int h = item.Data.Height;
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    int idx = targetIndex + r * _cols + c;
                    if (idx >= 0 && idx < Items.Length && Items[idx] != null)
                        uniqueItems.Add(Items[idx]);
                }
            }
            return uniqueItems.Count;
        }

        /// <summary>Получить единственный предмет в области (если он один) и его якорь.</summary>
        private bool GetSingleItemInArea(int targetIndex, InventoryItem item, out InventoryItem foundItem, out int foundAnchor)
        {
            foundItem = null;
            foundAnchor = -1;
            if (item?.Data == null || targetIndex < 0 || targetIndex >= Items.Length) return false;
            InventoryItem firstItem = null;
            int firstAnchor = -1;
            int w = item.Data.Width;
            int h = item.Data.Height;
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    int idx = targetIndex + r * _cols + c;
                    if (idx >= 0 && idx < Items.Length && Items[idx] != null)
                    {
                        InventoryItem cellItem = GetItemAt(idx, out int anchor);
                        if (firstItem == null)
                        {
                            firstItem = cellItem;
                            firstAnchor = anchor;
                        }
                        else if (cellItem != firstItem)
                        {
                            return false; // Больше одного уникального предмета
                        }
                    }
                }
            }
            if (firstItem != null)
            {
                foundItem = firstItem;
                foundAnchor = firstAnchor;
                return true;
            }
            return false;
        }

        private bool HandleBackpackMove(int fromIndex, int toIndex, InventoryItem itemA)
        {
            // 1. Проверка на множественный свап: если в целевой области больше 1 предмета - запрет
            int uniqueCount = CountUniqueItemsInArea(toIndex, itemA);
            if (uniqueCount > 1) return false;

            // 2. Пустая цель - обычный перенос
            if (uniqueCount == 0)
            {
                if (!CanPlaceItemAt(itemA, toIndex)) return false;
                ClearItemAtAnchor(fromIndex, itemA);
                PlaceItemAtAnchor(toIndex, itemA);
                OnInventoryChanged?.Invoke();
                return true;
            }

            // 3. Один предмет в области - определяем свап на себя или с другим
            if (!GetSingleItemInArea(toIndex, itemA, out InventoryItem itemB, out int indexB))
                return false;

            if (itemB == itemA)
            {
                // Свап на себя: убираем предмет, проверяем может ли он встать на место, размещаем
                ClearItemAtAnchor(fromIndex, itemA);
                if (CanPlaceItemAt(itemA, toIndex))
                {
                    PlaceItemAtAnchor(toIndex, itemA);
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                PlaceItemAtAnchor(fromIndex, itemA);
                return false;
            }
            else
            {
                // Свап с другим предметом: убираем оба, проверяем могут ли поменяться местами, размещаем оба
                if (fromIndex == indexB) return false; // Нельзя свапнуть на то же место
                InventoryItem itemTarget = GetItem(indexB);
                if (itemTarget == null) return false;

                // Проверяем ДО удаления
                if (!CanPlaceItemAt(itemA, indexB)) return false;
                if (!CanPlaceItemAt(itemTarget, fromIndex)) return false;

                ClearItemAtAnchor(fromIndex, itemA);
                ClearItemAtAnchor(indexB, itemTarget);
                PlaceItemAtAnchor(indexB, itemA);
                PlaceItemAtAnchor(fromIndex, itemTarget);
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        public InventorySaveData GetSaveData()
        {
            var data = new InventorySaveData();

            // 1. Сохраняем рюкзак (только по якорю каждого предмета, чтобы не дублировать многоклеточные)
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] != null && Items[i].Data != null)
                {
                    GetItemAt(i, out int anchor);
                    if (anchor == i)
                        data.Items.Add(Items[i].GetSaveData(i));
                }
            }

            // 2. Сохраняем экипировку (индексы будут 100, 101 и т.д.)
            for (int i = 0; i < EquipmentItems.Length; i++)
            {
                if (EquipmentItems[i] != null && EquipmentItems[i].Data != null)
                {
                    data.Items.Add(EquipmentItems[i].GetSaveData(EQUIP_OFFSET + i));
                }
            }

            // 3. Слот крафта и сферы
            if (CraftingSlotItem != null && CraftingSlotItem.Data != null)
                data.CraftingSlotItem = CraftingSlotItem.GetSaveData(CRAFT_SLOT_INDEX);
            data.OrbCounts = new List<OrbCountEntry>(_orbCounts);

            return data;
        }

        public void LoadState(InventorySaveData data, ItemDatabaseSO itemDB)
        {
            // 1. Очистка текущего состояния
            // Сначала снимаем все вещи, чтобы статы откатились
            for (int i = 0; i < EquipmentItems.Length; i++)
            {
                if (EquipmentItems[i] != null)
                {
                    OnItemUnequipped?.Invoke(EquipmentItems[i]);
                    EquipmentItems[i] = null;
                }
            }
            // Чистим рюкзак
            Array.Clear(Items, 0, Items.Length);
            CraftingSlotItem = null;
            _orbCounts.Clear();
            if (data.OrbCounts != null) _orbCounts.AddRange(data.OrbCounts);

            // 2. Восстановление
            foreach (var itemData in data.Items)
            {
                InventoryItem newItem = InventoryItem.LoadFromSave(itemData, itemDB);
                if (newItem == null) continue;

                if (itemData.SlotIndex >= EQUIP_OFFSET)
                {
                    // Это экипировка
                    int equipIndex = itemData.SlotIndex - EQUIP_OFFSET;
                    if (equipIndex < EquipmentItems.Length)
                    {
                        EquipmentItems[equipIndex] = newItem;
                        // ВАЖНО: Уведомляем PlayerStats, что вещь надета
                        OnItemEquipped?.Invoke(newItem); 
                    }
                }
                else
                {
                    // Это рюкзак (SlotIndex = якорь)
                    if (itemData.SlotIndex >= 0 && itemData.SlotIndex < Items.Length)
                        PlaceItemAtAnchor(itemData.SlotIndex, newItem);
                }
            }

            // 3. Слот крафта
            if (data.CraftingSlotItem != null)
            {
                var craftItem = InventoryItem.LoadFromSave(data.CraftingSlotItem, itemDB);
                if (craftItem != null) CraftingSlotItem = craftItem;
            }

            TriggerUIUpdate();
        }

        public bool CanPlaceItemAt(InventoryItem item, int targetIndex)
        {
            if (item == null || item.Data == null) return false;
            if (targetIndex == CRAFT_SLOT_INDEX) return true;
            if (targetIndex >= EQUIP_OFFSET) return true;
            
            int w = item.Data.Width;
            int h = item.Data.Height;
            int row = targetIndex / _cols;
            int col = targetIndex % _cols;

            if (col + w > _cols) return false; 
            if (targetIndex + (h - 1) * _cols >= Items.Length) return false; 

            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    int checkIndex = targetIndex + (r * _cols) + c;
                    if (IsSlotOccupied(checkIndex, out var occupier))
                    {
                        if (occupier != item) return false;
                    }
                }
            }
            return true;
        }

        public bool IsSlotOccupied(int slotIndex, out InventoryItem occupier)
        {
            occupier = null;
            if (slotIndex == CRAFT_SLOT_INDEX) { occupier = CraftingSlotItem; return CraftingSlotItem != null; }
            if (slotIndex < 0 || slotIndex >= Items.Length) return true;
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] != null && IsItemCoveringSlot(i, Items[i], slotIndex))
                {
                    occupier = Items[i];
                    return true;
                }
            }
            return false;
        }

        private bool IsItemCoveringSlot(int anchorIndex, InventoryItem item, int targetSlot)
        {
            if (item == null || item.Data == null) 
            {
                return false; 
            }

            int startRow = anchorIndex / _cols;
            int startCol = anchorIndex % _cols;
            int targetRow = targetSlot / _cols;
            int targetCol = targetSlot % _cols;
            return targetRow >= startRow && targetRow < startRow + item.Data.Height &&
                   targetCol >= startCol && targetCol < startCol + item.Data.Width;
        }

        public InventoryItem GetItemAt(int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            if (slotIndex == CRAFT_SLOT_INDEX) { anchorIndex = CRAFT_SLOT_INDEX; return CraftingSlotItem; }
            if (slotIndex >= EQUIP_OFFSET) { anchorIndex = slotIndex; return GetItem(slotIndex); }
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] != null && IsItemCoveringSlot(i, Items[i], slotIndex))
                {
                    anchorIndex = i;
                    return Items[i];
                }
            }
            return null;
        }
    }
}