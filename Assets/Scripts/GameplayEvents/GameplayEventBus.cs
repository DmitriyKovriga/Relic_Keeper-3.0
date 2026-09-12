using System;
using Scripts.Combat;
using UnityEngine;

namespace Scripts.GameplayEvents
{
    public static class GameplayEventBus
    {
        public static event Action<GameplayEventContext> EventRaised;

        public static void Raise(GameplayEventContext context)
        {
            if (context == null)
                return;

            EventRaised?.Invoke(context);
        }

        public static void Raise(
            GameplayEventType type,
            GameObject source = null,
            GameObject target = null,
            float amount = 0f,
            int count = 0,
            DamageSnapshot damage = null,
            Vector3? position = null)
        {
            Raise(new GameplayEventContext
            {
                Type = type,
                Source = source,
                Target = target,
                Amount = amount,
                Count = count,
                Damage = damage,
                Position = position ?? (target != null ? target.transform.position : source != null ? source.transform.position : Vector3.zero)
            });
        }
    }
}
