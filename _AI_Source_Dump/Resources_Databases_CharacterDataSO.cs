using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats; // Подключаем namespace, где лежит Enum

[CreateAssetMenu(menuName = "RPG/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    [field: SerializeField] public string ID { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: TextArea] [SerializeField] public string Description;

    [Header("Starting Stats Configuration")]
    [Tooltip("Добавь сюда только те статы, которые отличаются от стандартных.")]
    [SerializeField] private List<StatConfig> _startingStats;

    // Публичное свойство для чтения
    public List<StatConfig> StartingStats => _startingStats;

    // Вспомогательная структура для удобства в Инспекторе
    [System.Serializable]
    public struct StatConfig
    {
        public StatType Type;
        public float Value;
    }
}