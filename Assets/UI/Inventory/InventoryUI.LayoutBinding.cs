using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Inventory;

public partial class InventoryUI
{
    private static Dictionary<string, Sprite> _inventoryAtlasSprites;
    private static Dictionary<string, Sprite> _stashAtlasSprites;

    private VisualElement _inventoryBackgroundArt;
    private VisualElement _inventoryFrameArt;
    private VisualElement _equipmentFrameArt;
    private VisualElement _ghostLeftArt;
    private VisualElement _ghostRightArt;
    private VisualElement _stashBackgroundArt;
    private VisualElement _stashFrameArt;
    private Sprite _stashTabBackgroundSprite;
    private Sprite _stashTabDeleteBackgroundSprite;

    private bool TryBindBackpackGridFromUxml()
    {
        var slotsLayer = _inventoryContainer.Q<VisualElement>("BackpackSlotsLayer");
        var itemsLayer = _inventoryContainer.Q<VisualElement>("ItemsLayer");
        if (slotsLayer == null || itemsLayer == null)
            return false;

        _backpackSlots.Clear();
        _itemsLayer = itemsLayer;

        _inventoryContainer.style.width = COLUMNS * InventorySlotSize;
        _inventoryContainer.style.height = ROWS * InventorySlotSize;
        slotsLayer.style.width = COLUMNS * InventorySlotSize;
        slotsLayer.style.height = ROWS * InventorySlotSize;

        var inventoryWrap = _inventoryContainer.parent;
        if (inventoryWrap != null)
        {
            inventoryWrap.style.minWidth = COLUMNS * InventorySlotSize;
            inventoryWrap.style.width = COLUMNS * InventorySlotSize;
        }

        for (int i = 0; i < ROWS * COLUMNS; i++)
        {
            var slot = slotsLayer.Q<VisualElement>($"BackpackSlot_{i}");
            if (slot == null)
                return false;

            slot.userData = i;
            slot.style.width = InventorySlotSize;
            slot.style.height = InventorySlotSize;

            slot.UnregisterCallback<PointerDownEvent>(OnSlotPointerDown);
            slot.UnregisterCallback<PointerOverEvent>(OnPointerOverSlot);
            slot.UnregisterCallback<PointerOutEvent>(OnPointerOutSlot);

            slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
            slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
            slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);

            _backpackSlots.Add(slot);
        }

