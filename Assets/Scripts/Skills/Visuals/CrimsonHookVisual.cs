using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class CrimsonHookVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] _tethers;
        [SerializeField] private SpriteRenderer[] _hooks;
        private Vector2 _origin, _end;
        private float _width, _duration, _age;
        public void Initialize(Vector2 origin, Vector2 end, float width, float duration)
        {
            _origin = origin; _end = end; _width = width; _duration = Mathf.Max(.1f, duration) + .1f;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
            {
                renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
                renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, origin.y - .5f, 3);
            }
            Sample(0);
        }
        private void Update() => Sample(Time.deltaTime);
        public void Sample(float dt)
        {
            _age += Mathf.Max(0, dt);
            float t = Mathf.Clamp01(_age / _duration);
            float extent = Mathf.Lerp(1, .07f, 1 - (1 - t) * (1 - t));
            float alpha = Mathf.Clamp01((1 - t) * 4) * PlayerAttackVfxOpacity.Multiplier;
            for (int i = 0; i < _hooks.Length; i++)
            {
                float spread = (i - 1) * _width * .35f * extent;
                Vector2 head = Vector2.Lerp(_origin, _end, extent) + Vector2.up * spread;
                var hook = _hooks[i]; hook.transform.position = head;
                hook.flipX = _end.x < _origin.x;
                hook.color = new Color(1, 1, 1, alpha * (i == 1 ? 1 : .55f));
                var tether = _tethers[i];
                tether.drawMode = SpriteDrawMode.Tiled;
                tether.transform.position = (_origin + head) * .5f;
                tether.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(head.y - _origin.y, head.x - _origin.x) * Mathf.Rad2Deg);
                tether.size = new Vector2(Vector2.Distance(_origin, head), tether.sprite.rect.height / tether.sprite.pixelsPerUnit);
                tether.color = new Color(1, 1, 1, alpha * (i == 1 ? .9f : .35f));
            }
            if (t >= 1 && Application.isPlaying) Destroy(gameObject);
        }
    }
}
