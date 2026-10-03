using System.Collections.Generic;
using UnityEngine;

namespace Scripts.StatusEffects
{
    /// <summary>Flickering flames and a warm tint while ignite stacks are active.</summary>
    [DisallowMultipleComponent]
    public sealed class IgniteVisualController : MonoBehaviour
    {
        private const int FlameCount = 3;
        private const string FlameRootName = "IgniteFlames";

        private static Sprite _flameSprite;
        private static readonly Vector2[] FlameAnchors =
        {
            new Vector2(-0.28f, 0.42f),
            new Vector2(0.05f, 0.95f),
            new Vector2(0.3f, 0.58f)
        };

        private readonly List<SpriteRenderer> _bodyRenderers = new List<SpriteRenderer>(4);
        private readonly Dictionary<SpriteRenderer, Color> _baseColors = new Dictionary<SpriteRenderer, Color>();
        private readonly SpriteRenderer[] _flames = new SpriteRenderer[FlameCount];
        private Transform _flameRoot;
        private bool _burning;

        private void OnDisable()
        {
            SetBurning(false);
        }

        private void LateUpdate()
        {
            if (!_burning)
                return;

            AnimateBody();
            AnimateFlames();
        }

        public void SetBurning(bool burning)
        {
            if (_burning == burning)
                return;

            _burning = burning;
            if (burning)
            {
                CacheBody();
                EnsureFlames();
                enabled = true;
                return;
            }

            RestoreBody();
            if (_flameRoot != null)
                _flameRoot.gameObject.SetActive(false);
        }

        private void CacheBody()
        {
            _bodyRenderers.Clear();
            _baseColors.Clear();
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || IsFlame(renderer))
                    continue;

