using UnityEngine;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.Visuals;

namespace Scripts.Skills
{
    public enum SkillActionSpeedMode
    {
        Attack = 0,
        Spell = 1,
        Universal = 2
    }

    public enum SkillDescriptionMode
    {
        Automatic = 0,
        AutomaticWithLegacy = 1,
        LegacyOnly = 2
    }

    /// <summary>
    /// How this skill uses weapon Hold Stance / Swing Style.
    /// MainHand = primary weapon skill (stance-filtered swings).
    /// Special = 2H secondary skill (any swing; hold still used for idle).
    /// Equipment = item skill (no hold/swing UI or requirement).
    /// </summary>
    public enum SkillWeaponRole
    {
        MainHand = 0,
        Special = 1,
        Equipment = 2
    }

    [CreateAssetMenu(menuName = "RPG/Skills/Skill Data")]
    public class SkillDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string ID; // Р Р€Р Р…Р С‘Р С”Р В°Р В»РЎРЉР Р…РЎвЂ№Р в„– ID (Fireball_V1)
        public string SkillName;
        [Tooltip("Automatic reads the recipe and always reflects its current mechanics. Legacy text is preserved below and can be enabled when needed.")]
        public SkillDescriptionMode DescriptionMode = SkillDescriptionMode.Automatic;
        [Tooltip("Legacy fallback description. Disabled by default through Description Mode.")]
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
        [Tooltip("Р С™Р С•Р Р…РЎвЂљР ВµР С”РЎРѓРЎвЂљ РЎС“РЎР‚Р С•Р Р…Р В° Р Т‘Р В»РЎРЏ РЎР‚Р В°РЎРѓРЎвЂЎР ВµРЎвЂљР В° Context Modifier РЎРѓРЎвЂљР В°РЎвЂљР С•Р Р†. Р вЂўРЎРѓР В»Р С‘ Р С•РЎРѓРЎвЂљР В°Р Р†Р С‘РЎвЂљРЎРЉ None РЎС“ РЎРѓРЎвЂљР В°РЎР‚РЎвЂ№РЎвЂ¦ melee-РЎРѓР С”Р С‘Р В»Р В»Р С•Р Р†, РЎР‚Р В°Р Р…РЎвЂљР В°Р в„–Р С Р С—Р С•Р Т‘РЎРѓРЎвЂљР В°Р Р†Р С‘РЎвЂљ Р В±Р ВµР В·Р С•Р С—Р В°РЎРѓР Р…РЎвЂ№Р в„– legacy fallback Attack|Melee.")]
        public StatContextTagFlags DamageContextTags;

        [Header("Pushback")]
        [Tooltip("When enabled, this skill knocks enemies left or right using Pushback rating. Off by default.")]
        public bool EnablePushback;
        [Tooltip("Flat Pushback rating added on this skill's hits. 200 is a small nudge, 1000 is clearly noticeable.")]
        public float PushbackRating;

        [Header("Visuals & Logic")]
        [Tooltip("Р СџРЎР‚Р ВµРЎвЂћР В°Р В± Р В»Р С•Р С–Р С‘Р С”Р С‘ РЎРѓ Р С”Р С•Р СР С—Р С•Р Р…Р ВµР Р…РЎвЂљР С•Р С SkillBehaviour Р Р…Р В° Р С”Р С•РЎР‚Р Р…Р Вµ. Р вЂќР В»РЎРЏ Р Р…Р В°Р Р†РЎвЂ№Р С”Р В° РЎРѓ Recipe Р СР С•Р В¶Р Р…Р С• Р С•РЎРѓРЎвЂљР В°Р Р†Р С‘РЎвЂљРЎРЉ Р С—РЎС“РЎРѓРЎвЂљРЎвЂ№Р С: StepRunner РЎРѓР С•Р В·Р Т‘Р В°РЎвЂРЎвЂљРЎРѓРЎРЏ Р В°Р Р†РЎвЂљР С•Р СР В°РЎвЂљР С‘РЎвЂЎР ВµРЎРѓР С”Р С‘. Р вЂ™Р С‘Р В·РЎС“Р В°Р В»РЎРЉР Р…РЎвЂ№Р Вµ РЎРЊРЎвЂћРЎвЂћР ВµР С”РЎвЂљРЎвЂ№ Р Р…Р В°Р В·Р Р…Р В°РЎвЂЎР В°РЎР‹РЎвЂљРЎРѓРЎРЏ Р Р† РЎв‚¬Р В°Р С–Р В°РЎвЂ¦ РЎР‚Р ВµРЎвЂ Р ВµР С—РЎвЂљР В°, Р Р…Р Вµ Р В·Р Т‘Р ВµРЎРѓРЎРЉ.")]
        public GameObject SkillPrefab;
        [Tooltip("Р С’Р Р…Р С‘Р СР В°РЎвЂ Р С‘РЎРЏ Р С‘Р С–РЎР‚Р С•Р С”Р В° Р С—РЎР‚Р С‘ Р С”Р В°РЎРѓРЎвЂљР Вµ")]
        public string AnimationTrigger = "Attack";

