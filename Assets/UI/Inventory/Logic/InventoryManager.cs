using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Saving;

namespace Scripts.Inventory
{
    /// <summary>
    /// Инвентарь в стиле PoE: рюкзак — одна сетка (GridContainer), отдельно слот крафта и экипировка.
    /// Предмет в руке (carried) не хранится в контейнере — только в UI при перетаскивании.
    /// </summary>
    public partial class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private int _capacity = 40;
        [SerializeField] private int _cols = 10;

        private int _rows => (_capacity > 0 && _cols > 0) ? Mathf.Max(1, _capacity / _cols) : 4;

        // Рюкзак — единственный источник правды для сетки; Items — копия для совместимости с UI/сейвом
        private GridContainer _backpack;
        public InventoryItem[] Items;

        // ИНДЕКСЫ: 0=Head, 1=Body, 2=Main, 3=Off, 4=Gloves, 5=Boots
        public InventoryItem[] EquipmentItems = new InventoryItem[6];
        public const int EQUIP_OFFSET = 100;

        /// <summary>Один слот крафта (предмет сверху в режиме крафта). Сохраняется как инвентарь.</summary>
        public const int CRAFT_SLOT_INDEX = 200;
        public InventoryItem CraftingSlotItem { get; private set; }

        private List<OrbCountEntry> _orbCounts = new List<OrbCountEntry>();

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

