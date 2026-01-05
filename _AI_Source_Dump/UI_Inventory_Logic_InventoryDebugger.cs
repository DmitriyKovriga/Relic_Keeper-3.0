using UnityEngine;
using Scripts.Items;
using Scripts.Inventory;

public class InventoryDebugger : MonoBehaviour
{
    [Header("Test Settings")]
    [Tooltip("Предмет-пустышка, на базе которого будет генерация")]
    [SerializeField] private EquipmentItemSO _testItemBase;
    
    [Tooltip("Уровень генерируемых предметов")]
    [SerializeField] private int _itemLevel = 10;

    private void OnGUI()
    {
        // Стилизация для удобства дебага
        GUI.skin.button.fontSize = 14;

        // Кнопка 1: Генерация Магического предмета (1-2 аффикса)
        if (GUI.Button(new Rect(10, 10, 180, 50), "Gen Magic Item (1-2 aff)"))
        {
            SpawnGeneratedItem(1); // 1 - rarity Magic
        }

        // Кнопка 2: Генерация Редкого предмета (3-4 аффикса)
        if (GUI.Button(new Rect(10, 70, 180, 50), "Gen Rare Item (3-4 aff)"))
        {
            SpawnGeneratedItem(2); // 2 - rarity Rare
        }

        // Кнопка 3: Очистка инвентаря для тестов
        if (GUI.Button(new Rect(10, 130, 180, 50), "Clear Inventory"))
        {
            ClearAll();
        }
    }

    private void SpawnGeneratedItem(int rarity)
    {
        if (_testItemBase == null)
        {
            Debug.LogError("[Debugger] Не назначен _testItemBase в инспекторе!");
            return;
        }

        if (ItemGenerator.Instance == null)
        {
            Debug.LogError("[Debugger] ItemGenerator.Instance не найден! Проверь, есть ли он на сцене.");
            return;
        }

        // 1. Генерируем новый экземпляр через нашу систему генерации
        InventoryItem newItem = ItemGenerator.Instance.Generate(_testItemBase, _itemLevel, rarity);

        // 2. Пытаемся добавить его в инвентарь
        if (InventoryManager.Instance != null)
        {
            bool success = InventoryManager.Instance.AddItem(newItem);
            if (success)
            {
                Debug.Log($"[Debugger] Сгенерирован и добавлен: {newItem.Data.ItemName} (Rarity: {rarity})");
            }
            else
            {
                Debug.LogWarning("[Debugger] Нет места в инвентаре!");
            }
        }
    }

    private void ClearAll()
    {
        if (InventoryManager.Instance == null) return;

        // Обнуляем массивы рюкзака и экипировки
        for (int i = 0; i < InventoryManager.Instance.Items.Length; i++)
            InventoryManager.Instance.Items[i] = null;

        for (int i = 0; i < InventoryManager.Instance.EquipmentItems.Length; i++)
            InventoryManager.Instance.EquipmentItems[i] = null;

        // Вызываем событие обновления UI
        // (Примечание: OnInventoryChanged в InventoryManager должен быть вызван)
    }
}