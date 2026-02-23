using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Saving;

namespace Scripts.Inventory
{
    /// <summary>
    /// РРЅРІРµРЅС‚Р°СЂСЊ РІ СЃС‚РёР»Рµ PoE: СЂСЋРєР·Р°Рє вЂ” РѕРґРЅР° СЃРµС‚РєР° (GridContainer), РѕС‚РґРµР»СЊРЅРѕ СЃР»РѕС‚ РєСЂР°С„С‚Р° Рё СЌРєРёРїРёСЂРѕРІРєР°.
    /// РџСЂРµРґРјРµС‚ РІ СЂСѓРєРµ (carried) РЅРµ С…СЂР°РЅРёС‚СЃСЏ РІ РєРѕРЅС‚РµР№РЅРµСЂРµ вЂ” С‚РѕР»СЊРєРѕ РІ UI РїСЂРё РїРµСЂРµС‚Р°СЃРєРёРІР°РЅРёРё.
    /// </summary>
    public partial class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private int _capacity = 40;
        [SerializeField] private int _cols = 10;

        private int _rows => (_capacity > 0 && _cols > 0) ? Mathf.Max(1, _capacity / _cols) : 4;

        // Р СЋРєР·Р°Рє вЂ” РµРґРёРЅСЃС‚РІРµРЅРЅС‹Р№ РёСЃС‚РѕС‡РЅРёРє РїСЂР°РІРґС‹ РґР»СЏ СЃРµС‚РєРё; Items вЂ” РєРѕРїРёСЏ РґР»СЏ СЃРѕРІРјРµСЃС‚РёРјРѕСЃС‚Рё СЃ UI/СЃРµР№РІРѕРј
        private GridContainer _backpack;
        public InventoryItem[] Items;

        // РРќР”Р•РљРЎР«: 0=Head, 1=Body, 2=Main, 3=Off, 4=Gloves, 5=Boots
        public InventoryItem[] EquipmentItems = new InventoryItem[6];
        public const int EQUIP_OFFSET = 100;

        /// <summary>РћРґРёРЅ СЃР»РѕС‚ РєСЂР°С„С‚Р° (РїСЂРµРґРјРµС‚ СЃРІРµСЂС…Сѓ РІ СЂРµР¶РёРјРµ РєСЂР°С„С‚Р°). РЎРѕС…СЂР°РЅСЏРµС‚СЃСЏ РєР°Рє РёРЅРІРµРЅС‚Р°СЂСЊ.</summary>
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

        /// <summary>Р Р°Р·РјРµСЂ РїСЂРµРґРјРµС‚Р° РґР»СЏ СЃРµС‚РєРё СЂСЋРєР·Р°РєР°.</summary>
        public void GetBackpackItemSize(InventoryItem item, out int w, out int h)
        {
            GridContainer.GetItemSize(item, _cols, _rows, out w, out h);
        }

        /// <summary>РЈРЅРёРєР°Р»СЊРЅС‹Рµ РїСЂРµРґРјРµС‚С‹ РІ РѕР±Р»Р°СЃС‚Рё СЂСЋРєР·Р°РєР° (rootIndex + СЂР°Р·РјРµСЂ item). Р”Р»СЏ Swap-if-One: РµСЃР»Рё Count==1 вЂ” СЃРІРѕРї, РµСЃР»Рё >1 вЂ” Р±Р»РѕРє.</summary>
        public HashSet<InventoryItem> GetUniqueItemsInBackpackArea(InventoryItem item, int rootIndex)
        {
            return _backpack != null ? _backpack.GetUniqueItemsInAreaAtRoot(item, rootIndex) : new HashSet<InventoryItem>();
        }

        public void TriggerUIUpdate() => OnInventoryChanged?.Invoke();

        /// <summary>Р’С‹Р·РІР°С‚СЊ СЃРѕР±С‹С‚РёРµ СЌРєРёРїРёСЂРѕРІРєРё (РґР»СЏ РІРЅРµС€РЅРµРіРѕ РєРѕРґР°, РЅР°РїСЂРёРјРµСЂ StashManager).</summary>
        public void NotifyItemEquipped(InventoryItem item) => OnItemEquipped?.Invoke(item);

