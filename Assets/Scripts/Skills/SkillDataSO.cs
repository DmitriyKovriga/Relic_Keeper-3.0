using UnityEngine;
using Scripts.Skills.Steps;
using Scripts.Stats;

namespace Scripts.Skills
{
    public enum SkillActionSpeedMode
    {
        Attack = 0,
        Spell = 1,
        Universal = 2
    }

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
        [Tooltip("Attack uses AttackSpeed. Spell uses weapon base speed with CastSpeed modifiers. Universal uses the faster final result.")]
        public SkillActionSpeedMode ActionSpeedMode = SkillActionSpeedMode.Attack;
        [Tooltip("Multiplier applied after normal AttackSpeed/CastSpeed calculation. 1 = normal, 1.5 = 50% faster, 0.75 = 25% slower.")]
        public float SkillSpeedMultiplier = 1f;
        [Tooltip("Контекст урона для расчета Context Modifier статов. Если оставить None у старых melee-скиллов, рантайм подставит безопасный legacy fallback Attack|Melee.")]
        public StatContextTagFlags DamageContextTags;

        [Header("Visuals & Logic")]
        [Tooltip("Префаб логики с компонентом SkillBehaviour на корне. Для навыка с Recipe можно оставить пустым: StepRunner создаётся автоматически. Визуальные эффекты назначаются в шагах рецепта, не здесь.")]
        public GameObject SkillPrefab;
        [Tooltip("Анимация игрока при касте")]
        public string AnimationTrigger = "Attack";

        [Header("Step-based (optional)")]
        [Tooltip("Рецепт для StepRunner. Если SkillPrefab не задан, исполнитель создаётся автоматически. Иначе префаб должен содержать SkillBehaviour.")]
        public SkillRecipeSO Recipe;
    }
}
