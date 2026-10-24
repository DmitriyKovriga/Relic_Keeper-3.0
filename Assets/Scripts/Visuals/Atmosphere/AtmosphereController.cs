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

        [Header("Layer color profiles")]
        [SerializeField] private WorldColorProfileSO _hubColorProfile;
        [SerializeField] private WorldColorProfileSO _defaultDungeonColorProfile;
        [Tooltip("Serialized shader references keep graded sprite variants available in builds.")]
        [SerializeField] private Shader _colorUnlitShader;
        [SerializeField] private Shader _colorLitShader;
        private bool _dungeonColors;
        private WorldColorProfileSO _environmentColorProfile;
        public WorldColorProfileSO PreviewColorProfile { get; set; }
        public bool PreviewNeutralColors { get; set; }
        public WorldColorProfileSO ActiveColorProfile => PreviewColorProfile != null ? PreviewColorProfile :
            (_dungeonColors ? (_environmentColorProfile != null ? _environmentColorProfile : _defaultDungeonColorProfile) : _hubColorProfile);
        public Shader ColorUnlitShader => _colorUnlitShader;
        public Shader ColorLitShader => _colorLitShader;
        public void ApplyDungeonColors(WorldColorProfileSO profile) { _dungeonColors = true; _environmentColorProfile = profile; }
        public void RestoreHubColors() { _dungeonColors = false; _environmentColorProfile = null; }
        public void RefreshColorGroups() => WorldColorGrading.RefreshScene(this);
        private void OnEnable() => WorldColorGrading.Register(this);
        private void OnDisable() => WorldColorGrading.Unregister(this);

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
