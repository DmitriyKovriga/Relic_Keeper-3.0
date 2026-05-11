using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Inventory;

public class DebugInventoryWindowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private StyleSheet _styleSheet;
    [Tooltip("Sorting order при показе окна — выше инвентаря/сундука, чтобы окно было поверх и прокликивалось.")]
    [SerializeField] private float _sortOrderWhenVisible = 1000f;

    [Header("Data")]
    [SerializeField] private string _itemDatabasePath = ProjectPaths.ResourcesItemDatabase;

    private VisualElement _root;
    private Button _itemSelectorButton;
    private ScrollView _itemListPopup;
    private VisualElement _itemSelectorRow;
    private DropdownField _itemTypeDropdown;
    private TextField _itemSearchField;
    private DropdownField _rarityDropdown;
    private IntegerField _levelField;
    private Button _createButton;
    private Button _clearButton;

    private Button _orbSelectorButton;
    private ScrollView _orbListPopup;
    private Button _addOrbButton;
    private List<CraftingOrbSO> _orbList = new List<CraftingOrbSO>();
    private List<string> _orbChoiceNames = new List<string>();
    private int _selectedOrbIndex = 0;

    private ItemDatabaseSO _itemDb;
    private List<EquipmentItemSO> _itemList = new List<EquipmentItemSO>();
    private List<EquipmentItemSO> _filteredItemList = new List<EquipmentItemSO>();
    private int _selectedItemIndex = 0;
    private List<string> _itemChoiceNames = new List<string>();
    private float _sortOrderWhenHidden;
    private float _panelSortOrderWhenHidden;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc == null || _uiDoc.rootVisualElement == null) return;
        UIFontApplier.ApplyToRoot(_uiDoc.rootVisualElement);

        _root = _uiDoc.rootVisualElement.Q<VisualElement>("DebugRoot");
        if (_root == null) return;

        if (_styleSheet != null)
        {
            _root.styleSheets.Add(_styleSheet);
            var panelRoot = _uiDoc.rootVisualElement;
            if (panelRoot != _root && !panelRoot.styleSheets.Contains(_styleSheet))
                panelRoot.styleSheets.Add(_styleSheet);
        }

        _itemSelectorRow = _root.Q<VisualElement>("ItemSelectorRow");
        _itemSelectorButton = _root.Q<Button>("ItemSelectorButton");
        _itemListPopup = _root.Q<ScrollView>("ItemListPopup");
        _itemTypeDropdown = _root.Q<DropdownField>("ItemTypeDropdown");
        _itemSearchField = _root.Q<TextField>("ItemSearchField");
        _rarityDropdown = _root.Q<DropdownField>("RarityDropdown");
        _levelField = _root.Q<IntegerField>("LevelField");
        _createButton = _root.Q<Button>("CreateButton");
        _clearButton = _root.Q<Button>("ClearButton");
        _orbSelectorButton = _root.Q<Button>("OrbSelectorButton");
        _orbListPopup = _root.Q<ScrollView>("OrbListPopup");
        _addOrbButton = _root.Q<Button>("AddOrbButton");

        if (_itemSelectorButton == null || _itemListPopup == null || _rarityDropdown == null || _createButton == null || _clearButton == null)
        {
            Debug.LogError("[DebugInventoryWindow] Required UI elements not found.");
            return;
        }

        LoadItemDatabase();
        BuildItemList();
        BuildItemTypeDropdown();
        BuildRarityDropdown();
        _itemSelectorButton.clicked += OnItemSelectorClick;
        if (_itemTypeDropdown != null) _itemTypeDropdown.RegisterValueChangedCallback(OnItemFilterChanged);
        if (_itemSearchField != null) _itemSearchField.RegisterValueChangedCallback(OnItemFilterChanged);
        if (_levelField != null) _levelField.value = Mathf.Clamp(_levelField.value, 1, 99);

        _createButton.clicked += OnCreateClicked;
        _clearButton.clicked += OnClearClicked;

        if (_orbSelectorButton != null && _orbListPopup != null && _addOrbButton != null)
        {
            BuildOrbList();
            _orbListPopup.style.display = DisplayStyle.None;
            _orbSelectorButton.clicked += OnOrbSelectorClick;
            _addOrbButton.clicked += OnAddOrbClicked;
        }

        _root.style.display = DisplayStyle.None;
        _root.pickingMode = PickingMode.Position;
        if (_uiDoc != null)
        {
            _sortOrderWhenHidden = _uiDoc.sortingOrder;
            if (_uiDoc.panelSettings != null)
                _panelSortOrderWhenHidden = _uiDoc.panelSettings.sortingOrder;
        }
    }

    private VisualElement GetInventoryRoot()
    {
        var inv = GetComponentInParent<InventoryUI>();
        if (inv == null) inv = UnityEngine.Object.FindObjectOfType<InventoryUI>(true);
        return inv != null ? inv.RootVisualElement : null;
    }

    private void OnDisable()
    {
        if (_itemSelectorButton != null) _itemSelectorButton.clicked -= OnItemSelectorClick;
        if (_itemTypeDropdown != null) _itemTypeDropdown.UnregisterValueChangedCallback(OnItemFilterChanged);
        if (_itemSearchField != null) _itemSearchField.UnregisterValueChangedCallback(OnItemFilterChanged);
        if (_createButton != null) _createButton.clicked -= OnCreateClicked;
        if (_clearButton != null) _clearButton.clicked -= OnClearClicked;
        if (_orbSelectorButton != null) _orbSelectorButton.clicked -= OnOrbSelectorClick;
        if (_addOrbButton != null) _addOrbButton.clicked -= OnAddOrbClicked;
    }

    private void LoadItemDatabase()
    {
        _itemDb = Resources.Load<ItemDatabaseSO>(_itemDatabasePath);
        if (_itemDb == null)
        {
            Debug.LogWarning("[DebugInventoryWindow] ItemDatabaseSO not found at " + _itemDatabasePath);
            return;
        }
        if (_itemDb.AllItems == null || _itemDb.AllItems.Count == 0)
            _itemDb.Init();
    }

    private void BuildItemList()
    {
        _itemList.Clear();
        _itemChoiceNames.Clear();
        _itemChoiceNames.Add("(select item)");

        if (_itemDb != null && _itemDb.AllItems != null)
        {
            foreach (var item in _itemDb.AllItems)
            {
                if (item == null) continue;
                _itemList.Add(item);
            }
        }

        _itemList.Sort(CompareItemsForDebugList);
        RefreshFilteredItemList(null);
    }

    private void BuildItemTypeDropdown()
    {
        if (_itemTypeDropdown == null) return;

        _itemTypeDropdown.choices = new List<string>
        {
            "All",
            "Weapons",
            "Armor",
            "Helmet",
            "Body Armor",
            "Main Hand",
            "Off Hand",
            "Gloves",
            "Boots"
        };
        _itemTypeDropdown.value = "All";
    }

    private void OnItemFilterChanged(ChangeEvent<string> _)
    {
        EquipmentItemSO preserve = GetSelectedItem();
        RefreshFilteredItemList(preserve);
    }

    private void RefreshFilteredItemList(EquipmentItemSO preserve)
    {
        _filteredItemList.Clear();
        _itemChoiceNames.Clear();
        _itemChoiceNames.Add("(select item)");

        string typeFilter = _itemTypeDropdown != null ? _itemTypeDropdown.value : "All";
        string search = _itemSearchField != null ? _itemSearchField.value : string.Empty;
        search = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();

        foreach (var item in _itemList)
        {
            if (item == null) continue;
            if (!MatchesTypeFilter(item, typeFilter)) continue;
            if (!MatchesSearch(item, search)) continue;

            _filteredItemList.Add(item);
            _itemChoiceNames.Add(GetItemChoiceLabel(item));
        }

        _selectedItemIndex = 0;
        if (preserve != null)
        {
            int filteredIndex = _filteredItemList.IndexOf(preserve);
            if (filteredIndex >= 0)
                _selectedItemIndex = filteredIndex + 1;
        }

        _itemSelectorButton.text = _itemChoiceNames[0];
        if (_selectedItemIndex > 0 && _selectedItemIndex < _itemChoiceNames.Count)
            _itemSelectorButton.text = _itemChoiceNames[_selectedItemIndex];

        FillItemListPopup();
    }

    private EquipmentItemSO GetSelectedItem()
    {
        int index = _selectedItemIndex - 1;
        return index >= 0 && index < _filteredItemList.Count ? _filteredItemList[index] : null;
    }

    private static bool MatchesTypeFilter(EquipmentItemSO item, string filter)
    {
        if (item == null || string.IsNullOrEmpty(filter) || filter == "All") return true;
        if (filter == "Weapons") return item is WeaponItemSO;
        if (filter == "Armor") return item is ArmorItemSO;
        if (filter == "Body Armor") return item.Slot == EquipmentSlot.BodyArmor;
        if (filter == "Main Hand") return item.Slot == EquipmentSlot.MainHand;
        if (filter == "Off Hand") return item.Slot == EquipmentSlot.OffHand;

        return Enum.TryParse(filter, out EquipmentSlot slot) && item.Slot == slot;
    }

    private static bool MatchesSearch(EquipmentItemSO item, string search)
    {
        if (string.IsNullOrEmpty(search)) return true;

        return ContainsIgnoreCase(item.ItemName, search)
               || ContainsIgnoreCase(item.name, search)
               || ContainsIgnoreCase(item.ID, search)
               || ContainsIgnoreCase(item.Slot.ToString(), search)
               || ContainsIgnoreCase(item.GetType().Name, search);
    }

    private static bool ContainsIgnoreCase(string source, string search)
    {
        return !string.IsNullOrEmpty(source)
               && source.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetItemChoiceLabel(EquipmentItemSO item)
    {
        string name = string.IsNullOrEmpty(item.ItemName) ? item.name : item.ItemName;
        return $"[{GetSlotLabel(item.Slot)}] {name}";
    }

    private static string GetSlotLabel(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.BodyArmor: return "Body";
            case EquipmentSlot.MainHand: return "Main";
            case EquipmentSlot.OffHand: return "Off";
            default: return slot.ToString();
        }
    }

    private static int CompareItemsForDebugList(EquipmentItemSO a, EquipmentItemSO b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int slotCompare = a.Slot.CompareTo(b.Slot);
        if (slotCompare != 0) return slotCompare;

        return string.Compare(GetItemChoiceLabel(a), GetItemChoiceLabel(b), StringComparison.OrdinalIgnoreCase);
    }

    private void FillItemListPopup()
    {
        _itemListPopup.Clear();
        for (int i = 0; i < _itemChoiceNames.Count; i++)
        {
            int idx = i;
            var btn = new Button(() => OnItemListEntryClick(idx)) { text = _itemChoiceNames[i] };
            btn.AddToClassList("debug-item-list-entry");
            _itemListPopup.Add(btn);
        }
    }

    private void OnItemSelectorClick()
    {
        bool visible = _itemListPopup.style.display == DisplayStyle.Flex;
        if (visible)
        {
            _itemListPopup.style.display = DisplayStyle.None;
            return;
        }
        _itemListPopup.style.display = DisplayStyle.Flex;
        _itemListPopup.BringToFront();
        var row = _itemSelectorButton?.parent;
        if (row != null)
        {
            float top = row.layout.y + _itemSelectorButton.layout.y + _itemSelectorButton.layout.height + 2f;
            float left = row.layout.x + _itemSelectorButton.layout.x;
            _itemListPopup.style.top = top;
            _itemListPopup.style.left = left;
        }
    }

    private void OnItemListEntryClick(int choiceIndex)
    {
        _selectedItemIndex = choiceIndex;
        _itemSelectorButton.text = _itemChoiceNames[choiceIndex];
        _itemListPopup.style.display = DisplayStyle.None;
    }

    private void BuildRarityDropdown()
    {
        _rarityDropdown.choices = new List<string> { "Normal", "Magic", "Rare" };
        _rarityDropdown.value = "Magic";
    }

    private void OnCreateClicked()
    {
        if (InventoryManager.Instance == null || ItemGenerator.Instance == null)
        {
            Debug.LogWarning("[DebugInventoryWindow] InventoryManager or ItemGenerator not found.");
            return;
        }

        int index = _selectedItemIndex - 1;
        if (index < 0 || index >= _filteredItemList.Count)
        {
            Debug.LogWarning("[DebugInventoryWindow] Select an item.");
            return;
        }

        EquipmentItemSO baseItem = _filteredItemList[index];
        int rarity = _rarityDropdown.index;
        int level = _levelField != null ? Mathf.Clamp(_levelField.value, 1, 99) : 10;

        InventoryItem newItem = ItemGenerator.Instance.Generate(baseItem, level, rarity);
        bool added = InventoryManager.Instance.AddItem(newItem);
        if (!added) Debug.LogWarning("[DebugInventoryWindow] Inventory full.");
    }

    private void OnClearClicked()
    {
        if (InventoryManager.Instance == null) return;
        InventoryManager.Instance.ClearAllItemsForDebug();
    }

    private void BuildOrbList()
    {
        _orbList.Clear();
        _orbChoiceNames.Clear();
        _orbChoiceNames.Add("(select orb)");
        var orbs = Resources.LoadAll<CraftingOrbSO>(ProjectPaths.ResourcesCraftingOrbsFolder);
        if (orbs != null && orbs.Length > 0)
        {
            foreach (var orb in orbs)
            {
                if (orb == null) continue;
                _orbList.Add(orb);
                _orbChoiceNames.Add(string.IsNullOrEmpty(orb.ID) ? orb.name : orb.ID);
            }
        }
        _selectedOrbIndex = 0;
        if (_orbSelectorButton != null) _orbSelectorButton.text = _orbChoiceNames[0];
        FillOrbListPopup();
    }

    private void FillOrbListPopup()
    {
        if (_orbListPopup == null) return;
        _orbListPopup.Clear();
        for (int i = 0; i < _orbChoiceNames.Count; i++)
        {
            int idx = i;
            var btn = new Button(() => OnOrbListEntryClick(idx)) { text = _orbChoiceNames[i] };
            btn.AddToClassList("debug-item-list-entry");
            _orbListPopup.Add(btn);
        }
    }

    private void OnOrbSelectorClick()
    {
        if (_orbListPopup == null) return;
        bool visible = _orbListPopup.style.display == DisplayStyle.Flex;
        if (visible)
        {
            _orbListPopup.style.display = DisplayStyle.None;
            return;
        }
        _orbListPopup.style.display = DisplayStyle.Flex;
        _orbListPopup.BringToFront();
        var row = _orbSelectorButton?.parent;
        if (row != null)
        {
            float top = row.layout.y + _orbSelectorButton.layout.y + _orbSelectorButton.layout.height + 2f;
            float left = row.layout.x + _orbSelectorButton.layout.x;
            _orbListPopup.style.top = top;
            _orbListPopup.style.left = left;
        }
    }

    private void OnOrbListEntryClick(int choiceIndex)
    {
        _selectedOrbIndex = choiceIndex;
        if (_orbSelectorButton != null) _orbSelectorButton.text = _orbChoiceNames[choiceIndex];
        if (_orbListPopup != null) _orbListPopup.style.display = DisplayStyle.None;
    }

    private void OnAddOrbClicked()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[DebugInventoryWindow] InventoryManager not found.");
            return;
        }
        int index = _selectedOrbIndex - 1;
        if (index < 0 || index >= _orbList.Count)
        {
            Debug.LogWarning("[DebugInventoryWindow] Select an orb.");
            return;
        }
        var orb = _orbList[index];
        string orbId = string.IsNullOrEmpty(orb.ID) ? orb.name : orb.ID;
        if (string.IsNullOrEmpty(orbId)) return;
        InventoryManager.Instance.AddOrb(orbId, 1);
        InventoryManager.Instance.TriggerUIUpdate();
    }

    public void SetVisible(bool visible)
    {
        if (_root == null) return;

        VisualElement targetParent = null;
        if (visible)
        {
            VisualElement inventoryRoot = GetInventoryRoot();
            if (inventoryRoot != null && _root.parent != inventoryRoot)
            {
                _root.RemoveFromHierarchy();
                inventoryRoot.Add(_root);
                targetParent = inventoryRoot;
            }
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            if (targetParent != null)
                _root.schedule.Execute(() => _root.BringToFront()).ExecuteLater(1);
        }
        else
        {
            _root.style.display = DisplayStyle.None;
            if (_uiDoc != null && _uiDoc.rootVisualElement != null && _root.parent != _uiDoc.rootVisualElement)
            {
                _root.RemoveFromHierarchy();
                _uiDoc.rootVisualElement.Add(_root);
            }
        }

        if (_uiDoc != null)
        {
            _uiDoc.sortingOrder = visible ? _sortOrderWhenVisible : _sortOrderWhenHidden;
            if (_uiDoc.panelSettings != null)
                _uiDoc.panelSettings.sortingOrder = visible ? _sortOrderWhenVisible : _panelSortOrderWhenHidden;
        }
    }

    public bool IsVisible()
    {
        return _root != null && _root.style.display != DisplayStyle.None;
    }
}
