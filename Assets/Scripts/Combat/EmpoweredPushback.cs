using System;
using UnityEngine;

namespace Scripts.Combat
{
    // One charge belongs to one accepted push, never to the whole group of victims.
    public sealed class EmpoweredPushback
    {
        public float CollisionRadius { get; }
        public bool HasTriggered { get; private set; }
        private readonly Action<Vector2> _onCollision;

        public EmpoweredPushback(float collisionRadius, Action<Vector2> onCollision)
        {
            CollisionRadius = Mathf.Max(0, collisionRadius);
            _onCollision = onCollision;
        }

        public bool TryTrigger(Vector2 point)
        {
            if (HasTriggered) return false;
            HasTriggered = true;
            _onCollision?.Invoke(point);
            return true;
        }
    }
}
