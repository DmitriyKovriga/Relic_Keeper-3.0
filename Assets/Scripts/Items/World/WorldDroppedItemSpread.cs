using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Items.World
{
    public static class WorldDroppedItemSpread
    {
        public const float MinSeparation = 0.86f;
        public const float NeighborRadius = 2.2f;
        public const float ProbeRadius = 0.28f;
        public const float MaxGroundStep = 0.45f;
        public const float MaxTravel = 1.8f;
        public const int Iterations = 8;

        public static void SeparateFromNeighbors(WorldDroppedItem spawned)
        {
            if (spawned == null)
                return;

            WorldDroppedItem[] all = Object.FindObjectsByType<WorldDroppedItem>(FindObjectsSortMode.None);
            var cluster = new List<WorldDroppedItem>(all.Length);
            Vector2 origin = spawned.GroundPosition;
            float neighborRadiusSqr = NeighborRadius * NeighborRadius;
            for (int i = 0; i < all.Length; i++)
            {
                WorldDroppedItem item = all[i];
                if (item == null)
                    continue;
                if ((item.GroundPosition - origin).sqrMagnitude <= neighborRadiusSqr)
                    cluster.Add(item);
            }

            if (cluster.Count < 2)
                return;

            var startX = new float[cluster.Count];
            for (int i = 0; i < cluster.Count; i++)
                startX[i] = cluster[i].GroundPosition.x;

            int wallMask = WorldItemDropService.TerrainMask;
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                bool anyMoved = false;
                for (int i = 0; i < cluster.Count; i++)
                {
                    for (int j = i + 1; j < cluster.Count; j++)
                    {
                        if (TrySeparatePair(cluster[i], cluster[j], startX[i], startX[j], wallMask))
                            anyMoved = true;
                    }
                }

                if (!anyMoved)
                    break;
            }
        }

        public static void GetPairOffsets(Vector2 a, Vector2 b, float minSeparation, out float deltaAX, out float deltaBX)
        {
            float distX = b.x - a.x;
            float absDist = Mathf.Abs(distX);
            if (absDist >= minSeparation)
            {
                deltaAX = 0f;
                deltaBX = 0f;
                return;
            }

            float missing = minSeparation - absDist;
            float dirX = distX > 0.0001f ? 1f : distX < -0.0001f ? -1f : 1f;
            float half = missing * 0.5f;
            deltaAX = -dirX * half;
            deltaBX = dirX * half;
        }

        public static bool IsGroundStepSafe(float currentY, float nextY, float maxStep)
        {
            return Mathf.Abs(nextY - currentY) <= maxStep;
        }

        internal static bool TryShiftHorizontally(
            Vector2 current,
            float deltaX,
            float startX,
            int wallMask,
            out Vector2 landed)
        {
            landed = current;
            if (Mathf.Abs(deltaX) < 0.001f)
                return false;

            float remainingTravel = MaxTravel - Mathf.Abs(current.x - startX);
            if (remainingTravel <= 0.001f)
                return false;

            deltaX = Mathf.Clamp(deltaX, -remainingTravel, remainingTravel);
            Vector2 direction = new Vector2(Mathf.Sign(deltaX), 0f);
            float distance = Mathf.Abs(deltaX);
            RaycastHit2D hit = Physics2D.CircleCast(current, ProbeRadius, direction, distance, wallMask);
            if (hit.collider != null)
            {
                float allowed = hit.distance - 0.03f;
                if (allowed <= 0.001f)
                    return false;
                deltaX = allowed * Mathf.Sign(deltaX);
            }

            Vector2 candidate = new Vector2(current.x + deltaX, current.y);
            if (WorldItemDropService.TrySnapToGround(candidate, out Vector2 snapped))
            {
                if (!IsGroundStepSafe(current.y, snapped.y, MaxGroundStep))
                    return false;
                candidate = snapped;
            }

            if (Mathf.Abs(candidate.x - current.x) < 0.001f)
                return false;

            landed = candidate;
            return true;
        }

        private static bool TrySeparatePair(
            WorldDroppedItem itemA,
            WorldDroppedItem itemB,
            float startAX,
            float startBX,
            int wallMask)
        {
            if (itemA == null || itemB == null)
                return false;

            GetPairOffsets(itemA.GroundPosition, itemB.GroundPosition, MinSeparation, out float deltaAX, out float deltaBX);
            if (Mathf.Abs(deltaAX) < 0.001f && Mathf.Abs(deltaBX) < 0.001f)
                return false;

            bool movedA = TryMove(itemA, deltaAX, startAX, wallMask);
            bool movedB = TryMove(itemB, deltaBX, startBX, wallMask);
            if (movedA || movedB)
                return true;

            if (TryMove(itemA, deltaAX + deltaBX, startAX, wallMask))
                return true;
            return TryMove(itemB, deltaAX + deltaBX, startBX, wallMask);
        }

        private static bool TryMove(WorldDroppedItem item, float deltaX, float startX, int wallMask)
        {
            if (item == null || Mathf.Abs(deltaX) < 0.001f)
                return false;

            if (!TryShiftHorizontally(item.GroundPosition, deltaX, startX, wallMask, out Vector2 landed))
                return false;

            item.SetGroundedWorldPosition(landed);
            return true;
        }
    }
}
