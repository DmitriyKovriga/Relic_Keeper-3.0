using System.Collections.Generic;
using Scripts.Combat;
using UnityEngine;

namespace Scripts.Skills.Projectiles
{
    /// <summary>
    /// Runtime pixel VFX for the 48x48 elemental pulse balls. It deliberately creates
    /// its sprites at runtime so all three spells share one projectile prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElementalPulseBallVisual : MonoBehaviour
    {
        public const int SpriteSizePixels = 48;
        private const float PixelsPerUnit = 24f;
        private const int GeneratedSpriteVersion = 2;
        private const string FireAnimationResourcePath = "VFX/BigFireBall";
        private const string ColdAnimationResourcePath = "VFX/BigColdBall";
        private const string LightningAnimationResourcePath = "VFX/BigLightningBall";
        private const float AuthoredFrameSeconds = 1f / 12f;

        private static readonly Dictionary<DamageChannel, Sprite> SpriteCache = new();
        private static Sprite[] _fireFrames;
        private static Sprite[] _coldFrames;
        private static Sprite[] _lightningFrames;
        private static Sprite _pulseRingSprite;
        private static int _spriteCacheVersion;

        private SpriteRenderer _main;
        private SpriteRenderer _halo;
        private SpriteRenderer _pulse;
        private SpriteRenderer[] _motifs;
        private TrailRenderer _trail;
        private LineRenderer _lightningArc;
        private DamageChannel _element = DamageChannel.Fire;
        private float _pulseInterval = 0.35f;
        private float _age;
        private float _nextArcUpdate;
        private Color _core;
        private Color _edge;
        private Color _accent;
        private Sprite[] _authoredFrames;
        private bool _usesAuthoredFrames;

        private void Awake()
        {
            EnsureVisualObjects();
            ApplyElement();
        }

        private void OnEnable()
        {
            _age = 0f;
            _nextArcUpdate = 0f;
            EnsureVisualObjects();
            ApplyElement();
        }

        /// <summary>Called by SkillProjectile after the recipe has supplied its element and pulse cadence.</summary>
        public void Configure(DamageChannel element, float pulseInterval)
        {
            _element = element;
            _pulseInterval = Mathf.Max(0.05f, pulseInterval);
            EnsureVisualObjects();
            ApplyElement();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float phase = Mathf.Repeat(_age / Mathf.Max(0.05f, _pulseInterval), 1f);
            float wave = Mathf.Sin(phase * Mathf.PI * 2f) * 0.5f + 0.5f;

            if (_main != null)
            {
                _main.transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.08f, wave);
                if (_usesAuthoredFrames)
                    ApplyAuthoredFrame();
            }
            if (_halo != null)
            {
                _halo.transform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1.18f, wave);
                _halo.color = WithAlpha(_accent, Mathf.Lerp(0.12f, 0.22f, wave));
            }
            if (_pulse != null)
            {
                float pulse = Mathf.InverseLerp(0.05f, 0.85f, phase);
                _pulse.transform.localScale = Vector3.one * Mathf.Lerp(1.14f, 1.65f, pulse);
                _pulse.color = WithAlpha(_accent, Mathf.Lerp(0.48f, 0f, pulse));
            }

