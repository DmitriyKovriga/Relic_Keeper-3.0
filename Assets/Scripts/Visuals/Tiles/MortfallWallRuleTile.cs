using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Scripts.Visuals.Tiles
{
    // Retains the original RuleTile asset/GUID and its existing masonry rules.
    public sealed class MortfallWallRuleTile : RuleTile
    {
        // NW, N, NE, W, E, SW, S, SE. Diagonals matter only when both sides join.
        public static readonly Vector3Int[] Neighbors =
        {
            new Vector3Int(-1, 1), new Vector3Int(0, 1), new Vector3Int(1, 1),
            new Vector3Int(-1, 0), new Vector3Int(1, 0),
            new Vector3Int(-1, -1), new Vector3Int(0, -1), new Vector3Int(1, -1)
        };
        public static readonly int[] BottomMasks = { 0, 2, 8, 10, 11, 16, 18, 22, 24, 26, 27, 30, 31 };
        [SerializeField] private Sprite[] _exposedBottoms = Array.Empty<Sprite>();
        private readonly Dictionary<int, TilingRule> _rulesByMask = new Dictionary<int, TilingRule>();

        public void ConfigureBottoms(Sprite[] sprites) { _exposedBottoms = sprites; _rulesByMask.Clear(); }
        private void OnValidate() => _rulesByMask.Clear();

        public static int NormalizeMask(int mask)
        {
            if ((mask & 10) != 10) mask &= ~1;
            if ((mask & 18) != 18) mask &= ~4;
            if ((mask & 72) != 72) mask &= ~32;
            if ((mask & 80) != 80) mask &= ~128;
            return mask;
        }

        public int ResolveMask(Tilemap wall, Vector3Int cell)
        {
            var support = wall.GetComponent<MortfallWallSupport>(); int mask = 0;
            for (int i = 0; i < Neighbors.Length; i++)
            {
                var offset = Neighbors[i]; var neighbor = cell + offset;
                if (RuleMatch(TilingRuleOutput.Neighbor.This, wall.GetTile(neighbor)) ||
                    (offset.y == -1 && support != null && support.isActiveAndEnabled && support.HasSupport(neighbor))) mask |= 1 << i;
            }
            return NormalizeMask(mask);
        }

        public Sprite SpriteForMask(int rawMask)
        {
            int mask = NormalizeMask(rawMask);
            int bottom = Array.IndexOf(BottomMasks, mask);
            if (bottom >= 0 && bottom < _exposedBottoms.Length && _exposedBottoms[bottom] != null) return _exposedBottoms[bottom];
            var rule = RuleForMask(mask);
            return rule != null && rule.m_Sprites.Length > 0 ? rule.m_Sprites[0] : m_DefaultSprite;
        }

        private TilingRule RuleForMask(int mask)
        {
            if (_rulesByMask.TryGetValue(mask, out var cached)) return cached;
            foreach (var rule in m_TilingRules)
            {
                bool matches = true;
                for (int i = 0; i < rule.m_Neighbors.Count; i++)
                {
                    int index = Array.IndexOf(Neighbors, rule.m_NeighborPositions[i]);
                    if (index < 0) continue;
                    bool occupied = (mask & (1 << index)) != 0;
                    int required = rule.m_Neighbors[i];
                    if ((required == TilingRuleOutput.Neighbor.This && !occupied) ||
                        (required == TilingRuleOutput.Neighbor.NotThis && occupied)) { matches = false; break; }
                }
                if (matches) { _rulesByMask[mask] = rule; return rule; }
            }
            _rulesByMask[mask] = null; return null;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var map = tilemap.GetComponent<Tilemap>();
            if (map == null) { base.GetTileData(position, tilemap, ref tileData); return; }
            int mask = ResolveMask(map, position); var rule = RuleForMask(mask);
            tileData.sprite = SpriteForMask(mask);
            tileData.gameObject = rule?.m_GameObject ?? m_DefaultGameObject;
            tileData.colliderType = rule?.m_ColliderType ?? m_DefaultColliderType;
            tileData.flags = TileFlags.LockTransform; tileData.transform = Matrix4x4.identity;
        }
    }
}
