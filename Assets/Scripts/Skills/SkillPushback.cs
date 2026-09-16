using System.Collections.Generic;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    public static class SkillPushback
    {
        public static bool IsEnabled(SkillDataSO skill)
        {
            return skill != null && skill.EnablePushback;
        }

        public static void AppendFlatModifier(SkillDataSO skill, List<SerializableStatModifier> modifiers)
        {
            if (!IsEnabled(skill) || modifiers == null)
                return;
            if (Mathf.Abs(skill.PushbackRating) < 0.0001f)
                return;

            modifiers.Add(new SerializableStatModifier
            {
                Stat = StatType.Pushback,
                Type = StatModType.Flat,
                Value = skill.PushbackRating
            });
        }
    }
}
