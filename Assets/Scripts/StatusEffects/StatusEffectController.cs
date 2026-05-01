using System;
using System.Collections.Generic;
using Scripts.Enemies;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class StatusEffectController : MonoBehaviour
    {
        public sealed class ActiveEffectInstance
        {
            private readonly List<(StatType type, StatModifier modifier)> _appliedModifiers = new List<(StatType, StatModifier)>();

            public StatusEffectSO Effect { get; internal set; }
            public float DurationSeconds { get; internal set; }
            public float RemainingSeconds { get; internal set; }
            public float RemainingNormalized => DurationSeconds > 0.0001f ? Mathf.Clamp01(RemainingSeconds / DurationSeconds) : 0f;
            internal List<(StatType type, StatModifier modifier)> AppliedModifiers => _appliedModifiers;
        }

        private readonly List<ActiveEffectInstance> _activeEffects = new List<ActiveEffectInstance>();
        private readonly HashSet<StatType> _changedStatsBuffer = new HashSet<StatType>();

        private IStatsProvider _statsProvider;
        private PlayerStats _playerStats;
        private EnemyStats _enemyStats;
        private EnemyHealth _enemyHealth;

        public event Action OnActiveEffectsChanged;

        public IReadOnlyList<ActiveEffectInstance> ActiveEffects => _activeEffects;

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
            if (_activeEffects.Count == 0)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                ActiveEffectInstance instance = _activeEffects[i];
                instance.RemainingSeconds -= dt;
                if (instance.RemainingSeconds > 0f)
                    continue;

                RemoveInstanceAt(i);
            }
        }

        private void OnDisable()
        {
            ResetAll();
        }

        public bool ApplyStatusEffect(StatusEffectSO effect, object source = null)
        {
            if (effect == null)
                return false;

            if (!CacheOwner())
                return false;

            ActiveEffectInstance instance = FindInstance(effect);
            bool created = false;
            if (instance == null)
            {
                instance = new ActiveEffectInstance();
                _activeEffects.Add(instance);
                created = true;
            }

            instance.Effect = effect;
            instance.DurationSeconds = effect.DurationSeconds;
            instance.RemainingSeconds = effect.DurationSeconds;

            RemoveInstanceModifiers(instance);
            ApplyInstanceModifiers(instance, source ?? this);
            NotifyCarrierStatChanges();
            OnActiveEffectsChanged?.Invoke();
            return created || instance.AppliedModifiers.Count > 0 || effect.ShowInHud;
        }

        public void ResetAll()
        {
            CacheOwner();

            if (_activeEffects.Count == 0)
                return;

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
                RemoveInstanceModifiers(_activeEffects[i]);

            _activeEffects.Clear();
            NotifyCarrierStatChanges();
            OnActiveEffectsChanged?.Invoke();
        }

        public static bool TryResolve(Transform candidate, out StatusEffectController controller)
        {
            controller = null;
            if (candidate == null)
                return false;

            controller = candidate.GetComponent<StatusEffectController>();
            if (controller != null)
                return true;

            controller = candidate.GetComponentInParent<StatusEffectController>();
            if (controller != null)
                return true;

            PlayerStats playerStats = candidate.GetComponent<PlayerStats>() ?? candidate.GetComponentInParent<PlayerStats>();
            if (playerStats != null)
            {
                controller = playerStats.GetComponent<StatusEffectController>();
                if (controller == null)
                    controller = playerStats.gameObject.AddComponent<StatusEffectController>();
                return controller != null;
            }

            EnemyStats enemyStats = candidate.GetComponent<EnemyStats>() ?? candidate.GetComponentInParent<EnemyStats>();
            if (enemyStats != null)
            {
                controller = enemyStats.GetComponent<StatusEffectController>();
                if (controller == null)
                    controller = enemyStats.gameObject.AddComponent<StatusEffectController>();
                return controller != null;
            }

            return false;
        }

        private ActiveEffectInstance FindInstance(StatusEffectSO effect)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Effect == effect)
                    return _activeEffects[i];
            }

            return null;
        }

        private void RemoveInstanceAt(int index)
        {
            if (index < 0 || index >= _activeEffects.Count)
                return;

            ActiveEffectInstance instance = _activeEffects[index];
            RemoveInstanceModifiers(instance);
            _activeEffects.RemoveAt(index);
            NotifyCarrierStatChanges();
            OnActiveEffectsChanged?.Invoke();
        }

        private void ApplyInstanceModifiers(ActiveEffectInstance instance, object source)
        {
            instance.AppliedModifiers.Clear();
            if (instance.Effect == null || instance.Effect.Modifiers == null)
                return;

            for (int i = 0; i < instance.Effect.Modifiers.Count; i++)
            {
                SerializableStatModifier modifierData = instance.Effect.Modifiers[i];
                StatModifier runtimeModifier = modifierData.ToStatModifier(source ?? instance);
                if (!TryAddModifier(modifierData.Stat, runtimeModifier))
                    continue;

                instance.AppliedModifiers.Add((modifierData.Stat, runtimeModifier));
                _changedStatsBuffer.Add(modifierData.Stat);
            }
        }

        private void RemoveInstanceModifiers(ActiveEffectInstance instance)
        {
            if (instance == null || instance.AppliedModifiers == null)
                return;

            for (int i = instance.AppliedModifiers.Count - 1; i >= 0; i--)
            {
                var (type, modifier) = instance.AppliedModifiers[i];
                TryRemoveModifier(type, modifier);
                _changedStatsBuffer.Add(type);
            }

            instance.AppliedModifiers.Clear();
        }

        private bool TryAddModifier(StatType type, StatModifier modifier)
        {
            if (_playerStats != null)
            {
                _playerStats.GetStat(type).AddModifier(modifier);
                return true;
            }

            if (_enemyStats != null)
            {
                _enemyStats.AddModifier(type, modifier);
                return true;
            }

            if (_statsProvider != null && _statsProvider.TryGetStat(type, out CharacterStat stat) && stat != null)
            {
                stat.AddModifier(modifier);
                return true;
            }

            return false;
        }

        private void TryRemoveModifier(StatType type, StatModifier modifier)
        {
            if (_playerStats != null)
            {
                _playerStats.GetStat(type).RemoveModifier(modifier);
                return;
            }

            if (_enemyStats != null)
            {
                _enemyStats.RemoveModifier(type, modifier);
                return;
            }

            if (_statsProvider != null && _statsProvider.TryGetStat(type, out CharacterStat stat) && stat != null)
                stat.RemoveModifier(modifier);
        }

        private bool CacheOwner()
        {
            _playerStats = GetComponent<PlayerStats>();
            _enemyStats = _playerStats == null ? GetComponent<EnemyStats>() : null;
            _enemyHealth = GetComponent<EnemyHealth>();
            _statsProvider = _playerStats as IStatsProvider ?? _enemyStats as IStatsProvider ?? GetComponent<IStatsProvider>();
            return _statsProvider != null;
        }

        private void NotifyCarrierStatChanges()
        {
            if (_changedStatsBuffer.Count == 0)
                return;

            bool maxHealthChanged = _changedStatsBuffer.Contains(StatType.MaxHealth);
            bool maxManaChanged = _changedStatsBuffer.Contains(StatType.MaxMana);

            if (_playerStats != null)
            {
                if (maxHealthChanged || maxManaChanged)
                    _playerStats.RefreshDerivedResourcesAfterExternalStatChange();
                else
                    _playerStats.NotifyChanged();
            }
            else if (_enemyHealth != null && maxHealthChanged)
            {
                _enemyHealth.SyncMaxHealthFromStats();
            }

            _changedStatsBuffer.Clear();
        }
    }
}
