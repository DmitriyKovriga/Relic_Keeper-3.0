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
        private const float DefaultPoisonDuration = 2f;
        private const float DefaultPoisonDamageMult = 10f;
        private const float DefaultBleedDuration = 6f;
        private const float DefaultBleedDamageMult = 50f;
        private const float DefaultIgniteChance = 25f;
        private const float DefaultIgniteDuration = 4f;
        private const float DefaultIgniteSpreadDuration = 2f;
        private const float IgniteSpreadRadius = 2.5f;
        private const float DefaultIgniteDamageMult = 20f;
        private const float IgniteFireDamageShareThreshold = 0.3f;
        private const float DefaultFreezeDuration = 1f;
        private const float FreezeColdDamageShareThreshold = 0.3f;
        private const float DefaultShockDuration = 2f;
        private const float ShockLightningDamageShareThreshold = 0.3f;
        private const float ShockDamageTakenMoreMultiplier = 1.5f;
        private const float TickInterval = 1f;

        private readonly List<BleedStack> _bleedStacks = new List<BleedStack>();
        private float _poisonRemainingSeconds;
        private float _poisonTotalTickDamage;
        private int _poisonStackCount;
        private object _poisonSource;
        private float _poisonStrongestTick;
        private readonly List<IgniteStack> _igniteStacks = new List<IgniteStack>();
        private float _poisonTickTimer = TickInterval;
        private float _bleedTickTimer = TickInterval;
        private float _igniteTickTimer = TickInterval;
        private float _igniteSpreadTimer;
        private float _shockRemainingSeconds;

        private IStatsProvider _statsProvider;
        private EnemyHealth _enemyHealth;
        private EnemyFreezeController _enemyFreeze;
        private ShockVisualController _shockVisual;
        private IgniteVisualController _igniteVisual;
        private PlayerDamageReceiver _playerDamageReceiver;

        public event Action OnAilmentsChanged;
        public bool IsShocked => _shockRemainingSeconds > 0f;
        public float DamageTakenMoreMultiplier => IsShocked ? ShockDamageTakenMoreMultiplier : 1f;

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
            UpdateShock(Time.deltaTime);
        }

        public int GetStackCount(AilmentType ailmentType)
        {
            return ailmentType switch
            {
                AilmentType.Poison => _poisonStackCount,
                AilmentType.Bleed => _bleedStacks.Count,
                AilmentType.Ignite => _igniteStacks.Count,
                AilmentType.Freeze => _enemyFreeze != null && _enemyFreeze.IsFrozen ? 1 : 0,
                AilmentType.Shock => IsShocked ? 1 : 0,
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
            controller.TryApplyFreeze(sourceStats, source, hitSnapshot);
            controller.TryApplyShock(sourceStats, source, hitSnapshot);
        }

        public static void TryApplyHitAilmentsFromSource(object source, Transform target, DamageSnapshot hitSnapshot)
        {
            if (!TryResolveStatsProvider(source, out IStatsProvider sourceStats))
                return;

            TryApplyHitAilments(sourceStats, source, target, hitSnapshot);
        }

        public static bool TryPreviewHitAilment(
            IStatsProvider sourceStats,
            DamageSnapshot hitSnapshot,
            AilmentType ailmentType,
            out float tickDamage,
            out float chancePercent)
        {
            tickDamage = 0f;
            chancePercent = 0f;
            return ailmentType switch
            {
                AilmentType.Poison => TryPreviewPoison(sourceStats, hitSnapshot, out tickDamage, out chancePercent),
                AilmentType.Bleed => TryPreviewBleed(sourceStats, hitSnapshot, out tickDamage, out chancePercent),
                AilmentType.Ignite => TryPreviewIgnite(sourceStats, hitSnapshot, out tickDamage, out chancePercent),
                _ => false
            };
        }

        private static bool TryPreviewPoison(
            IStatsProvider sourceStats,
            DamageSnapshot hitSnapshot,
            out float tickDamage,
            out float chancePercent)
        {
            tickDamage = 0f;
            chancePercent = 0f;
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Physical <= 0f)
                return false;

            chancePercent = Mathf.Clamp(Mathf.Max(0f, sourceStats.GetValue(StatType.PoisonChance)), 0f, 100f);
            if (chancePercent <= 0f)
                return false;

            float damageMult = sourceStats.GetValue(StatType.PoisonDamageMult);
            if (damageMult <= 0f)
                damageMult = DefaultPoisonDamageMult;

            tickDamage = hitSnapshot.Physical * (damageMult / 100f) * Mathf.Max(0f, 1f + sourceStats.GetValue(StatType.PoisonDamage) / 100f);
            return tickDamage > 0f;
        }

        private static bool TryPreviewBleed(
            IStatsProvider sourceStats,
            DamageSnapshot hitSnapshot,
            out float tickDamage,
            out float chancePercent)
        {
            tickDamage = 0f;
            chancePercent = 0f;
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Physical <= 0f)
                return false;

            chancePercent = Mathf.Clamp(Mathf.Max(0f, sourceStats.GetValue(StatType.BleedChance)), 0f, 100f);
            if (chancePercent <= 0f)
                return false;

            float damageMult = sourceStats.GetValue(StatType.BleedDamageMult);
            if (damageMult <= 0f)
                damageMult = DefaultBleedDamageMult;

            tickDamage = hitSnapshot.Physical * (damageMult / 100f) * Mathf.Max(0f, 1f + sourceStats.GetValue(StatType.BleedDamage) / 100f);
            return tickDamage > 0f;
        }

        private static bool TryPreviewIgnite(
            IStatsProvider sourceStats,
            DamageSnapshot hitSnapshot,
            out float tickDamage,
            out float chancePercent)
        {
            tickDamage = 0f;
            chancePercent = 0f;
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Fire <= 0f || hitSnapshot.TotalDamage <= 0f)
                return false;
            if (hitSnapshot.Fire / hitSnapshot.TotalDamage < IgniteFireDamageShareThreshold)
                return false;

            float configuredChance = Mathf.Max(0f, sourceStats.GetValue(StatType.IgniteChance));
            chancePercent = Mathf.Clamp(Mathf.Max(DefaultIgniteChance, configuredChance), 0f, 100f);

            float damageMult = sourceStats.GetValue(StatType.IgniteDamageMult);
            if (damageMult <= 0f)
                damageMult = DefaultIgniteDamageMult;

            tickDamage = hitSnapshot.Fire * (damageMult / 100f) * Mathf.Max(0f, 1f + sourceStats.GetValue(StatType.IgniteDamage) / 100f);
            return tickDamage > 0f;
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

            _poisonRemainingSeconds = duration;
            _poisonTotalTickDamage += tickDamage;
            _poisonStackCount++;
            if (tickDamage > _poisonStrongestTick)
            {
                _poisonStrongestTick = tickDamage;
                _poisonSource = source;
            }

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

            return AddOrRefreshIgnite(source, sourceStats, tickDamage, duration);
        }

        public struct IgniteSpreadCandidate
        {
            public int StackCount;
            public int MaxStacks;
            public float DistanceSqr;
        }

        public static int SelectIgniteSpreadTarget(IReadOnlyList<IgniteSpreadCandidate> candidates)
        {
            int best = -1;
            int bestTier = int.MaxValue;
            float bestDistance = float.MaxValue;
            if (candidates == null)
                return -1;

            for (int i = 0; i < candidates.Count; i++)
            {
                IgniteSpreadCandidate candidate = candidates[i];
                int maxStacks = Mathf.Max(1, candidate.MaxStacks);
                int tier = candidate.StackCount <= 0 ? 0 : candidate.StackCount < maxStacks ? 1 : 2;
                if (tier > bestTier)
                    continue;
                if (tier == bestTier && candidate.DistanceSqr >= bestDistance)
                    continue;

                best = i;
                bestTier = tier;
                bestDistance = candidate.DistanceSqr;
            }

            return best;
        }

        private bool AddOrRefreshIgnite(object source, IStatsProvider sourceStats, float tickDamage, float duration)
        {
            if (tickDamage <= 0f || duration <= 0f)
                return false;

            bool wasBurning = _igniteStacks.Count > 0;
            int maxStacks = ResolveMaxIgniteStacks(sourceStats);
            if (_igniteStacks.Count >= maxStacks)
            {
                int refreshIndex = FindIgniteStackToRefresh();
                if (refreshIndex < 0)
                    return false;

                IgniteStack stack = _igniteStacks[refreshIndex];
                stack.RemainingSeconds = duration;
                if (tickDamage >= stack.TickDamage)
                {
                    stack.Source = source;
                    stack.TickDamage = tickDamage;
                    stack.DurationSeconds = duration;
                }

                _igniteStacks[refreshIndex] = stack;
                SyncIgniteVisual();
                OnAilmentsChanged?.Invoke();
                return true;
            }

            _igniteStacks.Add(new IgniteStack
            {
                Source = source,
                TickDamage = tickDamage,
                RemainingSeconds = duration,
                DurationSeconds = duration
            });

            if (!wasBurning)
                _igniteSpreadTimer = ResolveIgniteSpreadSeconds(sourceStats);

            SyncIgniteVisual();
            OnAilmentsChanged?.Invoke();
            return true;
        }

        public void ReceiveSpreadIgnite(object source, IStatsProvider sourceStats, float tickDamage, float duration)
        {
            CacheOwner();
            if (_enemyHealth != null && _enemyHealth.IsDead)
                return;

            AddOrRefreshIgnite(source, sourceStats, tickDamage, duration);
        }

        public bool TryApplyFreeze(IStatsProvider sourceStats, object source, DamageSnapshot hitSnapshot)
        {
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Cold <= 0f || hitSnapshot.TotalDamage <= 0f)
                return false;

            float coldShare = hitSnapshot.Cold / hitSnapshot.TotalDamage;
            if (coldShare < FreezeColdDamageShareThreshold)
                return false;

            CacheOwner();
            if (_enemyHealth == null || _enemyHealth.IsDead || _enemyFreeze == null)
                return false;

            float chance = Mathf.Max(0f, sourceStats.GetValue(StatType.FreezeChance));
            if (chance <= 0f)
                return false;

            float avoid = _statsProvider != null ? Mathf.Clamp(_statsProvider.GetValue(StatType.ChanseToAvoidFreeze), 0f, 100f) : 0f;
            float finalChance = Mathf.Clamp(chance * (1f - avoid / 100f), 0f, 100f);
            if (UnityEngine.Random.value > finalChance / 100f)
                return false;

            float duration = sourceStats.GetValue(StatType.FreezeDuration);
            if (duration <= 0f)
                duration = DefaultFreezeDuration;

            bool applied = _enemyFreeze.TryApplyFreeze(duration);
            if (applied)
                OnAilmentsChanged?.Invoke();

            return applied;
        }

        public bool TryApplyShock(IStatsProvider sourceStats, object source, DamageSnapshot hitSnapshot)
        {
            if (sourceStats == null || hitSnapshot == null || hitSnapshot.Lightning <= 0f || hitSnapshot.TotalDamage <= 0f)
                return false;

            float lightningShare = hitSnapshot.Lightning / hitSnapshot.TotalDamage;
            if (lightningShare < ShockLightningDamageShareThreshold)
                return false;

            CacheOwner();

            float chance = Mathf.Max(0f, sourceStats.GetValue(StatType.ShockChance));
            if (chance <= 0f)
                return false;

            float avoid = _statsProvider != null ? Mathf.Clamp(_statsProvider.GetValue(StatType.ChanseToAvoidShock), 0f, 100f) : 0f;
            float finalChance = Mathf.Clamp(chance * (1f - avoid / 100f), 0f, 100f);
            if (UnityEngine.Random.value > finalChance / 100f)
                return false;

            float duration = sourceStats.GetValue(StatType.ShockDuration);
            if (duration <= 0f)
                duration = DefaultShockDuration;

            bool wasShocked = IsShocked;
            _shockRemainingSeconds = Mathf.Max(_shockRemainingSeconds, duration);
            EnsureShockVisual();
            _shockVisual?.Play(duration);

            if (!wasShocked)
                OnAilmentsChanged?.Invoke();

            return true;
        }

        public static float ResolveDamageTakenMoreMultiplier(Transform target)
        {
            if (target == null)
                return 1f;

            return TryResolve(target, out AilmentController controller) && controller != null
                ? controller.DamageTakenMoreMultiplier
                : 1f;
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
            _enemyFreeze = GetComponent<EnemyFreezeController>() ?? GetComponentInParent<EnemyFreezeController>();
            _shockVisual = GetComponent<ShockVisualController>() ?? GetComponentInParent<ShockVisualController>();
            _playerDamageReceiver = GetComponent<PlayerDamageReceiver>() ?? GetComponentInParent<PlayerDamageReceiver>();
        }

        private void EnsureShockVisual()
        {
            if (_shockVisual != null)
                return;

            _shockVisual = GetComponent<ShockVisualController>();
            if (_shockVisual == null)
                _shockVisual = gameObject.AddComponent<ShockVisualController>();
        }

        private void SyncIgniteVisual()
        {
            bool burning = _igniteStacks.Count > 0;
            if (!burning && _igniteVisual == null)
                return;

            if (_igniteVisual == null)
                _igniteVisual = GetComponent<IgniteVisualController>() ?? gameObject.AddComponent<IgniteVisualController>();

            _igniteVisual.SetBurning(burning);
        }

        private void UpdatePoison(float dt)
        {
            if (_poisonStackCount <= 0)
            {
                _poisonTickTimer = TickInterval;
                return;
            }

            if (dt <= 0f)
                return;

            _poisonTickTimer -= dt;
            _poisonRemainingSeconds -= dt;
            bool expired = _poisonRemainingSeconds <= 0f;
            if (expired)
                ClearPoison();

            while (!expired && _poisonTickTimer <= 0f && _poisonStackCount > 0)
            {
                _poisonTickTimer += TickInterval;
                ApplyPurePoisonTick(_poisonTotalTickDamage, _poisonSource);
            }

            if (expired)
                OnAilmentsChanged?.Invoke();
        }

        private void ClearPoison()
        {
            _poisonRemainingSeconds = 0f;
            _poisonTotalTickDamage = 0f;
            _poisonStackCount = 0;
            _poisonSource = null;
            _poisonStrongestTick = 0f;
            _poisonTickTimer = TickInterval;
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
                _igniteSpreadTimer = 0f;
                SyncIgniteVisual();
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

            if (_igniteStacks.Count == 0 || _enemyHealth == null || _enemyHealth.IsDead)
            {
                _igniteSpreadTimer = 0f;
            }
            else
            {
                _igniteSpreadTimer -= dt;
                if (_igniteSpreadTimer <= 0f)
                {
                    TrySpreadIgnite();
                    _igniteSpreadTimer = ResolveCurrentIgniteSpreadSeconds();
                }
            }

            SyncIgniteVisual();
            if (changed)
                OnAilmentsChanged?.Invoke();
        }

        private void UpdateShock(float dt)
        {
            if (_shockRemainingSeconds <= 0f)
                return;

            if (dt <= 0f)
                return;

            _shockRemainingSeconds -= dt;
            if (_shockRemainingSeconds > 0f)
                return;

            _shockRemainingSeconds = 0f;
            _shockVisual?.Stop();
            OnAilmentsChanged?.Invoke();
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

        private int FindIgniteStackToRefresh()
        {
            int bestIndex = -1;
            float bestRemaining = float.MaxValue;
            float bestDamage = float.MaxValue;
            for (int i = 0; i < _igniteStacks.Count; i++)
            {
                IgniteStack stack = _igniteStacks[i];
                if (stack.RemainingSeconds > bestRemaining)
                    continue;
                if (Mathf.Approximately(stack.RemainingSeconds, bestRemaining) && stack.TickDamage >= bestDamage)
                    continue;

                bestRemaining = stack.RemainingSeconds;
                bestDamage = stack.TickDamage;
                bestIndex = i;
            }

            return bestIndex;
        }

        private void TrySpreadIgnite()
        {
            if (_igniteStacks.Count == 0 || _enemyHealth == null || _enemyHealth.IsDead)
                return;

            int strongest = FindStrongestIgniteStackIndex();
            if (strongest < 0)
                return;

            IgniteStack stack = _igniteStacks[strongest];
            TryResolveStatsProvider(stack.Source, out IStatsProvider sourceStats);
            int maxStacks = ResolveMaxIgniteStacks(sourceStats);
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, IgniteSpreadRadius);
            if (hits == null || hits.Length == 0)
                return;

            var options = new List<SpreadOption>(hits.Length);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                    continue;

                EnemyHealth health = hit.GetComponent<EnemyHealth>() ?? hit.GetComponentInParent<EnemyHealth>();
                if (health == null || health.IsDead || health.gameObject == gameObject)
                    continue;

                if (!TryResolve(health.transform, out AilmentController other) || other == null || other == this)
                    continue;

                options.Add(new SpreadOption
                {
                    Controller = other,
                    Candidate = new IgniteSpreadCandidate
                    {
                        StackCount = other.GetStackCount(AilmentType.Ignite),
                        MaxStacks = maxStacks,
                        DistanceSqr = (other.transform.position - transform.position).sqrMagnitude
                    }
                });
            }

            if (options.Count == 0)
                return;

            var candidates = new IgniteSpreadCandidate[options.Count];
            for (int i = 0; i < options.Count; i++)
                candidates[i] = options[i].Candidate;

            int selected = SelectIgniteSpreadTarget(candidates);
            if (selected < 0)
                return;

            float duration = stack.DurationSeconds > 0f ? stack.DurationSeconds : DefaultIgniteDuration;
            options[selected].Controller.ReceiveSpreadIgnite(stack.Source, sourceStats, stack.TickDamage, duration);
        }

        private float ResolveCurrentIgniteSpreadSeconds()
        {
            int strongest = FindStrongestIgniteStackIndex();
            if (strongest < 0)
                return DefaultIgniteSpreadDuration;

            TryResolveStatsProvider(_igniteStacks[strongest].Source, out IStatsProvider sourceStats);
            return ResolveIgniteSpreadSeconds(sourceStats);
        }

        private static float ResolveIgniteSpreadSeconds(IStatsProvider sourceStats)
        {
            if (sourceStats == null)
                return DefaultIgniteSpreadDuration;

            float seconds = sourceStats.GetValue(StatType.IgniteSpreadDuration);
            return seconds > 0f ? seconds : DefaultIgniteSpreadDuration;
        }

        private static int ResolveMaxIgniteStacks(IStatsProvider sourceStats)
        {
            if (sourceStats == null)
                return 1;

            return Mathf.Max(1, Mathf.FloorToInt(sourceStats.GetValue(StatType.MaxIgniteStacks)));
        }

        private int FindStrongestIgniteStackIndex()
        {
            int strongestIndex = -1;
            float strongestDamage = float.MinValue;
            for (int i = 0; i < _igniteStacks.Count; i++)
            {
                if (_igniteStacks[i].TickDamage <= strongestDamage)
                    continue;

                strongestDamage = _igniteStacks[i].TickDamage;
                strongestIndex = i;
            }

            return strongestIndex;
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
            public float DurationSeconds;
        }

        private struct SpreadOption
        {
            public AilmentController Controller;
            public IgniteSpreadCandidate Candidate;
        }
    }
}
