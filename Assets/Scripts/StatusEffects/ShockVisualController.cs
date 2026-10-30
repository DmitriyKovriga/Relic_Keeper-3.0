using Scripts.Enemies;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEngine;

namespace Scripts.StatusEffects
{
    /// <summary>One source-owned arc layer. Buildup and active shock never write body tint/material channels.</summary>
    [DisallowMultipleComponent]
    public sealed class ShockVisualController : MonoBehaviour
    {
        // Keep authored field identities; these now color/scale the arc layer.
        [SerializeField] private Color _warmTint = new Color(1f, .92f, .22f, 1f);
        [SerializeField] private Color _coldTint = new Color(.55f, .9f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float _intensity = .42f;
        [SerializeField] private float _pulseSpeed = 18f;
        private float _remainingSeconds, _buildup;
        private bool _active;
        private float _activationUntil = float.NegativeInfinity;
        private SpriteRenderer _body, _arcs;
        private MaterialPropertyBlock _properties;
        private EnemyHealth _health;
        private AilmentController _owner;
        private bool _followsOwner;
        private PlayerMovementVisual _playerVisual;
        private float _phase;
        private const float ActivationDuration = .1f;
        public bool IsActive => _active;
        public float BuildupNormalized => _buildup;

        private void OnDisable() => Stop();
        private void Update()
        {
            if (_health != null && _health.IsDead) { Stop(); return; }
            if (!_active) return;
            if (_followsOwner)
            {
                if (_owner == null || !_owner.IsShocked) Stop();
                return;
            }
            _remainingSeconds -= Time.deltaTime;
            if (_remainingSeconds <= 0) Stop();
        }
        public void SetBuildup(float fraction)
        {
            if (_active) return;
            _buildup = Mathf.Clamp(fraction, 0, .999f);
            if (_buildup > 0) { enabled = true; LateUpdate(); }
            else if (_arcs != null) _arcs.enabled = false;
        }
        public void Play(float duration)
        {
            if (duration <= 0) return;
            _owner = GetComponent<AilmentController>() ?? GetComponentInParent<AilmentController>();
            _followsOwner = _owner != null && _owner.IsShocked;
            if (!_active) _activationUntil = Time.time + ActivationDuration;
            _active = true; _buildup = 0;
            _remainingSeconds = Mathf.Max(_remainingSeconds, duration);
            enabled = true; LateUpdate();
        }
        public void Stop()
        {
            _remainingSeconds = _buildup = 0; _active = false;
            _followsOwner = false;
            _activationUntil = float.NegativeInfinity;
            if (_arcs != null) _arcs.enabled = false;
        }
        /// <summary>Fast held-shape snaps with irregular gaps; no gameplay RNG or scrolling topology.</summary>
        public static Vector2 SampleDischarge(float seconds, bool reduced)
        {
            float cycle = reduced ? .48f : .28f;
            float index = Mathf.Floor(seconds / cycle), local = seconds - index * cycle;
            float random = Mathf.Repeat(Mathf.Sin((index + 3) * 12.9898f) * 43758.5453f,1);
            float second = .085f + random * .025f;
            float seed = index * 2 + (!reduced && local >= second ? 1 : 0);
            float pulse = Pulse(local,0,reduced ? .16f : .04f + random * .025f,reduced);
            if (!reduced) pulse = Mathf.Max(pulse,Pulse(local,second,.028f + random * .016f,false));
            return new Vector2(seed,pulse);
        }
        private static float Pulse(float time,float start,float duration,bool reduced)
        {
            float t = (time-start)/duration;
            if (t < 0 || t >= 1) return 0;
            float attack = Mathf.SmoothStep(0,1,Mathf.Clamp01(t/(reduced ? .22f : .1f)));
            float tail = 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.3f)/.7f));
            return attack * tail;
        }
        private SpriteRenderer FindBody()
        {
            var entity = GetComponent<EnemyEntity>() ?? GetComponentInParent<EnemyEntity>();
            if (entity != null && entity.VisualRenderer != null) return entity.VisualRenderer;
            if (_playerVisual == null) _playerVisual = GetComponent<PlayerMovementVisual>() ?? GetComponentInParent<PlayerMovementVisual>();
            if (_playerVisual != null && _playerVisual.DisplayRenderer != null) return _playerVisual.DisplayRenderer;
            SpriteRenderer best = null; float size = -1;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == _arcs || renderer.name.Contains("Shadow") || renderer.GetComponentInParent<Canvas>() != null || IsIgniteFlame(renderer)) continue;
                float candidate = renderer.sprite != null ? renderer.bounds.size.sqrMagnitude : 0;
                if (candidate > size) { best = renderer; size = candidate; }
            }
            return best;
        }
        private void LateUpdate()
        {
            if (!_active && _buildup <= 0) return;
            if (_health == null) _health = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>();
            if (_health != null && _health.IsDead) { Stop(); return; }
            if (_body == null || _playerVisual != null) _body = FindBody();
            if (_body == null) return;
            if (_arcs == null)
            {
                var material = Resources.Load<Material>("Visuals/ShockArc");
                if (material == null || material.shader == null || !material.shader.isSupported) return;
                var host = new GameObject("ShockArcs"); host.layer = _body.gameObject.layer;
                host.transform.SetParent(_body.transform, false);
                _arcs = host.AddComponent<SpriteRenderer>(); _arcs.sharedMaterial = material;
                _properties = new MaterialPropertyBlock(); _phase = Mathf.Repeat(GetInstanceID() * .037f, 53);
                host.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Characters, 1, true);
            }
            if (_arcs.transform.parent != _body.transform) _arcs.transform.SetParent(_body.transform, false);
            _arcs.enabled = _body.enabled && _body.gameObject.activeInHierarchy && _body.sprite != null;
            if (!_arcs.enabled) return;
            _arcs.sprite = _body.sprite; _arcs.flipX = _body.flipX; _arcs.flipY = _body.flipY;
            _arcs.sortingLayerID = _body.sortingLayerID; _arcs.sortingOrder = _body.sortingOrder + 3;
            _arcs.maskInteraction = _body.maskInteraction; _arcs.spriteSortPoint = _body.spriteSortPoint;
            _arcs.color = new Color(1,1,1,_body.color.a);
            var bounds = _body.sprite.bounds; bool reduced = GameplayPresentationSettings.ReducedCombatEffects;
            _properties.Clear();
            _properties.SetVector("_ArcRect",new Vector4(bounds.min.x,bounds.min.y,bounds.size.x,bounds.size.y));
            _properties.SetFloat("_ArcPixels",Mathf.Max(1,bounds.size.x * _body.sprite.pixelsPerUnit));
            float arcTime = Time.time * Mathf.Max(.1f,_pulseSpeed/18f) + _phase;
            _properties.SetFloat("_ArcTime",arcTime);
            var discharge = SampleDischarge(_active ? arcTime : arcTime * .55f,reduced);
            _properties.SetFloat("_ArcSeed",discharge.x); _properties.SetFloat("_ArcPulse",discharge.y);
            _properties.SetFloat("_ArcActive",_active ? 1 : 0); _properties.SetFloat("_ArcBuildup",_buildup);
            _properties.SetFloat("_ArcActivation",_active ? Mathf.Clamp01((_activationUntil-Time.time)/ActivationDuration) : 0);
            _properties.SetFloat("_ArcStrength",Mathf.Clamp01(_intensity * 2) * (reduced ? .55f : 1));
            _properties.SetFloat("_ArcReduced",reduced ? 1 : 0);
            _properties.SetColor("_ArcColor",_coldTint);
            _properties.SetColor("_ArcCore",Color.Lerp(Color.white,_warmTint,.12f));
            _arcs.SetPropertyBlock(_properties);
        }
        private void OnDestroy()
        {
            if (_arcs == null) return;
            if (Application.isPlaying) Destroy(_arcs.gameObject); else DestroyImmediate(_arcs.gameObject);
        }
        private static bool IsIgniteFlame(SpriteRenderer renderer)
        {
            if (renderer == null) return false;
            for (var node = renderer.transform.parent; node != null; node = node.parent)
                if (node.name == "IgniteFlames") return true;
            return false;
        }
    }
}
