using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;
using Scripts.Items;

public partial class InventoryUI
{
    private bool _waitingForLocalizationInit;

    private void RegisterInventoryLocalization()
    {
        LocalizationSettings.SelectedLocaleChanged += OnInventoryLocaleChanged;
        RefreshInventorySlotLabels();
        if (LocalizationSettings.SelectedLocale == null)
            RefreshInventorySlotLabelsWhenReady();
    }

    private void UnregisterInventoryLocalization()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnInventoryLocaleChanged;
        UnsubscribeLocalizationInit();
    }

    private void OnInventoryLocaleChanged(Locale _)
    {
        RefreshInventorySlotLabels();
    }

    private void RefreshInventorySlotLabelsWhenReady()
    {
        if (LocalizationSettings.SelectedLocale != null)
        {
            RefreshInventorySlotLabels();
            return;
        }

        AsyncOperationHandle<LocalizationSettings> init = LocalizationSettings.InitializationOperation;
        if (init.IsDone || _waitingForLocalizationInit)
            return;

        _waitingForLocalizationInit = true;
        init.Completed += OnLocalizationInitialized;
    }

    private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> handle)
    {
        handle.Completed -= OnLocalizationInitialized;
        _waitingForLocalizationInit = false;
        if (!isActiveAndEnabled)
            return;
        RefreshInventorySlotLabels();
    }

    private void UnsubscribeLocalizationInit()
    {
        if (!_waitingForLocalizationInit)
            return;
        _waitingForLocalizationInit = false;
        LocalizationSettings.InitializationOperation.Completed -= OnLocalizationInitialized;
    }

    private void RefreshInventorySlotLabels()
    {
        for (int i = 0; i < EquipmentSlotUxmlNames.Count; i++)
        {
            var slotType = (EquipmentSlot)i;
            if (!InventorySlotLocKeys.TryGetEquipmentSlot(slotType, out string key, out string fallback))
                continue;
            string uxmlName = EquipmentSlotUxmlNames.GetName(slotType);
            VisualElement slot = !string.IsNullOrEmpty(uxmlName) ? _root?.Q<VisualElement>(uxmlName) : null;
            ApplyLocalizedSlotLabel(slot, key, fallback);
        }

        ApplyLocalizedSlotLabel(_craftSlot, InventorySlotLocKeys.Item, InventorySlotLocKeys.ItemFallback);
    }

    private static void ApplyLocalizedSlotLabel(VisualElement slot, string key, string fallback)
    {
        var label = slot?.Q<Label>();
        if (label == null)
            return;

        label.text = fallback;
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(InventorySlotLocKeys.Table, key);
        operation.Completed += _ =>
        {
            if (label.panel == null)
                return;
            string value = operation.Result;
            if (IsMissingLocalization(value))
                return;
            label.text = value;
        };
    }

    private static bool IsMissingLocalization(string value)
    {
        return string.IsNullOrEmpty(value) ||
               value.IndexOf("translation found", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
