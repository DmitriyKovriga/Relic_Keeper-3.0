using System;
using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class AilmentController : MonoBehaviour
    {
        private const float DefaultPoisonDuration = 4f;
        private const float DefaultPoisonDamageMult = 20f;
        private const float TickInterval = 1f;

        private readonly List<PoisonStack> _poisonStacks = new List<PoisonStack>();
        private float _poisonTickTimer = TickInterval;

        private IStatsProvider _statsProvider;
        private EnemyHealth _enemyHealth;
        private PlayerDamageReceiver _playerDamageReceiver;

        public event Action OnAilmentsChanged;

        private void Awake()
        {
            CacheOwner();
        }

        private void OnEnable()
        {
            CacheOwner();
        }

        private void Update()
        {
            if (_poisonStacks.Count == 0)
            {
                _poisonTickTimer = TickInterval;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            _poisonTickTimer -= dt;
            bool changed = false;
            for (int i = _poisonStacks.Count - 1; i >= 0; i--)
            {
                PoisonStack stack = _poisonStacks[i];
                stack.RemainingSeconds -= dt;

                if (stack.RemainingSeconds <= 0f)
                {
                    _poisonStacks.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    _poisonStacks[i] = stack;
                }
            }

            while (_poisonTickTimer <= 0f && _poisonStacks.Count > 0)
            {
                _poisonTickTimer += TickInterval;
                ApplyCombinedPoisonTick();
            }

            if (changed)
                OnAilmentsChanged?.Invoke();
        }

        public int GetStackCount(AilmentType ailmentType)
        {
            return ailmentType == AilmentType.Poison ? _poisonStacks.Count : 0;
        }

        public bool TryApplyPoison(IStatsProvider sourceStats, object source, DamageSnapshot hitSnapshot)
        {
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Physical <= 0f)
                return false;

            CacheOwner();

            float chance = Mathf.Max(0f, sourceStats.GetValue(StatType.PoisonChance));
            if (chance <= 0f)
                return false;

            float avoid = _statsProvider != null ? Mathf.Clamp(_statsProvider.GetValue(StatType.ChanseToAvoidPoison), 0f, 100f) : 0f;
            float finalChance = Mathf.Clamp(chance * (1f - avoid / 100f), 0f, 100f);
            if (UnityEngine.Random.value > finalChance / 100f)
                return false;

            float damageMult = sourceStats.GetValue(StatType.PoisonDamageMult);
            if (damageMult <= 0f)
                damageMult = DefaultPoisonDamageMult;

            float poisonDamagePercent = sourceStats.GetValue(StatType.PoisonDamage);
            float tickDamage = hitSnapshot.Physical * (damageMult / 100f) * Mathf.Max(0f, 1f + poisonDamagePercent / 100f);
            if (tickDamage <= 0f)
                return false;

            float duration = sourceStats.GetValue(StatType.PoisonDuration);
            if (duration <= 0f)
                duration = DefaultPoisonDuration;

            _poisonStacks.Add(new PoisonStack
            {
                Source = source,
                TickDamage = tickDamage,
                RemainingSeconds = duration
            });

            OnAilmentsChanged?.Invoke();
            return true;
        }

        public static bool TryResolve(Transform candidate, out AilmentController controller)
        {
            controller = null;
            if (candidate == null)
                return false;

            controller = candidate.GetComponent<AilmentController>();
            if (controller != null)
                return true;

            controller = candidate.GetComponentInParent<AilmentController>();
            if (controller != null)
                return true;

            PlayerStats playerStats = candidate.GetComponent<PlayerStats>() ?? candidate.GetComponentInParent<PlayerStats>();
            if (playerStats != null)
            {
                controller = playerStats.GetComponent<AilmentController>();
                if (controller == null)
                    controller = playerStats.gameObject.AddComponent<AilmentController>();
                return controller != null;
            }

            EnemyStats enemyStats = candidate.GetComponent<EnemyStats>() ?? candidate.GetComponentInParent<EnemyStats>();
            if (enemyStats != null)
            {
                controller = enemyStats.GetComponent<AilmentController>();
                if (controller == null)
                    controller = enemyStats.gameObject.AddComponent<AilmentController>();
                return controller != null;
            }

            return false;
        }

        private void CacheOwner()
        {
            _statsProvider = GetComponent<IStatsProvider>() ?? GetComponentInParent<IStatsProvider>();
            _enemyHealth = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>();
            _playerDamageReceiver = GetComponent<PlayerDamageReceiver>() ?? GetComponentInParent<PlayerDamageReceiver>();
        }

        private void ApplyCombinedPoisonTick()
        {
            float totalDamage = 0f;
            object source = null;
            float sourceDamage = float.MinValue;

            for (int i = 0; i < _poisonStacks.Count; i++)
            {
                PoisonStack stack = _poisonStacks[i];
                if (stack.RemainingSeconds <= 0f || stack.TickDamage <= 0f)
                    continue;

                totalDamage += stack.TickDamage;
                if (stack.TickDamage > sourceDamage)
                {
                    sourceDamage = stack.TickDamage;
                    source = stack.Source;
                }
            }

            if (totalDamage <= 0f)
                return;

            ApplyPurePoisonTick(totalDamage, source);
        }

        private void ApplyPurePoisonTick(float damage, object source)
        {
            if (_enemyHealth != null)
            {
                _enemyHealth.ApplyPureDamage(damage, source, "Poison");
                return;
            }

            if (_playerDamageReceiver != null)
                _playerDamageReceiver.ApplyPureDamage(damage, source, "Poison");
        }

        private struct PoisonStack
        {
            public object Source;
            public float TickDamage;
            public float RemainingSeconds;
        }
    }
}
