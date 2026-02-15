using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;

[CreateAssetMenu(menuName = "RPG/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    [field: SerializeField] public string ID { get; private set; }
    
    [Header("Localization")]
    [Tooltip("Ключ в MenuLabels для имени. Например: character.warrior.name")]
    [SerializeField] private string _nameKey;
    [Tooltip("Ключ в MenuLabels для описания. Например: character.warrior.description")]
    [SerializeField] private string _descriptionKey;
    
    [Header("Fallback (если локализация не задана)")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("<DisplayName>k__BackingField")] private string _displayNameFallback;
    [TextArea, UnityEngine.Serialization.FormerlySerializedAs("Description")] [SerializeField] private string _descriptionFallback;

    [Header("Visual")]
    [Tooltip("Портрет/спрайт персонажа для UI найма и хостела")]
    [SerializeField] private Sprite _portrait;

    [Header("Starting Stats Configuration")]
    [Tooltip("Добавь сюда только те статы, которые отличаются от стандартных.")]
    [SerializeField] private List<StatConfig> _startingStats;

    [Header("Passive Tree")]
    [Tooltip("Дерево пассивных навыков этого персонажа")]
    [SerializeField] private PassiveSkillTreeSO _passiveTree;

    /// <summary>Ключ локализации имени. Генерируется из ID: character.{id}.name</summary>
    public string NameKey => !string.IsNullOrEmpty(_nameKey) ? _nameKey : $"character.{ID ?? name}.name";
    /// <summary>Ключ локализации описания. Генерируется из ID: character.{id}.description</summary>
    public string DescriptionKey => !string.IsNullOrEmpty(_descriptionKey) ? _descriptionKey : $"character.{ID ?? name}.description";
    /// <summary>Имя для отображения (fallback, когда локализация не используется).</summary>
    public string DisplayName => !string.IsNullOrEmpty(_displayNameFallback) ? _displayNameFallback : (ID ?? "Unknown");
    /// <summary>Описание для отображения (fallback, когда локализация не загружена).</summary>
    public string DescriptionFallback => _descriptionFallback ?? "";
    public Sprite Portrait => _portrait;
    public PassiveSkillTreeSO PassiveTree => _passiveTree;

    public List<StatConfig> StartingStats => _startingStats ??= new List<StatConfig>();

    [System.Serializable]
    public struct StatConfig
    {
        public StatType Type;
        public float Value;
    }

#if UNITY_EDITOR
    public void SetNameKey(string value) => _nameKey = value;
    public void SetDescriptionKey(string value) => _descriptionKey = value;
    public void SetDisplayNameFallback(string value) => _displayNameFallback = value;
    public void SetDescriptionFallback(string value) => _descriptionFallback = value;
    public void SetPortrait(Sprite value) => _portrait = value;
    public void SetPassiveTree(PassiveSkillTreeSO value) => _passiveTree = value;
#endif
}