using Scripts.Combat;
using UnityEngine;

namespace Scripts.GameplayEvents
{
    public sealed class GameplayEventContext
    {
        public GameplayEventType Type;
        public GameObject Source;
        public GameObject Target;
        public DamageSnapshot Damage;
        public float Amount;
        public int Count;
        public Vector3 Position;

        public bool HasParticipant(GameObject gameObject)
        {
            return gameObject != null && (Source == gameObject || Target == gameObject);
        }

        public static GameObject ResolveGameObject(object source)
        {
            if (source is GameObject go)
            {
                if (go == null)
                    return null;
                return go;
            }

            if (source is Component component)
            {
                if (component == null)
                    return null;
                return component.gameObject;
            }

            return null;
        }
    }
}
