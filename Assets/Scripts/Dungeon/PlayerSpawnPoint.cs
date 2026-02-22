using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Маркер точки появления игрока при входе в комнату.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private Color _gizmoColor = new Color(0, 1, 0, 0.5f);
        [SerializeField] private float _gizmoRadius = 0.5f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _gizmoColor;
            Gizmos.DrawSphere(transform.position, _gizmoRadius);
        }
#endif
    }
}
