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

        // --- НОВОЕ: ЭКИПИРОВКА ---
        // Индексы: 0=Head, 1=Body, 2=Main, 3=Off, 4=Gloves, 5=Boots
        public InventoryItem[] EquipmentItems = new InventoryItem[6];
        public const int EQUIP_OFFSET = 100; // Индексы экипировки начинаются с 100

        public InventoryItem[] Items; // Рюкзак

        public event Action OnInventoryChanged;

        private void Awake()
        {
            if (Instance != null) Destroy(gameObject);
            else Instance = this;

            if (Items == null || Items.Length != _capacity)
                Items = new InventoryItem[_capacity];
            
            if (EquipmentItems == null || EquipmentItems.Length != 6)
                EquipmentItems = new InventoryItem[6];
        }

        // --- УНИВЕРСАЛЬНЫЙ GETTER ---
        public InventoryItem GetItem(int index)
        {
            // Если это слот экипировки (100+)
            if (index >= EQUIP_OFFSET)
            {
                int localIndex = index - EQUIP_OFFSET;
                if (localIndex < 0 || localIndex >= EquipmentItems.Length) return null;
                return EquipmentItems[localIndex];
            }

            // Если это рюкзак
            if (index < 0 || index >= Items.Length) return null;
            if (Items[index] != null && Items[index].Data == null) return null;
            return Items[index];
        }

        // --- ГЛАВНАЯ ЛОГИКА ПЕРЕМЕЩЕНИЯ ---
        public bool TryMoveOrSwap(int fromIndex, int toIndex)
        {
            InventoryItem itemFrom = GetItem(fromIndex);
            if (itemFrom == null) return false;

            // 1. Если цель - ЭКИПИРОВКА (>= 100)
            if (toIndex >= EQUIP_OFFSET)
            {
                return TryEquipItem(fromIndex, toIndex, itemFrom);
            }

            // 2. Если цель - РЮКЗАК (< 100)
            // А откуда тащим?
            if (fromIndex >= EQUIP_OFFSET)
            {
                // Тащим ИЗ экипировки В рюкзак (Снять)
                return TryUnequipItem(fromIndex, toIndex, itemFrom);
            }
            else
            {
                // Тащим ИЗ рюкзака В рюкзак (Обычный тетрис)
                return HandleBackpackMove(fromIndex, toIndex, itemFrom);
            }
        }

        // Логика надевания (Backpack -> Equipment)
        private bool TryEquipItem(int fromIndex, int equipGlobalIndex, InventoryItem itemToEquip)
        {
            int localEquipIndex = equipGlobalIndex - EQUIP_OFFSET;
            InventoryItem currentEquipped = EquipmentItems[localEquipIndex];

            // 1. Проверка типа слота
            EquipmentSlot targetSlotType = (EquipmentSlot)localEquipIndex;
            if (itemToEquip.Data.Slot != targetSlotType)
            {
                Debug.Log($"Wrong Slot! Item is {itemToEquip.Data.Slot}, Target is {targetSlotType}");
                return false;
            }

            // 2. СВАП
            Items[fromIndex] = null; // Освобождаем место в рюкзаке

            // Если в слоте экипировки УЖЕ что-то есть
            if (currentEquipped != null && currentEquipped.Data != null)
            {
                // Проверяем, влезет ли старая вещь в рюкзак
                if (CanPlaceItemAt(currentEquipped, fromIndex))
                {
                    Items[fromIndex] = currentEquipped;
                }
                else
                {
                    // Не влезло -> откат
                    Items[fromIndex] = itemToEquip; 
                    return false; 
                }
            }

            // Надеваем
            EquipmentItems[localEquipIndex] = itemToEquip;
            OnInventoryChanged?.Invoke();
            return true;
        }

        // Логика снятия (Equipment -> Backpack)
        private bool TryUnequipItem(int equipGlobalIndex, int backpackIndex, InventoryItem itemToUnequip)
        {
            // Здесь логика простая: проверяем, влезает ли предмет в рюкзак по targetIndex
            
            // 1. Смотрим, занят ли слот в рюкзаке
            InventoryItem itemInBackpack = GetItemAt(backpackIndex, out int anchorIndex);

            // Если в рюкзаке пусто -> Просто кладем
            if (itemInBackpack == null)
            {
                if (CanPlaceItemAt(itemToUnequip, backpackIndex))
                {
                    EquipmentItems[equipGlobalIndex - EQUIP_OFFSET] = null;
                    Items[backpackIndex] = itemToUnequip;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
                return false;
            }

            // Если в рюкзаке ЗАНЯТО -> Пытаемся СВАПНУТЬ (надеть то, что в рюкзаке)
            // Вызываем логику надевания, но наоборот
            return TryEquipItem(backpackIndex, equipGlobalIndex, itemInBackpack);
        }

        // Старая логика перемещения внутри рюкзака (вынесена в метод)
        private bool HandleBackpackMove(int fromIndex, int toIndex, InventoryItem itemA)
        {
            InventoryItem itemB = GetItemAt(toIndex, out int indexB);

            if (itemB == null)
            {
                // Move
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
                // Swap
                if (fromIndex == indexB) return false;
                InventoryItem itemTarget = Items[indexB];
                
                Items[fromIndex] = null;
                Items[indexB] = null;

                if (CanPlaceItemAt(itemA, indexB) && CanPlaceItemAt(itemTarget, fromIndex))
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

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (Без изменений) ---
        public bool AddItem(InventoryItem newItem)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (CanPlaceItemAt(newItem, i))
                {
                    Items[i] = newItem;
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        public bool CanPlaceItemAt(InventoryItem item, int targetIndex)
        {
            // --- FIX START: Если предмета нет, мы не можем проверить, влезает ли он ---
            if (item == null || item.Data == null) return false;
            // --- FIX END ---

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
                    if (IsSlotOccupied(checkIndex, out _)) return false;
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
                if (Items[i] != null && Items[i].Data != null)
                {
                    if (IsItemCoveringSlot(i, Items[i], slotIndex))
                    {
                        occupier = Items[i];
                        return true;
                    }
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
            int endRow = startRow + item.Data.Height - 1;
            int endCol = startCol + item.Data.Width - 1;

            return targetRow >= startRow && targetRow <= endRow && targetCol >= startCol && targetCol <= endCol;
        }

        public InventoryItem GetItemAt(int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            // Если это экипировка
            if (slotIndex >= EQUIP_OFFSET)
            {
                anchorIndex = slotIndex;
                return GetItem(slotIndex);
            }

            // Если рюкзак
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] != null && Items[i].Data != null)
                {
                    if (IsItemCoveringSlot(i, Items[i], slotIndex))
                    {
                        anchorIndex = i;
                        return Items[i];
                    }
                }
            }
            return null;
        }
    }
}