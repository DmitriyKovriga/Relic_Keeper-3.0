using UnityEngine;
using Scripts.Items;
using Scripts.Inventory;

public class InventoryDebugger : MonoBehaviour
{
    [Header("Debug Items")]
    [Tooltip("Перчатки (2x2)")]
    [SerializeField] private EquipmentItemSO _glovesItem; 
    
    [Tooltip("Двуручный топор (2x4)")]
    [SerializeField] private EquipmentItemSO _greatWeaponItem; // <-- Новое поле

    private void OnGUI()
    {
        // Кнопка 1: Добавить Перчатки
        if (GUI.Button(new Rect(10, 10, 150, 50), "Add Gloves (2x2)"))
        {
            AddItem(_glovesItem);
        }

        // Кнопка 2: Добавить Топор (смещаем вниз по Y на 60px)
        if (GUI.Button(new Rect(10, 70, 150, 50), "Add Axe (2x4)"))
        {
            AddItem(_greatWeaponItem);
        }
    }

    private void AddItem(EquipmentItemSO data)
    {
        if (data != null)
        {
            var newItem = new InventoryItem(data);
            // AddItem сам найдет первое свободное место, куда влезет эта громадина
            bool success = InventoryManager.Instance.AddItem(newItem);
            
            if (success) Debug.Log($"Added {data.ItemName}!");
            else Debug.Log("Inventory Full or No Space for this item!");
        }
        else
        {
            Debug.LogError("SO предмета не назначен в инспекторе Debugger'а!");
        }
    }
}