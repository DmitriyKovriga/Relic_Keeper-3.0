using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Four wedges around the start node. 0 north, 1 east, 2 south, 3 west.
    /// Angles use the tree convention: 0 = right, 90 = down.
    /// </summary>
    public static class PassiveZoneMath
    {
        public static readonly string[] ZoneNames = { "Север", "Восток", "Юг", "Запад" };

        public static int Opposite(int zone) => (zone + 2) % 4;

        public static PassiveNodeDefinition FindStart(PassiveSkillTreeSO tree)
        {
            if (tree?.Nodes == null)
                return null;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                if (tree.Nodes[i] != null && tree.Nodes[i].NodeType == PassiveNodeType.Start)
                    return tree.Nodes[i];
            }
            return null;
        }

        public static int CountBackbone(PassiveSkillTreeSO tree)
        {
            if (tree?.Nodes == null)
                return 0;
            int count = 0;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                if (tree.Nodes[i] != null && tree.Nodes[i].IsBackbone)
                    count++;
            }
            return count;
        }

        public static float AngleOf(Vector2 start, Vector2 point)
        {
            Vector2 d = point - start;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return angle < 0f ? angle + 360f : angle;
        }

        public static int ZoneOf(Vector2 start, Vector2 point)
        {
            float angle = AngleOf(start, point);
            if (angle >= 225f && angle < 315f) return 0;
            if (angle >= 45f && angle < 135f) return 2;
            if (angle >= 135f && angle < 225f) return 3;
            return 1;
        }

        public static Vector2 Place(Vector2 start, Vector2 local, int zone)
        {
            Vector2 p = local;
            for (int i = 0; i < ((zone % 4) + 4) % 4; i++)
                p = new Vector2(-p.y, p.x);
            return start + p;
        }

        public static Vector2 Rotate(Vector2 start, Vector2 point, int quarterTurns)
        {
            Vector2 p = point - start;
            for (int i = 0; i < ((quarterTurns % 4) + 4) % 4; i++)
                p = new Vector2(-p.y, p.x);
            return start + p;
        }

        public static Vector2 Flip(Vector2 start, Vector2 point, bool horizontal)
        {
            Vector2 d = point - start;
            if (horizontal) d.x = -d.x;
            else d.y = -d.y;
            return start + d;
        }

        public static float RotateAngle(float angle, int quarterTurns)
        {
            return Mathf.Repeat(angle + 90f * quarterTurns, 360f);
        }

        public static float FlipAngle(float angle, bool horizontal)
        {
            return Mathf.Repeat(horizontal ? 180f - angle : -angle, 360f);
        }

        public static bool FlipIsHorizontal(int zone) => zone == 0 || zone == 2;

        /// <summary>
        /// Small nodes before a notable, taking the cheapest path from any backbone entry.
        /// A notable glued to the backbone costs 0.
        /// </summary>
        public static int NotableCost(PassiveSkillTreeSO tree, PassiveNodeDefinition notable)
        {
            if (tree == null || notable == null) return -1;
            tree.InitLookup();
            var cost = new Dictionary<string, int> { [notable.ID] = 0 };
            var queue = new Queue<PassiveNodeDefinition>();
            queue.Enqueue(notable);
            int best = int.MaxValue;
            while (queue.Count > 0)
            {
                PassiveNodeDefinition node = queue.Dequeue();
                int here = cost[node.ID];
                if (here >= best || node.ConnectionIDs == null) continue;
                foreach (string id in node.ConnectionIDs)
                {
                    PassiveNodeDefinition next = tree.GetNode(id);
                    if (next == null) continue;
                    if (next.IsBackbone) { best = Mathf.Min(best, here); continue; }
                    int candidate = here + (next.NodeType == PassiveNodeType.Small ? 1 : 0);
                    if (cost.TryGetValue(id, out int previous) && previous <= candidate) continue;
                    cost[id] = candidate;
                    queue.Enqueue(next);
                }
            }
            return best == int.MaxValue ? -1 : best;
        }

        public static int EdgeDistanceToBackbone(PassiveSkillTreeSO tree, PassiveNodeDefinition origin)
        {
            if (tree == null || origin == null)
                return -1;
            tree.InitLookup();
            if (origin.IsBackbone)
                return 0;

            var dist = new Dictionary<string, int> { [origin.ID] = 0 };
            var queue = new Queue<PassiveNodeDefinition>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                PassiveNodeDefinition node = queue.Dequeue();
                int here = dist[node.ID];
                if (node.ConnectionIDs == null)
                    continue;
                for (int i = 0; i < node.ConnectionIDs.Count; i++)
                {
                    PassiveNodeDefinition next = tree.GetNode(node.ConnectionIDs[i]);
                    if (next == null)
                        continue;
                    if (next.IsBackbone)
                        return here + 1;
                    if (dist.ContainsKey(next.ID))
                        continue;
                    dist[next.ID] = here + 1;
                    queue.Enqueue(next);
                }
            }
            return -1;
        }

        public static int ContentCount(PassiveSkillTreeSO tree, int zone)
        {
            PassiveNodeDefinition start = FindStart(tree);
            if (start == null || tree.Nodes == null)
                return 0;
            Vector2 origin = start.GetWorldPosition(tree);
            int count = 0;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                PassiveNodeDefinition node = tree.Nodes[i];
                if (node == null || node.IsBackbone || node.NodeType == PassiveNodeType.Start)
                    continue;
                if (ZoneOf(origin, node.GetWorldPosition(tree)) == zone)
                    count++;
            }
            return count;
        }
    }
}
