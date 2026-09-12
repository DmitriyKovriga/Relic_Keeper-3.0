using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;

namespace Scripts.Skills.PassiveTree
{
    [System.Serializable]
    public class PassiveStatScalingRule
    {
        [Tooltip("Final stat used as the source, after its regular Flat/Increase/More modifiers.")]
        public StatType SourceStat = StatType.Armor;
        [Min(0.0001f)]
        [Tooltip("How much of the source stat is required for one step.")]
        public float SourceAmountPerStep = 100f;
        [Tooltip("Use complete steps only: 250 Armor with a step of 100 gives 2 steps.")]
        public bool UseWholeSteps = true;

        [Space(3f)]
        public StatType TargetStat = StatType.DamagePhysical;
        public float TargetValuePerStep = 10f;
        public StatModType TargetModifierType = StatModType.Flat;

        public float CalculateTargetValue(float sourceValue)
        {
            if (SourceAmountPerStep <= 0.0001f || sourceValue <= 0f)
                return 0f;

            float steps = sourceValue / SourceAmountPerStep;
            if (UseWholeSteps)
                steps = Mathf.Floor(steps + 0.0001f);
            return steps * TargetValuePerStep;
        }

        public PassiveStatScalingRule Clone()
        {
            return new PassiveStatScalingRule
            {
                SourceStat = SourceStat,
                SourceAmountPerStep = SourceAmountPerStep,
                UseWholeSteps = UseWholeSteps,
                TargetStat = TargetStat,
                TargetValuePerStep = TargetValuePerStep,
                TargetModifierType = TargetModifierType
            };
        }
    }

    [CreateAssetMenu(menuName = "RPG/Passive Tree/Node")]
    public class PassiveNodeTemplateSO : ScriptableObject
    {
        [Header("Visuals")]
        public string Name;
        [TextArea] public string Description;
        public Sprite Icon;
        
        [Header("Stats")]
        public List<SerializableStatModifier> Modifiers = new List<SerializableStatModifier>();

        [Header("Special Stat Scaling")]
        [Tooltip("Dynamic modifiers calculated from another final stat, for example +10 flat physical damage per 100 Armor.")]
        public List<PassiveStatScalingRule> StatScalingRules = new List<PassiveStatScalingRule>();
    }
}
