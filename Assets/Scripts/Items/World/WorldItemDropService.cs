using Scripts.Inventory;
using Scripts.Dungeon;
using UnityEngine;

namespace Scripts.Items.World
{
    public static class WorldItemDropService
    {
        private const int GroundLayerMask = 1 << 6;
        private const float PixelsPerUnit = 24f;
        private const float GroundLift = 0.08f;
        private const float RaycastUp = 3f;
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

        public static bool TryDropFromScreen(InventoryItem item, Vector2 screenPosition)
        {
            if (item?.Data == null)
                return false;

            Vector2 dropPosition = ResolveDropPosition(screenPosition);
            return Spawn(item, dropPosition) != null;
        }

        public static Vector2 ResolveDropPosition(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            Transform player = FindPlayerTransform();

            if (camera == null)
            {
                Vector2 fallback = player != null ? player.position : Vector3.zero;
                return ProjectToGround(fallback, fallback);
            }

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
            Vector2 desired = new Vector2(world.x, world.y);
            Vector2 fallbackPosition = player != null ? player.position : desired;
            return ProjectToGround(desired, fallbackPosition);
        }

        private static Vector2 ProjectToGround(Vector2 desiredPosition, Vector2 fallbackPosition)
        {
            int mask = BuildGroundMask();
            Vector2 origin = desiredPosition + Vector2.up * RaycastUp;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, RaycastUp + RaycastDown, mask);
            if (hit.collider != null)
                return hit.point + Vector2.up * GroundLift;

            origin = fallbackPosition + Vector2.up * RaycastUp;
            hit = Physics2D.Raycast(origin, Vector2.down, RaycastUp + RaycastDown, mask);
            if (hit.collider != null)
                return hit.point + Vector2.up * GroundLift;

            return fallbackPosition;
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
