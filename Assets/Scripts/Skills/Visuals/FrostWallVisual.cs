using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class FrostWallVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _barrier;
        [SerializeField] private SpriteRenderer _empoweredCrown;
        [SerializeField] private SpriteRenderer _impact;
        [SerializeField] private Sprite[] _impactFrames;
        private float _age, _flash;
        private float _impactAge = 100, _feet;
        private Vector3 _scale;

        public void Initialize(bool empowered, Vector2 size, float feet)
        {
            _feet = feet;
            _scale = new Vector3(size.x / _barrier.sprite.bounds.size.x * 1.6f, size.y / _barrier.sprite.bounds.size.y, 1);
            _empoweredCrown.enabled = empowered;
            _empoweredCrown.transform.localScale = new Vector3(1.25f, 1.07f, 1);
            _empoweredCrown.color = new Color(.5f, .8f, 1, .5f * PlayerAttackVfxOpacity.Multiplier);
            foreach (var renderer in new[] { _barrier, _empoweredCrown, _impact })
            {
                renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
                renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, feet, renderer == _barrier ? 3 : 2);
            }
            Sample(0);
        }
        public void Impact(Vector2 contact)
        {
            _flash = .12f; _impactAge = 0;
            _impact.transform.position = contact;
        }
        private void Update() => Sample(Time.deltaTime);
        private void Sample(float dt)
        {
            _age += dt; _flash = Mathf.Max(0, _flash - dt);
            _impactAge += dt;
            float growth = Mathf.Clamp01(_age / .16f);
            transform.localScale = new Vector3(_scale.x, _scale.y * Mathf.Lerp(.1f, 1, growth), 1);
            transform.position = new Vector3(transform.position.x, _feet + _barrier.sprite.bounds.size.y * transform.localScale.y * .5f, transform.position.z);
            _barrier.color = (_flash > 0 ? new Color(.8f, 1, 1) : Color.white) * new Color(1, 1, 1, PlayerAttackVfxOpacity.Multiplier);
            int frame = Mathf.FloorToInt(_impactAge * 12);
            _impact.enabled = _impactFrames != null && frame < _impactFrames.Length;
            if (_impact.enabled)
            {
                _impact.sprite = _impactFrames[frame];
                _impact.transform.localScale = new Vector3(1 / transform.localScale.x, 1 / transform.localScale.y, 1);
                _impact.color = new Color(1, 1, 1, PlayerAttackVfxOpacity.Multiplier);
            }
        }
    }
}
