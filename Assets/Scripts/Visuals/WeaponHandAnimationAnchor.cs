using UnityEngine;
using Scripts.Skills.Modules;

namespace Scripts.Visuals
{
    /// <summary>One stable rest pose and animation owner per hand, shared across skill instances.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponHandAnimationAnchor : MonoBehaviour
    {
        private bool _initialized;
        private SkillHandAnimation _owner;
        public Vector3 RestPosition { get; private set; }
        public Quaternion RestRotation { get; private set; }

        public void Initialize()
        {
            if (_initialized) return;
            RestPosition = transform.localPosition;
            RestRotation = transform.localRotation;
            _initialized = true;
        }

        public void Acquire(SkillHandAnimation owner)
        {
            Initialize();
            if (_owner != null && _owner != owner) _owner.ForceReset();
            _owner = owner;
        }

        public bool IsOwnedBy(SkillHandAnimation owner) => _owner == owner;

        public bool Release(SkillHandAnimation owner)
        {
            if (_owner != owner) return false;
            transform.localPosition = RestPosition;
            transform.localRotation = RestRotation;
            _owner = null;
            return true;
        }
    }
}
