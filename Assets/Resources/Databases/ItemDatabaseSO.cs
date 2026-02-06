using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;

[CreateAssetMenu(menuName = "RPG/Database/Item Database")]
public class ItemDatabaseSO : ScriptableObject
{
    public List<EquipmentItemSO> AllItems = new List<EquipmentItemSO>();
    public List<ItemAffixSO> AllAffixes = new List<ItemAffixSO>();
    public List<SkillDataSO> AllSkills = new List<SkillDataSO>();

    private Dictionary<string, EquipmentItemSO> _itemLookup;
    private Dictionary<string, ItemAffixSO> _affixLookup;
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

            // 2. Инициализация АФФИКСОВ
            _affixLookup = new Dictionary<string, ItemAffixSO>();
            
            if (AllAffixes != null)
            {
                foreach (var affix in AllAffixes)
                {
                    if (affix == null) continue;

                    // Используем UniqueID (путь), если есть, иначе имя файла
                    string key = string.IsNullOrEmpty(affix.UniqueID) ? affix.name : affix.UniqueID;

                    if (!_affixLookup.ContainsKey(key))
                    {
                        _affixLookup.Add(key, affix);
                    }
                }
            }

            _skillLookup = new Dictionary<string, SkillDataSO>();
            if (AllSkills != null)
            {
                foreach(var skill in AllSkills)
                {
                    if (skill != null && !string.IsNullOrEmpty(skill.ID) && !_skillLookup.ContainsKey(skill.ID))
                        _skillLookup.Add(skill.ID, skill);
                }
            }
            Debug.Log($"[ItemDatabase] Initialized. Items: {_itemLookup.Count}, Affixes: {_affixLookup.Count}");
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
            if (_affixLookup == null) Init();
            
            if (_affixLookup == null) return null;
            if (string.IsNullOrEmpty(id)) return null;

            if (_affixLookup.TryGetValue(id, out var affix))
            {
                return affix;
            }
            
            // Лог можно убрать, если часто спамит при смене версий игры
            Debug.LogWarning($"[ItemDatabase] Аффикс с ID '{id}' не найден в базе!");
            return null;
        }

        public SkillDataSO GetSkill(string id)
        {
            if (_skillLookup == null) Init();
            if (_skillLookup == null) return null;
            if (string.IsNullOrEmpty(id)) return null;
            if (_skillLookup.TryGetValue(id, out var skill))
                return skill;
            Debug.LogWarning($"[ItemDatabase] Скилл с ID '{id}' не найден в базе!");
            return null;
        }
}