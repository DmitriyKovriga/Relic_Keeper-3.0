using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Skills.Visuals
{
    public sealed class WinterStormVisual : MonoBehaviour
    {
        private Texture2D _texture;
        private Sprite _sprite;
        private Material _material;
        private SpriteRenderer _renderer;
        private Color32[] _pixels;
        private int _size, _frame = -1;
        private int _floorPixel;
        private float _radius, _elapsed, _duration, _release, _releaseAlpha;
        private bool _releasing;

        public static WinterStormVisual Create(float radius)
        {
            var visual = new GameObject("Winter Storm blizzard").AddComponent<WinterStormVisual>();
            visual._radius = radius;
            visual._size = Mathf.Clamp(Mathf.CeilToInt((radius + .4f) * 48), 48, 512);
            visual._pixels = new Color32[visual._size * visual._size];
            visual._texture = new Texture2D(visual._size, visual._size, TextureFormat.RGBA32, false)
                { name = "WinterStormPixels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            visual._sprite = Sprite.Create(visual._texture, new Rect(0, 0, visual._size, visual._size), Vector2.one * .5f, visual._size / ((radius + .4f) * 2));
            visual._renderer = visual.gameObject.AddComponent<SpriteRenderer>(); visual._renderer.sprite = visual._sprite;
            visual._material = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam"));
            visual._material.mainTexture = visual._texture; visual._renderer.sharedMaterial = visual._material;
            visual._renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            return visual;
        }

        public void Sample(Vector2 center, float elapsed, float duration)
        {
            transform.position = center;
            _elapsed = elapsed; _duration = duration;
            _renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, center.y, 1);
            int frame = Mathf.FloorToInt(elapsed * 16);
            if (_frame == frame) return;
            _frame = frame;
            System.Array.Clear(_pixels, 0, _pixels.Length);
            float ppu = _sprite.pixelsPerUnit, mid = _size * .5f;
            float r = _radius * ppu;
            _floorPixel = 0;
            foreach (var hit in Physics2D.RaycastAll(center, Vector2.down, _radius + .4f, (1 << 6) | (1 << 8)))
            {
                if (hit.collider.isTrigger || hit.normal.y < .6f) continue;
                _floorPixel = Mathf.Clamp(Mathf.CeilToInt(mid - (center.y - hit.point.y) * ppu), 0, _size);
                break;
            }
            // Open, offset wind bands leave the player and overlapping defensive effects readable.
            for (int band = 0; band < 4; band++)
            {
                float phase = elapsed * (1.8f + band * .17f) + band * 1.7f;
                for (int i = 0; i < 105; i++)
                {
                    if ((i + band * 11) % 37 > 22) continue;
                    float a = phase + i * .043f;
                    float radius = r * (.6f + band * .1f);
                    int x = Mathf.RoundToInt(mid + Mathf.Cos(a) * radius);
                    int y = Mathf.RoundToInt(mid + Mathf.Sin(a) * radius * (.72f + band * .08f));
                    Put(x, y, new Color32(93, 181, 226, 110));
                    if (i % 4 != 0) Put(x, y + 1, new Color32(189, 239, 251, 150));
                    Put(x, y - 1, new Color32(76, 145, 204, 50));
                }
            }
            for (int i = 0; i < 55; i++)
            {
                float a = i * 2.39996f + elapsed * (1.4f + i % 4 * .2f);
                float distance = r * (.3f + (i * 17 % 67) / 100f);
                float fall = Mathf.Repeat(i * .271f - elapsed * .6f, 1) - .5f;
                int x = Mathf.RoundToInt(mid + Mathf.Cos(a) * distance);
                int y = Mathf.RoundToInt(mid + Mathf.Sin(a) * distance * .8f + fall * r * .25f);
                Color32 color = i % 3 == 0 ? new Color32(237, 254, 255, 235) : new Color32(132, 211, 245, 190);
                Put(x, y, color); Put(x + 1, y + 1, color);
                if (i % 3 == 0) Put(x + 1, y, color);
                int dx = Mathf.RoundToInt(-Mathf.Sin(a)), dy = Mathf.RoundToInt(Mathf.Cos(a));
                Put(x - dx, y - dy, new Color32(115, 194, 231, 85));
                if (i % 7 == 0) { Put(x + 1, y - 1, color); Put(x - 1, y + 1, color); Put(x - 1, y - 1, color); }
            }
            _texture.SetPixels32(_pixels); _texture.Apply(false, false);
            float ending = Mathf.Clamp01((duration - elapsed) / .25f);
            _renderer.color = new Color(1, 1, 1, ending * PlayerAttackVfxOpacity.Multiplier);
        }

        private void Put(int x, int y, Color32 color)
        {
            if (x >= 0 && y >= _floorPixel && x < _size && y < _size) _pixels[x + y * _size] = color;
        }

        public void Release()
        {
            if (_releasing) return;
            _releasing = true; _release = 0; _releaseAlpha = _renderer.color.a;
        }

        private void Update()
        {
            if (!_releasing) return;
            _release += Time.deltaTime;
            Sample(transform.position, _elapsed + Time.deltaTime, Mathf.Max(_duration, _elapsed + .25f));
            _renderer.color = new Color(1, 1, 1, Mathf.Max(0, 1 - _release / .25f) * _releaseAlpha);
            if (_release >= .25f) Destroy(gameObject);
        }

        private void OnDestroy() { Dispose(_sprite); Dispose(_texture); Dispose(_material); }
        private static void Dispose(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
