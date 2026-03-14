using UnityEngine;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Inventory;
using Scripts.Items.Affixes; // Р”Р»СЏ ItemAffixSO
using Scripts.Stats;        // Р”Р»СЏ StatType

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

        // --- РќРћР’РђРЇ РљРќРћРџРљРђ ---
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

    // --- Р›РћР“РРљРђ РЎРџРђР’РќРђ Р‘Р«РЎРўР РћР“Рћ РўРћРџРћР Рђ ---
    private void SpawnFastItem()
    {
        if (!CheckSetup()) return;

        // 1. Р“РµРЅРµСЂРёСЂСѓРµРј РѕР±С‹С‡РЅС‹Р№ РїСЂРµРґРјРµС‚ (Magic)
        InventoryItem newItem = ItemGenerator.Instance.Generate(_testItemBase, _itemLevel, 1);

        // 2. РЎРѕР·РґР°РµРј С„РµР№РєРѕРІС‹Р№ SO Р°С„С„РёРєСЃР° РІ РїР°РјСЏС‚Рё
        ItemAffixSO fakeAffixSO = ScriptableObject.CreateInstance<ItemAffixSO>();
        fakeAffixSO.name = "Debug_Berserker";
        fakeAffixSO.TranslationKey = "DEBUG: +50% Attack Speed"; // Р’ С‚СѓР»С‚РёРїРµ Р±СѓРґРµС‚ СЌС‚РѕС‚ С‚РµРєСЃС‚ (РµСЃР»Рё РЅРµС‚ РєР»СЋС‡Р°)

        // 3. РЎРѕР·РґР°РµРј РёРЅСЃС‚Р°РЅСЃ Р°С„С„РёРєСЃР° РІСЂСѓС‡РЅСѓСЋ
        AffixInstance affixInst = new AffixInstance(fakeAffixSO, newItem);
        
        // РћС‡РёС‰Р°РµРј СЂР°РЅРґРѕРјРЅС‹Рµ РјРѕРґС‹ (РµСЃР»Рё РєРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ С‡С‚Рѕ-С‚Рѕ РЅР°СЃРѕР·РґР°РІР°Р») Рё РґРѕР±Р°РІР»СЏРµРј РЅР°С€
        affixInst.Modifiers.Clear();
        
        // Р”РѕР±Р°РІР»СЏРµРј +50% Increased Attack Speed (Local РёР»Рё Global - РґР»СЏ РѕСЂСѓР¶РёСЏ РѕР±С‹С‡РЅРѕ Local)
        // StatType, Modifier(Value, Type, Source), Scope
        affixInst.Modifiers.Add(new AffixModifierInstance(
            StatType.AttackSpeed,
            StatScope.Global, // РџСѓСЃС‚СЊ Р±СѓРґРµС‚ РіР»РѕР±Р°Р»СЊРЅС‹Рј РґР»СЏ С‚РµСЃС‚Р°
            new StatModifier(50f, StatModType.PercentAdd, newItem)
        ));

        // 4. Р”РѕР±Р°РІР»СЏРµРј Р°С„С„РёРєСЃ РІ РїСЂРµРґРјРµС‚
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
        InventoryManager.Instance.ClearAllItemsForDebug();
    }
}
