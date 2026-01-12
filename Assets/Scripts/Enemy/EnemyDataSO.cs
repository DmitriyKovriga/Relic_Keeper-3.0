using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;

namespace Scripts.Enemies
{
    [CreateAssetMenu(menuName = "RPG/Enemies/Enemy Data")]
    public class EnemyDataSO : ScriptableObject
    {
        [Header("Info")]
        public string ID;
        public string DisplayName;
        
        [Header("Base Stats")]
        // Используем список, чтобы гибко настраивать: одному ХП и Броню, другому ХП и Уклонение
        public List<CharacterDataSO.StatConfig> BaseStats;

        [Header("Rewards")]
        public float XPReward = 10f;
    }
}