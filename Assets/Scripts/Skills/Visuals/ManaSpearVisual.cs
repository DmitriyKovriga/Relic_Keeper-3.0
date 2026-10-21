using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class ManaSpearVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private SpriteRenderer _art;
        [SerializeField] private SpriteRenderer _glow;
        [SerializeField] private Sprite[] _glowFrames;
        [SerializeField] private float _framesPerSecond = 24;
        [SerializeField] private bool _loop = true;
        [SerializeField] private GameObject _impactPrefab;
        private SpriteRenderer _root;
        private float _age;
        private void OnEnable() => Configure();
        public void Configure()
        {
            _root = GetComponent<SpriteRenderer>(); _age = 0;
            if (_art != null) _root.enabled = false;
            if (_frames != null && _frames.Length > 0) Sample(0);
        }
        private void Update() => Sample(_age + Time.deltaTime);
        public void Sample(float age)
        {
            _age = Mathf.Max(0, age);
            if (_frames == null || _frames.Length == 0) return;
            int index = Mathf.FloorToInt(_age * _framesPerSecond);
            if (!_loop && index >= _frames.Length) { Destroy(gameObject); return; }
            var renderer = _art != null ? _art : _root;
            if (renderer == null) return;
            renderer.sprite = _frames[index % _frames.Length];
            renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
            if (_glow != null && _glowFrames != null && _glowFrames.Length > 0)
                _glow.sprite = _glowFrames[index % _glowFrames.Length];
        }
        private void LateUpdate()
        {
            if (_art == null || _root == null) return;
            _art.sortingLayerID = _root.sortingLayerID; _art.sortingOrder = _root.sortingOrder; _art.color = _root.color;
            if (_glow != null)
            {
                _glow.sortingLayerID = _root.sortingLayerID;
                _glow.sortingOrder = _root.sortingOrder - 1;
                _glow.color = new Color(_root.color.r, _root.color.g, _root.color.b, _root.color.a * .65f);
            }
        }
        public void ShowImpact(IDamageable target)
        {
            if (_impactPrefab == null || !Application.isPlaying) return;
            var component = target as Component;
            var body = component != null ? component.GetComponent<Collider2D>() : null;
            Vector2 point = body != null ? body.ClosestPoint(transform.position) : component != null ? (Vector2)component.transform.position : (Vector2)transform.position;
            var instance = Instantiate(_impactPrefab, point, Quaternion.identity);
            WorldRenderSorting.ConfigureAutoSorter(instance, RenderDepthCategory.HeroAttackVfx, point.y, 4);
        }
    }
}
