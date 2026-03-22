using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Scripts.Items.Affixes;
using Scripts.Stats;

public class AffixLocalizationSync : EditorWindow
{
    public StringTableCollection targetCollection;
    // Убедись, что путь правильный. AssetDatabase.FindAssets ищет рекурсивно!
    private const string AFFIX_PATH = "Assets/Resources/Affixes/Generated";

    [MenuItem("Tools/RPG/2. Sync Affix Text (Recursive)")]
    public static void ShowWindow() => GetWindow<AffixLocalizationSync>("Sync Affixes");

    private void OnGUI()
    {
        GUILayout.Label("Синхронизация (Рекурсивно по папкам)", EditorStyles.boldLabel);
        targetCollection = (StringTableCollection)EditorGUILayout.ObjectField("Table Collection", targetCollection, typeof(StringTableCollection), false);

        if (GUILayout.Button("Сгенерировать Текст") && targetCollection != null)
        {
            Sync();
        }
    }

    private void Sync()
    {
        var enTable = targetCollection.GetTable("en") as StringTable;
        var ruTable = targetCollection.GetTable("ru") as StringTable;

        if (enTable == null || ruTable == null) { Debug.LogError("Таблицы не найдены."); return; }

        // FindAssets ищет во всех подпапках указанного пути автоматически
        string[] guids = AssetDatabase.FindAssets("t:ItemAffixSO", new[] { AFFIX_PATH });
        int addedCount = 0;
        int skippedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
            
            if (string.IsNullOrEmpty(affix.TranslationKey)) continue;

            // Если ключ уже есть в английской таблице - пропускаем
            if (enTable.GetEntry(affix.TranslationKey) != null) 
            {
                skippedCount++;
                continue;
            }

            // Парсинг берем из имени файла: "DamagePhysical_Flat_T1"
            if (TryParseFileName(affix.name, out string statNameRaw, out string modType))
            {
                string prettyNameEN = GetEnglishStatName(statNameRaw);
                string prettyNameRU = GetRussianStatName(statNameRaw);

                string enText = GenerateEnglish(modType, statNameRaw, prettyNameEN);
                string ruText = GenerateRussian(modType, statNameRaw, prettyNameRU);

                AddEntry(enTable, affix.TranslationKey, enText);
                AddEntry(ruTable, affix.TranslationKey, ruText);
                
                addedCount++;
            }
        }