                _bodyRenderers.Add(renderer);
                _baseColors[renderer] = renderer.color;
            }
        }

        private void AnimateBody()
        {
            ShockVisualController shock = GetComponent<ShockVisualController>();
            if (shock != null && shock.IsActive)
                return;

            float flicker = (Mathf.Sin(Time.time * 14f) + 1f) * 0.5f;
            float amount = Mathf.Lerp(0.12f, 0.28f, flicker);
            Color heat = new Color(1f, 0.42f, 0.08f, 1f);

            for (int i = 0; i < _bodyRenderers.Count; i++)
            {
                SpriteRenderer renderer = _bodyRenderers[i];
                if (renderer == null || !_baseColors.TryGetValue(renderer, out Color baseColor))
                    continue;

                Color tinted = Color.Lerp(baseColor, heat, amount);
                tinted.a = baseColor.a;
                renderer.color = tinted;
            }
        }

        private void RestoreBody()
        {
            foreach (KeyValuePair<SpriteRenderer, Color> pair in _baseColors)
            {
                if (pair.Key != null)
                    pair.Key.color = pair.Value;
            }

            _baseColors.Clear();
            _bodyRenderers.Clear();
        }

        private void EnsureFlames()
        {
            if (_flameRoot == null)
            {
                var rootObject = new GameObject(FlameRootName);
                _flameRoot = rootObject.transform;
                _flameRoot.SetParent(transform, false);
                Sprite sprite = FlameSprite();
                Material material = FlameMaterial();
                for (int i = 0; i < FlameCount; i++)
                {
                    var flameObject = new GameObject("Flame");
                    flameObject.transform.SetParent(_flameRoot, false);
                    SpriteRenderer renderer = flameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sharedMaterial = material;
                    _flames[i] = renderer;
                }
            }

            _flameRoot.gameObject.SetActive(true);
            PlaceFlames();
        }

        private void AnimateFlames()
        {
            PlaceFlames();
            for (int i = 0; i < FlameCount; i++)
            {
                SpriteRenderer flame = _flames[i];
                if (flame == null)
                    continue;

                float wave = (Mathf.Sin(Time.time * (11f + i * 3f) + i) + 1f) * 0.5f;
                Transform flameTransform = flame.transform;
                Vector3 scale = flameTransform.localScale;
                scale.x = Mathf.Lerp(0.65f, 1.05f, wave);
                scale.y = Mathf.Lerp(0.55f, 1.25f, wave);
                flameTransform.localScale = scale;
                flame.color = Color.Lerp(new Color(1f, 0.28f, 0.05f, 0.75f), new Color(1f, 0.92f, 0.35f, 0.95f), wave);
            }
        }

        private void PlaceFlames()
        {
            SpriteRenderer body = FindBody();
            Bounds bounds = body != null && body.sprite != null ? body.sprite.bounds : new Bounds(Vector3.zero, new Vector3(0.6f, 0.8f, 0f));
            int sortingLayer = body != null ? body.sortingLayerID : 0;
            int sortingOrder = body != null ? body.sortingOrder + 2 : 2;
            Transform parent = body != null ? body.transform : transform;
            if (_flameRoot.parent != parent)
                _flameRoot.SetParent(parent, false);

            for (int i = 0; i < FlameCount; i++)
            {
                SpriteRenderer flame = _flames[i];
                if (flame == null)
                    continue;

                Vector2 anchor = FlameAnchors[i];
                float x = Mathf.Lerp(bounds.min.x, bounds.max.x, 0.5f + anchor.x);
                float y = Mathf.Lerp(bounds.min.y, bounds.max.y, anchor.y);
                flame.transform.localPosition = new Vector3(x, y, 0f);
                flame.sortingLayerID = sortingLayer;
                flame.sortingOrder = sortingOrder;
            }
        }

        private SpriteRenderer FindBody()
        {
            SpriteRenderer best = null;
            float bestSize = -1f;
            for (int i = 0; i < _bodyRenderers.Count; i++)
            {
                SpriteRenderer renderer = _bodyRenderers[i];
                if (renderer == null || renderer.sprite == null)
                    continue;

                float size = renderer.sprite.bounds.size.sqrMagnitude;
                if (size <= bestSize)
                    continue;

                bestSize = size;
                best = renderer;
            }

            return best;
        }

        private static bool IsFlame(SpriteRenderer renderer)
        {
            return renderer.transform.parent != null && renderer.transform.parent.name == FlameRootName;
        }

        private static Sprite FlameSprite()
        {
            if (_flameSprite != null)
                return _flameSprite;

            var texture = new Texture2D(5, 7, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color red = new Color(0.85f, 0.12f, 0.02f, 0.9f);
            Color orange = new Color(1f, 0.45f, 0.05f, 0.95f);
            Color yellow = new Color(1f, 0.92f, 0.4f, 1f);
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 5; x++)
                texture.SetPixel(x, y, clear);

            texture.SetPixel(2, 6, yellow);
            texture.SetPixel(1, 5, orange);
            texture.SetPixel(2, 5, yellow);
            texture.SetPixel(3, 5, orange);
            texture.SetPixel(1, 4, orange);
            texture.SetPixel(2, 4, yellow);
            texture.SetPixel(3, 4, orange);
            texture.SetPixel(0, 3, red);
            texture.SetPixel(1, 3, orange);
            texture.SetPixel(2, 3, yellow);
            texture.SetPixel(3, 3, orange);
            texture.SetPixel(4, 3, red);
            texture.SetPixel(1, 2, red);
            texture.SetPixel(2, 2, orange);
            texture.SetPixel(3, 2, red);
            texture.SetPixel(2, 1, red);
            texture.Apply();

            _flameSprite = Sprite.Create(texture, new Rect(0f, 0f, 5f, 7f), new Vector2(0.5f, 0f), 16f);
            _flameSprite.name = "IgniteFlame";
            return _flameSprite;
        }

        private static Material FlameMaterial()
        {
            Shader shader = Shader.Find("RelicKeeper/Sprites/Aura Halo");
            return shader != null ? new Material(shader) : null;
        }
    }
}
