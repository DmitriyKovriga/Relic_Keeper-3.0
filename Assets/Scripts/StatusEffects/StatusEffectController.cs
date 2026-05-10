using System;
using System.Collections.Generic;
using Scripts.Enemies;
using Scripts.GameplayEvents;
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
            public string RuntimeId { get; internal set; }
            public StatusEffectKind RuntimeKind { get; internal set; }
            public float DurationSeconds { get; internal set; }
            public float RemainingSeconds { get; internal set; }
            public float RemainingNormalized => DurationSeconds > 0.0001f ? Mathf.Clamp01(RemainingSeconds / DurationSeconds) : 0f;
            internal List<(StatType type, StatModifier modifier)> AppliedModifiers => _appliedModifiers;
        }

        public sealed class RuntimeStatusHandle : IDisposable
        {
            private StatusEffectController _owner;
            private ActiveEffectInstance _instance;

            internal RuntimeStatusHandle(StatusEffectController owner, ActiveEffectInstance instance)
            {
                _owner = owner;
                _instance = instance;
            }

            public void Dispose()
            {
                if (_owner == null || _instance == null)
                    return;

                _owner.RemoveRuntimeInstance(_instance);
                _owner = null;
                _instance = null;
            }
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
            GameplayEventBus.EventRaised += HandleGameplayEvent;
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
            GameplayEventBus.EventRaised -= HandleGameplayEvent;
            ResetAll();
        }

        public bool ApplyStatusEffect(StatusEffectSO effect, object source = null)
        {
            return ApplyStatusEffectScaled(effect, 1, source);
        }

        public bool ApplyStatusEffectScaled(StatusEffectSO effect, int stackCount, object source = null)
        {
            if (effect == null)
                return false;

            if (!CacheOwner())
                return false;

            int effectiveStacks = Mathf.Max(1, stackCount);

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
            ApplyInstanceModifiers(instance, source ?? this, effectiveStacks);
            NotifyCarrierStatChanges();
            OnActiveEffectsChanged?.Invoke();
            return created || instance.AppliedModifiers.Count > 0 || effect.ShowInHud;
        }

        public RuntimeStatusHandle ApplyRuntimeStatusEffect(
            IReadOnlyList<SerializableStatModifier> modifiers,
            float durationSeconds,
            StatusEffectKind kind,
            object source = null,
            string runtimeId = null)
        {
            if (modifiers == null || modifiers.Count == 0)
                return null;

            if (!CacheOwner())
                return null;

            var instance = new ActiveEffectInstance
            {
                Effect = null,
                RuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? "RuntimeStatus" : runtimeId,
                RuntimeKind = kind,
                DurationSeconds = durationSeconds,
                RemainingSeconds = durationSeconds
            };

            ApplyRuntimeModifiers(instance, modifiers, source ?? this);
            if (instance.AppliedModifiers.Count == 0)
                return null;

            if (durationSeconds > 0f)
                _activeEffects.Add(instance);

            NotifyCarrierStatChanges();
            OnActiveEffectsChanged?.Invoke();
            return new RuntimeStatusHandle(this, instance);
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

        private void RemoveRuntimeInstance(ActiveEffectInstance instance)
        {
            if (instance == null)
                return;

            int activeIndex = _activeEffects.IndexOf(instance);
            RemoveInstanceModifiers(instance);
            if (activeIndex >= 0)
                _activeEffects.RemoveAt(activeIndex);

            NotifyCarrierStatChanges();
            OnActiveEffectsChanged?.Invoke();
        }

        private void ApplyInstanceModifiers(ActiveEffectInstance instance, object source, int stackCount = 1)
        {
            instance.AppliedModifiers.Clear();
            if (instance.Effect == null)
                return;

            int effectiveStacks = Mathf.Max(1, stackCount);
            if (instance.Effect.Modifiers != null)
            {
                for (int i = 0; i < instance.Effect.Modifiers.Count; i++)
                {
                    SerializableStatModifier modifierData = instance.Effect.Modifiers[i];
                    modifierData.Value *= effectiveStacks;
                    ApplySingleModifier(instance, modifierData, source ?? instance);
                }
            }

            if (instance.Effect.DerivedModifiers != null)
            {
                for (int i = 0; i < instance.Effect.DerivedModifiers.Count; i++)
                {
                    if (TryBuildDerivedModifier(instance.Effect.DerivedModifiers[i], effectiveStacks, out SerializableStatModifier modifierData))
                        ApplySingleModifier(instance, modifierData, source ?? instance);
                }
            }
        }

        private void ApplyRuntimeModifiers(
            ActiveEffectInstance instance,
            IReadOnlyList<SerializableStatModifier> modifiers,
            object source)
        {
            instance.AppliedModifiers.Clear();
            for (int i = 0; i < modifiers.Count; i++)
            {
                SerializableStatModifier modifierData = modifiers[i];
                ApplySingleModifier(instance, modifierData, source ?? instance);
            }
        }

        private void ApplySingleModifier(ActiveEffectInstance instance, SerializableStatModifier modifierData, object source)
        {
            StatModifier runtimeModifier = modifierData.ToStatModifier(source ?? instance);
            if (!TryAddModifier(modifierData.Stat, runtimeModifier))
                return;

            instance.AppliedModifiers.Add((modifierData.Stat, runtimeModifier));
            _changedStatsBuffer.Add(modifierData.Stat);
        }

        private bool TryBuildDerivedModifier(DerivedStatModifier derived, int stackCount, out SerializableStatModifier modifierData)
        {
            modifierData = default;
            if (_statsProvider == null)
                return false;

            float sourceValue = _statsProvider.GetValue(derived.SourceStat);
            float value = sourceValue * (derived.SourcePercent / 100f) * Mathf.Max(1, stackCount);
            if (Mathf.Approximately(value, 0f))
                return false;

            modifierData = new SerializableStatModifier
            {
                Stat = derived.TargetStat,
                Value = value,
                Type = derived.TargetModifierType
            };
            return true;
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

        private void HandleGameplayEvent(GameplayEventContext context)
        {
            if (context == null || _activeEffects.Count == 0 || gameObject == null)
                return;

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (i >= _activeEffects.Count)
                    continue;

                ActiveEffectInstance instance = _activeEffects[i];
                StatusEffectSO effect = instance.Effect;
                if (effect == null || effect.EventReactions == null || effect.EventReactions.Count == 0)
                    continue;

                for (int r = 0; r < effect.EventReactions.Count; r++)
                {
                    StatusEventReaction reaction = effect.EventReactions[r];
                    if (reaction == null || reaction.EventType != context.Type)
                        continue;
                    if (!MatchesSubject(reaction.Subject, context))
                        continue;

                    ExecuteReaction(instance, reaction);
                    if (!_activeEffects.Contains(instance))
                        break;
                }
            }
        }

        private bool MatchesSubject(StatusEventSubject subject, GameplayEventContext context)
        {
            return subject switch
            {
                StatusEventSubject.CarrierAsTarget => context.Target == gameObject,
                StatusEventSubject.CarrierAsSource => context.Source == gameObject,
                StatusEventSubject.CarrierAsSourceOrTarget => context.HasParticipant(gameObject),
                StatusEventSubject.Any => true,
                _ => false
            };
        }

        private void ExecuteReaction(ActiveEffectInstance instance, StatusEventReaction reaction)
        {
            switch (reaction.Action)
            {
                case StatusEventReactionAction.ApplyStatusEffect:
                    if (reaction.StatusEffectToApply != null)
                        ApplyStatusEffect(reaction.StatusEffectToApply, instance.Effect != null ? instance.Effect : this);
                    break;
                case StatusEventReactionAction.ApplyQuickEffect:
                    ApplyQuickReactionEffect(reaction, instance);
                    break;
                case StatusEventReactionAction.EndCurrentEffect:
                    RemoveRuntimeInstance(instance);
                    break;
                case StatusEventReactionAction.ExtendCurrentEffect:
                    ExtendInstance(instance, reaction.ExtendSeconds);
                    break;
            }
        }

        private void ApplyQuickReactionEffect(StatusEventReaction reaction, ActiveEffectInstance sourceInstance)
        {
            var modifiers = new List<SerializableStatModifier>();
            if (reaction.QuickModifiers != null)
                modifiers.AddRange(reaction.QuickModifiers);

            if (reaction.QuickDerivedModifiers != null)
            {
                for (int i = 0; i < reaction.QuickDerivedModifiers.Count; i++)
                {
                    if (TryBuildDerivedModifier(reaction.QuickDerivedModifiers[i], 1, out SerializableStatModifier modifierData))
                        modifiers.Add(modifierData);
                }
            }

            if (modifiers.Count == 0)
                return;

            float duration = Mathf.Max(0.01f, reaction.QuickEffectDurationSeconds);
            ApplyRuntimeStatusEffect(
                modifiers,
                duration,
                reaction.QuickEffectKind,
                sourceInstance.Effect != null ? sourceInstance.Effect : this,
                "EventQuickEffect");
        }

        private void ExtendInstance(ActiveEffectInstance instance, float seconds)
        {
            if (instance == null || seconds <= 0f)
                return;

            instance.RemainingSeconds += seconds;
            instance.DurationSeconds = Mathf.Max(instance.DurationSeconds, instance.RemainingSeconds);
            OnActiveEffectsChanged?.Invoke();
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