        /// <summary>Р’С‹Р·РІР°С‚СЊ СЃРѕР±С‹С‚РёРµ СЃРЅСЏС‚РёСЏ (РґР»СЏ РІРЅРµС€РЅРµРіРѕ РєРѕРґР°).</summary>
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

        /// <summary>Р—Р°С‰РёС‚РЅР°СЏ РјРµС…Р°РЅРёРєР°: РµСЃР»Рё РїСЂРµРґРјРµС‚ РјРѕРі Р±С‹ Р±С‹С‚СЊ СЂР°Р·СЂСѓС€РµРЅ (РЅРµРєСѓРґР° РїРѕР»РѕР¶РёС‚СЊ), РґРѕР±Р°РІР»СЏРµРј РІ СЂСЋРєР·Р°Рє РёР»Рё РІ СЃРєР»Р°Рґ Рё РїРёС€РµРј РІ Р»РѕРі.</summary>
        /// <returns>true, РµСЃР»Рё РїСЂРµРґРјРµС‚ СѓРґР°Р»РѕСЃСЊ РєСѓРґР°-С‚Рѕ РїРѕР»РѕР¶РёС‚СЊ.</returns>
        public bool RecoverItemToInventory(InventoryItem item)
        {
            if (item == null || item.Data == null) return false;
            bool added = AddItem(item);
            if (added)
            {
                Debug.LogWarning($"[InventoryManager] Р—Р°С‰РёС‚РЅРѕРµ РІРѕСЃСЃС‚Р°РЅРѕРІР»РµРЅРёРµ: РїСЂРµРґРјРµС‚ \"{item.Data.ItemName}\" (ID: {item.Data.ID}) Р±С‹Р» Р±С‹ РїРѕС‚РµСЂСЏРЅ Рё РґРѕР±Р°РІР»РµРЅ РІ СЂСЋРєР·Р°Рє. РџСЂРѕРІРµСЂСЊС‚Рµ Р»РѕРіРёРєСѓ РґСЂРѕРїР°/СЃРІРѕРїР°.");
                return true;
            }
            if (StashManager.Instance != null && StashManager.Instance.TryAddItemToAnyTab(item))
            {
                Debug.LogWarning($"[InventoryManager] Р—Р°С‰РёС‚РЅРѕРµ РІРѕСЃСЃС‚Р°РЅРѕРІР»РµРЅРёРµ: РїСЂРµРґРјРµС‚ \"{item.Data.ItemName}\" (ID: {item.Data.ID}) РґРѕР±Р°РІР»РµРЅ РІ СЃРєР»Р°Рґ (СЂСЋРєР·Р°Рє Р±С‹Р» РїРѕР»РѕРЅ). РџСЂРѕРІРµСЂСЊС‚Рµ Р»РѕРіРёРєСѓ РґСЂРѕРїР°/СЃРІРѕРїР°.");
                return true;
            }
            Debug.LogError($"[InventoryManager] РќРµ СѓРґР°Р»РѕСЃСЊ РІРѕСЃСЃС‚Р°РЅРѕРІРёС‚СЊ РїСЂРµРґРјРµС‚ \"{item.Data.ItemName}\" (ID: {item.Data.ID}) вЂ” СЂСЋРєР·Р°Рє Рё СЃРєР»Р°Рґ РїРѕР»РЅС‹. РџСЂРµРґРјРµС‚ РїРѕС‚РµСЂСЏРЅ!");
            return false;
        }

        /// <summary>Р—Р°Р±СЂР°С‚СЊ РїСЂРµРґРјРµС‚ РёР· СЃР»РѕС‚Р° (СЂСЋРєР·Р°Рє/СЌРєРёРїРёСЂРѕРІРєР°/РєСЂР°С„С‚). РџСЂРµРґРјРµС‚ Р±РѕР»СЊС€Рµ РЅРµ РІ РєРѕРЅС‚РµР№РЅРµСЂРµ вЂ” В«РІ СЂСѓРєРµВ» Сѓ РІС‹Р·С‹РІР°СЋС‰РµРіРѕ.</summary>
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
    }
}

