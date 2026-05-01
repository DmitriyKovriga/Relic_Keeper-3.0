using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "RPG/Dungeons/Dungeon Data", fileName = "Dungeon_")]
    public class DungeonDataSO : ScriptableObject
    {
        [Header("Info")]
        public string ID;
        public string DisplayName;

        [Header("Levels")]
        [Tooltip("Level range for this dungeon.")]
        public int MinLevel = 1;
        public int MaxLevel = 10;

        [Header("Rooms")]
        [Min(1)] public int RoomCount = 10;
        [Tooltip("Room prefab paths in Resources. Example: Prefabs/Dungeon/MineDungeon/MineRoom_001")]
        [SerializeField] private List<string> _normalRoomPrefabPaths = new List<string>();
        [Tooltip("Boss room prefab path in Resources.")]
        [SerializeField] private string _bossRoomPrefabPath;

        [Header("Presentation")]
        [Tooltip("Background sprite path in Resources. Example: Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-background")]
        [SerializeField] private string _backgroundSpriteResourcePath;

        public IReadOnlyList<string> NormalRoomPrefabPaths => _normalRoomPrefabPaths;
        public string BossRoomPrefabPath => _bossRoomPrefabPath;
        public string BackgroundSpriteResourcePath => _backgroundSpriteResourcePath;

        public GameObject LoadRoomPrefab(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string normalized = NormalizeResourcesPath(path);
            var prefab = Resources.Load<GameObject>(normalized);
            if (prefab == null)
                Debug.LogWarning($"[DungeonDataSO] Failed to load room prefab. RawPath='{path}', Normalized='{normalized}'.");

            return prefab;
        }

        public Sprite LoadBackgroundSprite()
        {
            if (string.IsNullOrWhiteSpace(_backgroundSpriteResourcePath))
                return null;

            string normalized = NormalizeResourcesPath(_backgroundSpriteResourcePath);

            var directSprite = Resources.Load<Sprite>(normalized);
            if (directSprite != null)
                return directSprite;

            var sprites = Resources.LoadAll<Sprite>(normalized);
            if (sprites != null && sprites.Length > 0)
                return sprites[0];

            Debug.LogWarning($"[DungeonDataSO] Failed to load background sprite. RawPath='{_backgroundSpriteResourcePath}', Normalized='{normalized}'.");
            return null;
        }

        private static string NormalizeResourcesPath(string path)
        {
            string p = path.Replace('\\', '/').Trim();

            int resourcesIdx = p.IndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
            if (resourcesIdx >= 0)
                p = p.Substring(resourcesIdx + "Resources/".Length);

            if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(0, p.Length - ".prefab".Length);

            if (p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(0, p.Length - ".png".Length);

            return p;
        }
    }
}
