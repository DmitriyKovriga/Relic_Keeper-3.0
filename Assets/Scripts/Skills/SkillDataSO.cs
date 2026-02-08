using UnityEngine;
using Scripts.Skills.Steps;

namespace Scripts.Skills
{
    [CreateAssetMenu(menuName = "RPG/Skills/Skill Data")]
    public class SkillDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string ID; // Уникальный ID (Fireball_V1)
        public string SkillName;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Mechanics")]
        public bool IsActive; // Active or Passive
        public float Cooldown;
        public float ManaCost;

        [Header("Visuals & Logic")]
        [Tooltip("Префаб, который спавнится при атаке (снаряд, эффект удара). Если задан Recipe — используется StepRunner на префабе.")]
        public GameObject SkillPrefab;
        [Tooltip("Анимация игрока при касте")]
        public string AnimationTrigger = "Attack";

        [Header("Step-based (optional)")]
        [Tooltip("Если задан — скилл выполняется по рецепту степов (StepRunner на префабе). Иначе — классический SkillBehaviour (например CleaveSkill).")]
        public SkillRecipeSO Recipe;
    }
}