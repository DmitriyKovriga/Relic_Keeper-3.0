using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Items.Affixes
{
    [Serializable]
    public class AffixTagEntry
    {
        public string Id;       // Уникальный id тега (используется в ItemAffixSO.TagIds)
        public string NameKey; // Ключ локализации (EN/RU в таблице AffixTags)
    }

    /// <summary>
    /// База тегов аффиксов для крафта и генерации. Локали по NameKey в таблице AffixTags.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Affixes/Affix Tag Database", fileName = "AffixTagDatabase")]
    public class AffixTagDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<AffixTagEntry> _tags = new List<AffixTagEntry>();

        public IReadOnlyList<AffixTagEntry> Tags => _tags;

        public AffixTagEntry GetTag(string id)
        {
            foreach (var t in _tags)
                if (t.Id == id) return t;
            return null;
        }

        public bool HasTag(string id)
        {
            return GetTag(id) != null;
        }

#if UNITY_EDITOR
        public void AddTag(string id, string nameKey)
        {
            if (HasTag(id)) return;
            _tags.Add(new AffixTagEntry { Id = id, NameKey = nameKey ?? id });
        }

        public void RemoveTag(string id)
        {
            _tags.RemoveAll(t => t.Id == id);
        }

        public void EnsureTag(string id, string nameKey)
        {
            var t = GetTag(id);
            if (t != null) { t.NameKey = nameKey ?? id; return; }
            AddTag(id, nameKey);
        }
#endif
    }
}