            UpdateMotifs(phase, wave);
            if (!_usesAuthoredFrames && _element == DamageChannel.Lightning && Time.time >= _nextArcUpdate)
            {
                _nextArcUpdate = Time.time + 0.055f;
                UpdateLightningArc();
            }
        }

        private void EnsureVisualObjects()
        {
            _main = GetComponent<SpriteRenderer>();
            if (_main == null)
                _main = gameObject.AddComponent<SpriteRenderer>();
            if (_main.sharedMaterial == null)
                _main.sharedMaterial = CreateSpriteMaterial();

            _halo = GetChildRenderer("PulseBallHalo", -2);
            _pulse = GetChildRenderer("PulseBallWave", -3);
            _motifs ??= new SpriteRenderer[4];
            for (int i = 0; i < _motifs.Length; i++)
                _motifs[i] = GetChildRenderer("PulseBallMotif_" + i, 1);

            if (_trail == null)
            {
                _trail = gameObject.GetComponent<TrailRenderer>();
                if (_trail == null)
                    _trail = gameObject.AddComponent<TrailRenderer>();
                _trail.time = 0.22f;
                _trail.minVertexDistance = 0.03f;
                _trail.startWidth = 0.20f;
                _trail.endWidth = 0.015f;
                _trail.sortingOrder = -4;
                _trail.material = CreateSpriteMaterial();
            }

            if (_lightningArc == null)
            {
                GameObject arc = new GameObject("PulseBallLightningArc");
                arc.transform.SetParent(transform, false);
                _lightningArc = arc.AddComponent<LineRenderer>();
                _lightningArc.useWorldSpace = false;
                _lightningArc.positionCount = 6;
                _lightningArc.widthMultiplier = 0.035f;
                _lightningArc.sortingOrder = 2;
                _lightningArc.material = CreateSpriteMaterial();
            }
        }

        private void ApplyElement()
        {
            (_core, _edge, _accent) = _element switch
            {
                DamageChannel.Cold => (
                    new Color(0.84f, 0.98f, 1f, 1f),
                    new Color(0.10f, 0.56f, 1f, 1f),
                    new Color(0.36f, 0.85f, 1f, 1f)),
                DamageChannel.Lightning => (
                    new Color(1f, 0.93f, 0.22f, 1f),
                    new Color(0.18f, 0.04f, 0.46f, 1f),
                    new Color(0.68f, 0.34f, 1f, 1f)),
                _ => (
                    new Color(1f, 0.94f, 0.46f, 1f),
                    new Color(1f, 0.18f, 0.04f, 1f),
                    new Color(1f, 0.48f, 0.05f, 1f))
            };

            _authoredFrames = GetAuthoredFrames(_element);
            _usesAuthoredFrames = _authoredFrames != null && _authoredFrames.Length > 0;
            Sprite sprite = _usesAuthoredFrames ? _authoredFrames[0] : GetElementSprite(_element, _core, _edge);
            _main.sprite = sprite;
            _main.color = Color.white;
            _main.sortingOrder = 0;
            _halo.sprite = GetPulseRingSprite();
            _pulse.sprite = GetPulseRingSprite();
            _halo.color = WithAlpha(_accent, 0.14f);
            _pulse.color = WithAlpha(_accent, 0f);

            for (int i = 0; i < _motifs.Length; i++)
            {
                _motifs[i].sprite = sprite;
                _motifs[i].color = WithAlpha(_accent, _element == DamageChannel.Lightning ? 0.72f : 0.50f);
                _motifs[i].transform.localScale = Vector3.one * 0.13f;
                _motifs[i].enabled = !_usesAuthoredFrames && _element != DamageChannel.Lightning;
            }

            _trail.colorGradient = CreateGradient(WithAlpha(_accent, 0.72f), WithAlpha(_edge, 0f));
            _trail.emitting = !_usesAuthoredFrames;
            _trail.enabled = !_usesAuthoredFrames;
            _lightningArc.enabled = _element == DamageChannel.Lightning && !_usesAuthoredFrames;
            if (_element == DamageChannel.Lightning)
                UpdateLightningArc();
        }

        private void UpdateMotifs(float phase, float wave)
        {
            if (_motifs == null)
                return;

            for (int i = 0; i < _motifs.Length; i++)
            {
                if (_motifs[i] == null)
                    continue;

                float angle = _age * (_element == DamageChannel.Cold ? 1.8f : 3.5f) + i * Mathf.PI * 0.5f;
                float radius = _element == DamageChannel.Fire
                    ? 0.78f + 0.14f * Mathf.Sin(_age * 6f + i)
                    : _element == DamageChannel.Cold ? 0.72f : 0.60f;
                _motifs[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                _motifs[i].transform.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.17f, wave);
                _motifs[i].enabled = !_usesAuthoredFrames && _element != DamageChannel.Lightning;
            }
        }

        private void UpdateLightningArc()
        {
            if (_lightningArc == null)
                return;

            float angle = _age * 5.5f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            _lightningArc.SetPosition(0, Vector3.zero);
            for (int i = 1; i < 6; i++)
            {
                float t = i / 5f;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                float zigzag = Mathf.Sin((_age * 37f + i * 3.7f)) * (1f - t) * 0.16f;
                Vector2 point = direction * (t * 1.05f) + perpendicular * zigzag;
                _lightningArc.SetPosition(i, point);
            }
            _lightningArc.startColor = WithAlpha(_core, 0.95f);
            _lightningArc.endColor = WithAlpha(_edge, 0f);
        }

        private SpriteRenderer GetChildRenderer(string childName, int sortingOrder)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
            }
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplyAuthoredFrame()
        {
            if (_authoredFrames == null || _authoredFrames.Length == 0 || _main == null)
                return;

            int index = Mathf.FloorToInt(_age / AuthoredFrameSeconds) % _authoredFrames.Length;
            if (_main.sprite != _authoredFrames[index])
                _main.sprite = _authoredFrames[index];
        }

        private static Sprite[] GetAuthoredFrames(DamageChannel element)
        {
            if (element == DamageChannel.Fire)
                return LoadFrames(FireAnimationResourcePath, ref _fireFrames);
            if (element == DamageChannel.Cold)
                return LoadFrames(ColdAnimationResourcePath, ref _coldFrames);
            if (element == DamageChannel.Lightning)
                return LoadFrames(LightningAnimationResourcePath, ref _lightningFrames);
            return null;
        }

        private static Sprite[] LoadFrames(string resourcePath, ref Sprite[] cache)
        {
            if (cache != null && cache.Length > 0)
                return cache;

            Sprite[] loaded = Resources.LoadAll<Sprite>(resourcePath);
            if (loaded == null || loaded.Length == 0)
                return null;

            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            cache = loaded;
            return cache;
        }

        private static Sprite GetElementSprite(DamageChannel element, Color core, Color edge)
        {
            EnsureCurrentSpriteCache();
            if (SpriteCache.TryGetValue(element, out Sprite sprite) && sprite != null)
                return sprite;

            var texture = new Texture2D(SpriteSizePixels, SpriteSizePixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            float center = (SpriteSizePixels - 1) * 0.5f;
            const float radius = 20.5f;
            const float rimWidth = 2f;
            for (int y = 0; y < SpriteSizePixels; y++)
            for (int x = 0; x < SpriteSizePixels; x++)
            {
                Vector2 offset = new Vector2(x - center, y - center);
                float distance = offset.magnitude;
                if (distance > radius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                if (distance >= radius - rimWidth)
                {
                    texture.SetPixel(x, y, Color.Lerp(edge, Color.black, 0.38f));
                    continue;
                }

                Vector2 lightDirection = new Vector2(-0.58f, 0.72f);
                float light = Mathf.Clamp01(Vector2.Dot(-offset.normalized, lightDirection));
                float edgeT = Mathf.Pow(Mathf.Clamp01(distance / (radius - rimWidth)), 1.45f);
                Color color = Color.Lerp(core, edge, edgeT);
                color = Color.Lerp(color, Color.white, light * light * 0.42f);
                texture.SetPixel(x, y, color);
            }
            texture.Apply(false, false);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, SpriteSizePixels, SpriteSizePixels), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = "Big" + element + "Ball_48px";
            SpriteCache[element] = sprite;
            return sprite;
        }

        private static Sprite GetPulseRingSprite()
        {
            EnsureCurrentSpriteCache();
            if (_pulseRingSprite != null)
                return _pulseRingSprite;

            var texture = new Texture2D(SpriteSizePixels, SpriteSizePixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float center = (SpriteSizePixels - 1) * 0.5f;
            const float radius = 20.5f;
            const float ringWidth = 1.15f;
            for (int y = 0; y < SpriteSizePixels; y++)
            for (int x = 0; x < SpriteSizePixels; x++)
            {
                float distance = new Vector2(x - center, y - center).magnitude;
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) / ringWidth);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            texture.Apply(false, false);
            _pulseRingSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, SpriteSizePixels, SpriteSizePixels),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            _pulseRingSprite.name = "BigElementalBallPulseRing_48px";
            return _pulseRingSprite;
        }

        private static void EnsureCurrentSpriteCache()
        {
            if (_spriteCacheVersion == GeneratedSpriteVersion)
                return;

            SpriteCache.Clear();
            _pulseRingSprite = null;
            _spriteCacheVersion = GeneratedSpriteVersion;
        }

        private static Material CreateSpriteMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            return shader != null ? new Material(shader) { hideFlags = HideFlags.HideAndDontSave } : null;
        }

        private static Gradient CreateGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
