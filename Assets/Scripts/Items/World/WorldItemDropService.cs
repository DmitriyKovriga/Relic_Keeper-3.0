using Scripts.Inventory;
using Scripts.Dungeon;
using UnityEngine;

namespace Scripts.Items.World
{
    public static class WorldItemDropService
    {
        private const int GroundLayerMask = 1 << 6;
        private const float PixelsPerUnit = 24f;
        private const float GroundLift = 0.62f;
        private const float PlayerRaycastLift = 0.5f;
        private const float RaycastDown = 12f;

        private static Transform _runtimeRoot;

        public static WorldDroppedItem Spawn(InventoryItem item, Vector2 position)
        {
            if (item?.Data == null)
                return null;

            var go = new GameObject($"DroppedItem_{item.Data.ID}");
            go.transform.SetParent(ResolveDropParent(), true);
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var dropped = go.AddComponent<WorldDroppedItem>();
            dropped.Initialize(item, PixelsPerUnit);
            return dropped;
        }

        public static WorldDroppedItem SpawnOnGround(InventoryItem item, Vector2 position)
        {
            if (item?.Data == null)
                return null;

            return Spawn(item, ProjectToGroundUnder(position));
        }

        public static bool TryDropAtPlayer(InventoryItem item)
        {
            if (item?.Data == null)
                return false;

            return Spawn(item, ResolveDropPositionAtPlayer()) != null;
        }

        public static Vector2 ResolveDropPositionAtPlayer()
        {
            Transform player = FindPlayerTransform();
            Vector2 origin = player != null ? (Vector2)player.position : Vector2.zero;
            return ProjectToGroundUnder(origin);
        }

        internal static Vector2 ProjectToGroundUnder(Vector2 origin)
        {
            int mask = BuildGroundMask();
            Vector2 rayOrigin = origin + Vector2.up * PlayerRaycastLift;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, PlayerRaycastLift + RaycastDown, mask);
            if (hit.collider != null)
                return hit.point + Vector2.up * GroundLift;

            return origin + Vector2.up * GroundLift;
        }

        private static int BuildGroundMask()
        {
            int mask = GroundLayerMask;
            int oneWayPlatformLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (oneWayPlatformLayer >= 0)
                mask |= 1 << oneWayPlatformLayer;
            return mask;
        }

        private static Transform FindPlayerTransform()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                return player.transform;

            var movement = Object.FindFirstObjectByType<PlayerMovement>();
            return movement != null ? movement.transform : null;
        }

        private static void EnsureRoot()
        {
            if (_runtimeRoot != null)
                return;

            var root = GameObject.Find("WorldDroppedItems");
            if (root == null)
                root = new GameObject("WorldDroppedItems");
            _runtimeRoot = root.transform;
        }

        private static Transform ResolveDropParent()
        {
            RoomController room = Object.FindFirstObjectByType<RoomController>();
            if (room != null && room.gameObject.activeInHierarchy)
                return room.transform;

            EnsureRoot();
            return _runtimeRoot;
        }
    }
}
