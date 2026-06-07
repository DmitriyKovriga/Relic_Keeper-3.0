using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField] private bool _useSortingGroup = false;

        private SortingGroup _sortingGroup;

        public RenderDepthCategory Category => _category;
        public bool UsesStaticAnchor => _staticAnchor;

        private void Awake()
        {
            if (_useSortingGroup)
            {
                _sortingGroup = GetComponent<SortingGroup>();
                if (_sortingGroup == null)
                    _sortingGroup = gameObject.AddComponent<SortingGroup>();
                return;
            }

            SortingGroup existingGroup = GetComponent<SortingGroup>();
            if (existingGroup != null)
                Destroy(existingGroup);
        }

        private void OnEnable()
        {
            WorldDepthSortManager.Register(this);
            ApplySort();
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
        }

        public void ApplySort()
        {
            float worldY = ResolveAnchorY();

            if (_useSortingGroup)
            {
                if (_sortingGroup == null)
                {
                    _sortingGroup = GetComponent<SortingGroup>();
                    if (_sortingGroup == null)
                        _sortingGroup = gameObject.AddComponent<SortingGroup>();
                }

                WorldRenderSorting.ApplyToSortingGroup(_sortingGroup, _category, worldY, _localOffset);
                return;
            }

            WorldRenderSorting.ApplyToRenderers(gameObject, _category, worldY, _localOffset);
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
