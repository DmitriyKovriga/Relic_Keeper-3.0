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
        private const float DefaultBleedDuration = 4f;
        private const float DefaultBleedDamageMult = 70f;
        private const float DefaultIgniteChance = 25f;
        private const float DefaultIgniteDuration = 4f;
        private const float DefaultIgniteDamageMult = 20f;
        private const float IgniteFireDamageShareThreshold = 0.3f;
        private const float TickInterval = 1f;

        private readonly List<PoisonStack> _poisonStacks = new List<PoisonStack>();
        private readonly List<BleedStack> _bleedStacks = new List<BleedStack>();
        private readonly List<IgniteStack> _igniteStacks = new List<IgniteStack>();
        private float _poisonTickTimer = TickInterval;
        private float _bleedTickTimer = TickInterval;
        private float _igniteTickTimer = TickInterval;

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
            UpdatePoison(Time.deltaTime);
            UpdateBleed(Time.deltaTime);
            UpdateIgnite(Time.deltaTime);
        }

        public int GetStackCount(AilmentType ailmentType)
        {
            return ailmentType switch
            {
                AilmentType.Poison => _poisonStacks.Count,
                AilmentType.Bleed => _bleedStacks.Count,
                AilmentType.Ignite => _igniteStacks.Count,
                _ => 0
            };
        }

        public static void TryApplyHitAilments(IStatsProvider sourceStats, object source, Transform target, DamageSnapshot hitSnapshot)
        {
            if (sourceStats == null || target == null || hitSnapshot == null || hitSnapshot.TotalDamage <= 0f)
                return;

            if (!TryResolve(target, out AilmentController controller) || controller == null)
                return;

            controller.TryApplyPoison(sourceStats, source, hitSnapshot);
            controller.TryApplyBleed(sourceStats, source, hitSnapshot);
            controller.TryApplyIgnite(sourceStats, source, hitSnapshot);
        }

        public static void TryApplyHitAilmentsFromSource(object source, Transform target, DamageSnapshot hitSnapshot)
        {
            if (!TryResolveStatsProvider(source, out IStatsProvider sourceStats))
                return;

            TryApplyHitAilments(sourceStats, source, target, hitSnapshot);
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

        public bool TryApplyBleed(IStatsProvider sourceStats, object source, DamageSnapshot hitSnapshot)
        {
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Physical <= 0f)
                return false;

            CacheOwner();

            float chance = Mathf.Max(0f, sourceStats.GetValue(StatType.BleedChance));
            if (chance <= 0f)
                return false;

            float avoid = _statsProvider != null ? Mathf.Clamp(_statsProvider.GetValue(StatType.ChanseToAvoidBleed), 0f, 100f) : 0f;
            float finalChance = Mathf.Clamp(chance * (1f - avoid / 100f), 0f, 100f);
            if (UnityEngine.Random.value > finalChance / 100f)
                return false;

            float damageMult = sourceStats.GetValue(StatType.BleedDamageMult);
            if (damageMult <= 0f)
                damageMult = DefaultBleedDamageMult;

            float bleedDamagePercent = sourceStats.GetValue(StatType.BleedDamage);
            float tickDamage = hitSnapshot.Physical * (damageMult / 100f) * Mathf.Max(0f, 1f + bleedDamagePercent / 100f);
            if (tickDamage <= 0f)
                return false;

            float duration = sourceStats.GetValue(StatType.BleedDuration);
            if (duration <= 0f)
                duration = DefaultBleedDuration;

            int maxStacks = 1 + Mathf.Max(0, Mathf.FloorToInt(sourceStats.GetValue(StatType.MaxBleedStack)));

            if (_bleedStacks.Count >= maxStacks)
            {
                int weakestIndex = FindWeakestBleedStackIndex();
                if (weakestIndex < 0 || _bleedStacks[weakestIndex].TickDamage > tickDamage)
                    return false;

                _bleedStacks.RemoveAt(weakestIndex);
            }

            _bleedStacks.Add(new BleedStack
            {
                Source = source,
                TickDamage = tickDamage,
                RemainingSeconds = duration
            });

            OnAilmentsChanged?.Invoke();
            return true;
        }

        public bool TryApplyIgnite(IStatsProvider sourceStats, object source, DamageSnapshot hitSnapshot)
        {
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Fire <= 0f || hitSnapshot.TotalDamage <= 0f)
                return false;

            float fireShare = hitSnapshot.Fire / hitSnapshot.TotalDamage;
            if (fireShare < IgniteFireDamageShareThreshold)
                return false;

            CacheOwner();

            float configuredChance = Mathf.Max(0f, sourceStats.GetValue(StatType.IgniteChance));
            float chance = Mathf.Max(DefaultIgniteChance, configuredChance);
            if (chance <= 0f)
                return false;

            float avoid = _statsProvider != null ? Mathf.Clamp(_statsProvider.GetValue(StatType.ChanseToAvoidIgnite), 0f, 100f) : 0f;
            float finalChance = Mathf.Clamp(chance * (1f - avoid / 100f), 0f, 100f);
            if (UnityEngine.Random.value > finalChance / 100f)
                return false;

            float damageMult = sourceStats.GetValue(StatType.IgniteDamageMult);
            if (damageMult <= 0f)
                damageMult = DefaultIgniteDamageMult;

            float igniteDamagePercent = sourceStats.GetValue(StatType.IgniteDamage);
            float tickDamage = hitSnapshot.Fire * (damageMult / 100f) * Mathf.Max(0f, 1f + igniteDamagePercent / 100f);
            if (tickDamage <= 0f)
                return false;

            float duration = sourceStats.GetValue(StatType.IgniteDuration);
            if (duration <= 0f)
                duration = DefaultIgniteDuration;

            int maxStacks = Mathf.Max(1, Mathf.FloorToInt(sourceStats.GetValue(StatType.MaxIgniteStacks)));
            if (_igniteStacks.Count >= maxStacks)
            {
                int weakestIndex = FindWeakestIgniteStackIndex();
                if (weakestIndex < 0 || _igniteStacks[weakestIndex].TickDamage > tickDamage)
                    return false;

                _igniteStacks.RemoveAt(weakestIndex);
            }

            _igniteStacks.Add(new IgniteStack
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

        private void UpdatePoison(float dt)
        {
            if (_poisonStacks.Count == 0)
            {
                _poisonTickTimer = TickInterval;
                return;
            }

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

        private void UpdateBleed(float dt)
        {
            if (_bleedStacks.Count == 0)
            {
                _bleedTickTimer = TickInterval;
                return;
            }

            if (dt <= 0f)
                return;

            _bleedTickTimer -= dt;
            bool changed = false;
            for (int i = _bleedStacks.Count - 1; i >= 0; i--)
            {
                BleedStack stack = _bleedStacks[i];
                stack.RemainingSeconds -= dt;

                if (stack.RemainingSeconds <= 0f)
                {
                    _bleedStacks.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    _bleedStacks[i] = stack;
                }
            }

            while (_bleedTickTimer <= 0f && _bleedStacks.Count > 0)
            {
                _bleedTickTimer += TickInterval;
                ApplyCombinedBleedTick();
            }

            if (changed)
                OnAilmentsChanged?.Invoke();
        }

        private void UpdateIgnite(float dt)
        {
            if (_igniteStacks.Count == 0)
            {
                _igniteTickTimer = TickInterval;
                return;
            }

            if (dt <= 0f)
                return;

            _igniteTickTimer -= dt;
            bool changed = false;
            for (int i = _igniteStacks.Count - 1; i >= 0; i--)
            {
                IgniteStack stack = _igniteStacks[i];
                stack.RemainingSeconds -= dt;

                if (stack.RemainingSeconds <= 0f)
                {
                    _igniteStacks.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    _igniteStacks[i] = stack;
                }
            }

            while (_igniteTickTimer <= 0f && _igniteStacks.Count > 0)
            {
                _igniteTickTimer += TickInterval;
                ApplyCombinedIgniteTick();
            }

            if (changed)
                OnAilmentsChanged?.Invoke();
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

        private void ApplyCombinedBleedTick()
        {
            float totalDamage = 0f;
            object source = null;
            float sourceDamage = float.MinValue;

            for (int i = 0; i < _bleedStacks.Count; i++)
            {
                BleedStack stack = _bleedStacks[i];
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

            ApplyPureBleedTick(totalDamage, source);
        }

        private void ApplyCombinedIgniteTick()
        {
            float totalDamage = 0f;
            object source = null;
            float sourceDamage = float.MinValue;

            for (int i = 0; i < _igniteStacks.Count; i++)
            {
                IgniteStack stack = _igniteStacks[i];
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

            ApplyPureIgniteTick(totalDamage, source);
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

        private void ApplyPureBleedTick(float damage, object source)
        {
            if (_enemyHealth != null)
            {
                _enemyHealth.ApplyPureDamage(damage, source, "Bleed");
                return;
            }

            if (_playerDamageReceiver != null)
                _playerDamageReceiver.ApplyPureDamage(damage, source, "Bleed");
        }

        private void ApplyPureIgniteTick(float damage, object source)
        {
            if (_enemyHealth != null)
            {
                _enemyHealth.ApplyPureDamage(damage, source, "Ignite");
                return;
            }

            if (_playerDamageReceiver != null)
                _playerDamageReceiver.ApplyPureDamage(damage, source, "Ignite");
        }

        private int FindWeakestBleedStackIndex()
        {
            int weakestIndex = -1;
            float weakestDamage = float.MaxValue;
            for (int i = 0; i < _bleedStacks.Count; i++)
            {
                if (_bleedStacks[i].TickDamage >= weakestDamage)
                    continue;

                weakestDamage = _bleedStacks[i].TickDamage;
                weakestIndex = i;
            }

            return weakestIndex;
        }

        private int FindWeakestIgniteStackIndex()
        {
            int weakestIndex = -1;
            float weakestDamage = float.MaxValue;
            for (int i = 0; i < _igniteStacks.Count; i++)
            {
                if (_igniteStacks[i].TickDamage >= weakestDamage)
                    continue;

                weakestDamage = _igniteStacks[i].TickDamage;
                weakestIndex = i;
            }

            return weakestIndex;
        }

        private static bool TryResolveStatsProvider(object source, out IStatsProvider statsProvider)
        {
            statsProvider = null;
            if (source == null)
                return false;

            if (source is IStatsProvider directStats)
            {
                statsProvider = directStats;
                return true;
            }

            GameObject sourceObject = Scripts.GameplayEvents.GameplayEventContext.ResolveGameObject(source);
            if (sourceObject == null)
                return false;

            statsProvider = sourceObject.GetComponent<IStatsProvider>() ?? sourceObject.GetComponentInParent<IStatsProvider>();
            return statsProvider != null;
        }

        private struct PoisonStack
        {
            public object Source;
            public float TickDamage;
            public float RemainingSeconds;
        }

        private struct BleedStack
        {
            public object Source;
            public float TickDamage;
            public float RemainingSeconds;
        }

        private struct IgniteStack
        {
            public object Source;
            public float TickDamage;
            public float RemainingSeconds;
        }
    }
}
