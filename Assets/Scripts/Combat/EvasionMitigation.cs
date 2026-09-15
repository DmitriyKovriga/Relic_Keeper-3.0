using UnityEngine;
using Scripts.GameplayEvents;
using Scripts.Stats;

namespace Scripts.Combat
{
    public static class EvasionMitigation
    {
        public const float CapPercent = DefenseRatingCurve.CapPercent;

        public static float EvasionToDodgeChance(float evasion)
        {
            return DefenseRatingCurve.ToPercent(evasion);
        }

        public static float GetDodgeChancePercent(IStatsProvider statsProvider)
        {
            if (statsProvider == null)
                return 0f;

            return EvasionToDodgeChance(Mathf.Max(0f, statsProvider.GetValue(StatType.Evasion)));
        }

        public static bool CanEvade(DamageSnapshot damage)
        {
            return damage != null && damage.IsDirectHit;
        }

        public static bool RollDodge(float dodgeChancePercent)
        {
            if (dodgeChancePercent <= 0f)
                return false;

            return Random.value < dodgeChancePercent / 100f;
        }

        public static bool TryEvade(
            IStatsProvider statsProvider,
            DamageSnapshot damage,
            GameObject target,
            Vector3 popupPosition)
        {
            if (!CanEvade(damage))
                return false;

            float chance = GetDodgeChancePercent(statsProvider);
            if (!RollDodge(chance))
                return false;

            GameplayEventBus.Raise(
                GameplayEventType.Evaded,
                source: GameplayEventContext.ResolveGameObject(damage.Source),
                target: target,
                damage: damage);

            if (FloatingTextManager.Instance != null)
                FloatingTextManager.Instance.Show(0f, false, "Evade", popupPosition);

            return true;
        }
    }
}
