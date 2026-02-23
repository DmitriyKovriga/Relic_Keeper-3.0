using Scripts.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryUI
{
    private void GenerateBackpackGrid()
    {
        if (_inventoryContainer == null) return;

        if (TryBindBackpackGridFromUxml())
            return;

        _inventoryContainer.Clear();
        _backpackSlots.Clear();

        _inventoryContainer.style.width = COLUMNS * InventorySlotSize;
        _inventoryContainer.style.height = ROWS * InventorySlotSize;
        var inventoryWrap = _inventoryContainer.parent;
        if (inventoryWrap != null)
        {
            inventoryWrap.style.minWidth = COLUMNS * InventorySlotSize;
            inventoryWrap.style.width = COLUMNS * InventorySlotSize;
        }

        _itemsLayer = new VisualElement { name = "ItemsLayer" };
        _itemsLayer.style.position = Position.Absolute;
        _itemsLayer.StretchToParentSize();
        _itemsLayer.style.overflow = Overflow.Visible;

        int slotIndex = 0;
        for (int r = 0; r < ROWS; r++)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("inventory-row");
            row.style.width = COLUMNS * InventorySlotSize;
            row.style.height = InventorySlotSize;
            _inventoryContainer.Add(row);

            for (int c = 0; c < COLUMNS; c++)
            {
                VisualElement slot = new VisualElement();
                slot.AddToClassList("slot");
                slot.style.width = InventorySlotSize;
                slot.style.height = InventorySlotSize;
                slot.userData = slotIndex;
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                row.Add(slot);
                _backpackSlots.Add(slot);
                slotIndex++;
            }
        }
        _inventoryContainer.Add(_itemsLayer);
    }

    private void RefreshInventory()
    {
        if (_itemsLayer == null || InventoryManager.Instance == null) return;
        _itemsLayer.style.display = DisplayStyle.None;
        _itemsLayer.Clear();
        foreach (var slot in _equipmentSlots)
        {
            var oldImg = slot.Q<Image>();
            if (oldImg != null) slot.Remove(oldImg);
        }
        var inv = InventoryManager.Instance;
        int slotCount = inv.BackpackSlotCount;
        for (int i = 0; i < slotCount; i++)
        {
            InventoryItem item = inv.GetItemAt(i, out int anchorIndex);
            if (item == null || item.Data == null) continue;
            if (i != anchorIndex) continue;
            inv.GetBackpackItemSize(item, out int w, out int h);
            var icon = CreateItemIcon(item, w, h, InventorySlotSize, receivePointerEvents: true);
            icon.style.left = (i % COLUMNS) * InventorySlotSize;
            icon.style.top = (i / COLUMNS) * InventorySlotSize;
            icon.userData = i;
            icon.RegisterCallback<PointerOverEvent>(OnPointerOverBackpackIcon);
            icon.RegisterCallback<PointerOutEvent>(OnPointerOutBackpackIcon);
            icon.RegisterCallback<PointerDownEvent>(OnBackpackIconPointerDown);
            _itemsLayer.Add(icon);
            icon.MarkDirtyRepaint();
        }
        if (_currentTab == 0)
            DrawEquipmentIcons();
        else
            DrawCraftSlotIcon();
        RefreshOrbSlots();
        _itemsLayer.style.display = DisplayStyle.Flex;
        _itemsLayer.BringToFront();
        _root.MarkDirtyRepaint();
    }

    private void DrawEquipmentIcons()
    {
        var equipItems = InventoryManager.Instance.EquipmentItems;

        for (int i = 0; i < equipItems.Length; i++)
        {
            var item = equipItems[i];

            int targetID = InventoryManager.EQUIP_OFFSET + i;
            VisualElement slot = _equipmentSlots.Find(s => (int)s.userData == targetID);

            if (slot != null && item != null && item.Data != null)
            {
                var icon = CreateItemIcon(item, null, null, EquipmentIconCellSize);
                float iconW = item.Data.Width * EquipmentIconCellSize;
                float iconH = item.Data.Height * EquipmentIconCellSize;
                float slotW = i < EquipmentSlotSizes.Length ? EquipmentSlotSizes[i].w : 48f;
                float slotH = i < EquipmentSlotSizes.Length ? EquipmentSlotSizes[i].h : 48f;
                icon.style.left = (slotW - iconW) * 0.5f;
                icon.style.top = (slotH - iconH) * 0.5f;
                icon.style.right = StyleKeyword.Null;
                icon.style.bottom = StyleKeyword.Null;
                slot.Add(icon);
            }
        }
    }

    private void DrawCraftSlotIcon()
    {
        if (_craftSlot == null) return;
        var oldImg = _craftSlot.Q<Image>();
        if (oldImg != null) _craftSlot.Remove(oldImg);
        var item = InventoryManager.Instance.CraftingSlotItem;
        if (item != null && item.Data != null)
        {
            var icon = CreateItemIcon(item, null, null, EquipmentIconCellSize);
            float iconW = item.Data.Width * EquipmentIconCellSize;
            float iconH = item.Data.Height * EquipmentIconCellSize;
            icon.style.left = (CraftSlotWidth - iconW) * 0.5f;
            icon.style.top = (CraftSlotHeight - iconH) * 0.5f;
            icon.style.right = StyleKeyword.Null;
            icon.style.bottom = StyleKeyword.Null;
            _craftSlot.Add(icon);
        }
    }

    private void RefreshOrbSlots()
    {
        if (InventoryManager.Instance == null) return;
        for (int i = 0; i < _orbSlots.Count; i++)
        {
            var (slot, countLabel) = _orbSlots[i];
            var orb = _orbSlotsConfig != null ? _orbSlotsConfig.GetOrbInSlot(i) : null;
            int count = orb != null ? InventoryManager.Instance.GetOrbCount(orb.ID) : 0;
            slot.style.backgroundImage = (orb != null && orb.Icon != null) ? new StyleBackground(orb.Icon) : default;
            countLabel.text = count.ToString();
            countLabel.style.visibility = Visibility.Visible;
        }
    }

    /// <param name="slotSizePx">Cell size in px for inventory, stash or equipment icon rendering.</param>
    /// <param name="receivePointerEvents">True for backpack/stash icon interactions.</param>
    private VisualElement CreateItemIcon(InventoryItem item, int? widthSlots, int? heightSlots, float slotSizePx, bool receivePointerEvents = false)
    {
        Image icon = new Image();
        icon.sprite = item.Data.Icon;
        int w = widthSlots ?? item.Data.Width;
        int h = heightSlots ?? item.Data.Height;
        w = Mathf.Clamp(w, 1, 10);
        h = Mathf.Clamp(h, 1, 10);
        icon.style.width = w * slotSizePx;
        icon.style.height = h * slotSizePx;
        icon.style.position = Position.Absolute;
        icon.pickingMode = receivePointerEvents ? PickingMode.Position : PickingMode.Ignore;
        if (item.Data.Icon != null) icon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        return icon;
    }

    private VisualElement GetSlotVisual(int index)
    {
        if (index == InventoryManager.CRAFT_SLOT_INDEX)
            return _craftSlot;
        if (index >= InventoryManager.EQUIP_OFFSET)
            return _equipmentSlots.Find(s => (int)s.userData == index);
        if (index >= 0 && index < _backpackSlots.Count)
            return _backpackSlots[index];
        return null;
    }
}
