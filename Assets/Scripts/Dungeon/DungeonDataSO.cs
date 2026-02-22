using UnityEngine;
using System.Collections.Generic;
using System;

namespace Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "RPG/Dungeons/Dungeon Data", fileName = "Dungeon_")]
    public class DungeonDataSO : ScriptableObject
    {
        [Header("Info")]
        public string ID;
        public string DisplayName;

        [Header("Levels")]
        [Tooltip("Уровень 1–10 для первого данжа, 10–20 для второго и т.д.")]
        public int MinLevel = 1;
        public int MaxLevel = 10;

        [Header("Rooms")]
        [Min(1)] public int RoomCount = 10;
        [Tooltip("Пути к префабам комнат в Resources (без .prefab). Пример: Prefabs/Dungeon/MineDungeon/MineRoom_001")]
        [SerializeField] private List<string> _normalRoomPrefabPaths = new List<string>();
        [Tooltip("Путь к префабу босс-комнаты в Resources")]
        [SerializeField] private string _bossRoomPrefabPath;

        public IReadOnlyList<string> NormalRoomPrefabPaths => _normalRoomPrefabPaths;
        public string BossRoomPrefabPath => _bossRoomPrefabPath;

        /// <summary>Загружает префаб комнаты по пути. Путь относительно папки Resources.</summary>
        public GameObject LoadRoomPrefab(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string normalized = NormalizeResourcesPath(path);
            var prefab = Resources.Load<GameObject>(normalized);
            if (prefab == null)
            {
                Debug.LogWarning($"[DungeonDataSO] Failed to load room prefab. RawPath='{path}', Normalized='{normalized}'.");
            }
            return prefab;
        }

        private static string NormalizeResourcesPath(string path)
        {
            string p = path.Replace('\\', '/').Trim();

            // Allow full asset path: Assets/Resources/Prefabs/Foo.prefab -> Prefabs/Foo
            int resourcesIdx = p.IndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
            if (resourcesIdx >= 0)
            {
                p = p.Substring(resourcesIdx + "Resources/".Length);
            }

            if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                p = p.Substring(0, p.Length - ".prefab".Length);
            }

            return p;
        }
    }
}
