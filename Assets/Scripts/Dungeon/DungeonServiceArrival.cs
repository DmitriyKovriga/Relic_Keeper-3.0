using Scripts.Hub;
using Scripts.Items.World;
using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>Room-owned visual portal and a short, terrain-checked NPC entrance.</summary>
    public sealed class DungeonServiceArrival : MonoBehaviour
    {
        private DungeonServiceArrivalSettings _settings;
        private HubServiceNpc _service;
        private SpriteRenderer _renderer, _portalRenderer;
        private Animator _idle;
        private Sprite[] _walk;
        private Sprite _idleSprite;
        private Vector3 _start, _end;
        private GameObject _portal;
        private Transform _label;
        private bool _labelWasActive, _idleWasEnabled, _originalFlip, _complete;
        private Color _color;
        private float _elapsed;

        public bool IsComplete => _complete;
        public GameObject PortalVisual => _portal;
        public float TotalDuration => _settings.PortalOpenDuration + _settings.WalkDuration + _settings.PortalCloseDuration;

        public void Initialize(HubServiceNpc service, DungeonServiceArrivalSettings settings, Vector3 target)
        {
            _service = service;
            _settings = settings;
            _end = target;
            _start = ResolveStart(target, settings);
            _renderer = GetComponent<SpriteRenderer>();
            _idleSprite = _renderer.sprite;
            _color = _renderer.color;
            _originalFlip = _renderer.flipX;
            _idle = GetComponent<Animator>();
            if (_idle != null) { _idleWasEnabled = _idle.enabled; _idle.enabled = false; }
            _walk = service.Service == HubService.Market ? settings.MarketWalk : settings.StashWalk;
            _label = transform.Find("WorldLabel");
            if (_label != null) { _labelWasActive = _label.gameObject.activeSelf; _label.gameObject.SetActive(false); }
            service.SetArrivalInProgress(true);
            _portal = new GameObject("ServiceArrivalPortal");
            _portal.transform.SetParent(transform.parent, true);
            _portalRenderer = _portal.AddComponent<SpriteRenderer>();
            _portalRenderer.sprite = settings.PortalSprite;
            _portalRenderer.sharedMaterial = settings.SpriteMaterial;
            _portal.transform.position = new Vector3(_start.x,
                _start.y - settings.NpcRootHeight - settings.PortalSprite.bounds.min.y, 0);
            _portalRenderer.sortingLayerName = _renderer.sortingLayerName;
            _portalRenderer.sortingOrder = _renderer.sortingOrder - 1;
            _portal.AddComponent<Animator>().runtimeAnimatorController = settings.PortalController;
            Sample(0);
        }

        internal static Vector3 ResolveStart(Vector3 target, DungeonServiceArrivalSettings settings)
        {
            // Prefer walking right; use the other side if a wall, edge or drop blocks it.
            foreach (float direction in new[] { -1f, 1f })
            {
                Vector2 floor = (Vector2)target - Vector2.up * settings.NpcRootHeight;
                Vector2 candidate = floor + Vector2.right * (direction * settings.WalkDistance);
                bool safe = !Physics2D.Linecast(floor + Vector2.up * settings.NpcRootHeight,
                    candidate + Vector2.up * settings.NpcRootHeight, WorldItemDropService.TerrainMask);
                for (int step = 1; safe && step <= 8; step++)
                {
                    Vector2 point = Vector2.Lerp(floor, candidate, step / 8f);
                    safe = WorldItemDropService.TrySnapToGround(point, out Vector2 ground, 0)
                        && Mathf.Abs(ground.y - floor.y) <= settings.MaximumFloorStep;
                }
                if (safe) return new Vector3(candidate.x, target.y, 0);
            }
            // On a narrow platform, emerge in place rather than walk over an edge.
            return target;
        }

        private void Update()
        {
            if (_settings == null || _complete) return;
            _elapsed += Time.deltaTime;
            Sample(_elapsed);
        }

        public void Sample(float elapsed)
        {
            if (_complete || _settings == null) return;
            float progress = Mathf.Clamp01((elapsed - _settings.PortalOpenDuration) / _settings.WalkDuration);
            transform.position = Vector3.Lerp(_start, _end, progress);
            float npcAlpha = Mathf.Clamp01((elapsed - _settings.PortalOpenDuration) / _settings.PortalOpenDuration);
            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * npcAlpha);
            bool walking = progress > 0 && progress < 1 && (_start - _end).sqrMagnitude > 0.0001f;
            if (walking && _walk != null && _walk.Length > 0)
                _renderer.sprite = _walk[Mathf.FloorToInt((elapsed - _settings.PortalOpenDuration) / _settings.WalkFrameDuration) % _walk.Length];
            else _renderer.sprite = _idleSprite;
            _renderer.flipX = walking ? _start.x > _end.x : _originalFlip;
            if (_portalRenderer != null)
            {
                float alpha = Mathf.Min(Mathf.Clamp01(elapsed / _settings.PortalOpenDuration),
                    Mathf.Clamp01((TotalDuration - elapsed) / _settings.PortalCloseDuration));
                _portalRenderer.color = new Color(1, 1, 1, alpha);
            }
            if (elapsed >= TotalDuration) Finish();
        }

        private void Finish()
        {
            _complete = true;
            transform.position = _end;
            if (_renderer != null) { _renderer.sprite = _idleSprite; _renderer.color = _color; _renderer.flipX = _originalFlip; }
            if (_idle != null) _idle.enabled = _idleWasEnabled;
            if (_label != null) _label.gameObject.SetActive(_labelWasActive);
            if (_service != null) _service.SetArrivalInProgress(false);
            if (_portal != null)
            {
                _portal.SetActive(false);
                if (Application.isPlaying) Destroy(_portal); else DestroyImmediate(_portal);
                _portal = null;
            }
        }

        private void OnDisable() { if (_settings != null && !_complete) Finish(); }
    }
}
