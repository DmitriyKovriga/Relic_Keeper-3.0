using System;
using Scripts.Enemies;
using Scripts.GameplayEvents;
using UnityEngine;

namespace Scripts.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerParryController : MonoBehaviour
    {
        private object _windowOwner;
        private float _until;
        private CombatTriggerDamage _retaliation;
        public Func<float> Clock = () => Time.time;
        public bool IsActive => _windowOwner != null && Clock() < _until;

        public void OpenWindow(object owner, float duration, CombatTriggerDamage retaliation)
        {
            _windowOwner = owner;
            _until = Clock() + Mathf.Max(0, duration);
            _retaliation = retaliation;
        }

        public void CloseWindow(object owner)
        {
            if (!ReferenceEquals(owner, _windowOwner)) return;
            _windowOwner = null;
            _retaliation = null;
        }

        public bool TryParry(DamageSnapshot damage)
        {
            if (!isActiveAndEnabled || !IsActive || damage == null || !damage.IsDirectHit || damage.TotalDamage <= 0) return false;
            var source = GameplayEventContext.ResolveGameObject(damage.Source);
            var attacker = source != null ? source.GetComponentInParent<EnemyHealth>() : null;
            if (attacker == null) return false;
            // Target is the parrying player; source is the enemy whose hit was absorbed.
            _retaliation?.Apply(attacker.gameObject, GetComponent<PlayerStats>());
            GameplayEventBus.Raise(GameplayEventType.Parried, attacker.gameObject, gameObject,
                amount: damage.TotalDamage, damage: damage, position: transform.position);
            return true;
        }

        private void OnDisable()
        {
            _windowOwner = null;
            _retaliation = null;
        }
    }
}
