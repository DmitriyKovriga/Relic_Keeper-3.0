using UnityEngine;

namespace Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "RK/Dungeon/Service NPC Arrival")]
    public sealed class DungeonServiceArrivalSettings : ScriptableObject
    {
        public const string ResourcePath = "Dungeons/ServiceNpcArrival";
        public Sprite PortalSprite;
        public RuntimeAnimatorController PortalController;
        public Material SpriteMaterial;
        public Sprite[] MarketWalk;
        public Sprite[] StashWalk;
        [Min(0)] public float NpcRootHeight = 0.5f;
        [Min(0)] public float WalkDistance = 1f;
        [Min(0.01f)] public float WalkDuration = 1.2f;
        [Min(0.01f)] public float WalkFrameDuration = 0.12f;
        [Min(0.01f)] public float PortalOpenDuration = 0.3f;
        [Min(0.01f)] public float PortalCloseDuration = 0.3f;
        [Min(0)] public float MaximumFloorStep = 0.1f;
    }
}
