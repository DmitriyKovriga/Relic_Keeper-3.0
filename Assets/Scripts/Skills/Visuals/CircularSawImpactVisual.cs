using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class CircularSawImpactVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 24;
        private SpriteRenderer _renderer;
        private float _age;
        private void OnEnable()
        {
            _age = 0;
            _renderer = GetComponent<SpriteRenderer>();
            if (_frames == null || _frames.Length == 0) return;
            WorldRenderSorting.ConfigureAutoSorter(gameObject, RenderDepthCategory.HeroAttackVfx, transform.position.y);
            Sample(0);
        }
        private void Update() => Sample(_age + Time.deltaTime);
        public void Sample(float age)
        {
            _age = age;
            int index = Mathf.FloorToInt(age * _framesPerSecond);
            if (_frames == null || index >= _frames.Length) { Destroy(gameObject); return; }
            _renderer.sprite = _frames[index];
            _renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
        }
    }
}
