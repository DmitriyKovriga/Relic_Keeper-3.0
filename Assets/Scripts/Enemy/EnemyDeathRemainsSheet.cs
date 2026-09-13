using System.Collections.Generic;
using Scripts.Dungeon;
using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>
    /// One remains sheet per room. The newest kills always receive the full visual effect;
    /// when the budget is full, the oldest remains are recycled into the pool first.
    /// </summary>
    public sealed class EnemyDeathRemainsSheet : MonoBehaviour
    {
        public const string ObjectName = "EnemyDeathRemainsSheet";
        private const int MaxActiveFragments = 96;
        private const int MaxActiveDecals = 180;

        private readonly EnemyDeathRemainsLayer _layer = new EnemyDeathRemainsLayer();
        private readonly Queue<EnemyDeathFragment> _fragmentAgeQueue = new Queue<EnemyDeathFragment>();
        private readonly Queue<EnemyDeathDecal> _decalAgeQueue = new Queue<EnemyDeathDecal>();
        private EnemyDeathVfxPool _pool;
        private int _activeFragments;
        private int _activeDecals;

        private void Awake()
        {
            EnemyDeathVisualFactory.Prewarm();
        }

        public EnemyDeathRemainsLayer Layer => _layer;
        public int AllocateSpriteOrder() => _layer.AllocateSpriteOrder();
        public EnemyDeathVfxPool Pool
        {
            get
            {
                if (_pool == null)
                    _pool = GetComponent<EnemyDeathVfxPool>();
                if (_pool == null)
                    _pool = gameObject.AddComponent<EnemyDeathVfxPool>();
                return _pool;
            }
        }

        public int ActiveFragments => _activeFragments;
        public int ActiveDecals => _activeDecals;

        // Full-quality new deaths are retained; age determines what is removed, not quality.
        public DeathEffectQuality GetQuality() => DeathEffectQuality.Full;

        public bool TryReserveFragment()
        {
            if (_activeFragments >= MaxActiveFragments && !EvictOldestFragment())
                return false;
            _activeFragments++;
            return true;
        }

        public bool TryReserveDecal()
        {
            if (_activeDecals >= MaxActiveDecals && !EvictOldestDecal())
                return false;
            _activeDecals++;
            return true;
        }

        public void RegisterFragment(EnemyDeathFragment fragment)
        {
            if (fragment != null)
                _fragmentAgeQueue.Enqueue(fragment);
        }

        public void RegisterDecal(EnemyDeathDecal decal)
        {
            if (decal != null)
                _decalAgeQueue.Enqueue(decal);
        }

        public void ReleaseFragment() => _activeFragments = Mathf.Max(0, _activeFragments - 1);
        public void ReleaseDecal() => _activeDecals = Mathf.Max(0, _activeDecals - 1);

        private bool EvictOldestFragment()
        {
            while (_fragmentAgeQueue.Count > 0)
            {
                EnemyDeathFragment oldest = _fragmentAgeQueue.Dequeue();
                if (oldest == null || !oldest.IsActiveEffect)
                    continue;
                oldest.ForceReturnToPool();
                return true;
            }
            return false;
        }

        private bool EvictOldestDecal()
        {
            while (_decalAgeQueue.Count > 0)
            {
                EnemyDeathDecal oldest = _decalAgeQueue.Dequeue();
                if (oldest == null || !oldest.IsActiveEffect)
                    continue;
                oldest.ForceReturnToPool();
                return true;
            }
            return false;
        }

        public static EnemyDeathRemainsSheet GetOrCreate(Transform from)
        {
            Transform roomRoot = ResolveRoomRoot(from);
            if (roomRoot != null)
            {
                Transform existing = roomRoot.Find(ObjectName);
                if (existing != null && existing.TryGetComponent(out EnemyDeathRemainsSheet sheet))
                    return sheet;

                var host = new GameObject(ObjectName);
                host.transform.SetParent(roomRoot, false);
                host.transform.localPosition = Vector3.zero;
                host.layer = roomRoot.gameObject.layer;
                return host.AddComponent<EnemyDeathRemainsSheet>();
            }

            EnemyDeathRemainsSheet loose = FindFirstObjectByType<EnemyDeathRemainsSheet>();
            if (loose != null)
                return loose;
            return new GameObject(ObjectName).AddComponent<EnemyDeathRemainsSheet>();
        }

        private static Transform ResolveRoomRoot(Transform from)
        {
            if (from == null)
                return null;
            RoomController room = from.GetComponentInParent<RoomController>();
            return room != null ? room.transform : from;
        }
    }

    public enum DeathEffectQuality { Full, Medium, Low, Minimal }
}
