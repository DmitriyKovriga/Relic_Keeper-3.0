using UnityEngine;

namespace Scripts.Visuals.Atmosphere
{
    // Opt-in normal-map receiver. The region is local to the map, so room
    // placement and preview copies use the same selected patch of stone.
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Relic Keeper/Visuals/Stone Relief Surface")]
    public sealed class StoneReliefSurface : MonoBehaviour
    {
        [SerializeField] private Rect _localRegion = new Rect(-3, -3, 6, 6);
        private Renderer _renderer;
        private MaterialPropertyBlock _properties;
        private Vector4 _previousRegion, _lastRegion;
        private float _previousReceiver;
        private bool _bound;
        private static readonly int RegionId = Shader.PropertyToID("_RKStoneReliefRegion");
        private static readonly int ReceiverId = Shader.PropertyToID("_RKStoneReliefReceiver");

        public Rect LocalRegion => _localRegion;
        public void Configure(Rect region) { _localRegion = region; Apply(); }

        private Vector4 WorldRegion
        {
            get
            {
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 point = transform.TransformPoint(new Vector3((i & 1) == 0 ? _localRegion.xMin : _localRegion.xMax,
                        (i & 2) == 0 ? _localRegion.yMin : _localRegion.yMax));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                return new Vector4(min.x, min.y, max.x, max.y);
            }
        }

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;
            _properties = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_properties);
            _previousRegion = _properties.GetVector(RegionId); _previousReceiver = _properties.GetFloat(ReceiverId);
            _bound = true; Apply();
        }
        private void LateUpdate() { if (_bound && WorldRegion != _lastRegion) Apply(); }
        private void OnValidate() { if (_bound) Apply(); }
        private void Apply()
        {
            if (!_bound || _renderer == null) return;
            _lastRegion = WorldRegion;
            _renderer.GetPropertyBlock(_properties);
            _properties.SetVector(RegionId, _lastRegion); _properties.SetFloat(ReceiverId, 1);
            _renderer.SetPropertyBlock(_properties);
        }
        private void OnDisable()
        {
            if (!_bound || _renderer == null) return;
            _renderer.GetPropertyBlock(_properties);
            _properties.SetVector(RegionId, _previousRegion); _properties.SetFloat(ReceiverId, _previousReceiver);
            _renderer.SetPropertyBlock(_properties); _bound = false;
        }
    }
}