        [Header("Step-based (optional)")]
        [Tooltip("Р В Р ВµРЎвЂ Р ВµР С—РЎвЂљ Р Т‘Р В»РЎРЏ StepRunner. Р вЂўРЎРѓР В»Р С‘ SkillPrefab Р Р…Р Вµ Р В·Р В°Р Т‘Р В°Р Р…, Р С‘РЎРѓР С—Р С•Р В»Р Р…Р С‘РЎвЂљР ВµР В»РЎРЉ РЎРѓР С•Р В·Р Т‘Р В°РЎвЂРЎвЂљРЎРѓРЎРЏ Р В°Р Р†РЎвЂљР С•Р СР В°РЎвЂљР С‘РЎвЂЎР ВµРЎРѓР С”Р С‘. Р ВР Р…Р В°РЎвЂЎР Вµ Р С—РЎР‚Р ВµРЎвЂћР В°Р В± Р Т‘Р С•Р В»Р В¶Р ВµР Р… РЎРѓР С•Р Т‘Р ВµРЎР‚Р В¶Р В°РЎвЂљРЎРЉ SkillBehaviour.")]
        public SkillRecipeSO Recipe;

        [Header("Weapon Role")]
        [Tooltip("MainHand: swing filtered by Allowed Stances. Special: any swing (2H secondary / RB). Equipment: hide Hold/Swing (armor/boots/gloves/helmet skills).")]
        public SkillWeaponRole WeaponRole = SkillWeaponRole.MainHand;

        [Header("Weapon Hold (idle pose)")]
        [Tooltip("Idle pose number when this skill is a weapon's active skill #1 (auto-attack / primary). 0 Default, 1 Aggressive, 2 LowGuard, 3 Dagger, 4 Shoulder, 5 Staff. Swing Style dropdown is filtered to swings allowed for this stance.")]
        public WeaponHoldStance HoldStance = WeaponHoldStance.Default;

        [HideInInspector]
        [Tooltip("Named stance Id from WeaponStancePoseTable. Source of truth when non-empty; Hold Stance dropdown writes this.")]
        public string HoldStanceId;

        // Attack swing / windup style - separate from HoldStance so pose is never locked to one swing.
        // FromStance (0) = use that stance's default swing; only stance-allowed overrides appear in the UI.
        [Tooltip("FromStance = use this skill's HoldStance default swing. Dropdown lists only swings allowed for the selected Hold Stance.")]
        public WeaponSwingStyle SwingStyle = WeaponSwingStyle.FromStance;

        [HideInInspector]
        [Tooltip("Named swing Id from WeaponSwingStyleTable. Source of truth when non-empty; Swing Style dropdown writes this.")]
        public string SwingStyleId;

        private void OnValidate()
        {
            // Equipment skills never animate hold/swing — leave stored values alone.
            if (WeaponRole == SkillWeaponRole.Equipment)
                return;

            // Special (2H secondary): any swing is valid; do not clamp to Allowed Stances.
            if (WeaponRole == SkillWeaponRole.Special)
                return;

            string stanceId = !string.IsNullOrEmpty(HoldStanceId)
                ? HoldStanceId
                : WeaponStancePoseTableSO.CanonicalId(HoldStance);

            if (!string.IsNullOrEmpty(SwingStyleId))
            {
                if (!WeaponSwingStyleResolver.IsStyleIdAllowedForStanceId(stanceId, SwingStyleId))
                {
                    SwingStyleId = string.Empty;
                    SwingStyle = WeaponSwingStyle.FromStance;
                }
                return;
            }

            if (!WeaponSwingStyleResolver.IsAllowedForStanceId(stanceId, SwingStyle))
                SwingStyle = WeaponSwingStyle.FromStance;
        }
    }
}