        EditorUtility.SetDirty(enTable);
        EditorUtility.SetDirty(ruTable);
        AssetDatabase.SaveAssets();
        Debug.Log($"✅ Синхронизация завершена. Добавлено: {addedCount}. Пропущено: {skippedCount}.");
    }

    private bool TryParseFileName(string fileName, out string statName, out string modType)
    {
        statName = ""; modType = "";
        var parts = fileName.Split('_');
        if (parts.Length < 3) return false;
        
        modType = parts[parts.Length - 2]; // Flat
        statName = string.Join("_", parts.Take(parts.Length - 2)); // DamagePhysical
        return true;
    }

    private void AddEntry(StringTable table, string key, string value)
    {
        table.AddEntry(key, value);
    }

    // --- TEMPLATES ---
    private string GenerateEnglish(string modType, string rawStat, string statName)
    {
        if (modType == "Flat" && !IsPercentageStat(rawStat)) return $"Adds {{0}} to {statName}";
        if (modType == "Flat" && IsPercentageStat(rawStat)) return $"+{{0}}% to {statName}";
        if (modType == "Increase") return $"{{0}}% increased {statName}";
        if (modType == "More") return $"{{0}}% more {statName}";
        return $"+{{0}} {statName}";
    }

    private string GenerateRussian(string modType, string rawStat, string statName)
    {
        if (modType == "Flat" && !IsPercentageStat(rawStat)) return $"Добавляет {{0}} к {statName}";
        if (modType == "Flat" && IsPercentageStat(rawStat)) return $"+{{0}}% к {statName}";
        if (modType == "Increase") return $"{{0}}% увеличение {statName}";
        if (modType == "More") return $"{{0}}% больше {statName}";
        return $"+{{0}} {statName}";
    }

    private bool IsPercentageStat(string rawStat)
    {
        return rawStat.Contains("Resist") || 
               rawStat.Contains("Multiplier") || 
               rawStat.Contains("Chance") || 
               rawStat.Contains("Mitigation") ||
               rawStat.Contains("Mult") || // Добавили Mult сюда, чтобы он был +%
               rawStat.Contains("Percent");
    }

    // --- ENGLISH DICTIONARY ---
    private string GetEnglishStatName(string raw)
    {
        return raw switch
        {
            "MaxHealth" => "Maximum Life",
            "MaxMana" => "Maximum Mana",
            "MoveSpeed" => "Movement Speed",
            "CritChance" => "Critical Strike Chance",
            "CritMultiplier" => "Critical Strike Multiplier",
            "MaxMysticShield" => "Maximum Mystic Shield",
            "BleedDamageMult" => "Damage over Time Multiplier for Bleeding",
            "DamagePhysical" => "Physical Damage",
            "Armor" => "Armour",
            "Evasion" => "Evasion Rating",
            "BlockChance" => "Chance to Block",
            "AttackSpeed" => "Attack Speed",
            "CastSpeed" => "Cast Speed",
            "FireResist" => "Fire Resistance",
            "ColdResist" => "Cold Resistance",
            "LightningResist" => "Lightning Resistance",
            "PhysicalResist" => "Physical Damage Reduction",
            // Fallback for generic CamelCase
            _ => Regex.Replace(raw, "(\\B[A-Z])", " $1")
        };
    }

    // --- RUSSIAN DICTIONARY ---
    private string GetRussianStatName(string raw)
    {
        return raw switch
        {
            // Vitals
            "MaxHealth" => "Макс. Здоровье",
            "HealthRegen" => "Регенерация Здоровья",
            "HealthRegenPercent" => "% Регенерации Здоровья",
            "HealthOnHit" => "Здоровье за Удар",
            "HealthOnBlock" => "Здоровье при Блоке",

            "MaxMana" => "Макс. Мана",
            "ManaRegen" => "Регенерация Маны",
            "ManaRegenPercent" => "% Регенерации Маны",
            "ManaOnHit" => "Мана за Удар",
            "ManaOnBlock" => "Мана при Блоке",

            // Defense
            "MaxMysticShield" => "\u041c\u0430\u043a\u0441. \u041c\u0438\u0441\u0442\u0438\u0447\u0435\u0441\u043a\u043e\u0433\u043e \u0429\u0438\u0442\u0430",
            "MysticShieldRechargeDuration" => "\u0421\u043a\u043e\u0440. \u041f\u0435\u0440\u0435\u0437\u0430\u0440\u044f\u0434\u043a\u0438 \u041c\u0438\u0441\u0442\u0438\u0447\u0435\u0441\u043a\u043e\u0433\u043e \u0429\u0438\u0442\u0430",
            "MysticShieldMitigationPercent" => "% \u041f\u043e\u0433\u043b\u043e\u0449\u0435\u043d\u0438\u044f \u041c\u0438\u0441\u0442\u0438\u0447\u0435\u0441\u043a\u043e\u0433\u043e \u0429\u0438\u0442\u0430",
            "MaxMysticShieldMitigationPercent" => "\u041c\u0430\u043a\u0441. % \u041f\u043e\u0433\u043b\u043e\u0449\u0435\u043d\u0438\u044f \u041c\u0438\u0441\u0442\u0438\u0447\u0435\u0441\u043a\u043e\u0433\u043e \u0429\u0438\u0442\u0430",
            
            "Armor" => "Броня",
            "Evasion" => "Уклонение",
            "BlockChance" => "Шанс Блока",
            "MaxBlockChance" => "Макс. Шанс Блока",

            "FireResist" => "Сопр. Огню",
            "ColdResist" => "Сопр. Холоду",
            "LightningResist" => "Сопр. Молнии",
            "PhysicalResist" => "Физ. Сопротивление",
            "MaxFireResist" => "Макс. Сопр. Огню",
            "MaxColdResist" => "Макс. Сопр. Холоду",
            "MaxLightningResist" => "Макс. Сопр. Молнии",
            "MaxPhysicalResist" => "Макс. Физ. Сопр.",

            "MoveSpeed" => "Скорость Бега",
            "JumpForce" => "Сила Прыжка",

            // Damage
            "DamagePhysical" => "Физ. Урон",
            "DamageFire" => "Урон Огнем",
            "DamageCold" => "Урон Холодом",
            "DamageLightning" => "Урон Молнией",

            // Offense
            "AttackSpeed" => "Скор. Атаки",
            "CastSpeed" => "Скор. Каста",
            "ProjectileSpeed" => "Скор. Снарядов",
            "AreaOfEffect" => "Область Действия (AoE)",
            "Duration" => "Длительность",
            "CooldownReduction" => "Снижение КД",
            "CooldownReductionPercent" => "% Снижение КД",

            // Crit
            "Accuracy" => "Меткость",
            "CritChance" => "Шанс Крита",
            "CritMultiplier" => "Множитель Крита",

            // Ailments
            "BleedChance" => "Шанс Кровотечения",
            "BleedDamage" => "Урон Кровотечения",
            "BleedDamageMult" => "Множитель Урона Кровотечения",
            "BleedDuration" => "Длительность Кровотечения",
            "ChanseToAvoidBleed" => "Шанс Избежать Кровотечения",

            "PoisonChance" => "Шанс Отравления",
            "PoisonDamage" => "Урон Ядом",
            "PoisonDamageMult" => "Множитель Урона Ядом",
            "PoisonDuration" => "Длительность Отравления",
            "ChanseToAvoidBleedPoison" => "Шанс Избежать Отравления",

            "IgniteChance" => "Шанс Поджога",
            "IgniteDamage" => "Урон Поджога",
            "IgniteDamageMult" => "Множитель Урона Поджога",
            "IgniteDuration" => "Длительность Поджога",
            "ChanseToAvoidBleedIgnite" => "Шанс Избежать Поджога",

            "FreezeChance" => "Шанс Заморозки",
            "FreezeDuration" => "Длительность Заморозки",
            "ShockChance" => "Шанс Шока",
            "ShockDuration" => "Длительность Шока",
            "ChanseToAvoidBleedShock" => "Шанс Избежать Шока",

            "ReduceDamageTaken" => "Снижение Вход. Урона",

            // Conversion
            "PhysicalToFire" => "Физ. в Огонь",
            "PhysicalToCold" => "Физ. в Холод",
            "PhysicalToLightning" => "Физ. в Молнию",
            "ElementalToPhysical" => "Стихийный в Физ.",
            
            "TakePhysicalAsFire" => "Физ. как Огонь",
            "TakePhysicalAsCold" => "Физ. как Холод",
            "TakePhysicalAsLightning" => "Физ. как Молния",
            "TakeColdAsPhys" => "Холод как Физ.",
            "TakeFireAsPhys" => "Огонь как Физ.",
            "TakeLightningAsPhys" => "Молния как Физ.",

            // Penetration
            "PenetrationPhysical" => "Пробивание Физы",
            "PenetrationFire" => "Пробивание Огня",
            "PenetrationCold" => "Пробивание Холода",
            "PenetrationLightning" => "Пробивание Молнии",

            "ProjectileCount" => "Кол-во Снарядов",
            "ProjectileFork" => "Разветвление Снарядов",
            "ProjectileChain" => "Цепная Реакция",

            _ => raw // Если забыли что-то, вернет английский ключ
        };
    }
}