            _backpack = new GridContainer(_cols, _rows);
            if (Items == null || Items.Length != _capacity) Items = new InventoryItem[_capacity];
            if (EquipmentItems == null || EquipmentItems.Length != 6) EquipmentItems = new InventoryItem[6];
            if (_orbCounts == null) _orbCounts = new List<OrbCountEntry>();
        }

        /// <summary>Размер предмета для сетки рюкзака.</summary>
        public void GetBackpackItemSize(InventoryItem item, out int w, out int h)
        {
            GridContainer.GetItemSize(item, _cols, _rows, out w, out h);
        }

        /// <summary>Уникальные предметы в области рюкзака (rootIndex + размер item). Для Swap-if-One: если Count==1 — своп, если >1 — блок.</summary>
        public HashSet<InventoryItem> GetUniqueItemsInBackpackArea(InventoryItem item, int rootIndex)
        {
            return _backpack != null ? _backpack.GetUniqueItemsInAreaAtRoot(item, rootIndex) : new HashSet<InventoryItem>();
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

        private void SyncFromBackpack()
        {
            if (_backpack == null || Items == null) return;
            for (int i = 0; i < _backpack.Length && i < Items.Length; i++)
                _backpack.GetItemAt(i, out Items[i], out _);
        }

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
            return _backpack != null ? _backpack.GetItemAt(index) : null;
        }

        public bool AddItem(InventoryItem newItem)
        {
            if (_backpack == null || newItem?.Data == null) return false;
            int root = _backpack.FindFirstEmptyRoot(newItem, -1);
            if (root < 0) return false;
            if (!_backpack.Place(newItem, root)) return false;
            SyncFromBackpack();
            TriggerUIUpdate();
            return true;
        }

        /// <summary>Защитная механика: если предмет мог бы быть разрушен (некуда положить), добавляем в рюкзак или в склад и пишем в лог.</summary>
        /// <returns>true, если предмет удалось куда-то положить.</returns>
        public bool RecoverItemToInventory(InventoryItem item)
        {
            if (item == null || item.Data == null) return false;
            bool added = AddItem(item);
            if (added)
            {
                Debug.LogWarning($"[InventoryManager] Защитное восстановление: предмет \"{item.Data.ItemName}\" (ID: {item.Data.ID}) был бы потерян и добавлен в рюкзак. Проверьте логику дропа/свопа.");
                return true;
            }
            if (StashManager.Instance != null && StashManager.Instance.TryAddItemToAnyTab(item))
            {
                Debug.LogWarning($"[InventoryManager] Защитное восстановление: предмет \"{item.Data.ItemName}\" (ID: {item.Data.ID}) добавлен в склад (рюкзак был полон). Проверьте логику дропа/свопа.");
                return true;
            }
            Debug.LogError($"[InventoryManager] Не удалось восстановить предмет \"{item.Data.ItemName}\" (ID: {item.Data.ID}) — рюкзак и склад полны. Предмет потерян!");
            return false;
        }

        /// <summary>Забрать предмет из слота (рюкзак/экипировка/крафт). Предмет больше не в контейнере — «в руке» у вызывающего.</summary>
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
            _backpack.Take(anchor);
            SyncFromBackpack();
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
                InventoryItem prevCraft = CraftingSlotItem;
                if (prevCraft != null && sourceAnchorForSwap >= 0)
                {
                    int dest = FindSlotForDisplacedItem(prevCraft, -1, -1, 0, 0, sourceAnchorForSwap);
                    if (dest < 0) return false;
                    CraftingSlotItem = item;
                    PlaceItemAtSlotInternal(prevCraft, dest);
                }
                else
                    CraftingSlotItem = item;
                TriggerUIUpdate();
                return true;
            }

            // Экипировка
            if (toIndex >= EQUIP_OFFSET)
            {
                int local = toIndex - EQUIP_OFFSET;
                if (local < 0 || local >= EquipmentItems.Length) return false;
                if ((int)item.Data.Slot != local) return false;
                InventoryItem prevEquip = EquipmentItems[local];
                if (prevEquip != null && sourceAnchorForSwap >= 0)
                {
                    int dest = FindSlotForDisplacedItem(prevEquip, -1, -1, 0, 0, sourceAnchorForSwap);
                    if (dest < 0) return false;
                    EquipmentItems[local] = item;
                    OnItemEquipped?.Invoke(item);
                    PlaceItemAtSlotInternal(prevEquip, dest);
                }
                else
                {
                    EquipmentItems[local] = item;
                    OnItemEquipped?.Invoke(item);
                }
                TriggerUIUpdate();
                return true;
            }

            // Рюкзак — Swap-if-One: 0 предметов → место, 1 → своп, >1 → блок
            var uniqueInArea = GetUniqueItemsInBackpackArea(item, toIndex);
            if (uniqueInArea.Count > 1) return false;
            if (uniqueInArea.Count == 0)
            {
                if (!_backpack.CanPlace(item, toIndex)) return false;
                if (!_backpack.Place(item, toIndex)) return false;
                SyncFromBackpack();
                TriggerUIUpdate();
                return true;
            }
            InventoryItem other = null;
            foreach (var x in uniqueInArea) { other = x; break; }
            if (other == null || other == item)
            {
                if (!_backpack.CanPlace(item, toIndex)) return false;
                if (!_backpack.Place(item, toIndex)) return false;
                SyncFromBackpack();
                TriggerUIUpdate();
                return true;
            }
            // PoE-style: сначала полностью очищаем все ячейки вытесняемого (Take), затем вписываем новый — иначе возможна потеря предмета
            _backpack.GetItemAt(toIndex, out _, out int otherRoot);
            InventoryItem displaced = _backpack.Take(otherRoot);
            if (displaced == null) return false;
            if (!_backpack.Place(item, toIndex))
            {
                _backpack.Place(displaced, otherRoot);
                SyncFromBackpack();
                return false;
            }
            int destRoot = (sourceAnchorForSwap >= 0 && _backpack.CanPlace(displaced, sourceAnchorForSwap))
                ? sourceAnchorForSwap
                : _backpack.FindFirstEmptyRoot(displaced, -1);
            if (destRoot < 0 || !_backpack.Place(displaced, destRoot))
            {
                _backpack.Take(toIndex);
                _backpack.Place(displaced, otherRoot);
                SyncFromBackpack();
                return false;
            }
            SyncFromBackpack();
            TriggerUIUpdate();
            return true;
        }

        /// <summary>Проверяет, пересекаются ли два прямоугольника в сетке рюкзака (по якорю и размеру).</summary>
        private bool GridRectsOverlap(int anchor1, int w1, int h1, int anchor2, int w2, int h2)
        {
            int r1 = anchor1 / _cols, c1 = anchor1 % _cols;
            int r2 = anchor2 / _cols, c2 = anchor2 % _cols;
            return r1 < r2 + h2 && r2 < r1 + h1 && c1 < c2 + w2 && c2 < c1 + w1;
        }

        /// <summary>Найти якорь для вытесненного предмета: не пересекаться с областью exclude, предпочитать preferredAnchor.</summary>
        private int FindSlotForDisplacedItem(InventoryItem displaced, int displacedCurrentAnchor, int excludeAnchor, int excludeW, int excludeH, int preferredAnchor)
        {
            if (displaced?.Data == null || _backpack == null) return -1;
            GetBackpackItemSize(displaced, out int w, out int h);
            bool hasExclude = excludeAnchor >= 0;
            if (preferredAnchor >= 0 && preferredAnchor < _backpack.Length)
            {
                if ((!hasExclude || !GridRectsOverlap(preferredAnchor, w, h, excludeAnchor, excludeW, excludeH)) && _backpack.CanPlace(displaced, preferredAnchor))
                    return preferredAnchor;
            }
            for (int row = 0; row <= _rows - h; row++)
            {
                for (int col = 0; col <= _cols - w; col++)
                {
                    int anchor = row * _cols + col;
                    if (hasExclude && GridRectsOverlap(anchor, w, h, excludeAnchor, excludeW, excludeH)) continue;
                    if (_backpack.CanPlace(displaced, anchor)) return anchor;
                }
            }
            return -1;
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
            if (item?.Data == null || _backpack == null || anchorIndex < 0 || anchorIndex >= _backpack.Length) return;
            _backpack.Remove(item);
            SyncFromBackpack();
        }

        private void PlaceItemAtAnchor(int anchorIndex, InventoryItem item)
        {
            if (item?.Data == null || _backpack == null || anchorIndex < 0 || anchorIndex >= _backpack.Length) return;
            _backpack.Place(item, anchorIndex);
            SyncFromBackpack();
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

        /// <summary>Подсчитать уникальные предметы в области рюкзака (targetIndex + размер item).</summary>
        private int CountUniqueItemsInArea(int targetIndex, InventoryItem item)
        {
            return GetUniqueItemsInBackpackArea(item, targetIndex).Count;
        }

        /// <summary>Получить единственный предмет в области (если он один) и его якорь.</summary>
        private bool GetSingleItemInArea(int targetIndex, InventoryItem item, out InventoryItem foundItem, out int foundAnchor)
        {
            foundItem = null;
            foundAnchor = -1;
            var unique = GetUniqueItemsInBackpackArea(item, targetIndex);
            if (unique.Count != 1) return false;
            foreach (var u in unique)
            {
                foundItem = u;
                _backpack.GetItemAt(targetIndex, out _, out foundAnchor);
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

            // 1. Рюкзак: по одному сохранению на предмет (по корню)
            if (_backpack != null)
            {
                for (int i = 0; i < _backpack.Length; i++)
                {
                    _backpack.GetItemAt(i, out InventoryItem item, out int root);
                    if (item != null && item.Data != null && root == i)
                        data.Items.Add(item.GetSaveData(i));
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
            // Чистим рюкзак (удаляем все предметы по корням)
            if (_backpack != null)
            {
                var toRemove = new List<InventoryItem>();
                for (int i = 0; i < _backpack.Length; i++)
                {
                    _backpack.GetItemAt(i, out InventoryItem it, out int root);
                    if (it != null && root == i) toRemove.Add(it);
                }
                foreach (var it in toRemove) _backpack.Remove(it);
            }
            SyncFromBackpack();
            CraftingSlotItem = null;
            _orbCounts.Clear();
            if (data.OrbCounts != null) _orbCounts.AddRange(data.OrbCounts);

            // 2. Восстановление (рюкзак: по якорю)
            var claimedBackpack = new HashSet<int>();
            foreach (var itemData in data.Items)
            {
                InventoryItem newItem = InventoryItem.LoadFromSave(itemData, itemDB);
                if (newItem == null) continue;

                if (itemData.SlotIndex >= EQUIP_OFFSET)
                {
                    int equipIndex = itemData.SlotIndex - EQUIP_OFFSET;
                    if (equipIndex < EquipmentItems.Length)
                    {
                        EquipmentItems[equipIndex] = newItem;
                        OnItemEquipped?.Invoke(newItem);
                    }
                    continue;
                }
                if (itemData.SlotIndex == CRAFT_SLOT_INDEX) continue;

                int anchor = itemData.SlotIndex;
                if (anchor < 0 || anchor >= (_backpack?.Length ?? 0)) continue;
                GetBackpackItemSize(newItem, out int w, out int h);
                bool anyClaimed = false;
                for (int r = 0; r < h && !anyClaimed; r++)
                    for (int c = 0; c < w; c++)
                        if (claimedBackpack.Contains(anchor + r * _cols + c)) { anyClaimed = true; break; }
                if (anyClaimed) continue;
                _backpack.Place(newItem, anchor);
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++)
                        claimedBackpack.Add(anchor + r * _cols + c);
            }
            SyncFromBackpack();

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
            return _backpack != null && _backpack.CanPlace(item, targetIndex);
        }

        public bool IsSlotOccupied(int slotIndex, out InventoryItem occupier)
        {
            occupier = null;
            if (slotIndex == CRAFT_SLOT_INDEX) { occupier = CraftingSlotItem; return CraftingSlotItem != null; }
            if (slotIndex >= EQUIP_OFFSET)
            {
                int local = slotIndex - EQUIP_OFFSET;
                if (local >= 0 && local < EquipmentItems.Length) { occupier = EquipmentItems[local]; return occupier != null; }
                return false;
            }
            if (slotIndex < 0 || slotIndex >= (_backpack?.Length ?? 0)) return false;
            occupier = _backpack.GetItemAt(slotIndex);
            return occupier != null;
        }

        private bool IsItemCoveringSlot(int anchorIndex, InventoryItem item, int targetSlot)
        {
            if (item == null || item.Data == null) return false;
            GetBackpackItemSize(item, out int w, out int h);
            int startRow = anchorIndex / _cols;
            int startCol = anchorIndex % _cols;
            int targetRow = targetSlot / _cols;
            int targetCol = targetSlot % _cols;
            return targetRow >= startRow && targetRow < startRow + h && targetCol >= startCol && targetCol < startCol + w;
        }

        public InventoryItem GetItemAt(int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            if (slotIndex == CRAFT_SLOT_INDEX) { anchorIndex = CRAFT_SLOT_INDEX; return CraftingSlotItem; }
            if (slotIndex >= EQUIP_OFFSET) { anchorIndex = slotIndex; return GetItem(slotIndex); }
            if (_backpack == null || slotIndex < 0 || slotIndex >= _backpack.Length) return null;
            _backpack.GetItemAt(slotIndex, out InventoryItem backpackItem, out anchorIndex);
            return backpackItem;
        }

        /// <summary>Количество слотов рюкзака (для итерации в UI).</summary>
        public int BackpackSlotCount => _backpack?.Length ?? 0;

        /// <summary>Первый свободный корень в рюкзаке для item. -1 если нет места.</summary>
        public int FindFreeBackpackAnchor(InventoryItem item, int excludeAnchor = -1)
        {
            return _backpack?.FindFirstEmptyRoot(item, excludeAnchor) ?? -1;
        }

        /// <summary>Снять предмет с экипировки в первый свободный слот рюкзака. Для дебаггера/очистки.</summary>
        public bool UnequipToBackpack(int equipSlotIndex)
        {
            if (equipSlotIndex < 0 || equipSlotIndex >= EquipmentItems.Length) return true;
            InventoryItem item = EquipmentItems[equipSlotIndex];
            if (item == null) return true;
            int root = FindFreeBackpackAnchor(item, -1);
            if (root < 0) return false;
            EquipmentItems[equipSlotIndex] = null;
            OnItemUnequipped?.Invoke(item);
            _backpack.Place(item, root);
            SyncFromBackpack();
            TriggerUIUpdate();
            return true;
        }

        /// <summary>Очистить весь рюкзак (все уникальные предметы удаляются из сетки). Для дебаггера.</summary>
        public void ClearBackpack()
        {
            if (_backpack == null) return;
            var seen = new HashSet<InventoryItem>();
            for (int i = 0; i < _backpack.Length; i++)
            {
                _backpack.GetItemAt(i, out InventoryItem it, out _);
                if (it != null && !seen.Contains(it))
                {
                    seen.Add(it);
                    _backpack.Remove(it);
                }
            }
            SyncFromBackpack();
            TriggerUIUpdate();
        }
    }
}
