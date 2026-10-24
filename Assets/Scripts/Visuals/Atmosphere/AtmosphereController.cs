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

        [Header("Background depth mist — independent of screen fog")]
        [SerializeField] private AtmosphereProfileSO _hubDepthMistProfile;
        [SerializeField] private AtmosphereProfileSO _defaultDungeonDepthMistProfile;
        [SerializeField] private Shader _depthMistShader;
        private AtmosphereProfileSO _environmentDepthMistProfile;
        private DepthMistLayer _depthMist;
        private Vector2 _depthMistAnchor;
        private float _depthMistStartTime;
        public AtmosphereProfileSO PreviewDepthMistProfile { get; set; }
        public AtmosphereProfileSO ActiveDepthMistProfile => PreviewDepthMistProfile != null ? PreviewDepthMistProfile : AssignedDepthMistProfile;
        public AtmosphereProfileSO AssignedDepthMistProfile => _dungeonColors
            ? (_environmentDepthMistProfile != null ? _environmentDepthMistProfile : _defaultDungeonDepthMistProfile)
            : (_hubDepthMistProfile != null ? _hubDepthMistProfile : _profile);
        public Vector2 DepthMistAnchor => _depthMistAnchor;
        internal Shader DepthMistShader => _depthMistShader;
        internal float DepthMistTime => Mathf.Max(0f, (Application.isPlaying ? Time.time : Time.realtimeSinceStartup) - _depthMistStartTime);
        public void ResetDepthMistAnchor(Vector2 origin)
        {
            _depthMistAnchor = origin;
            _depthMistStartTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        }
        public void ApplyDungeonPresentation(WorldColorProfileSO colors, AtmosphereProfileSO mist)
        {
            ApplyDungeonColors(colors); _environmentDepthMistProfile = mist;
            ResetDepthMistAnchor(Vector2.zero);
        }

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
        public WorldColorProfileSO AssignedColorProfile => _dungeonColors ? (_environmentColorProfile != null ? _environmentColorProfile : _defaultDungeonColorProfile) : _hubColorProfile;
        public WorldColorProfileSO ActiveColorProfile => PreviewColorProfile != null ? PreviewColorProfile : AssignedColorProfile;
        public Shader ColorUnlitShader => _colorUnlitShader;
        public Shader ColorLitShader => _colorLitShader;
        public void ApplyDungeonColors(WorldColorProfileSO profile) { _dungeonColors = true; _environmentColorProfile = profile; _environmentDepthMistProfile = null; }
        public void RestoreHubColors() { _dungeonColors = false; _environmentColorProfile = null; _environmentDepthMistProfile = null; ResetDepthMistAnchor(Vector2.zero); }
        public void RefreshColorGroups() => WorldColorGrading.RefreshScene(this);
        private void OnEnable()
        {
            WorldColorGrading.Register(this);
            _depthMist?.Dispose();
            _depthMist = new DepthMistLayer(this);
        }
        private void OnDisable()
        {
            _depthMist?.Dispose(); _depthMist = null;
            WorldColorGrading.Unregister(this);
        }

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
