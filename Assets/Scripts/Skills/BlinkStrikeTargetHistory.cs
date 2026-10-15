using System.Collections.Generic;
using Scripts.Enemies;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>Owned by the caster so target priority survives casts and skill runner replacement.</summary>
    public sealed class BlinkStrikeTargetHistory : MonoBehaviour
    {
        private readonly Dictionary<Component, long> _lastStrike = new Dictionary<Component, long>();
        private readonly List<Component> _expired = new List<Component>();
        private long _sequence;

        public long LastStrike(Component target) => _lastStrike.TryGetValue(target, out long order) ? order : 0;

        public void Remember(Component target)
        {
            if (IsAlive(target)) _lastStrike[target] = ++_sequence;
            else if (target != null) _lastStrike.Remove(target);
        }

        public void PruneDeadTargets()
        {
            _expired.Clear();
            foreach (var entry in _lastStrike)
                if (!IsAlive(entry.Key)) _expired.Add(entry.Key);
            foreach (var target in _expired) _lastStrike.Remove(target);
            _expired.Clear();
        }

        private static bool IsAlive(Component target)
        {
            if (target == null) return false;
            var health = target.GetComponentInParent<EnemyHealth>();
            return health == null || !health.IsDead;
        }
    }
}
