using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Skills
{
    [CreateAssetMenu(menuName = "RPG/Skills/Skill Pool")]
    public class SkillPoolSO : ScriptableObject
    {
        [System.Serializable]
        public struct SkillWeight
        {
            public SkillDataSO Skill;
            public int Weight; // Шанс выпадения
        }

        public List<SkillWeight> PossibleSkills;

        public SkillDataSO GetRandomSkill()
        {
            if (PossibleSkills == null || PossibleSkills.Count == 0) return null;

            int totalWeight = 0;
            foreach (var s in PossibleSkills) totalWeight += s.Weight;

            int random = Random.Range(0, totalWeight);
            int current = 0;

            foreach (var s in PossibleSkills)
            {
                current += s.Weight;
                if (random < current) return s.Skill;
            }
            return PossibleSkills[0].Skill;
        }
    }
}