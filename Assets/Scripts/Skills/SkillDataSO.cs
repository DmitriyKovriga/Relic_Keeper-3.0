using UnityEngine;

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
        [Tooltip("Префаб, который спавнится при атаке (снаряд, эффект удара)")]
        public GameObject SkillPrefab;
        
        [Tooltip("Анимация игрока при касте")]
        public string AnimationTrigger = "Attack";
    }
}