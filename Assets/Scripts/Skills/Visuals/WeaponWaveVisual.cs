using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class WeaponWaveVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 24;
        [SerializeField] private bool _loop = true;
        [SerializeField] private GameObject _impactPrefab;
        private SpriteRenderer _renderer;
        private float _age;
        private void OnEnable() => Configure();
        public void Configure()
        {
            _renderer = GetComponent<SpriteRenderer>(); _age = 0;
            Sample(0);
        }
        private void Update() => Sample(_age + Time.deltaTime);
        public void Sample(float age)
        {
            _age = Mathf.Max(0, age);
            if (_renderer == null || _frames == null || _frames.Length == 0) return;
            int frame = Mathf.FloorToInt(_age * _framesPerSecond);
            if (!_loop && frame >= _frames.Length) { Destroy(gameObject); return; }
            _renderer.sprite = _frames[frame % _frames.Length];
            _renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
        }
        public void ShowImpact(IDamageable target)
        {
            if (_impactPrefab == null || !Application.isPlaying) return;
            var component = target as Component;
            var collider = component != null ? component.GetComponent<Collider2D>() : null;
            Vector2 point = collider != null ? collider.ClosestPoint(transform.position) : transform.position;
            var effect = Instantiate(_impactPrefab, point, Quaternion.identity);
            WorldRenderSorting.ConfigureAutoSorter(effect, RenderDepthCategory.HeroAttackVfx, point.y, 4);
        }
    }
}
