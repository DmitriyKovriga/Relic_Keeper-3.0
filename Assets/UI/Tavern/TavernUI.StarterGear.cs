using System.Collections;
using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

public partial class TavernUI
{
    /// <summary>Adventurer's Dagger — стартовое оружие для героя без единого оружия.</summary>
    private const string StarterWeaponItemId = "54e1b823";

    private const float ToastHoldDuration = 2.2f;
    private const float ToastFadeDuration = 0.6f;

    private void BuildStarterGearTab(VisualElement panel)
    {
        _gearContent = new VisualElement();
        _gearContent.style.flexDirection = FlexDirection.Column;
        _gearContent.style.flexGrow = 1;
        _gearContent.style.minHeight = 0;
        _gearContent.style.justifyContent = Justify.Center;
        _gearContent.style.alignItems = Align.Center;

        var title = new Label("Starter Gear");
        title.style.fontSize = 9;
        title.style.color = new Color(0.9f, 0.8f, 0.6f);
        title.style.marginBottom = 4;
        _gearContent.Add(title);
        SetLocalizedLabel(title, TavernLocKeys.GearTitle, "Starter Gear");

        var hint = new Label("Available only while you have no weapon in your inventory or stash.");
        hint.style.fontSize = 7;
        hint.style.color = new Color(0.78f, 0.72f, 0.62f);
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        hint.style.maxWidth = 260;
        hint.style.marginBottom = 8;
        _gearContent.Add(hint);
        SetLocalizedLabel(hint, TavernLocKeys.GearHint, "Available only while you have no weapon in your inventory or stash.");

        var grantButton = new Button(OnGrantStarterGearClicked) { text = "Get Starter Equipment" };
        grantButton.style.fontSize = 8;
        grantButton.style.width = 220;
        grantButton.style.height = 18;
        SetLocalizedButton(grantButton, TavernLocKeys.GrantStarterGear, "Get Starter Equipment");
        _gearContent.Add(grantButton);

        _gearContent.style.display = DisplayStyle.None;
        panel.Add(_gearContent);
    }

    private void BuildToast()
    {
        _toastContainer = new VisualElement { name = "Toast", pickingMode = PickingMode.Ignore };
        _toastContainer.style.position = Position.Absolute;
        _toastContainer.style.left = 0;
        _toastContainer.style.right = 0;
        _toastContainer.style.bottom = 16;
        _toastContainer.style.alignItems = Align.Center;
        _toastContainer.style.display = DisplayStyle.None;

        _toastLabel = new Label { pickingMode = PickingMode.Ignore };
        _toastLabel.style.fontSize = 8;
        _toastLabel.style.color = new Color(0.94f, 0.88f, 0.72f);
        _toastLabel.style.whiteSpace = WhiteSpace.Normal;
        _toastLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _toastLabel.style.maxWidth = 320;
        _toastLabel.style.paddingLeft = _toastLabel.style.paddingRight = 6;
        _toastLabel.style.paddingTop = _toastLabel.style.paddingBottom = 3;
        _toastLabel.style.backgroundColor = new Color(0.1f, 0.08f, 0.07f, 0.95f);
        _toastLabel.style.borderLeftWidth = _toastLabel.style.borderRightWidth =
            _toastLabel.style.borderTopWidth = _toastLabel.style.borderBottomWidth = 1;
        _toastLabel.style.borderLeftColor = _toastLabel.style.borderRightColor =
            _toastLabel.style.borderTopColor = _toastLabel.style.borderBottomColor = new Color(0.46f, 0.36f, 0.24f);
        _toastContainer.Add(_toastLabel);

        _windowRoot.Add(_toastContainer);
    }

    private void ShowToast(string key, string fallback, bool isError)
    {
        if (_toastContainer == null || _toastLabel == null)
            return;

        _toastLabel.text = fallback;
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
        operation.Completed += _ =>
        {
            if (_toastLabel != null && _toastLabel.panel != null && !IsMissingLocalization(operation.Result))
                _toastLabel.text = operation.Result;
        };

        _toastLabel.style.color = isError ? new Color(0.92f, 0.6f, 0.5f) : new Color(0.78f, 0.92f, 0.7f);
        _toastContainer.style.display = DisplayStyle.Flex;
        _toastContainer.style.opacity = 1f;

        if (_toastRoutine != null)
            StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(FadeOutToast());
    }

    private IEnumerator FadeOutToast()
    {
        float elapsed = 0f;
        while (elapsed < ToastHoldDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < ToastFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_toastContainer != null)
                _toastContainer.style.opacity = 1f - Mathf.Clamp01(elapsed / ToastFadeDuration);
            yield return null;
        }

