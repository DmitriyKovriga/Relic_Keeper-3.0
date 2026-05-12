using UnityEngine;
using Scripts.Skills.Steps;
using Scripts.Stats;

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

        [Header("Localization")]
        [Tooltip("Stable key in SkillsLabels for the skill name. If empty, runtime falls back to skills.{ID}.")]
        public string NameKey;
        [Tooltip("Stable key in SkillsLabels for the skill description. If empty, runtime falls back to skills.{ID}.description.")]
        public string DescriptionKey;

        [Header("Mechanics")]
        public bool IsActive; // Active or Passive
        public float Cooldown;
        public float ManaCost;
        [Tooltip("Multiplier applied after normal AttackSpeed/CastSpeed calculation. 1 = normal, 1.5 = 50% faster, 0.75 = 25% slower.")]
        public float SkillSpeedMultiplier = 1f;
        [Tooltip("Контекст урона для расчета Context Modifier статов. Если оставить None у старых melee-скиллов, рантайм подставит безопасный legacy fallback Attack|Melee.")]
        public StatContextTagFlags DamageContextTags;

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
