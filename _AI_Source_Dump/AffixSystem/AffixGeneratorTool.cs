using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic; // Важно для списков
using Scripts.Stats;
using Scripts.Items.Affixes; 

public class AffixGeneratorTool
{
    private const string BASE_PATH = "Assets/Resources/Affixes/Generated";

    [MenuItem("Tools/RPG/Generate Affixes (No Filters + Report)", false, 50)]
    public static void GenerateAffixes()
    {
        if (!Directory.Exists(BASE_PATH)) Directory.CreateDirectory(BASE_PATH);

        // --- СЧЕТЧИКИ ДЛЯ ОТЧЕТА ---
        int totalAffixesCreated = 0; 
        int statsWithExistingFolders = 0; 
        int statsProcessed = 0; 
        
        var statTypes = Enum.GetValues(typeof(StatType));

        AssetDatabase.StartAssetEditing(); 

        try 
        {
            foreach (StatType stat in statTypes)
            {
                // === ПОЛНЫЙ ПРОХОД (БЕЗ ФИЛЬТРОВ) ===
                // HealthOnHit, RegenPercent, Mitigation — всё пройдет.

                string category = GetStatCategory(stat); 
                string statName = stat.ToString();
                
                string folderPath = Path.Combine(BASE_PATH, category, statName);
                
                // --- ПРОВЕРКА ПАПКИ ---
                if (Directory.Exists(folderPath))
                {
                    // Папка уже есть — пропускаем (не ломаем то, что ты настроил)
                    statsWithExistingFolders++;
                    continue; 
                }
                // ----------------------

                // Папки нет — создаем папку и генерируем аффиксы
                Directory.CreateDirectory(folderPath);
                statsProcessed++;

                GenerateTierSet(stat, StatModType.Flat, "Flat", folderPath, ref totalAffixesCreated);
                GenerateTierSet(stat, StatModType.PercentAdd, "Increase", folderPath, ref totalAffixesCreated);
                GenerateTierSet(stat, StatModType.PercentMult, "More", folderPath, ref totalAffixesCreated);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CRITICAL ERROR: {e.Message}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // --- ПОДРОБНЫЙ ОТЧЕТ В КОНСОЛЬ ---
        Debug.Log($"<color=cyan><b>[AFFIX GENERATOR REPORT]</b></color>\n" +
                  $"Total Stats in Enum: <b>{statTypes.Length}</b>\n" +
                  $"--------------------------------------------------\n" +
                  $"✅ <b>PROCESSED (New Folders Created):</b> {statsProcessed}\n" +
                  $"⏭️ <b>SKIPPED (Folder Already Exists):</b> {statsWithExistingFolders}\n" +
                  $"--------------------------------------------------\n" +
                  $"📄 <b>Total .asset Files Created:</b> {totalAffixesCreated}");
    }

    private static void GenerateTierSet(StatType stat, StatModType modType, string typeSuffix, string folder, ref int count)
    {
        for (int tier = 1; tier <= 5; tier++) 
        {
            string fileName = $"{stat}_{typeSuffix}_T{tier}.asset";
            string fullPath = Path.Combine(folder, fileName);
            
            ItemAffixSO asset = ScriptableObject.CreateInstance<ItemAffixSO>();
            AssetDatabase.CreateAsset(asset, fullPath);

            asset.GroupID = $"{stat}_{typeSuffix}"; 
            asset.Tier = tier;
            asset.TranslationKey = $"affix_{typeSuffix.ToLower()}_{stat.ToString().ToLower()}"; 
            asset.RequiredLevel = GetLevelForTier(tier); 

            asset.Stats = new ItemAffixSO.AffixStatData[1];
            asset.Stats[0].Stat = stat;
            asset.Stats[0].Type = modType;
            asset.Stats[0].Scope = Scripts.Items.StatScope.Global; 

            SetValues(ref asset.Stats[0], stat, modType, tier);

            EditorUtility.SetDirty(asset);
            count++;
        }
    }

    private static int GetLevelForTier(int tier)
    {
        return tier switch { 1 => 30, 2 => 20, 3 => 10, 4 => 5, 5 => 1, _ => 1 };
    }

    private static void SetValues(ref ItemAffixSO.AffixStatData data, StatType stat, StatModType type, int tier)
    {
        int stepIndex = 5 - tier; 
        
        if (type == StatModType.Flat)
        {
            // Для HP/Маны цифры побольше
            float multiplier = (stat.ToString().Contains("Health") || stat.ToString().Contains("Mana")) ? 5f : 1f;

            float baseMin = 1f * multiplier; 
            float baseMax = 10f * multiplier; 
            float step = 10f * multiplier;

            data.MinValue = baseMin + (stepIndex * step);
            data.MaxValue = baseMax + (stepIndex * step);
        }
        else if (type == StatModType.PercentAdd)
        {
            float baseMin = 5f; float baseMax = 10f; float step = 5f;
            data.MinValue = baseMin + (stepIndex * step);
            data.MaxValue = baseMax + (stepIndex * step);
        }
        else if (type == StatModType.PercentMult)
        {
            float baseMin = 2f; float baseMax = 5f; float step = 2f;
            data.MinValue = baseMin + (stepIndex * step);
            data.MaxValue = baseMax + (stepIndex * step);
        }
    }

    private static string GetStatCategory(StatType type)
    {
        string s = type.ToString();
        if (s.Contains("Bleed") || s.Contains("Poison") || s.Contains("Ignite")) return "Ailments";
        if (s.Contains("Resist") || s.Contains("Penetration") || s.Contains("Mitigation") || s.Contains("ReduceDamage")) return "Resistances";
        if (s.Contains("Health") || s.Contains("Mana")) return "Vitals";
        if (s.Contains("Armor") || s.Contains("Evasion") || s.Contains("Block") || s.Contains("MysticShield")) return "Defense";
        if (s.Contains("Crit") || s.Contains("Accuracy")) return "Critical";
        if (s.Contains("Speed")) return "Speed";
        if (s.Contains("Damage")) return "Damage";
        if (s.Contains("To") || s.Contains("As")) return "Conversion";
        
        return "Misc"; // Всё остальное (OnHit и т.д.) падает сюда
    }
}