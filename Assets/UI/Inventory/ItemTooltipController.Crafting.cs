using Scripts.Inventory;
using Scripts.UI;
using UnityEngine;
using UnityEngine.UIElements;

public partial class ItemTooltipController
{
    private InventoryItem _craftingResultItem;
    private System.Collections.Generic.IReadOnlyList<CraftingChangeSnapshot.Change> _craftingChanges;

    public void ShowCraftedItemTooltip(InventoryItem item, VisualElement anchorSlot, ItemTooltipPriceMode priceMode, CraftingChangeSnapshot before)
    {
        HideTooltipImmediate();
        if (item == null || anchorSlot == null) return;
        if (before != null)
        {
            _craftingResultItem = item;
            _craftingChanges = before.CompareTo(CraftingChangeSnapshot.Capture(item));
        }
        ShowTooltip(item, anchorSlot, priceMode);
    }

    private bool AppendCraftingAffixes(InventoryItem item)
    {
        if (_inspectDetailsVisible || item != _craftingResultItem || _craftingChanges == null || _statsContainer == _compareStatsContainer) return false;
        var heading = CreateLabel(RuntimeLocalization.Resolve("transaction.craftResult", "Craft result: before > now", "Крафт: было > стало"), 7, FontStyle.Bold, TextAnchor.MiddleCenter);
        heading.name = "CraftResultHeading"; heading.style.color = _colGoldText;
        heading.pickingMode = PickingMode.Ignore; _statsContainer.Add(heading);
        foreach (var change in _craftingChanges)
        {
            string oldText = change.Before?.Format(_statsDb), newText = change.After?.Format(_statsDb);
            string text = change.Before == null ? "+ " + newText : change.After == null ? "- " + oldText :
                change.Changed ? oldText + " > " + newText : newText;
            var row = CreateLabel(text, 7, FontStyle.Normal, TextAnchor.MiddleCenter);
            row.name = "CraftChangeRow"; row.pickingMode = PickingMode.Ignore;
            row.style.color = change.After == null ? new Color(.86f, .59f, .55f) : change.Changed ? _colGoldText : _colAffix;
            _statsContainer.Add(row);
        }
        return true;
    }
}
