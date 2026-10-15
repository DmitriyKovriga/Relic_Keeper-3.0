using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    [CreateAssetMenu(menuName = "RPG/Skills/Ground Surface", fileName = "GroundSurface")]
    public sealed class GroundSurfaceEffectSO : ScriptableObject
    {
        public string Id;
        public Sprite Sprite;
        [Min(.1f)] public float PatchWidth = 1;
        [Min(.05f)] public float Lifetime = 3;
        [Min(.05f)] public float DamageInterval = .25f;
        [Min(0)] public float WeaponDamagePerSecond = .3f;
        public List<DamageConversionRule> DamageConversions = new List<DamageConversionRule>();
        public List<SerializableStatModifier> ContactModifiers = new List<SerializableStatModifier>();
    }
}
