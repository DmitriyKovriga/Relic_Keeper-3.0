using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Skills.Visuals
{
    public sealed class LightningRodVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _firstRod;
        [SerializeField] private SpriteRenderer _secondRod;
        [SerializeField] private SpriteRenderer _arc;
        [SerializeField] private Sprite[] _arcFrames;

        public void Initialize(Vector2 start, Vector2 end)
        {
            _firstRod.transform.position = start;
            _secondRod.transform.position = end;
            foreach (var renderer in new[] { _firstRod, _secondRod, _arc })
            {
                renderer.color = new Color(1, 1, 1, PlayerAttackVfxOpacity.Multiplier);
                renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
                renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, start.y, renderer == _arc ? 3 : 2);
            }
            _arc.drawMode = SpriteDrawMode.Tiled;
            Sample(start, start, false, 0);
        }

        public void Sample(Vector2 source, Vector2 head, bool active, float elapsed)
        {
            float length = Vector2.Distance(source, head);
            _arc.enabled = active && length >= 1f / 24 && _arcFrames != null && _arcFrames.Length > 0;
            if (!_arc.enabled) return;
            _arc.sprite = _arcFrames[Mathf.FloorToInt(elapsed * 20) % _arcFrames.Length];
            _arc.transform.position = (source + head) * .5f;
            _arc.transform.rotation = Quaternion.Euler(0, 0, head.x < source.x ? 180 : 0);
            // Tiling preserves authored pixel size as the gap and travelling front grow.
            _arc.size = new Vector2(Mathf.Round(length * 24) / 24, _arc.sprite.rect.height / _arc.sprite.pixelsPerUnit);
        }
    }
}
