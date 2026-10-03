using System;
using System.Collections.Generic;
using Scripts.GameplayEvents;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills.PassiveTree
{
    public interface IPassiveSkillCooldowns
    {
        bool ReduceRandomSkillCooldown(float seconds, Func<int, int> pickIndex);
        void ReduceAllCooldowns(float seconds);
        void ReduceSkillCooldown(int slotIndex, float seconds);
    }

    /// <summary>
    /// Runs the non-static parts of allocated passive nodes for one character:
    /// conditional modifiers, event triggers and per-hit scaling.
    /// </summary>
    public sealed class PassiveEffectRuntime : IDisposable
    {
        private static readonly Dictionary<PlayerStats, PassiveEffectRuntime> Registry = new Dictionary<PlayerStats, PassiveEffectRuntime>();

        private sealed class ConditionalEntry
        {
            public PassiveConditionalModifiers Data;
            public bool Active;
            public readonly List<(StatType type, StatModifier modifier)> Applied = new List<(StatType, StatModifier)>();
        }

        private sealed class TriggerEntry
        {
            public PassiveTriggeredEffect Data;
            public string Key;
        }

        private readonly PlayerStats _owner;
        private readonly GameObject _ownerObject;
        private readonly List<ConditionalEntry> _conditionals = new List<ConditionalEntry>();
        private readonly List<TriggerEntry> _triggers = new List<TriggerEntry>();
        private readonly List<PassiveHitScalingRule> _hitRules = new List<PassiveHitScalingRule>();
        private readonly Dictionary<PassiveCondition, float> _lastConditionEventTime = new Dictionary<PassiveCondition, float>();
        private readonly Dictionary<string, float> _nextTriggerAllowedAt = new Dictionary<string, float>();
        private IPassiveSkillCooldowns _cooldowns;
        private bool _disposed;

        public Func<float> Clock = () => Time.time;
        public Func<float> Random01 = () => UnityEngine.Random.value;
        public Func<int, int> RandomIndex = count => UnityEngine.Random.Range(0, count);

        public IPassiveSkillCooldowns Cooldowns
        {
            get => _cooldowns ??= _ownerObject != null ? _ownerObject.GetComponent<PlayerSkillManager>() : null;
            set => _cooldowns = value;
        }

        public int ActiveConditionalCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _conditionals.Count; i++)
                    if (_conditionals[i].Active) count++;
                return count;
            }
        }

        public PassiveEffectRuntime(PlayerStats owner)
        {
            _owner = owner;
            _ownerObject = owner != null ? owner.gameObject : null;
            if (_owner != null)
                Registry[_owner] = this;
            GameplayEventBus.EventRaised += HandleEvent;
        }

        public static bool TryGet(PlayerStats owner, out PassiveEffectRuntime runtime)
        {
            runtime = null;
            return owner != null && Registry.TryGetValue(owner, out runtime) && runtime != null;
        }

        public static void AppendHitModifiers(PlayerStats owner, in PassiveHitContext context, List<SerializableStatModifier> into)
        {
            if (into != null && TryGet(owner, out PassiveEffectRuntime runtime))
                runtime.CollectHitModifiers(context, into);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            GameplayEventBus.EventRaised -= HandleEvent;
            Clear();
            if (_owner != null && Registry.TryGetValue(_owner, out PassiveEffectRuntime registered) && registered == this)
                Registry.Remove(_owner);
        }

        /// <summary>Replaces the effect set. Recency memory and internal cooldowns survive the rebuild.</summary>
        public void Rebuild(IEnumerable<PassiveNodeDefinition> allocatedNodes)
        {
            bool removed = RemoveAllConditionalModifiers();
            _conditionals.Clear();
            _triggers.Clear();
            _hitRules.Clear();

            if (allocatedNodes != null)
            {
                foreach (PassiveNodeDefinition node in allocatedNodes)
                    AddNode(node);
            }

            EvaluateConditions(forceNotify: removed);
        }

        public void Clear()
        {
            bool changed = RemoveAllConditionalModifiers();
            _conditionals.Clear();
            _triggers.Clear();
            _hitRules.Clear();
            if (changed)
                _owner?.NotifyChanged();
        }

        public void Tick()
        {
            if (_conditionals.Count > 0)
                EvaluateConditions(forceNotify: false);
        }

        public void CollectHitModifiers(in PassiveHitContext context, List<SerializableStatModifier> into)
        {
            if (into == null)
                return;

            for (int i = 0; i < _hitRules.Count; i++)
            {
                PassiveHitScalingRule rule = _hitRules[i];
                float steps = rule.CalculateSteps(context);
                if (steps <= 0f || rule.ModifiersPerStep == null)
                    continue;

                for (int m = 0; m < rule.ModifiersPerStep.Count; m++)
                {
                    SerializableStatModifier perStep = rule.ModifiersPerStep[m];
                    into.Add(new SerializableStatModifier { Stat = perStep.Stat, Type = perStep.Type, Value = perStep.Value * steps });
                }
            }
        }

        public void HandleEvent(GameplayEventContext context)
        {
            if (_disposed || context == null || _ownerObject == null)
                return;

            float now = Clock();
            bool recencyChanged = false;
            for (int i = 0; i < _conditionals.Count; i++)
            {
                PassiveCondition condition = _conditionals[i].Data.Condition;
                if (condition == null || condition.Kind != PassiveConditionKind.EventRecency)
                    continue;
                if (!condition.Event.Matches(context, _ownerObject))
                    continue;

                _lastConditionEventTime[condition] = now;
                recencyChanged = true;
            }

            if (recencyChanged)
                EvaluateConditions(forceNotify: false);

            for (int i = 0; i < _triggers.Count; i++)
                TryFire(_triggers[i], context, now);
        }

        private void AddNode(PassiveNodeDefinition node)
        {
            PassiveNodeTemplateSO template = node?.Template;
            if (template == null)
                return;

            if (template.ConditionalModifiers != null)
            {
                foreach (PassiveConditionalModifiers group in template.ConditionalModifiers)
                {
                    if (group?.Condition != null && group.Modifiers != null && group.Modifiers.Count > 0)
                        _conditionals.Add(new ConditionalEntry { Data = group });
                }
            }

            if (template.TriggeredEffects != null)
            {
                for (int i = 0; i < template.TriggeredEffects.Count; i++)
                {
                    PassiveTriggeredEffect effect = template.TriggeredEffects[i];
                    if (effect?.Trigger != null)
                        _triggers.Add(new TriggerEntry { Data = effect, Key = $"Passive:{node.ID}:{i}" });
                }
            }

            if (template.HitScalingRules != null)
            {
                foreach (PassiveHitScalingRule rule in template.HitScalingRules)
                {
                    if (rule != null)
                        _hitRules.Add(rule);
                }
            }
        }

        // ---------------------------------------------------------------- conditions

        private void EvaluateConditions(bool forceNotify)
        {
            if (_owner == null)
                return;

            bool changed = false;
            float now = Clock();
            for (int i = 0; i < _conditionals.Count; i++)
            {
                ConditionalEntry entry = _conditionals[i];
                bool shouldBeActive = IsConditionMet(entry.Data.Condition, now);
                if (shouldBeActive == entry.Active)
                    continue;

                if (shouldBeActive)
                    ApplyConditional(entry);
                else
                    RemoveConditional(entry);
                changed = true;
            }

            if (changed || forceNotify)
                _owner.NotifyChanged();
        }

        public bool IsConditionMet(PassiveCondition condition, float now)
        {
            if (condition == null)
                return false;

            switch (condition.Kind)
            {
                case PassiveConditionKind.EventRecency:
                    bool happenedRecently = _lastConditionEventTime.TryGetValue(condition, out float last)
                                            && now - last < Mathf.Max(0.05f, condition.WindowSeconds);
                    return condition.Recency == PassiveRecencyMode.Recently ? happenedRecently : !happenedRecently;
                case PassiveConditionKind.ResourceThreshold:
                    StatResource resource = condition.Resource == PassiveResource.Mana ? _owner.Mana : _owner.Health;
                    if (resource == null)
                        return false;
                    float percent = resource.Percent * 100f;
                    return condition.Comparison == PassiveComparison.AtLeast
                        ? percent >= condition.ThresholdPercent - 0.001f
                        : percent <= condition.ThresholdPercent + 0.001f;
                default:
                    return false;
            }
        }

        private void ApplyConditional(ConditionalEntry entry)
        {
            entry.Active = true;
            entry.Applied.Clear();
            foreach (SerializableStatModifier data in entry.Data.Modifiers)
            {
                StatModifier modifier = data.ToStatModifier(entry.Data);
                _owner.GetStat(data.Stat).AddModifier(modifier);
                entry.Applied.Add((data.Stat, modifier));
            }
        }

        private void RemoveConditional(ConditionalEntry entry)
        {
            entry.Active = false;
            foreach (var (type, modifier) in entry.Applied)
                _owner.GetStat(type).RemoveModifier(modifier);
            entry.Applied.Clear();
        }

        private bool RemoveAllConditionalModifiers()
        {
            bool any = false;
            if (_owner == null)
                return false;

            for (int i = 0; i < _conditionals.Count; i++)
            {
                if (!_conditionals[i].Active)
                    continue;
                RemoveConditional(_conditionals[i]);
                any = true;
            }

            return any;
        }

        // ---------------------------------------------------------------- triggers

        private void TryFire(TriggerEntry entry, GameplayEventContext context, float now)
        {
            PassiveTriggeredEffect effect = entry.Data;
            if (!effect.Trigger.Matches(context, _ownerObject))
                return;
            if (_nextTriggerAllowedAt.TryGetValue(entry.Key, out float allowedAt) && now < allowedAt)
                return;
            if (effect.ChancePercent < 100f && Random01() >= effect.ChancePercent / 100f)
                return;

            if (!Execute(entry))
                return;

            if (effect.InternalCooldownSeconds > 0f)
                _nextTriggerAllowedAt[entry.Key] = now + effect.InternalCooldownSeconds;
        }

        private bool Execute(TriggerEntry entry)
        {
            PassiveTriggeredEffect effect = entry.Data;
            switch (effect.Action)
            {
                case PassiveTriggerAction.ReduceSkillCooldown:
                    return ReduceCooldown(effect);
                case PassiveTriggerAction.ApplyBuff:
                    return ApplyBuff(entry);
                case PassiveTriggerAction.RestoreResource:
                    return RestoreResource(effect);
                default:
                    return false;
            }
        }

        private bool ReduceCooldown(PassiveTriggeredEffect effect)
        {
            IPassiveSkillCooldowns cooldowns = Cooldowns;
            if (cooldowns == null || effect.CooldownSeconds <= 0f)
                return false;

            switch (effect.CooldownTarget)
            {
                case PassiveCooldownTarget.RandomOnCooldown:
                    return cooldowns.ReduceRandomSkillCooldown(effect.CooldownSeconds, RandomIndex);
                case PassiveCooldownTarget.AllSkills:
                    cooldowns.ReduceAllCooldowns(effect.CooldownSeconds);
                    return true;
                case PassiveCooldownTarget.Slot:
                    cooldowns.ReduceSkillCooldown(effect.SkillSlot, effect.CooldownSeconds);
                    return true;
                default:
                    return false;
            }
        }

        private bool ApplyBuff(TriggerEntry entry)
        {
            PassiveTriggeredEffect effect = entry.Data;
            if (_ownerObject == null || !StatusEffectController.TryResolve(_ownerObject.transform, out StatusEffectController controller) || controller == null)
                return false;

            if (effect.StatusEffect != null)
            {
                if (effect.Stacking == PassiveBuffStacking.IgnoreWhileActive && HasAuthoredEffect(controller, effect.StatusEffect))
                    return false;
                return controller.ApplyStatusEffect(effect.StatusEffect, entry);
            }

            if (effect.BuffModifiers == null || effect.BuffModifiers.Count == 0)
                return false;

            string id = ResolveBuffInstanceId(controller, entry);
            if (id == null)
                return false;

            return controller.ApplyRuntimeStatusEffect(
                effect.BuffModifiers,
                effect.BuffDurationSeconds,
                StatusEffectKind.Buff,
                entry,
                id,
                effect.BuffAuraColor) != null;
        }

        private static string ResolveBuffInstanceId(StatusEffectController controller, TriggerEntry entry)
        {
            PassiveTriggeredEffect effect = entry.Data;
            switch (effect.Stacking)
            {
                case PassiveBuffStacking.IgnoreWhileActive:
                    return FindRuntimeInstance(controller, entry.Key) != null ? null : entry.Key;
                case PassiveBuffStacking.IndependentStacks:
                    int maxStacks = Mathf.Max(1, effect.MaxStacks);
                    string weakestId = null;
                    float weakestRemaining = float.MaxValue;
                    for (int i = 0; i < maxStacks; i++)
                    {
                        string stackId = $"{entry.Key}#{i}";
                        StatusEffectController.ActiveEffectInstance instance = FindRuntimeInstance(controller, stackId);
                        if (instance == null)
                            return stackId;
                        if (instance.RemainingSeconds < weakestRemaining)
                        {
                            weakestRemaining = instance.RemainingSeconds;
                            weakestId = stackId;
                        }
                    }
                    return weakestId;
                default:
                    return entry.Key;
            }
        }

        private static StatusEffectController.ActiveEffectInstance FindRuntimeInstance(StatusEffectController controller, string id)
        {
            IReadOnlyList<StatusEffectController.ActiveEffectInstance> active = controller.ActiveEffects;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Effect == null && string.Equals(active[i].RuntimeId, id, StringComparison.Ordinal))
                    return active[i];
            }

            return null;
        }

        private static bool HasAuthoredEffect(StatusEffectController controller, StatusEffectSO effect)
        {
            IReadOnlyList<StatusEffectController.ActiveEffectInstance> active = controller.ActiveEffects;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Effect == effect)
                    return true;
            }

            return false;
        }

        private bool RestoreResource(PassiveTriggeredEffect effect)
        {
            StatResource resource = effect.Resource == PassiveResource.Mana ? _owner?.Mana : _owner?.Health;
            if (resource == null || effect.RestoreAmount <= 0f)
                return false;

            float amount = effect.RestoreAsPercentOfMax ? resource.Max * effect.RestoreAmount / 100f : effect.RestoreAmount;
            resource.Increase(amount);
            return true;
        }
    }
}
