using Scripts.Dungeon;
using Scripts.Inventory;
using UnityEngine;

namespace Scripts.Items.World
{
    /// <summary>Owns the room's single loot container beside its exit, independently of drop positions.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RoomController))]
    public sealed class RoomLootCollector : MonoBehaviour
    {
        [SerializeField, Tooltip("World-space offset from the next-room portal before snapping to the floor.")]
        private Vector2 _exitOffset = new Vector2(-3f, 0f);
        [SerializeField, Min(0.75f)] private float _minimumExitDistance = 1.25f;
        [SerializeField, Min(0f)] private float _maximumFloorHeightDifference = 1f;

        private const float FloorSearchStep = 0.25f;
        private const float SupportHalfWidth = 0.4f;

        private WorldLootCache _cache;

        public WorldLootCache Cache => _cache;

        public bool TryStore(InventoryItem item, Vector3? sourcePosition = null, bool isPlayerDiscard = false)
        {
            if (item?.Data == null)
                return false;

            if (_cache == null || !_cache.CanAcceptItems)
                _cache = WorldLootCache.Create(ResolveCachePosition(), transform);

            // Insert the actual rolled item; filtering changes visibility, never ownership.
            int previousCount = _cache.Count;
            if (!_cache.TryAddItemPreferringTab(item, _cache.CurrentTabIndex))
                return false;

            if (Application.isPlaying && !isPlayerDiscard && sourcePosition.HasValue && _cache.Count > previousCount &&
                !LootFilterSettings.ShouldHide(item))
                WorldLootCacheFlightVisual.Spawn(sourcePosition.Value, _cache, item);
            return true;
        }

        public Vector2 ResolveCachePosition()
        {
            DungeonPortal exit = null;
            foreach (DungeonPortal portal in GetComponentsInChildren<DungeonPortal>(true))
            {
                // Locked/inactive exit portals already have their final room position.
                if (portal.Type == PortalType.NextRoom)
                {
                    exit = portal;
                    break;
                }
                if (portal.Type == PortalType.ReturnToHub)
                    exit = portal;
            }

            // Boss rooms use their return portal; unfinished room templates use the player spawn.
            Vector2 origin = exit != null
                ? (Vector2)exit.transform.position
                : (Vector2)GetComponent<RoomController>().PlayerSpawnPosition;

            if (exit != null && WorldItemDropService.TrySnapToGround(origin, out Vector2 exitFloor))
            {
                float minimum = Mathf.Max(0.75f, _minimumExitDistance);
                float preferred = Mathf.Max(minimum, Mathf.Abs(_exitOffset.x));
                int steps = Mathf.CeilToInt((preferred - minimum) / FloorSearchStep);
                for (int i = 0; i <= steps; i++)
                {
                    float distance = Mathf.Max(minimum, preferred - i * FloorSearchStep);
                    Vector2 candidate = origin + new Vector2(-distance, _exitOffset.y);
                    if (!WorldItemDropService.TrySnapToGround(candidate, out Vector2 floor) ||
                        Mathf.Abs(floor.y - exitFloor.y) > _maximumFloorHeightDifference)
                        continue;
                    // Both sides must fit on the platform rather than overhanging a ledge.
                    if (HasSupport(candidate + Vector2.left * SupportHalfWidth, floor.y) &&
                        HasSupport(candidate + Vector2.right * SupportHalfWidth, floor.y))
                        return floor;
                }
            }
            return WorldItemDropService.ProjectToGroundUnder(origin + _exitOffset);
        }

        private static bool HasSupport(Vector2 origin, float height)
        {
            return WorldItemDropService.TrySnapToGround(origin, out Vector2 floor) &&
                   Mathf.Abs(floor.y - height) <= FloorSearchStep;
        }
    }
}
