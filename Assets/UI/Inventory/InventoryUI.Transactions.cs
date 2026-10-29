using Scripts.Inventory;
using Scripts.UI;
using UnityEngine.UIElements;

public partial class InventoryUI
{
    private UiTransactionFeedback _transactionFeedback;

    private UiTransactionFeedback TransactionFeedback => _transactionFeedback ??= new UiTransactionFeedback(_root);

    private InventoryItem[] CaptureEquipment()
    {
        return InventoryManager.Instance?.EquipmentItems != null
            ? (InventoryItem[])InventoryManager.Instance.EquipmentItems.Clone() : null;
    }

    private void PresentEquipmentResult(InventoryItem[] before, bool accepted, VisualElement rejectedTarget)
    {
        if (!accepted) { TransactionFeedback.Show(rejectedTarget, false); return; }
        var after = InventoryManager.Instance?.EquipmentItems;
        if (before == null || after == null) return;
        for (int i = 0; i < after.Length && i < before.Length; i++)
            if (!ReferenceEquals(before[i], after[i]))
            {
                TransactionFeedback.Show(GetSlotVisual(InventoryManager.EQUIP_OFFSET + i), true);
                break;
            }
    }

    private bool TryCtrlEquipWithFeedback(int anchor)
    {
        var before = CaptureEquipment();
        bool accepted = InventoryManager.Instance.TryCtrlEquipFromBackpack(anchor);
        PresentEquipmentResult(before, accepted, GetCraftingTargetVisual(anchor));
        return accepted;
    }

    private bool RejectCraftingTarget(int anchor)
    {
        TransactionFeedback.Show(GetCraftingTargetVisual(anchor), false);
        return false;
    }
}
