using UnityEngine;
using System;
using Scripts.Items;

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

        public InventoryItem[] Items; 

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
        }

        public void TriggerUIUpdate() => OnInventoryChanged?.Invoke();

        public InventoryItem GetItem(int index)
        {
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
                    Items[i] = newItem;
                    TriggerUIUpdate();
                    return true;
                }
            }
            return false;
        }

        public bool TryMoveOrSwap(int fromIndex, int toIndex)
        {
            InventoryItem itemFrom = GetItem(fromIndex);
            if (itemFrom == null) 
            {
                Debug.LogError($"[InventoryManager] Error: Item at {fromIndex} is null!");
                return false;
            }

            bool success = false;
            
            if (toIndex >= EQUIP_OFFSET) 
            {
                success = TryEquipItem(fromIndex, toIndex, itemFrom);
            }
            else if (fromIndex >= EQUIP_OFFSET) 
            {
                success = TryUnequipItem(fromIndex, toIndex, itemFrom);
            }
            else 
            {
                success = HandleBackpackMove(fromIndex, toIndex, itemFrom);
            }
            
            if (success) TriggerUIUpdate();
            return success;
        }

        private bool TryEquipItem(int fromIndex, int equipGlobalIndex, InventoryItem itemToEquip)
        {
            int localEquipIndex = equipGlobalIndex - EQUIP_OFFSET;
            InventoryItem currentEquipped = EquipmentItems[localEquipIndex];
            
            // 1. ПРОВЕРКА ТИПА СЛОТА
            int requiredSlotType = localEquipIndex; 
            int itemSlotType = (int)itemToEquip.Data.Slot;

            if (itemSlotType != requiredSlotType)
            {
                // Оставляем Warning, чтобы знать, почему предмет не наделся
                Debug.LogWarning($"[Inventory] Wrong slot type. Item: {itemSlotType}, Slot: {requiredSlotType}");
                return false;
            }

            // 2. УДАЛЕНИЕ ИЗ РЮКЗАКА
            Items[fromIndex] = null; 

            // 3. ОБРАБОТКА СВАПА
            if (currentEquipped != null && currentEquipped.Data != null)
            {
                if (CanPlaceItemAt(currentEquipped, fromIndex))
                {
                    Items[fromIndex] = currentEquipped;
                    OnItemUnequipped?.Invoke(currentEquipped);
                }
                else
                {
                    Debug.LogWarning("[Inventory] Swap failed (no space). Reverting.");
                    Items[fromIndex] = itemToEquip; // Возврат
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
            InventoryItem itemInBackpack = GetItemAt(backpackIndex, out int anchorIndex);

            // Если в целевом слоте рюкзака пусто
            if (itemInBackpack == null)
            {
                if (CanPlaceItemAt(itemToUnequip, backpackIndex))
                {
                    EquipmentItems[equipGlobalIndex - EQUIP_OFFSET] = null;
                    Items[backpackIndex] = itemToUnequip;
                    OnItemUnequipped?.Invoke(itemToUnequip);
                    return true;
                }
                return false;
            }

            // Если там что-то есть - пробуем свапнуть
            return TryEquipItem(backpackIndex, equipGlobalIndex, itemInBackpack);
        }

        private bool HandleBackpackMove(int fromIndex, int toIndex, InventoryItem itemA)
        {
            InventoryItem itemB = GetItemAt(toIndex, out int indexB);

            if (itemB == null) 
            {
                Items[fromIndex] = null;
                if (CanPlaceItemAt(itemA, toIndex))
                {
                    Items[toIndex] = itemA;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                Items[fromIndex] = itemA;
                return false;
            }
            else 
            {
                if (fromIndex == indexB) return false;
                InventoryItem itemTarget = Items[indexB];
                
                Items[fromIndex] = null;
                Items[indexB] = null;

                bool aFitsB = CanPlaceItemAt(itemA, indexB);
                bool bFitsA = CanPlaceItemAt(itemTarget, fromIndex);

                if (aFitsB && bFitsA)
                {
                    Items[indexB] = itemA;
                    Items[fromIndex] = itemTarget;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                
                Items[fromIndex] = itemA;
                Items[indexB] = itemTarget;
                return false;
            }
        }

         public InventorySaveData GetSaveData()
        {
            var data = new InventorySaveData();

            // 1. Сохраняем рюкзак
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] != null && Items[i].Data != null)
                {
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
                    // Это рюкзак
                    if (itemData.SlotIndex < Items.Length)
                    {
                        Items[itemData.SlotIndex] = newItem;
                    }
                }
            }

            TriggerUIUpdate();
        }

        public bool CanPlaceItemAt(InventoryItem item, int targetIndex)
        {
            if (item == null || item.Data == null) return false; 
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