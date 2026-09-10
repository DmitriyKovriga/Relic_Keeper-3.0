using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Optional art layer for dungeon UI. Functional layout remains usable while these sprites are empty.
    /// Assign pixel-art frames here during the decoration pass without changing UI logic.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Dungeons/UI Presentation", fileName = "DungeonUIPresentation")]
    public sealed class DungeonUIPresentationSO : ScriptableObject
    {
        public const string ResourcePath = "Dungeons/DungeonUIPresentation";

        [Header("Modifier choice")]
        public Sprite ChoiceWindowFrame;
        public Sprite ChoiceCardFrame;
        public Sprite ChoiceImagePlaceholder;

        [Header("Room HUD")]
        public Sprite HudFrame;
        public Sprite HudSectionFrame;

        private static DungeonUIPresentationSO _cached;

        public static DungeonUIPresentationSO Load()
        {
            if (_cached == null)
                _cached = Resources.Load<DungeonUIPresentationSO>(ResourcePath);
            return _cached;
        }
    }
}
