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

    public enum SkillDescriptionMode
    {
        Automatic = 0,
        AutomaticWithLegacy = 1,
        LegacyOnly = 2
    }

    [CreateAssetMenu(menuName = "RPG/Skills/Skill Data")]
    public class SkillDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string ID; // РЈРЅРёРєР°Р»СЊРЅС‹Р№ ID (Fireball_V1)
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
        [Tooltip("РљРѕРЅС‚РµРєСЃС‚ СѓСЂРѕРЅР° РґР»СЏ СЂР°СЃС‡РµС‚Р° Context Modifier СЃС‚Р°С‚РѕРІ. Р•СЃР»Рё РѕСЃС‚Р°РІРёС‚СЊ None Сѓ СЃС‚Р°СЂС‹С… melee-СЃРєРёР»Р»РѕРІ, СЂР°РЅС‚Р°Р№Рј РїРѕРґСЃС‚Р°РІРёС‚ Р±РµР·РѕРїР°СЃРЅС‹Р№ legacy fallback Attack|Melee.")]
        public StatContextTagFlags DamageContextTags;

        [Header("Pushback")]
        [Tooltip("When enabled, this skill knocks enemies left or right using Pushback rating. Off by default.")]
        public bool EnablePushback;
        [Tooltip("Flat Pushback rating added on this skill's hits. 200 is a small nudge, 1000 is clearly noticeable.")]
        public float PushbackRating;

        [Header("Visuals & Logic")]
        [Tooltip("РџСЂРµС„Р°Р± Р»РѕРіРёРєРё СЃ РєРѕРјРїРѕРЅРµРЅС‚РѕРј SkillBehaviour РЅР° РєРѕСЂРЅРµ. Р”Р»СЏ РЅР°РІС‹РєР° СЃ Recipe РјРѕР¶РЅРѕ РѕСЃС‚Р°РІРёС‚СЊ РїСѓСЃС‚С‹Рј: StepRunner СЃРѕР·РґР°С‘С‚СЃСЏ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё. Р’РёР·СѓР°Р»СЊРЅС‹Рµ СЌС„С„РµРєС‚С‹ РЅР°Р·РЅР°С‡Р°СЋС‚СЃСЏ РІ С€Р°РіР°С… СЂРµС†РµРїС‚Р°, РЅРµ Р·РґРµСЃСЊ.")]
        public GameObject SkillPrefab;
        [Tooltip("РђРЅРёРјР°С†РёСЏ РёРіСЂРѕРєР° РїСЂРё РєР°СЃС‚Рµ")]
        public string AnimationTrigger = "Attack";

        [Header("Step-based (optional)")]
        [Tooltip("Р РµС†РµРїС‚ РґР»СЏ StepRunner. Р•СЃР»Рё SkillPrefab РЅРµ Р·Р°РґР°РЅ, РёСЃРїРѕР»РЅРёС‚РµР»СЊ СЃРѕР·РґР°С‘С‚СЃСЏ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё. РРЅР°С‡Рµ РїСЂРµС„Р°Р± РґРѕР»Р¶РµРЅ СЃРѕРґРµСЂР¶Р°С‚СЊ SkillBehaviour.")]
        public SkillRecipeSO Recipe;

        [Header("Weapon Hold (idle pose)")]
        [Tooltip("Idle pose number when this skill is a weapon's active skill #1 (auto-attack / primary). 0 Default, 1 Aggressive, 2 LowGuard, 3 Dagger, 4 Shoulder, 5 Staff. Swing Style dropdown is filtered to swings allowed for this stance.")]
        public WeaponHoldStance HoldStance = WeaponHoldStance.Default;

        // Attack swing / windup style - separate from HoldStance so pose is never locked to one swing.
        // FromStance (0) = use that stance's default swing; only stance-allowed overrides appear in the UI.
        [Tooltip("FromStance = use this skill's HoldStance default swing. Dropdown lists only swings allowed for the selected Hold Stance.")]
        public WeaponSwingStyle SwingStyle = WeaponSwingStyle.FromStance;

        private void OnValidate()
        {
            WeaponSwingStyle clamped = WeaponSwingStyleResolver.ClampToAllowed(HoldStance, SwingStyle);
            if (clamped != SwingStyle)
                SwingStyle = clamped;
        }
    }
}