        _toastRoutine = null;
        HideToast();
    }

    private void HideToast()
    {
        if (_toastRoutine != null)
        {
            StopCoroutine(_toastRoutine);
            _toastRoutine = null;
        }

        if (_toastContainer != null)
        {
            _toastContainer.style.display = DisplayStyle.None;
            _toastContainer.style.opacity = 1f;
        }
    }

    private void OnGrantStarterGearClicked()
    {
        StarterGearGrantResult result = TryGrantStarterWeapon(checkStash: true, preferEquip: false);
        switch (result)
        {
            case StarterGearGrantResult.Granted:
                FindObjectOfType<GameSaveManager>()?.SaveGame();
                ShowToast(TavernLocKeys.StarterGearGranted, "Adventurer's Dagger added to your inventory", isError: false);
                break;
            case StarterGearGrantResult.NoCharacter:
                ShowToast(TavernLocKeys.StarterGearNoCharacter, "Choose a hero first", isError: true);
                break;
            case StarterGearGrantResult.AlreadyArmed:
                ShowToast(TavernLocKeys.StarterGearAlreadyArmed, "You already have a weapon in your inventory or stash", isError: true);
                break;
            case StarterGearGrantResult.NoSpace:
                ShowToast(TavernLocKeys.StarterGearNoSpace, "Not enough free space in your inventory", isError: true);
                break;
            default:
                ShowToast(TavernLocKeys.StarterGearUnavailable, "Starter equipment is unavailable", isError: true);
                break;
        }
    }

    private enum StarterGearGrantResult
    {
        Granted,
        AlreadyArmed,
        NoCharacter,
        Unavailable,
        NoSpace
    }

    /// <summary>
    /// Выдаёт стартовый кинжал текущему герою, если у него нет своего оружия.
    /// При найме stash не учитываем — это общий сундук, а не инвентарь нового персонажа.
    /// </summary>
    private StarterGearGrantResult TryGrantStarterWeapon(bool checkStash, bool preferEquip)
    {
        if (CharacterPartyManager.Instance == null || !CharacterPartyManager.Instance.HasActiveCharacter)
            return StarterGearGrantResult.NoCharacter;

        if (InventoryManager.Instance == null || _itemDatabase == null)
            return StarterGearGrantResult.Unavailable;

        if (PlayerOwnsWeapon(checkStash))
            return StarterGearGrantResult.AlreadyArmed;

        var baseItem = _itemDatabase.GetItem(StarterWeaponItemId);
        if (baseItem == null)
            return StarterGearGrantResult.Unavailable;

        var weapon = ItemGenerator.GenerateRuntime(baseItem, itemLevel: 1, rarity: 0);
        if (weapon == null)
            return StarterGearGrantResult.Unavailable;

        if (preferEquip)
        {
            int mainHandIndex = InventoryManager.EQUIP_OFFSET + (int)EquipmentSlot.MainHand;
            if (InventoryManager.Instance.PlaceItemAt(weapon, mainHandIndex, -1))
                return StarterGearGrantResult.Granted;
        }

        if (InventoryManager.Instance.AddItem(weapon))
            return StarterGearGrantResult.Granted;

        return StarterGearGrantResult.NoSpace;
    }

    /// <summary>Есть ли у игрока хоть одно оружие: в рюкзаке, в экипировке, в слоте крафта или на складе.</summary>
    private static bool PlayerOwnsWeapon(bool includeStash = true)
    {
        var inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            int backpackSlots = inventory.Items != null ? inventory.Items.Length : 0;
            for (int i = 0; i < backpackSlots; i++)
            {
                if (IsWeapon(inventory.GetItem(i)))
                    return true;
            }

            var equipment = inventory.EquipmentItems;
            if (equipment != null)
            {
                for (int i = 0; i < equipment.Length; i++)
                {
                    if (IsWeapon(equipment[i]))
                        return true;
                }
            }

            if (IsWeapon(inventory.CraftingSlotItem))
                return true;
        }

        if (!includeStash)
            return false;

        var stash = StashManager.Instance;
        if (stash != null)
        {
            for (int tab = 0; tab < stash.TabCount; tab++)
            {
                for (int slot = 0; slot < StashManager.STASH_SLOTS_PER_TAB; slot++)
                {
                    if (IsWeapon(stash.GetItem(tab, slot)))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>Щиты тоже описаны через WeaponItemSO, поэтому оружием считаем только то, что носится в основной руке.</summary>
    private static bool IsWeapon(InventoryItem item)
    {
        if (item?.Data is WeaponItemSO weapon)
            return weapon.IsTwoHanded || weapon.Slot == EquipmentSlot.MainHand;
        return false;
    }
}
