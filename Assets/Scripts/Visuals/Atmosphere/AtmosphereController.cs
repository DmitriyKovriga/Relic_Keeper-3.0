using UnityEngine;

namespace Scripts.Visuals.Atmosphere
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [AddComponentMenu("Relic Keeper/Visuals/Atmosphere Controller")]
    public sealed class AtmosphereController : MonoBehaviour
    {
        [SerializeField] private AtmosphereProfileSO _profile;
        [Tooltip("Assign the player (or call BindPlayer after spawning). Missing player disables reveal only.")]
        [SerializeField] private Transform _player;
        [Tooltip("XY world plane used for orthographic reconstruction.")]
        [SerializeField] private float _worldPlaneZ;

        public AtmosphereProfileSO Profile { get => _profile; set => _profile = value; }
        public Transform Player => _player;
        public float WorldPlaneZ => _worldPlaneZ;
        public void BindPlayer(Transform player) => _player = player;

        public Vector4 RevealParameters
        {
            get
            {
                if (_profile == null || _player == null) return Vector4.zero;
                Vector2 center = AtmosphereProfileSO.SnapToPixel(_player.position, _profile.pixelsPerUnit);
                return new Vector4(center.x, center.y, Mathf.Max(0f, _profile.revealRadius), Mathf.Clamp01(_profile.revealIntensity));
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_player == null || _profile == null) return;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(_player.position, Mathf.Max(0f, _profile.revealRadius));
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.3f);
            Gizmos.DrawWireSphere(_player.position, Mathf.Max(0f, _profile.revealRadius + _profile.revealSoftness));
        }
    }
}
