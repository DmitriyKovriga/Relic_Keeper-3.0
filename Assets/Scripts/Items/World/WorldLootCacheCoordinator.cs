using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Items.World
{
    /// <summary>
    /// Converts dense clusters of visible ground loot into one compact container.
    /// Hidden loot stays fully reversible and only participates after the loot filter reveals it.
    /// </summary>
    public static class WorldLootCacheCoordinator
    {
        public const float AbsorbRadius = 3f;
        public const int RequiredVisibleItems = 5;

        public static bool ProcessVisibleDrop(WorldDroppedItem spawned)
        {
            if (spawned == null || !spawned.ParticipatesInWorldLayout)
                return false;

            WorldLootCache nearestCache = FindNearestCache(spawned.GroundPosition, spawned.transform.parent);
            if (nearestCache != null)
                return nearestCache.Absorb(spawned);

            List<WorldDroppedItem> cluster = FindVisibleCluster(spawned);
            if (!ShouldCreateCache(cluster.Count))
                return false;

            Vector2 cachePosition = ResolveCachePosition(cluster, spawned.GroundPosition);
            WorldLootCache cache = WorldLootCache.Create(cachePosition, spawned.transform.parent);
            for (int i = 0; i < cluster.Count; i++)
                cache.Absorb(cluster[i]);
            return true;
        }

        public static bool ShouldCreateCache(int visibleItemCount)
        {
            return visibleItemCount >= RequiredVisibleItems;
        }

        public static bool IsWithinAbsorbRange(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude <= AbsorbRadius * AbsorbRadius;
        }

        private static WorldLootCache FindNearestCache(Vector2 position, Transform scope)
        {
            WorldLootCache[] caches = Object.FindObjectsByType<WorldLootCache>(FindObjectsSortMode.None);
            WorldLootCache nearest = null;
            float nearestSqrDistance = float.PositiveInfinity;
            float radiusSqr = AbsorbRadius * AbsorbRadius;
            for (int i = 0; i < caches.Length; i++)
            {
                WorldLootCache cache = caches[i];
                if (cache == null || !cache.CanAcceptItems || cache.transform.parent != scope)
                    continue;

                float sqrDistance = ((Vector2)cache.transform.position - position).sqrMagnitude;
                if (sqrDistance > radiusSqr || sqrDistance >= nearestSqrDistance)
                    continue;

                nearest = cache;
                nearestSqrDistance = sqrDistance;
            }

            return nearest;
        }

        private static List<WorldDroppedItem> FindVisibleCluster(WorldDroppedItem spawned)
        {
            WorldDroppedItem[] all = Object.FindObjectsByType<WorldDroppedItem>(FindObjectsSortMode.None);
            var cluster = new List<WorldDroppedItem>(RequiredVisibleItems + 2);
            Vector2 origin = spawned.GroundPosition;
            Transform scope = spawned.transform.parent;
            float radiusSqr = AbsorbRadius * AbsorbRadius;
            for (int i = 0; i < all.Length; i++)
            {
                WorldDroppedItem item = all[i];
                if (item == null || !item.ParticipatesInWorldLayout || item.transform.parent != scope)
                    continue;
                if ((item.GroundPosition - origin).sqrMagnitude <= radiusSqr)
                    cluster.Add(item);
            }

            return cluster;
        }

        private static Vector2 ResolveCachePosition(IReadOnlyList<WorldDroppedItem> cluster, Vector2 fallback)
        {
            if (cluster == null || cluster.Count == 0)
                return fallback;

            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < cluster.Count; i++)
            {
                if (cluster[i] == null)
                    continue;
                sum += cluster[i].GroundPosition;
                count++;
            }

            return count > 0 ? sum / count : fallback;
        }
    }
}
