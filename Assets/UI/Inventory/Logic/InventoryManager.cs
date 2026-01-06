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

        // --- ЭКИПИРОВКА ---
        public InventoryItem[] EquipmentItems = new InventoryItem[6];
        public const int EQUIP_OFFSET = 100; 

        public InventoryItem[] Items; // Рюкзак

        public event Action OnInventoryChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) 
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (Items == null || Items.Length != _capacity)
                Items = new InventoryItem[_capacity];
            
            if (EquipmentItems == null || EquipmentItems.Length != 6)
                EquipmentItems = new InventoryItem[6];
        }

        // Публичный метод для принудительного обновления UI (нужен дебаггеру)
        public void TriggerUIUpdate()
        {
            OnInventoryChanged?.Invoke();
        }

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
                    // ВАЖНО: Уведомляем подписчиков сразу
                    TriggerUIUpdate(); 
                    return true;
                }
            }
            return false;
        }

        public bool TryMoveOrSwap(int fromIndex, int toIndex)
        {
            InventoryItem itemFrom = GetItem(fromIndex);
            if (itemFrom == null) return false;

            bool success = false;
            // Логика свапа в зависимости от источника и назначения
            if (toIndex >= EQUIP_OFFSET) success = TryEquipItem(fromIndex, toIndex, itemFrom);
            else if (fromIndex >= EQUIP_OFFSET) success = TryUnequipItem(fromIndex, toIndex, itemFrom);
            else success = HandleBackpackMove(fromIndex, toIndex, itemFrom);

            if (success) TriggerUIUpdate();
            return success;
        }

        // --- Внутренняя логика перемещения (оставляем как было, сокращено для краткости) ---
        private bool TryEquipItem(int fromIndex, int equipGlobalIndex, InventoryItem itemToEquip)
        {
            int localEquipIndex = equipGlobalIndex - EQUIP_OFFSET;
            InventoryItem currentEquipped = EquipmentItems[localEquipIndex];
            EquipmentSlot targetSlotType = (EquipmentSlot)localEquipIndex;

            if (itemToEquip.Data.Slot != targetSlotType) return false;

            Items[fromIndex] = null; 

            if (currentEquipped != null)
            {
                if (CanPlaceItemAt(currentEquipped, fromIndex)) Items[fromIndex] = currentEquipped;
                else { Items[fromIndex] = itemToEquip; return false; }
            }

            EquipmentItems[localEquipIndex] = itemToEquip;
            return true;
        }

        private bool TryUnequipItem(int equipGlobalIndex, int backpackIndex, InventoryItem itemToUnequip)
        {
            InventoryItem itemInBackpack = GetItemAt(backpackIndex, out int anchorIndex);
            if (itemInBackpack == null)
            {
                if (CanPlaceItemAt(itemToUnequip, backpackIndex))
                {
                    EquipmentItems[equipGlobalIndex - EQUIP_OFFSET] = null;
                    Items[backpackIndex] = itemToUnequip;
                    return true;
                }
                return false;
            }
            return TryEquipItem(backpackIndex, equipGlobalIndex, itemInBackpack);
        }

        private bool HandleBackpackMove(int fromIndex, int toIndex, InventoryItem itemA)
        {
            // Смотрим, что лежит в целевом слоте (и получаем его "родной" индекс-якорь)
            InventoryItem itemB = GetItemAt(toIndex, out int indexB);

            // Сценарий 1: В целевом слоте ПУСТО
            if (itemB == null)
            {
                // Удаляем со старого места
                Items[fromIndex] = null;
                
                // Проверяем, влезает ли на новое
                if (CanPlaceItemAt(itemA, toIndex))
                {
                    Items[toIndex] = itemA;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                
                // Откат (не влезло)
                Items[fromIndex] = itemA;
                return false;
            }
            // Сценарий 2: СВАП (В целевом слоте что-то есть)
            else
            {
                // Нельзя свапать предмет сам с собой
                if (fromIndex == indexB) return false;

                InventoryItem itemTarget = Items[indexB]; // Это itemB
                
                // --- НАЧАЛО ТРАНЗАКЦИИ ---
                // 1. Временно удаляем ОБА предмета из сетки, 
                // чтобы они не мешали проверкам (Collision Check)
                Items[fromIndex] = null;
                Items[indexB] = null;

                // 2. Проверяем перекрестную совместимость:
                // - Влезет ли Перетаскиваемый (A) на место Целевого (indexB)?
                // - Влезет ли Целевой (Target) на место Старого (fromIndex)?
                bool aFitsB = CanPlaceItemAt(itemA, indexB);
                bool bFitsA = CanPlaceItemAt(itemTarget, fromIndex);

                if (aFitsB && bFitsA)
                {
                    // Успех! Фиксируем новые позиции
                    Items[indexB] = itemA;
                    Items[fromIndex] = itemTarget;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                else
                {
                    // Провал! Откат (Rollback) на исходные позиции
                    Items[fromIndex] = itemA;
                    Items[indexB] = itemTarget;
                    
                    Debug.LogWarning($"Swap failed: A fits B? {aFitsB}, B fits A? {bFitsA}");
                    return false;
                }
            }
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
            if (row + h > (_capacity / _cols)) return false; 

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