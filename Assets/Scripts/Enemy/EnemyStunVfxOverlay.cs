using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStunController))]
    public sealed class EnemyStunVfxOverlay : MonoBehaviour
    {
        private const string DefaultResourcesPath = "VFX/StunEffectVFX/StunVFXPrefab";

        [SerializeField] private GameObject _stunVfxPrefab;
        [SerializeField] private string _fallbackResourcesPath = DefaultResourcesPath;
        [SerializeField, Min(0.01f)] private float _vfxLifetime = 0.85f;
        [SerializeField] private Vector2 _worldOffset = new(0f, 0.65f);

        private EnemyStunController _stun;
        private EnemyHealth _health;
        private EnemyEntity _entity;
        private GameObject _activeVfx;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnDisable()
        {
            DestroyActiveVfx();
        }

        private void LateUpdate()
        {
            CacheComponents();

            if (_stun == null || _health == null || _health.IsDead || !_stun.IsStunned)
            {
                DestroyActiveVfx();
                return;
            }

            if (_activeVfx == null)
                SpawnVfx();

            if (_activeVfx != null)
                _activeVfx.transform.position = ResolveWorldPosition();
        }

        private void CacheComponents()
        {
            if (_stun == null)
                _stun = GetComponent<EnemyStunController>();
            if (_health == null)
                _health = GetComponent<EnemyHealth>();
            if (_entity == null)
                _entity = GetComponent<EnemyEntity>();
        }

        private void SpawnVfx()
        {
            GameObject prefab = ResolvePrefab();
            if (prefab == null)
                return;

            _activeVfx = Instantiate(prefab, ResolveWorldPosition(), Quaternion.identity);
            ConfigureSorting(_activeVfx);

            var autoDestroy = AutoDestroyVFX.Ensure(_activeVfx);
            if (autoDestroy != null)
                autoDestroy.Initialize(_vfxLifetime, fadeOutEnabled: false);
        }

        private GameObject ResolvePrefab()
        {
            if (_stunVfxPrefab != null)
                return _stunVfxPrefab;

            if (string.IsNullOrWhiteSpace(_fallbackResourcesPath))
                return null;

            _stunVfxPrefab = Resources.Load<GameObject>(_fallbackResourcesPath);
            return _stunVfxPrefab;
        }

        private Vector3 ResolveWorldPosition()
        {
            Bounds bounds = _entity != null
                ? _entity.GetVisualBounds()
                : new Bounds(transform.position, Vector3.zero);

            if (bounds.size.sqrMagnitude <= 0.0001f)
                bounds = new Bounds(transform.position + Vector3.up, Vector3.one);

            return new Vector3(
                bounds.center.x + _worldOffset.x,
                bounds.max.y + _worldOffset.y,
                transform.position.z);
        }

        private void ConfigureSorting(GameObject vfx)
        {
            if (vfx == null)
                return;

            WorldRenderSorting.ConfigureAutoSorter(
                vfx,
                RenderDepthCategory.GameplayVfx,
                ResolveWorldPosition().y,
                localOffset: 5,
                staticAnchor: false);
        }

        private void DestroyActiveVfx()
        {
            if (_activeVfx == null)
                return;

            Destroy(_activeVfx);
            _activeVfx = null;
        }
    }
}
