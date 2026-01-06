using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using Scripts.Stats;
using Scripts.Items.Affixes; 

public class AffixGeneratorTool
{
    private const string BASE_PATH = "Assets/Resources/Affixes/Generated";

    [MenuItem("Tools/RPG/Generate All Base Affixes (Flat, Increase, More)", false, 50)]
    public static void GenerateAffixes()
    {
        if (!Directory.Exists(BASE_PATH))
        {
            Directory.CreateDirectory(BASE_PATH);
        }

        int count = 0;
        var statTypes = Enum.GetValues(typeof(StatType));
        
        // 3 типа (Flat/Inc/More) * 5 тиров * кол-во статов
        int totalOperations = statTypes.Length * 3 * 5; 

        foreach (StatType stat in statTypes)
        {
            string statName = stat.ToString();
            string folderPath = Path.Combine(BASE_PATH, statName);
            
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            // 1. FLAT (+10)
            GenerateTierSet(stat, StatModType.Flat, "Flat", folderPath, ref count, totalOperations);

            // 2. INCREASE (+10%)
            GenerateTierSet(stat, StatModType.PercentAdd, "Increase", folderPath, ref count, totalOperations);

            // 3. MORE (x1.1)
            GenerateTierSet(stat, StatModType.PercentMult, "More", folderPath, ref count, totalOperations);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"✅ Генерация завершена! Обработано {count} аффиксов.");
    }

    private static void GenerateTierSet(StatType stat, StatModType type, string prefix, string folderPath, ref int count, int totalOps)
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            CreateAffixAsset(stat, tier, type, prefix, folderPath);
            count++;
            EditorUtility.DisplayProgressBar("Generating Affixes", $"Processing {stat} ({prefix})...", (float)count / totalOps);
        }
    }

    private static void CreateAffixAsset(StatType stat, int tier, StatModType modType, string typePrefix, string folderPath)
    {
        string statName = stat.ToString();
        
        // Имя файла остается уникальным: Increase_MaxHealth_Tier1.asset
        string fileName = $"{typePrefix}_{statName}_Tier{tier}.asset";
        string fullPath = Path.Combine(folderPath, fileName);

        ItemAffixSO asset = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(fullPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ItemAffixSO>();
            AssetDatabase.CreateAsset(asset, fullPath);
        }

        // --- НАСТРОЙКИ ---
        
        // GroupID: "IncreaseMaxHealth"
        asset.GroupID = $"{typePrefix}{statName}"; 
        
        // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
        // Генерируем ключ БЕЗ тира. 
        // Пример: affix_increase_strength (для всех тиров одинаково)
        asset.TranslationKey = $"affix_{typePrefix.ToLower()}_{statName.ToLower()}";

        asset.Tier = tier;
        asset.RequiredLevel = GetLevelForTier(tier);

        // Инициализация статов
        if (asset.Stats == null || asset.Stats.Length == 0)
        {
            asset.Stats = new ItemAffixSO.AffixStatData[1];
        }

        asset.Stats[0].Stat = stat;
        asset.Stats[0].Type = modType;

        // --- БАЛАНС ЗНАЧЕНИЙ (Как ты и прислал) ---
        if (modType == StatModType.Flat)
        {
            // Flat: 10..50 (Примерная логика из твоего кода)
            asset.Stats[0].MinValue = (6 - tier) * 10 - 5; 
            asset.Stats[0].MaxValue = (6 - tier) * 10;
        }
        else if (modType == StatModType.PercentAdd) // Increase
        {
            // Increase: 3..20%
            asset.Stats[0].MinValue = (6 - tier) * 3;
            asset.Stats[0].MaxValue = (6 - tier) * 4;
        }
        else // More (PercentMult)
        {
            float baseMult = 1.0f;
            float step = 0.02f; // 2% за тир
            
            asset.Stats[0].MinValue = baseMult + ((6 - tier) * step) - 0.01f;
            asset.Stats[0].MaxValue = baseMult + ((6 - tier) * step);
        }

        EditorUtility.SetDirty(asset);
    }

    private static int GetLevelForTier(int tier)
    {
        switch (tier)
        {
            case 5: return 1;
            case 4: return 10;
            case 3: return 15;
            case 2: return 20;
            case 1: return 30;
            default: return 1;
        }
    }
}