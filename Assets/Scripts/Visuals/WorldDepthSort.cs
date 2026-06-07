using UnityEngine;

namespace Scripts.Visuals
{
    [DisallowMultipleComponent]
    public sealed class WorldDepthSort : MonoBehaviour
    {
        private const int EnemyTieBreakerRange = 128;
        private static int s_nextEnemyTieBreaker;

        [SerializeField] private RenderDepthCategory _category = RenderDepthCategory.Enemy;
        [SerializeField] private int _localOffset;
        [SerializeField] private bool _staticAnchor;
        [SerializeField] private float _staticAnchorY;
        [SerializeField] private Transform _anchor;
        [SerializeField] private bool _useFixedOrder;

        private Collider2D _collider;
        private float _lastAppliedAnchorY = float.NaN;
        private int _stableTieBreakerOffset;
        private int _temporaryOrderBoost;

        public RenderDepthCategory Category => _category;
        public bool UsesStaticAnchor => _staticAnchor;
        public bool RequiresDynamicUpdates => !_staticAnchor && !_useFixedOrder;

        private void OnEnable()
        {
            _lastAppliedAnchorY = float.NaN;
            RefreshTieBreakerOffset();
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
            _useFixedOrder = false;
            _lastAppliedAnchorY = float.NaN;
            RefreshTieBreakerOffset();
            WorldDepthSortManager.Register(this);
            ApplySort(force: true);
        }

        public void ConfigureFixed(RenderDepthCategory category, int localOffset)
        {
            _category = category;
            _localOffset = localOffset;
            _staticAnchor = false;
            _staticAnchorY = 0f;
            _useFixedOrder = true;
            _lastAppliedAnchorY = float.NaN;
            _stableTieBreakerOffset = 0;
            WorldDepthSortManager.Unregister(this);
            ApplySort(force: true);
        }

        public void SetTemporaryOrderBoost(int boost)
        {
            int clampedBoost = Mathf.Max(0, boost);
            if (_temporaryOrderBoost == clampedBoost)
                return;

            _temporaryOrderBoost = clampedBoost;
            ApplySort(force: true);
        }

        public void ApplySort(bool force = false)
        {
            if (_useFixedOrder)
            {
                WorldRenderSorting.ApplyToRenderers(transform, _category, 0f, ResolveLocalOffset(), respectNestedSorters: true);
                return;
            }

            float worldY = ResolveAnchorY();
            if (!force && !UsesStaticAnchor)
            {
                float threshold = WorldRenderSorting.Settings.YSortUpdateThreshold;
                if (!float.IsNaN(_lastAppliedAnchorY) && Mathf.Abs(worldY - _lastAppliedAnchorY) < threshold)
                    return;
            }

            _lastAppliedAnchorY = worldY;
            WorldRenderSorting.ApplyToRenderers(transform, _category, worldY, ResolveLocalOffset(), respectNestedSorters: true);
        }

        public float ResolveAnchorY()
        {
            if (_staticAnchor)
                return _staticAnchorY;

            if (_anchor != null)
                return _anchor.position.y;

            if (_category == RenderDepthCategory.Enemy && TryResolveColliderBottomY(out float bottomY))
                return bottomY;

            return transform.position.y;
        }

        private int ResolveLocalOffset()
        {
            return _localOffset + _stableTieBreakerOffset + _temporaryOrderBoost;
        }

        private void RefreshTieBreakerOffset()
        {
            if (!ShouldUseTieBreaker())
            {
                _stableTieBreakerOffset = 0;
                return;
            }

            int maxOffset = Mathf.Min(EnemyTieBreakerRange, WorldRenderSorting.Settings.SameYSortStride - 1);
            _stableTieBreakerOffset = maxOffset > 0
                ? s_nextEnemyTieBreaker++ % maxOffset
                : 0;
        }

        private bool ShouldUseTieBreaker()
        {
            return !_staticAnchor && _category == RenderDepthCategory.Enemy;
        }

        private bool TryResolveColliderBottomY(out float bottomY)
        {
            if (_collider == null)
                _collider = GetComponent<Collider2D>();

            if (_collider == null || !_collider.enabled)
            {
                bottomY = 0f;
                return false;
            }

            bottomY = _collider.bounds.min.y;
            return true;
        }
    }
}
