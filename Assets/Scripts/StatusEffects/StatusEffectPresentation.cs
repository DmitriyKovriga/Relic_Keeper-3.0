using Scripts.Skills;
using Scripts.Skills.Steps;
using UnityEngine;

namespace Scripts.StatusEffects
{
    public static class StatusEffectPresentation
    {
        public static StatusAuraColor ResolveStepAura(StepEntry step)
        {
            if (step?.Overrides == null)
                return StatusAuraColor.Green;

            for (int i = 0; i < step.Overrides.Count; i++)
            {
                StepParamValue value = step.Overrides[i];
                if (value != null && value.Key == "AuraColor" && value.Type == StepParamValue.ParamKind.Int)
                    return (StatusAuraColor)Mathf.Clamp(value.IntVal, 0, (int)StatusAuraColor.Purple);
            }

            return StatusAuraColor.Green;
        }

        public static Sprite ResolveHudIcon(StatusEffectSO effect, object source)
        {
            if (source is SkillBehaviour skill && skill.Data != null && skill.Data.Icon != null)
                return skill.Data.Icon;

            return effect != null ? effect.Icon : null;
        }

        public static Color AuraTint(StatusAuraColor color)
        {
            switch (color)
            {
                case StatusAuraColor.Red:
                    return new Color(1f, 0.28f, 0.22f, 1f);
                case StatusAuraColor.Green:
                    return new Color(0.35f, 1f, 0.42f, 1f);
                case StatusAuraColor.Blue:
                    return new Color(0.35f, 0.62f, 1f, 1f);
                case StatusAuraColor.Purple:
                    return new Color(0.72f, 0.38f, 1f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
