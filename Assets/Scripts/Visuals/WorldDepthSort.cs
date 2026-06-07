using UnityEngine;

namespace Scripts.Visuals
{
    [DisallowMultipleComponent]
    public sealed class WorldDepthSort : MonoBehaviour
    {
        [SerializeField] private RenderDepthCategory _category = RenderDepthCategory.Enemy;
        [SerializeField] private int _localOffset;
        [SerializeField] private bool _staticAnchor;
        [SerializeField] private float _staticAnchorY;
        [SerializeField] private Transform _anchor;

        private float _lastAppliedAnchorY = float.NaN;

        public RenderDepthCategory Category => _category;
        public bool UsesStaticAnchor => _staticAnchor;

        private void OnEnable()
        {
            _lastAppliedAnchorY = float.NaN;
            WorldDepthSortManager.Register(this);
            ApplySort(force: true);
        }

        private void OnDisable()
        {
            WorldDepthSortManager.Unregister(this);
        }

        public void Configure(RenderDepthCategory category, int localOffset, bool staticAnchor, float anchorY)
        {
            _category = category;
            _localOffset = localOffset;
            _staticAnchor = staticAnchor;
            _staticAnchorY = anchorY;
            _lastAppliedAnchorY = float.NaN;
            ApplySort(force: true);
        }

        public void ApplySort(bool force = false)
        {
            float worldY = ResolveAnchorY();
            if (!force && !UsesStaticAnchor)
            {
                float threshold = WorldRenderSorting.Settings.YSortUpdateThreshold;
                if (!float.IsNaN(_lastAppliedAnchorY) && Mathf.Abs(worldY - _lastAppliedAnchorY) < threshold)
                    return;
            }

            _lastAppliedAnchorY = worldY;
            WorldRenderSorting.ApplyToRenderers(transform, _category, worldY, _localOffset, respectNestedSorters: true);
        }

        public float ResolveAnchorY()
        {
            if (_staticAnchor)
                return _staticAnchorY;

            if (_anchor != null)
                return _anchor.position.y;

            return transform.position.y;
        }
    }
}
