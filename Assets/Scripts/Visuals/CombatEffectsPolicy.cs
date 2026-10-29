using System.Collections.Generic;

namespace Scripts.Visuals
{
    public static class CombatEffectsPolicy
    {
        public static float AccentScale(bool reduced) => reduced ? .5f : 1f;
        public static int FlashLimit(bool reduced) => reduced ? 3 : 8;
        public static float AccentStrength => AccentScale(GameplayPresentationSettings.ReducedCombatEffects);
        public static int EnemyFlashLimit => FlashLimit(GameplayPresentationSettings.ReducedCombatEffects);
        public static int ImpactDecorationLimit(bool reduced) => reduced ? 6 : 16;
        public static int RippleDecorationLimit(bool reduced) => reduced ? 2 : 8;
        public static int ParticleDecorationLimit(bool reduced) => reduced ? 96 : 384;
        public static int DeathFragmentCount(int count) => GameplayPresentationSettings.ReducedCombatEffects ? System.Math.Min(count, 2) : count;
    }

    // Active leases, not the number of pooled renderers. Owners release on
    // pulse end, disable, death, settings change and destruction.
    public sealed class CombatFlashBudget
    {
        private readonly HashSet<object> _active = new HashSet<object>(32);
        private static readonly System.Predicate<object> Destroyed = owner => owner is UnityEngine.Object value && value == null;
        public int Count => _active.Count;
        public bool TryAcquire(object owner, int limit)
        {
            _active.RemoveWhere(Destroyed);
            if (owner == null || limit <= 0) return false;
            if (_active.Contains(owner)) return true;
            return _active.Count < limit && _active.Add(owner);
        }
        public void Release(object owner) { if (owner != null) _active.Remove(owner); }
    }
}
