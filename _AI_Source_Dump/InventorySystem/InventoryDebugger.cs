using UnityEngine;
using Scripts.Items;
using Scripts.Inventory;

public class InventoryDebugger : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private EquipmentItemSO _testItemBase;
    [SerializeField] private int _itemLevel = 10;

    private void OnGUI()
    {
        GUI.skin.button.fontSize = 14;

        if (GUI.Button(new Rect(10, 10, 180, 50), "Gen Magic Item (1-2 aff)"))
            SpawnGeneratedItem(1);

        if (GUI.Button(new Rect(10, 70, 180, 50), "Gen Rare Item (3-4 aff)"))
            SpawnGeneratedItem(2);

        if (GUI.Button(new Rect(10, 130, 180, 50), "Clear Inventory"))
            ClearAll();
    }

    private void SpawnGeneratedItem(int rarity)
    {
        if (_testItemBase == null || ItemGenerator.Instance == null || InventoryManager.Instance == null) return;

        InventoryItem newItem = ItemGenerator.Instance.Generate(_testItemBase, _itemLevel, rarity);
        bool success = InventoryManager.Instance.AddItem(newItem);
        
        if (!success) Debug.LogWarning("[Debugger] Inventory Full");
    }

    private void ClearAll()
    {
        if (InventoryManager.Instance == null) return;

        for (int i = 0; i < InventoryManager.Instance.Items.Length; i++)
            InventoryManager.Instance.Items[i] = null;

        for (int i = 0; i < InventoryManager.Instance.EquipmentItems.Length; i++)
            InventoryManager.Instance.EquipmentItems[i] = null;

        InventoryManager.Instance.TriggerUIUpdate();
    }
}