        _itemsLayer.style.overflow = Overflow.Visible;
        _itemsLayer.BringToFront();
        return true;
    }

    private bool TryBindStashGridFromUxml()
    {
        var slotsLayer = _stashGridContainer.Q<VisualElement>("StashSlotsLayer");
        var itemsLayer = _stashGridContainer.Q<VisualElement>("StashItemsLayer");
        if (slotsLayer == null || itemsLayer == null)
            return false;

        _stashSlots.Clear();
        _stashItemsLayer = itemsLayer;

        _stashGridContainer.style.width = StashGridWidth;
        _stashGridContainer.style.height = StashGridHeight;
        slotsLayer.style.width = StashGridWidth;
        slotsLayer.style.height = StashGridHeight;

        for (int i = 0; i < StashManager.STASH_SLOTS_PER_TAB; i++)
        {
            var slot = slotsLayer.Q<VisualElement>($"StashSlot_{i}");
            if (slot == null)
                return false;

            slot.userData = STASH_SLOT_OFFSET + i;
            slot.style.width = StashSlotSize;
            slot.style.height = StashSlotSize;

            slot.UnregisterCallback<PointerDownEvent>(OnStashSlotPointerDown);
            slot.UnregisterCallback<PointerOverEvent>(OnPointerOverSlot);
            slot.UnregisterCallback<PointerOutEvent>(OnPointerOutSlot);

            slot.RegisterCallback<PointerDownEvent>(OnStashSlotPointerDown);
            slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
            slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);

            _stashSlots.Add(slot);
        }

        _stashItemsLayer.style.overflow = Overflow.Visible;
        _stashItemsLayer.BringToFront();
        return true;
    }

    private void ApplyInventoryArtTheme()
    {
        if (_root == null)
            return;

        _inventoryBackgroundArt = _root.Q<VisualElement>("InventoryBackgroundArt");
        _inventoryFrameArt = _root.Q<VisualElement>("InventoryFrameArt");
        _equipmentFrameArt = _root.Q<VisualElement>("EquipmentFrameArt");
        _ghostLeftArt = _root.Q<VisualElement>("GhostLeftArt");
        _ghostRightArt = _root.Q<VisualElement>("GhostRightArt");
        _stashBackgroundArt = _root.Q<VisualElement>("StashBackgroundArt");
        _stashFrameArt = _root.Q<VisualElement>("StashFrameArt");

        SetDecorativeIgnorePointer(_inventoryBackgroundArt);
        SetDecorativeIgnorePointer(_inventoryFrameArt);
        SetDecorativeIgnorePointer(_equipmentFrameArt);
        SetDecorativeIgnorePointer(_ghostLeftArt);
        SetDecorativeIgnorePointer(_ghostRightArt);
        SetDecorativeIgnorePointer(_stashBackgroundArt);
        SetDecorativeIgnorePointer(_stashFrameArt);

        var sprites = GetInventoryAtlasSprites();
        ApplyBackground(_inventoryBackgroundArt, GetSprite(sprites, "InventoryAssets_4"));
        ApplyBackground(_inventoryFrameArt, GetSprite(sprites, "InventoryAssets_16"));
        ApplyBackground(_equipmentFrameArt, GetSprite(sprites, "InventoryAssets_57"));
        ApplyBackground(_ghostLeftArt, GetSprite(sprites, "InventoryAssets_0"));
        ApplyBackground(_ghostRightArt, GetSprite(sprites, "InventoryAssets_2"));

        var slot2x4 = GetSprite(sprites, "InventoryAssets_7");

        if (_craftSlot != null)
            ApplyBackground(_craftSlot, slot2x4);

        ApplyStashArtTheme();
        _equipmentFrameArt?.BringToFront();
    }

    private static Dictionary<string, Sprite> GetInventoryAtlasSprites()
    {
        if (_inventoryAtlasSprites != null)
            return _inventoryAtlasSprites;

        _inventoryAtlasSprites = new Dictionary<string, Sprite>();
        var sprites = Resources.LoadAll<Sprite>("UI/Inventory/InventoryAssets");
        foreach (var sprite in sprites)
            _inventoryAtlasSprites[sprite.name] = sprite;

        return _inventoryAtlasSprites;
    }

    private static Dictionary<string, Sprite> GetStashAtlasSprites()
    {
        if (_stashAtlasSprites != null)
            return _stashAtlasSprites;

        _stashAtlasSprites = new Dictionary<string, Sprite>();
        LoadStashSpritesFromResource("UI/Stash/stash2.2", _stashAtlasSprites);
        LoadStashSpritesFromResource("UI/Stash/stash2", _stashAtlasSprites);

        return _stashAtlasSprites;
    }

    private void ApplyStashArtTheme()
    {
        var sprites = GetStashAtlasSprites();
        if (sprites == null || sprites.Count == 0)
            return;

        // New panel sprite first, then legacy fallback names.
        var stashPanelSprite = GetSprite(sprites, "stash2.2_0", "stash2_2");
        _stashTabBackgroundSprite = GetSprite(sprites, "stash2.2_1", "stash2_0");
        _stashTabDeleteBackgroundSprite = GetSprite(sprites, "stash2.2_2", "stash2_1");

        ApplyBackground(_stashBackgroundArt, stashPanelSprite);
        ApplyStashPanelFill(_stashBackgroundArt);

        // Separate frame layer is disabled: frame is integrated into stash2_2 art.
        if (_stashFrameArt != null)
        {
            _stashFrameArt.style.display = DisplayStyle.None;
            _stashFrameArt.style.backgroundImage = StyleKeyword.Null;
        }

        _stashWindowController?.SetTabArt(_stashTabBackgroundSprite, _stashTabDeleteBackgroundSprite);
        if (_stashTabsRow != null)
            _stashWindowController?.RefreshTabs(_stashTabsRow);
    }

    private static Sprite GetSprite(Dictionary<string, Sprite> sprites, string name)
    {
        if (sprites == null || string.IsNullOrEmpty(name))
            return null;

        return sprites.TryGetValue(name, out var sprite) ? sprite : null;
    }

    private static Sprite GetSprite(Dictionary<string, Sprite> sprites, params string[] names)
    {
        if (sprites == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            if (sprites.TryGetValue(names[i], out var sprite) && sprite != null)
                return sprite;
        }

        return null;
    }

    private static void LoadStashSpritesFromResource(string resourcePath, Dictionary<string, Sprite> target)
    {
        if (target == null || string.IsNullOrEmpty(resourcePath))
            return;

        var sprites = Resources.LoadAll<Sprite>(resourcePath);
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            if (sprite == null || string.IsNullOrEmpty(sprite.name))
                continue;

            if (!target.ContainsKey(sprite.name))
                target[sprite.name] = sprite;
        }
    }

    private static void ApplyBackground(VisualElement element, Sprite sprite)
    {
        if (element == null)
            return;

        if (sprite == null)
        {
            element.style.backgroundImage = StyleKeyword.Null;
            return;
        }

        element.style.backgroundImage = new StyleBackground(sprite);
    }

    private static void ApplyNativeSpriteBox(VisualElement element, Sprite sprite, float parentWidth, float parentHeight, float offsetX, float offsetY)
    {
        if (element == null)
            return;

        if (sprite == null)
        {
            element.style.width = StyleKeyword.Null;
            element.style.height = StyleKeyword.Null;
            element.style.left = StyleKeyword.Null;
            element.style.top = StyleKeyword.Null;
            return;
        }

        float width = sprite.rect.width;
        float height = sprite.rect.height;
        element.style.width = width;
        element.style.height = height;
        element.style.left = Mathf.Round((parentWidth - width) * 0.5f + offsetX);
        element.style.top = Mathf.Round((parentHeight - height) * 0.5f + offsetY);
    }

    private static void ApplyStashPanelFill(VisualElement element)
    {
        if (element == null)
            return;

        element.style.left = 0;
        element.style.top = 0;
        element.style.width = 200f;
        element.style.height = 270f;
    }

    private static void SetDecorativeIgnorePointer(VisualElement element)
    {
        if (element != null)
            element.pickingMode = PickingMode.Ignore;
    }
}


