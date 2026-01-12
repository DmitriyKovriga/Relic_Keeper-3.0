using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Skills.Modules
{
    public abstract class SkillHitbox : MonoBehaviour
    {
        [SerializeField] protected LayerMask _targetLayer;
        [SerializeField] protected Vector2 _offset; 

        // Добавили scaleMultiplier
        public abstract List<IDamageable> GetTargets(Vector3 origin, float facingDirection, float scaleMultiplier = 1f);
    }
}