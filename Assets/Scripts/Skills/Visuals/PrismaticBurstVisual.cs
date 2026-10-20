using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class PrismaticBurstVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 24;
        private SpriteRenderer _renderer;
        private float _age;
        public void Initialize(float y)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _age = 0;
            if (_frames != null && _frames.Length > 0) _renderer.sprite = _frames[0];
            _renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            _renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, y, 5);
        }
        private void Update()
        {
            _age += Time.deltaTime;
            if (_renderer == null) return;
            int frame = Mathf.FloorToInt(_age * _framesPerSecond);
            if (_frames == null || frame >= _frames.Length) { Destroy(gameObject); return; }
            _renderer.sprite = _frames[frame];
            _renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
        }
    }
}
