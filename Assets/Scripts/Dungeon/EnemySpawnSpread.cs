using System.Collections.Generic;
using Scripts.Items.World;
using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Spreads a same-spawner pack horizontally so stacked enemies are readable.
    /// Enemies ignore each other physically, so this has to be a spawn-time nudge.
    /// </summary>
    public static class EnemySpawnSpread
    {
        public const float MinSeparation = 0.72f;
        public const float ProbeRadius = 0.32f;
        public const float MaxTravel = 1.6f;
        public const float WallPadding = 0.04f;
        public const int Iterations = 6;

        public static Vector2 ResolvePackOffset(int index, int count, float separation = MinSeparation)
        {
            if (count <= 1)
                return Vector2.zero;

            float start = -(count - 1) * separation * 0.5f;
            return new Vector2(start + index * separation, 0f);
        }

        public static void Separate(IList<Transform> bodies)
        {
            if (bodies == null || bodies.Count < 2)
                return;

            var startX = new float[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
                startX[i] = bodies[i] != null ? bodies[i].position.x : 0f;

            int wallMask = WorldItemDropService.TerrainMask;
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                bool anyMoved = false;
                for (int i = 0; i < bodies.Count; i++)
                {
                    for (int j = i + 1; j < bodies.Count; j++)
                    {
                        if (TrySeparatePair(bodies[i], bodies[j], startX[i], startX[j], wallMask))
                            anyMoved = true;
                    }
                }

                if (!anyMoved)
                    break;
            }
        }

        private static bool TrySeparatePair(
            Transform bodyA,
            Transform bodyB,
            float startAX,
            float startBX,
            int wallMask)
        {
            if (bodyA == null || bodyB == null)
                return false;

            WorldDroppedItemSpread.GetPairOffsets(
                bodyA.position,
                bodyB.position,
                MinSeparation,
                out float deltaAX,
                out float deltaBX);
            if (Mathf.Abs(deltaAX) < 0.001f && Mathf.Abs(deltaBX) < 0.001f)
                return false;

            bool movedA = TryMoveX(bodyA, deltaAX, startAX, wallMask);
            bool movedB = TryMoveX(bodyB, deltaBX, startBX, wallMask);
            if (movedA || movedB)
                return true;

            if (TryMoveX(bodyA, deltaAX + deltaBX, startAX, wallMask))
                return true;
            return TryMoveX(bodyB, deltaAX + deltaBX, startBX, wallMask);
        }

        public static bool TryMoveX(Transform body, float deltaX, float startX, int wallMask)
        {
            if (body == null || Mathf.Abs(deltaX) < 0.001f)
                return false;

            Vector2 current = body.position;
            float remainingTravel = MaxTravel - Mathf.Abs(current.x - startX);
            if (remainingTravel <= 0.001f)
                return false;

            deltaX = Mathf.Clamp(deltaX, -remainingTravel, remainingTravel);
            Vector2 direction = new Vector2(Mathf.Sign(deltaX), 0f);
            float distance = Mathf.Abs(deltaX);
            RaycastHit2D hit = Physics2D.CircleCast(current, ProbeRadius, direction, distance, wallMask);
            if (hit.collider != null)
            {
                float allowed = hit.distance - WallPadding;
                if (allowed <= 0.001f)
                    return false;
                deltaX = allowed * Mathf.Sign(deltaX);
            }

            if (Mathf.Abs(deltaX) < 0.001f)
                return false;

            body.position = new Vector3(current.x + deltaX, current.y, body.position.z);
            return true;
        }
    }
}
