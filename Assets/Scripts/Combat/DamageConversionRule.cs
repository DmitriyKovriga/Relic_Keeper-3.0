using System;
using UnityEngine;

namespace Scripts.Combat
{
    [Serializable]
    public struct DamageConversionRule
    {
        public DamageChannel Source;
        public DamageChannel Target;
        [Range(0f, 100f)] public float Percent;

        public bool IsValid => Percent > 0f && Source != Target;
    }
}
