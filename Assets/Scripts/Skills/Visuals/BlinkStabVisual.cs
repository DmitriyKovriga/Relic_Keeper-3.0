using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Skills.Visuals
{
    public sealed class BlinkStabVisual : MonoBehaviour
    {
        private Texture2D _texture;
        private Sprite _sprite;
        private float _elapsed;
        private bool _strike;
        private int _frame = -1;
        private readonly Color32[] _pixels = new Color32[48 * 40];

        public static BlinkStabVisual Spawn(Vector2 center, int facing, bool strike)
        {
            var host = new GameObject(strike ? "NinjaStabImpact" : "NinjaBlinkDeparture");
            host.transform.position = center;
            var visual = host.AddComponent<BlinkStabVisual>();
            visual._strike = strike;
            visual._texture = new Texture2D(48, 40, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            visual._sprite = Sprite.Create(visual._texture, new Rect(0, 0, 48, 40), new Vector2(0.5f, 0.5f), 24);
            var renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = visual._sprite; renderer.flipX = facing < 0;
            renderer.sharedMaterial = Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam");
            renderer.color = new Color(1, 1, 1, PlayerAttackVfxOpacity.Multiplier);
            renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, center.y, 2);
            visual.Sample(0);
            return visual;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= 0.24f) { Destroy(gameObject); return; }
            Sample(_elapsed / 0.24f);
        }

        public void Sample(float progress)
        {
            int frame = Mathf.Clamp((int)(progress * 6), 0, 5);
            if (_frame == frame || _texture == null) return;
            _frame = frame;
            System.Array.Clear(_pixels, 0, _pixels.Length);
            for (int i = 0; i < 9; i++)
            {
                int x = 13 + i * 7 % 19 + (i % 2 == 0 ? -frame : frame);
                int y = 8 + i * 11 % 25 + frame;
                int length = Mathf.Max(0, 9 - frame * 2 + i % 3);
                for (int n = 0; n < length; n++)
                {
                    Put(x + n / 3, y + n, new Color32(100, 79, 155, (byte)(190 - frame * 25)));
                    Put(x + n / 3 + 1, y + n, new Color32(208, 198, 243, (byte)(230 - frame * 25)));
                }
            }
            if (_strike && frame < 4)
            {
                int start = 23 + frame * 3;
                for (int x = start; x < 47; x++)
                {
                    int thickness = Mathf.Max(0, (46 - x) / 8 - frame / 2);
                    for (int y = -thickness; y <= thickness; y++)
                        Put(x, 20 + y, y == 0 ? new Color32(250, 249, 255, 255) : new Color32(169, 145, 226, 230));
                }
            }
            _texture.SetPixels32(_pixels); _texture.Apply(false, false);
        }

        private void Put(int x, int y, Color32 color)
        {
            if (x >= 0 && x < 48 && y >= 0 && y < 40) _pixels[x + y * 48] = color;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying) { if (_sprite != null) Destroy(_sprite); if (_texture != null) Destroy(_texture); }
            else { if (_sprite != null) DestroyImmediate(_sprite); if (_texture != null) DestroyImmediate(_texture); }
        }
    }
}
