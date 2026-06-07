using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>
    /// Optional override for prefabs whose render category cannot be inferred safely.
    /// Most runtime objects are sorted automatically by their spawner and do not need this.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RenderStackProfile : MonoBehaviour
    {
        [SerializeField] private RenderDepthCategory _category = RenderDepthCategory.Auto;
        [SerializeField] private int _localOffset;
        [SerializeField] private bool _staticAnchor;
        [SerializeField] private bool _applyOnEnable;

        public RenderDepthCategory Category => _category;
        public int LocalOffset => _localOffset;
        public bool StaticAnchor => _staticAnchor;

        private void OnEnable()
        {
            if (!_applyOnEnable)
                return;

            WorldRenderSorting.ConfigureAutoSorter(
                gameObject,
                _category,
                transform.position.y,
                _localOffset,
                _staticAnchor);
        }
    }
}
