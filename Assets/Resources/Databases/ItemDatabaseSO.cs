using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;

[CreateAssetMenu(menuName = "RPG/Database/Item Database")]
public class ItemDatabaseSO : ScriptableObject
{
    [Header("Enemy Loot Chances")]
    [Range(0f, 1f)] public float CommonItemDropChance = 0.10f;
    [Range(0f, 1f)] public float MagicItemDropChance = 0.05f;
    [Range(0f, 1f)] public float RareItemDropChance = 0.02f;

    [Header("Database Contents")]
    public List<EquipmentItemSO> AllItems = new List<EquipmentItemSO>();
    public List<ItemAffixSO> AllAffixes = new List<ItemAffixSO>();
    public List<SkillDataSO> AllSkills = new List<SkillDataSO>();

    private Dictionary<string, EquipmentItemSO> _itemLookup;
    private Dictionary<string, ItemAffixSO> _affixLookup;
    private Dictionary<string, int> _legacyAffixTierLookup;
    private Dictionary<string, SkillDataSO> _skillLookup;

    public void Init()
        {
            // 1. Инициализация ПРЕДМЕТОВ
            _itemLookup = new Dictionary<string, EquipmentItemSO>();
            
            if (AllItems != null)
            {
                foreach (var item in AllItems)
                {
                    if (item == null) continue;
                    if (string.IsNullOrEmpty(item.ID)) 
                    {
                        Debug.LogWarning($"[ItemDatabase] Предмет '{item.name}' не имеет ID! Пропускаем.");
                        continue;
                    }

                    if (!_itemLookup.ContainsKey(item.ID))
                    {
                        _itemLookup.Add(item.ID, item);
                    }
                }
            }

            // 2. Инициализация АФФИКСОВ (список + подгрузка из Resources для сгенерированных)
            _affixLookup = new Dictionary<string, ItemAffixSO>();
            _legacyAffixTierLookup = new Dictionary<string, int>();
            
            if (AllAffixes != null)
            {
                foreach (var affix in AllAffixes)
                {
                    if (affix == null) continue;
                    RegisterAffixLookupKeys(affix);
                }
            }
            // Подгрузить аффиксы из Resources/Affixes, чтобы сгенерированные были в базе без ручного Auto-Find
            var fromResources = Resources.LoadAll<ItemAffixSO>("Affixes");
            if (fromResources != null)
            {
                foreach (var affix in fromResources)
                {
                    if (affix == null) continue;
                    RegisterAffixLookupKeys(affix);
                }
            }

            _skillLookup = new Dictionary<string, SkillDataSO>();
            if (AllSkills != null)
            {
                foreach(var skill in AllSkills)
                {
                    RegisterSkillLookupKeys(skill);
                }
            }
            RegisterSkillsFromResources();

            Debug.Log($"[ItemDatabase] Initialized. Items: {_itemLookup.Count}, Affixes: {_affixLookup.Count}, Skills: {_skillLookup.Count}");
        }

    public EquipmentItemSO GetItem(string id)
        {
            // Защита: если словарь пуст, пробуем инициализировать
            if (_itemLookup == null) Init();
            
            // Вторая защита: если даже после Init он null (странно, но бывает), возвращаем null
            if (_itemLookup == null) return null;
            if (string.IsNullOrEmpty(id)) return null;

            if (_itemLookup.TryGetValue(id, out var item))
            {
                return item;
            }
            
            Debug.LogWarning($"[ItemDatabase] Предмет с ID '{id}' не найден в базе!");
            return null;
        }

        public ItemAffixSO GetAffix(string id)
        {
            return TryResolveAffix(id, out ItemAffixSO affix, out _) ? affix : null;
        }

        public bool TryResolveAffix(string id, out ItemAffixSO affix, out int tier)
        {
            if (_affixLookup == null) Init();

            affix = null;
            tier = 0;
            if (_affixLookup == null || string.IsNullOrEmpty(id)) return false;

            if (_affixLookup.TryGetValue(id, out affix))
            {
                if (_legacyAffixTierLookup != null)
                    _legacyAffixTierLookup.TryGetValue(id, out tier);
                if (tier <= 0 && affix != null)
                    tier = affix.GetDefaultTier();
                return affix != null;
            }

            Debug.LogWarning($"[ItemDatabase] Аффикс с ID '{id}' не найден в базе!");
            return false;
        }

        private void RegisterAffixLookupKeys(ItemAffixSO affix)
        {
            if (affix == null) return;

            RegisterAffixLookupKey(affix.UniqueID, affix, 0);
            RegisterAffixLookupKey(affix.GroupID, affix, 0);
            RegisterAffixLookupKey(affix.name, affix, 0);

            if (affix.LegacyTierIds == null) return;
            foreach (ItemAffixSO.LegacyTierId legacy in affix.LegacyTierIds)
                RegisterAffixLookupKey(legacy.Id, affix, legacy.Tier);
        }

        private void RegisterAffixLookupKey(string key, ItemAffixSO affix, int tier)
        {
            if (string.IsNullOrWhiteSpace(key) || affix == null) return;
            string normalized = key.Trim();
            if (!_affixLookup.ContainsKey(normalized))
                _affixLookup.Add(normalized, affix);
            if (tier > 0 && !_legacyAffixTierLookup.ContainsKey(normalized))
                _legacyAffixTierLookup.Add(normalized, tier);
        }

        public SkillDataSO GetSkill(string id)
        {
            if (_skillLookup == null) Init();
            if (_skillLookup == null) return null;
            if (string.IsNullOrEmpty(id)) return null;
            if (_skillLookup.TryGetValue(id, out var skill))
                return skill;

            RegisterSkillsFromResources();
            if (_skillLookup.TryGetValue(id, out skill))
                return skill;
            Debug.LogWarning($"[ItemDatabase] Скилл с ID '{id}' не найден в базе!");
            return null;
        }

        private void RegisterSkillsFromResources()
        {
            var skillsFromResources = Resources.LoadAll<SkillDataSO>("Skills");
            if (skillsFromResources == null)
                return;

            foreach (var skill in skillsFromResources)
                RegisterSkillLookupKeys(skill);
        }

        private void RegisterSkillLookupKeys(SkillDataSO skill)
        {
            if (skill == null)
                return;

            RegisterSkillLookupKey(skill.ID, skill);
            RegisterSkillLookupKey(skill.name, skill);
            RegisterSkillLookupKey(skill.SkillName, skill);
            RegisterSkillLookupKey(skill.NameKey, skill);
        }

        private void RegisterSkillLookupKey(string key, SkillDataSO skill)
        {
            if (string.IsNullOrWhiteSpace(key) || skill == null)
                return;

            string normalizedKey = key.Trim();
            if (!_skillLookup.ContainsKey(normalizedKey))
                _skillLookup.Add(normalizedKey, skill);
        }
}
