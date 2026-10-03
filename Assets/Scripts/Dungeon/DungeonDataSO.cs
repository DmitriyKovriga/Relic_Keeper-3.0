using System;
using System.Collections.Generic;
using Scripts.UI;
using UnityEngine;

namespace Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "RPG/Dungeons/Dungeon Data", fileName = "Dungeon_")]
    public class DungeonDataSO : ScriptableObject
    {
        [Header("Info")]
        public string ID;
        public string DisplayName;
        [Tooltip("Ключ в таблице MenuLabels для подписи над порталом входа. Если пусто — используется DisplayName.")]
        public string NameLocalizationKey;

        [Header("Levels")]
        [Tooltip("Level range for this dungeon.")]
        public int MinLevel = 1;
        public int MaxLevel = 10;

        [Header("Rooms")]
        [Min(1)] public int RoomCount = 10;
        [Tooltip("Эти комнаты идут первыми при входе с начала данжа, сверху вниз. Пустые строки пропускаются. Если игрок начинает с более поздней комнаты, уже пройденные из этого списка не повторяются.")]
        [SerializeField] private List<string> _openingRoomPrefabPaths = new List<string>();
        [Tooltip("Room prefab paths in Resources. Example: Prefabs/Dungeon/MineDungeon/MineRoom_001")]
        [SerializeField] private List<string> _normalRoomPrefabPaths = new List<string>();
        [Tooltip("Boss room prefab path in Resources.")]
        [SerializeField] private string _bossRoomPrefabPath;

        [Header("Dungeon Modifiers")]
        [Tooltip("Всегда действуют во всех комнатах этого данжа.")]
        [SerializeField] private List<DungeonModifierSO> _builtInModifiers = new List<DungeonModifierSO>();
        [Tooltip("Из этого пула игрок выбирает один глобальный модификатор при входе. Пустой список отключает выбор.")]
        [SerializeField] private List<DungeonModifierSO> _entryModifierPool = new List<DungeonModifierSO>();
        [Tooltip("Из этого пула предлагаются три усиления перед следующей комнатой.")]
        [SerializeField] private List<DungeonModifierSO> _roomModifierPool = new List<DungeonModifierSO>();
        [SerializeField, Range(1, 5)] private int _entryChoiceCount = 3;
        [SerializeField, Range(1, 5)] private int _roomChoiceCount = 3;

        [Header("Presentation")]
        [Tooltip("Background sprite path in Resources. Example: Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-background")]
        [SerializeField] private string _backgroundSpriteResourcePath;

        public IReadOnlyList<string> OpeningRoomPrefabPaths => _openingRoomPrefabPaths;
        public IReadOnlyList<string> NormalRoomPrefabPaths => _normalRoomPrefabPaths;
        public string BossRoomPrefabPath => _bossRoomPrefabPath;
        public string BackgroundSpriteResourcePath => _backgroundSpriteResourcePath;
        public IReadOnlyList<DungeonModifierSO> BuiltInModifiers => _builtInModifiers;
        public IReadOnlyList<DungeonModifierSO> EntryModifierPool => _entryModifierPool;
        public IReadOnlyList<DungeonModifierSO> RoomModifierPool => _roomModifierPool;
        public int EntryChoiceCount => Mathf.Max(1, _entryChoiceCount);
        public int RoomChoiceCount => Mathf.Max(1, _roomChoiceCount);

        public static int AppendOpeningRooms(
            IReadOnlyList<string> openingRooms,
            int roomsCompletedBeforeSegment,
            int slots,
            List<string> sequence)
        {
            if (openingRooms == null || sequence == null || slots <= 0)
                return 0;

            int completed = Mathf.Max(0, roomsCompletedBeforeSegment);
            int placed = 0;
            int openingIndex = 0;
            for (int i = 0; i < openingRooms.Count && placed < slots; i++)
            {
                if (string.IsNullOrEmpty(openingRooms[i]))
                    continue;

                if (openingIndex >= completed)
                {
                    sequence.Add(openingRooms[i]);
                    placed++;
                }

                openingIndex++;
            }

            return placed;
        }

        public string GetLocalizedDisplayName()
        {
            string fallback = string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;
            return RuntimeLocalization.Resolve(NameLocalizationKey, fallback, fallback);
        }

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
