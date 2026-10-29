using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Visuals
{
    // Counts admitted particles until their authored lifetime expires. Scaled time pauses
    // both emission admission and expiry with combat; denied decoration creates no backlog.
    public sealed class CombatParticleBudget
    {
        private struct Reservation { public float End; public int Count; }
        private readonly List<Reservation> _active = new List<Reservation>(128);
        public int Count { get; private set; }
        public int Admit(int requested, float lifetime, float now, int limit)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i].End <= now) { Count -= _active[i].Count; _active.RemoveAt(i); }
            if (requested <= 0 || lifetime <= 0 || float.IsNaN(lifetime) || float.IsInfinity(lifetime)) return 0;
            int count = Mathf.Min(requested, Mathf.Max(0, limit - Count));
            if (count > 0) { _active.Add(new Reservation { End = now + lifetime, Count = count }); Count += count; }
            return count;
        }
    }

    // Explicitly decorative callers only. Never apply this admission gate to a projectile,
    // hitbox, telegraph, or a SpawnVFX step whose lifetime can drive damage timing.
    public static class CombatDecorationBudget
    {
        private static readonly List<GameObject> Impacts = new List<GameObject>(16);
        private static readonly List<GameObject> Ripples = new List<GameObject>(8);
        private static CombatParticleBudget _particles = new CombatParticleBudget();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Impacts.Clear(); Ripples.Clear(); _particles = new CombatParticleBudget(); }

        private static bool HasRoom(List<GameObject> active, int limit)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i] == null || !active[i].activeInHierarchy) active.RemoveAt(i);
            return active.Count < limit;
        }

        public static GameObject SpawnImpact(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            if (Application.isPlaying && (Time.timeScale <= 0 || !HasRoom(Impacts,
                CombatEffectsPolicy.ImpactDecorationLimit(GameplayPresentationSettings.ReducedCombatEffects)))) return null;
            var result = Object.Instantiate(prefab, position, rotation);
            if (Application.isPlaying) Impacts.Add(result);
            return result;
        }

        public static GameObject CreateRipple(Vector3 position)
        {
            if (Application.isPlaying && (Time.timeScale <= 0 || !HasRoom(Ripples,
                CombatEffectsPolicy.RippleDecorationLimit(GameplayPresentationSettings.ReducedCombatEffects)))) return null;
            var result = new GameObject("ProjectileRipple"); result.transform.position = position;
            if (Application.isPlaying) Ripples.Add(result);
            return result;
        }

        public static int AdmitParticles(int count, float lifetime)
        {
            if (!Application.isPlaying) return count;
            if (Time.timeScale <= 0) return 0;
            return _particles.Admit(count, lifetime, Time.time,
                CombatEffectsPolicy.ParticleDecorationLimit(GameplayPresentationSettings.ReducedCombatEffects));
        }
    }
}
