using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Marker for a hub portal that opens Mortfall floor-skip selection.
    /// Attach this to the FloorPortal object, or just name that object FloorPortal.
    /// </summary>
    public sealed class FloorPortal : MonoBehaviour
    {
        public const string ObjectName = "FloorPortal";
        public const string DefaultDungeonResourcePath = "Dungeons/MortFallDungeonSO";

        [SerializeField] private DungeonDataSO _targetDungeon;

        public DungeonDataSO TargetDungeon =>
            _targetDungeon != null
                ? _targetDungeon
                : Resources.Load<DungeonDataSO>(DefaultDungeonResourcePath);
    }
}
