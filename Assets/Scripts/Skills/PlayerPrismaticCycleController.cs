using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    // Player-owned progress survives replacement of a skill runner on equipment changes.
    // Character changes and owner disable clear it; launched volleys own immutable snapshots.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerPrismaticCycleController : MonoBehaviour
    {
        private readonly Dictionary<int, int> _nextPhases = new Dictionary<int, int>();
        private PlayerStats _stats;
        private void Awake() => _stats = GetComponent<PlayerStats>();
        private void Update() { if (_stats != null && _stats.Health != null && _stats.Health.Current <= 0) ResetCycles(); }
        public PrismaticCycleCast BeginCast(int slot, StepEntry step)
        {
            _nextPhases.TryGetValue(slot, out int phase);
            _nextPhases[slot] = (phase + 1) % 4;
            return new PrismaticCycleCast(phase, step);
        }
        public void ResetCycles() => _nextPhases.Clear();
        private void OnDisable() => ResetCycles();
    }

    public sealed class PrismaticCycleCast
    {
        public DamageChannel Element { get; }
        public StepEntry Step { get; }
        public Sprite ProjectileSprite { get; }
        public StepEntry BonusStep { get; }
        public Sprite BonusSprite { get; }
        public GameObject BurstVfxPrefab { get; }
        public bool CompletesCycle { get; }

        public PrismaticCycleCast(int phase, StepEntry source)
        {
            Element = phase % 4 == 0 ? DamageChannel.Fire : phase % 4 == 1 ? DamageChannel.Cold : DamageChannel.Lightning;
            CompletesCycle = phase % 4 == 3;
            ProjectileSprite = source.GetObject<Sprite>(Element + "Sprite");
            BurstVfxPrefab = source.GetObject<GameObject>("BurstVfxPrefab");
            BonusSprite = source.GetObject<Sprite>("BonusSprite");
            Step = new StepEntry
            {
                StepDefinition = source.StepDefinition, Overrides = new List<StepParamValue>(source.Overrides),
                DamageConversions = ConvertAllTo(Element), ScopedStatModifiers = source.ScopedStatModifiers,
                TargetAilmentStackModifiers = source.TargetAilmentStackModifiers, OnHitEffects = source.OnHitEffects
            };
            BonusStep = new StepEntry
            {
                StepDefinition = source.StepDefinition, Overrides = new List<StepParamValue>(source.Overrides),
                DamageConversions = BurstConversions(), ScopedStatModifiers = new List<SerializableStatModifier>(source.ScopedStatModifiers),
                TargetAilmentStackModifiers = source.TargetAilmentStackModifiers, OnHitEffects = source.OnHitEffects
            };
            BonusStep.ScopedStatModifiers.Add(new SerializableStatModifier { Stat = StatType.SpellDamage,
                Type = StatModType.PercentMult, Value = source.GetFloat("BonusMoreSpellDamage", 100) });
            if (CompletesCycle) { Step = BonusStep; ProjectileSprite = BonusSprite; }
        }
        public static List<DamageConversionRule> ConvertAllTo(DamageChannel element)
        {
            var rules = new List<DamageConversionRule>();
            foreach (var source in new[] { DamageChannel.Physical, DamageChannel.Fire, DamageChannel.Cold, DamageChannel.Lightning })
                if (source != element) rules.Add(new DamageConversionRule { Source = source, Target = element, Percent = 100 });
            return rules;
        }
        public static List<DamageConversionRule> BurstConversions()
        {
            var rules = new List<DamageConversionRule>();
            foreach (var source in new[] { DamageChannel.Physical, DamageChannel.Fire, DamageChannel.Cold, DamageChannel.Lightning })
                foreach (var target in new[] { DamageChannel.Fire, DamageChannel.Cold, DamageChannel.Lightning })
                    if (source != target) rules.Add(new DamageConversionRule { Source = source, Target = target, Percent = 100f / 3 });
            return rules;
        }
    }
}
