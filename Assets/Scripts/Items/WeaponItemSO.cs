using UnityEngine;
using Scripts.Skills;

namespace Scripts.Items
{
    [CreateAssetMenu(menuName = "RPG/Inventory/Weapon Item")]
    public class WeaponItemSO : EquipmentItemSO
    {
        [Header("Weapon Config")]
        public bool IsTwoHanded;
        [Tooltip("Offhand shield with offensive weapon stats. Ordinary defensive shields leave this disabled.")]
        public bool IsCombatShield;

        [Header("Visuals")]
        [Tooltip("РЎРїСЂР°Р№С‚, РєРѕС‚РѕСЂС‹Р№ РѕС‚РѕР±СЂР°Р¶Р°РµС‚СЃСЏ РІ СЂСѓРєРµ РїРµСЂСЃРѕРЅР°Р¶Р°")]
        public Sprite InHandSprite;

        [Tooltip("Z tilt painted into InHandSprite vs tip-up. Axe hand art approx -45. finalEuler = poseEuler - tilt.")]
        public float InHandSpriteTiltZ;

        // Persistence choice (Weapon Editor): prefer sprite.pivot via TextureImporter when InHand
        // texture is unique. This offset is the fallback for shared multi-sprite sheets — applied
        // in WeaponVisualController.ApplyHandVisual as HandPivot-local nudge (+ stance LocalPosition).
        [Tooltip("Per-weapon handle nudge in HandPivot space (added to stance LocalPosition). Used when InHand sprite pivot cannot be edited (shared sheet). Prefer fixing sprite.pivot in Weapon Editor when the texture is unique.")]
        public Vector2 InHandSpriteLocalOffset;

        [Header("Base Offense Stats")]
        public float MinPhysicalDamage;
        public float MaxPhysicalDamage;

        [Space]
        public float MinFireDamage;
        public float MaxFireDamage;

        public float MinColdDamage;
        public float MaxColdDamage;

        public float MinLightningDamage;
        public float MaxLightningDamage;
        // AI ADDED END

        [Tooltip("РђС‚Р°Рє РІ СЃРµРєСѓРЅРґСѓ (APS)")]
        public float AttacksPerSecond = 1.0f;
        
        // РћР‘РќРћР’Р›Р•РќРћ: РСЃРїРѕР»СЊР·СѓРµРј 5.0 РґР»СЏ 5%
        [Tooltip("Р‘Р°Р·РѕРІС‹Р№ С€Р°РЅСЃ РєСЂРёС‚Р° РІ РїСЂРѕС†РµРЅС‚Р°С… (5 = 5%). Р›РѕРєР°Р»СЊРЅС‹Рµ РјРѕРґС‹ Р±СѓРґСѓС‚ СѓРјРЅРѕР¶Р°С‚СЊ СЌС‚Рѕ С‡РёСЃР»Рѕ.")]
        public float BaseCritChance = 5f; 
    

        [Header("2H Special Skills")]
        [Tooltip("РўРѕР»СЊРєРѕ РґР»СЏ РґРІСѓСЂСѓС‡РЅРѕРіРѕ РѕСЂСѓР¶РёСЏ: РџСѓР» СЃРєРёР»Р»РѕРІ РґР»СЏ РїСЂР°РІРѕР№ РєРЅРѕРїРєРё РјС‹С€Рё (Secondary Attack)")]
        public SkillPoolSO SecondarySkillPool;

        /// <summary>Defensive shields use their own skill speed without contributing weapon damage or crit.</summary>
        public bool IsDefensiveOffHand => Slot == EquipmentSlot.OffHand && !IsCombatShield;
    }
}
