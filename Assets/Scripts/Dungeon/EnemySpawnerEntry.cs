using UnityEngine;
using Scripts.Enemies;

namespace Scripts.Dungeon
{
    [System.Serializable]
    public class EnemySpawnerEntry
    {
        [Tooltip("Какой враг может заспавниться")]
        public EnemyDataSO EnemyData;

        [Tooltip("Вес для случайного выбора (чем больше — тем чаще выбирается)")]
        [Min(1)]
        public int Weight = 1;
    }
}
