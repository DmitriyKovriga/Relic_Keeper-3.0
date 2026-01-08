// AI ADDED START
using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Stats
{
    [CreateAssetMenu(menuName = "RPG/Global Base Stats")]
    public class GlobalBaseStatsSO : ScriptableObject
    {
        [Tooltip("Базовые статы, которые получает КАЖДЫЙ персонаж по умолчанию.")]
        [SerializeField] private List<CharacterDataSO.StatConfig> _baseStats;

        // Публичное свойство для доступа
        public List<CharacterDataSO.StatConfig> BaseStats => _baseStats;
    }
}