using UnityEngine;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Inventory;
using Scripts.Items.Affixes; // Для ItemAffixSO
using Scripts.Stats;        // Для StatType

public class InventoryDebugger : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private EquipmentItemSO _testItemBase;
    [SerializeField] private int _itemLevel = 10;

    private void OnEnable()
    {
        if (FindFirstObjectByType<DebugInventoryWindowUI>() != null)
            enabled = false;
    }

    private void OnGUI()
    {
        GUI.skin.button.fontSize = 14;
        float btnHeight = 40;
        float y = 10;

        if (GUI.Button(new Rect(10, y, 220, btnHeight), "Gen Magic (1-2 aff)"))
            SpawnGeneratedItem(1);
        y += 50;

        if (GUI.Button(new Rect(10, y, 220, btnHeight), "Gen Rare (3-4 aff)"))
            SpawnGeneratedItem(2);
        y += 50;

        // --- НОВАЯ КНОПКА ---
        if (GUI.Button(new Rect(10, y, 220, btnHeight), "Gen FAST Axe (+50% APS)"))
            SpawnFastItem();
        y += 50;
        // --------------------

        if (GUI.Button(new Rect(10, y, 220, btnHeight), "Clear Inventory"))
            ClearAll();
    }

    private void SpawnGeneratedItem(int rarity)
    {
        if (!CheckSetup()) return;

        InventoryItem newItem = ItemGenerator.Instance.Generate(_testItemBase, _itemLevel, rarity);
        TryAddItem(newItem);
    }

    // --- ЛОГИКА СПАВНА БЫСТРОГО ТОПОРА ---
    private void SpawnFastItem()
    {
        if (!CheckSetup()) return;

        // 1. Генерируем обычный предмет (Magic)
        InventoryItem newItem = ItemGenerator.Instance.Generate(_testItemBase, _itemLevel, 1);

        // 2. Создаем фейковый SO аффикса в памяти
        ItemAffixSO fakeAffixSO = ScriptableObject.CreateInstance<ItemAffixSO>();
        fakeAffixSO.name = "Debug_Berserker";
        fakeAffixSO.TranslationKey = "DEBUG: +50% Attack Speed"; // В тултипе будет этот текст (если нет ключа)

        // 3. Создаем инстанс аффикса вручную
        AffixInstance affixInst = new AffixInstance(fakeAffixSO, newItem);
        
        // Очищаем рандомные моды (если конструктор что-то насоздавал) и добавляем наш
        affixInst.Modifiers.Clear();
        
        // Добавляем +50% Increased Attack Speed (Local или Global - для оружия обычно Local)
        // StatType, Modifier(Value, Type, Source), Scope
        affixInst.Modifiers.Add((
            StatType.AttackSpeed, 
            new StatModifier(50f, StatModType.PercentAdd, newItem), 
            StatScope.Global // Пусть будет глобальным для теста
        ));

        // 4. Добавляем аффикс в предмет
        newItem.Affixes.Add(affixInst);

        TryAddItem(newItem);
    }

    private bool CheckSetup()
    {
        if (_testItemBase == null)
        {
            Debug.LogError("[Debugger] Test Item Base is missing!");
            return false;
        }
        if (InventoryManager.Instance == null || ItemGenerator.Instance == null) return false;
        return true;
    }

    private void TryAddItem(InventoryItem item)
    {
        if (item.Data == null) return;
        bool success = InventoryManager.Instance.AddItem(item);
        if (!success) Debug.LogWarning("[Debugger] Inventory Full");
    }

    private void ClearAll()
    {
        if (InventoryManager.Instance == null) return;

        // Снимаем экипировку в рюкзак (первый свободный слот)
        for (int i = 0; i < InventoryManager.Instance.EquipmentItems.Length; i++)
        {
            if (InventoryManager.Instance.EquipmentItems[i] != null)
            {
                bool ok = InventoryManager.Instance.UnequipToBackpack(i);
                if (!ok)
                    Debug.LogWarning("[Debugger] Не удалось снять экипировку в слот " + i + " (рюкзак полон).");
            }
        }

        // Очищаем рюкзак через бэкенд (не трогаем массив Items напрямую)
        InventoryManager.Instance.ClearBackpack();
    }
}