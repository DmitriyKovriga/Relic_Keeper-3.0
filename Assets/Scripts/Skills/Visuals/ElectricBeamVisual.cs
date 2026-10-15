using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Skills.Visuals
{
    /// <summary>A full-length pixel beam on its first frame, followed by snapping arcs and dissolving sparks.</summary>
    public sealed class ElectricBeamVisual : MonoBehaviour
    {
        private const int PixelsPerUnit = 24;
        private Texture2D _texture;
        private Sprite _sprite;
        private SpriteRenderer _renderer;
        private Color32[] _pixels;
        private float _lifetime;
        private float _elapsed;
        private int _frame = -1;
        private int _width;
        private int _height;
        private int _spread;
        private static readonly Color32 Halo = new Color32(34, 117, 210, 100);
        private static readonly Color32 Blue = new Color32(62, 192, 255, 210);
        private static readonly Color32 White = new Color32(226, 253, 255, 255);

        public static ElectricBeamVisual Spawn(Vector2 start, Vector2 end, float lifetime, float hitWidth = 0.35f)
        {
            var host = new GameObject("ElectricBeamVFX");
            host.transform.position = (start + end) * 0.5f;
            var visual = host.AddComponent<ElectricBeamVisual>();
            visual._lifetime = Mathf.Max(0.08f, lifetime);
            visual._width = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(start, end) * PixelsPerUnit), 2, 1024);
            visual._spread = Mathf.Clamp(Mathf.RoundToInt(hitWidth * PixelsPerUnit * 0.5f), 3, 20);
            visual._height = Mathf.Max(32, visual._spread * 4 + 8);
            visual._pixels = new Color32[visual._width * visual._height];
            visual._texture = new Texture2D(visual._width, visual._height, TextureFormat.RGBA32, false)
            {
                name = "ElectricBeamPixels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            visual._sprite = Sprite.Create(visual._texture,
                new Rect(0, 0, visual._width, visual._height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            visual._renderer = host.AddComponent<SpriteRenderer>();
            visual._renderer.sprite = visual._sprite;
            visual._renderer.flipX = end.x < start.x;
            // An asset reference in Resources retains the unlit shader in standalone builds.
            visual._renderer.sharedMaterial = Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam");
            visual._renderer.color = new Color(1f, 1f, 1f, PlayerAttackVfxOpacity.Multiplier);
            visual._renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            visual._renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, start.y, 2);
            visual.Sample(0f);
            return visual;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime) { Destroy(gameObject); return; }
            Sample(_elapsed / _lifetime);
        }

        public void Sample(float progress)
        {
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * 7f), 0, 6);
            if (frame == _frame || _pixels == null) return;
            _frame = frame;
            System.Array.Clear(_pixels, 0, _pixels.Length);
            int mid = _height / 2;
            int previous = mid;
            int chunk = 8;
            for (int x = 0; x < _width; x += chunk)
            {
                int nextX = Mathf.Min(_width - 1, x + chunk);
                int next = mid + Hash(x / chunk + frame * 37) % (2 * _spread + 1) - _spread;
                if (nextX == _width - 1) next = mid;
                // The final frames break into separate filaments instead of fading a fixed silhouette.
                bool visible = frame < 4 || Hash(x + frame * 61) % 7 >= frame - 3;
                if (visible)
                {
                    Stroke(x, previous, nextX, next, Halo, frame < 3 ? 3 : 1);
                    Stroke(x, previous, nextX, next, Blue, frame < 4 ? 1 : 0);
                    Stroke(x, previous, nextX, next, White, 0);
                }
                if (x > 8 && Hash(x + frame * 11) % 5 == 0)
                {
                    int sign = Hash(x) % 2 == 0 ? 1 : -1;
                    Stroke(x, previous, Mathf.Min(_width - 1, x + 6),
                        previous + sign * (_spread + 3), Blue, 0);
                }
                previous = next;
            }
            if (frame < 4)
            {
                int radius = 3 + frame;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    int x = 4 + Mathf.RoundToInt(Mathf.Cos(a) * radius * 0.6f);
                    int y = mid + Mathf.RoundToInt(Mathf.Sin(a) * radius);
                    Dot(x, y, White);
                }
            }
            for (int i = 0; i < 12; i++)
            {
                int x = Hash(i * 19 + frame * 29) % _width;
                int y = mid + (Hash(i * 17 + frame * 43) % (_spread * 4 + 1)) - _spread * 2;
                Dot(x, y, Blue);
                if (frame < 4) Dot(x + 1, y, White);
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            var color = _renderer.color;
            color.a = PlayerAttackVfxOpacity.Multiplier * (frame < 4 ? 1f : (7f - frame) / 4f);
            _renderer.color = color;
        }

        private void Stroke(int x0, int y0, int x1, int y1, Color32 color, int radius)
        {
            int count = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= count; i++)
            {
                float t = count == 0 ? 0f : (float)i / count;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int dy = -radius; dy <= radius; dy++) Dot(x, y + dy, color);
            }
        }

        private void Dot(int x, int y, Color32 color)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height) return;
            int index = y * _width + x;
            if (_pixels[index].a <= color.a) _pixels[index] = color;
        }

        private static int Hash(int value)
        {
            unchecked { uint v = (uint)value * 747796405u + 2891336453u; return (int)((v ^ (v >> 16)) & 0x7fffffffu); }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (_sprite != null) Destroy(_sprite);
                if (_texture != null) Destroy(_texture);
            }
            else
            {
                if (_sprite != null) DestroyImmediate(_sprite);
                if (_texture != null) DestroyImmediate(_texture);
            }
        }
    }
}
