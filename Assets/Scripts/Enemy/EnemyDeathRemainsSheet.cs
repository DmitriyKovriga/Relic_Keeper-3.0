using Scripts.Dungeon;
using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>
    /// One remains sheet per room. All corpse sprites parent here and take packed sorting orders.
    /// </summary>
    public sealed class EnemyDeathRemainsSheet : MonoBehaviour
    {
        public const string ObjectName = "EnemyDeathRemainsSheet";

        private readonly EnemyDeathRemainsLayer _layer = new EnemyDeathRemainsLayer();

        public EnemyDeathRemainsLayer Layer => _layer;
        public int AllocateSpriteOrder() => _layer.AllocateSpriteOrder();

        public static EnemyDeathRemainsSheet GetOrCreate(Transform from)
        {
            Transform roomRoot = ResolveRoomRoot(from);
            if (roomRoot != null)
            {
                Transform existing = roomRoot.Find(ObjectName);
                if (existing != null)
                {
                    EnemyDeathRemainsSheet sheet = existing.GetComponent<EnemyDeathRemainsSheet>();
                    if (sheet != null)
                        return sheet;
                }

                var host = new GameObject(ObjectName);
                host.transform.SetParent(roomRoot, false);
                host.transform.localPosition = Vector3.zero;
                host.layer = roomRoot.gameObject.layer;
                return host.AddComponent<EnemyDeathRemainsSheet>();
            }

            EnemyDeathRemainsSheet loose = FindFirstObjectByType<EnemyDeathRemainsSheet>();
            if (loose != null)
                return loose;

            var fallback = new GameObject(ObjectName);
            return fallback.AddComponent<EnemyDeathRemainsSheet>();
        }

        private static Transform ResolveRoomRoot(Transform from)
        {
            if (from == null)
                return null;

            RoomController room = from.GetComponentInParent<RoomController>();
            return room != null ? room.transform : from;
        }
    }
}
