using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Skills.Visuals
{
    public sealed class PlasmaFieldVisual : MonoBehaviour
    {
        private Texture2D _texture;
        private Sprite _sprite;
        private SpriteRenderer _renderer;
        private Color32[] _pixels;
        private int _size, _frame = -1;
        private float _ppu;
        private static readonly Color32 Halo = new Color32(38, 79, 212, 65);
        private static readonly Color32 Blue = new Color32(48, 153, 255, 225);
        private static readonly Color32 Cyan = new Color32(116, 242, 255, 255);
        private static readonly Color32 White = new Color32(236, 255, 255, 255);

        public static PlasmaFieldVisual Create(float radius)
        {
            var visual = new GameObject("Plasma Field VFX").AddComponent<PlasmaFieldVisual>();
            visual._size = Mathf.Clamp(Mathf.CeilToInt((radius + .5f) * 48), 32, 512);
            visual._ppu = visual._size / ((radius + .5f) * 2);
            visual._pixels = new Color32[visual._size * visual._size];
            visual._texture = new Texture2D(visual._size, visual._size, TextureFormat.RGBA32, false)
            { name = "PlasmaFieldPixels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            visual._sprite = Sprite.Create(visual._texture, new Rect(0, 0, visual._size, visual._size), Vector2.one * .5f, visual._ppu);
            visual._renderer = visual.gameObject.AddComponent<SpriteRenderer>(); visual._renderer.sprite = visual._sprite;
            visual._renderer.sharedMaterial = Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam");
            visual._renderer.color = new Color(1, 1, 1, PlayerAttackVfxOpacity.Multiplier);
            visual._renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            return visual;
        }

        public void Sample(Vector2 center, float progress, float radius)
        {
            transform.position = center;
            _renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, center.y, 2);
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * 24), 0, 24);
            if (frame == _frame) return;
            _frame = frame;
            System.Array.Clear(_pixels, 0, _pixels.Length);
            int mid = _size / 2;
            float r = radius * _ppu;
            bool returning = progress > .5f;
            int segments = Mathf.Max(24, Mathf.CeilToInt(r * 1.7f));
            Vector2Int previous = default;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float jag = Mathf.Sin(i * 2.17f + frame * 1.61f) * 1.5f + Mathf.Sin(i * .73f - frame) * .8f;
                var point = new Vector2Int(mid + Mathf.RoundToInt(Mathf.Cos(angle) * (r + jag)), mid + Mathf.RoundToInt(Mathf.Sin(angle) * (r + jag)));
                if (i > 0)
                {
                    Stroke(previous, point, Halo, 4); Stroke(previous, point, Blue, 2);
                    Stroke(previous, point, Cyan, 1); Stroke(previous, point, White, 0);
                    // Broken trailing arcs shift outward on expansion and inward on the return.
                    if ((i + frame) % 11 < 5)
                    {
                        float trail = Mathf.Max(0, r + (returning ? 5 : -5));
                        var a = new Vector2Int(mid + Mathf.RoundToInt(Mathf.Cos(angle) * trail), mid + Mathf.RoundToInt(Mathf.Sin(angle) * trail));
                        Plot(a.x, a.y, Blue, 1);
                    }
                    if ((i * 13 + frame * 7) % 23 == 0)
                    {
                        float spark = r + (returning ? -1 : 1) * (4 + (i + frame) % 6);
                        var tip = new Vector2Int(mid + Mathf.RoundToInt(Mathf.Cos(angle + .04f) * spark), mid + Mathf.RoundToInt(Mathf.Sin(angle + .04f) * spark));
                        Stroke(point, tip, Cyan, 0);
                    }
                }
                previous = point;
            }
            // A brief core flash collapses into fragments instead of leaving a solid shrinking disk.
            if (progress > .91f)
                for (int i = 0; i < 10; i++)
                {
                    float angle = i * Mathf.PI / 5 + frame;
                    float distance = Mathf.Lerp(7, 1, Mathf.InverseLerp(.91f, 1, progress));
                    Plot(mid + Mathf.RoundToInt(Mathf.Cos(angle) * distance), mid + Mathf.RoundToInt(Mathf.Sin(angle) * distance), Cyan, 0);
                }
            _texture.SetPixels32(_pixels); _texture.Apply(false, false);
        }

        private void Stroke(Vector2Int a, Vector2Int b, Color32 color, int width)
        {
            int steps = Mathf.Max(1, Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)));
            for (int i = 0; i <= steps; i++)
                Plot(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, (float)i / steps)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, (float)i / steps)), color, width);
        }
        private void Plot(int x, int y, Color32 color, int width)
        {
            for (int dy = -width; dy <= width; dy++) for (int dx = -width; dx <= width; dx++)
            {
                int px = x + dx, py = y + dy;
                if (px < 0 || py < 0 || px >= _size || py >= _size) continue;
                int index = py * _size + px;
                if (_pixels[index].a <= color.a) _pixels[index] = color;
            }
        }
        private void OnDestroy()
        {
            if (Application.isPlaying) { Destroy(_sprite); Destroy(_texture); }
            else { DestroyImmediate(_sprite); DestroyImmediate(_texture); }
        }
    }
}
