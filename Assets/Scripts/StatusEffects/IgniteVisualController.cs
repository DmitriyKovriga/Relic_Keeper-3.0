using System.Collections.Generic;
using Scripts.Enemies;
using Scripts.Visuals.Atmosphere;
using UnityEngine;

namespace Scripts.StatusEffects
{
    /// <summary>Source-owned fire. Never replaces the actor's material, tint or hit-flash properties.</summary>
    [DisallowMultipleComponent]
    public sealed class IgniteVisualController : MonoBehaviour
    {
        [Header("Burning appearance")]
        [SerializeField, Range(0f, 2f)] private float _strength = .8f;
        [SerializeField, Range(.1f, 1f)] private float _flameHeight = .5f;
        [Header("Nearby illumination")]
        [SerializeField, Range(0f, .5f)] private float _lightStrength = .1f;
        [SerializeField, Range(.2f, 4f)] private float _lightRadius = 1.8f;
        [SerializeField, Range(0f, .5f)] private float _glowStrength = .1f;
        [SerializeField, Range(0f, .75f)] private float _lightBreath = .45f;
        [SerializeField, Range(.1f, 3f)] private float _breathSpeed = 1;
        private static Sprite _quad;
        private static readonly int RectId = Shader.PropertyToID("_BurnRect");
        private static readonly int PixelsId = Shader.PropertyToID("_BurnPixels");
        private static readonly int ModeId = Shader.PropertyToID("_BurnMode");
        private static readonly int TimeId = Shader.PropertyToID("_BurnTime");
        private static readonly int StrengthId = Shader.PropertyToID("_BurnStrength");
        private static readonly int HeightId = Shader.PropertyToID("_BurnHeight");
        private static readonly int MaskTexId = Shader.PropertyToID("_BurnMaskTex");
        private static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");
        private static readonly int MaskXId = Shader.PropertyToID("_MaskUVX");
        private static readonly int MaskYId = Shader.PropertyToID("_MaskUVY");
        private static readonly int AreaId = Shader.PropertyToID("_BurnArea");
        private readonly Dictionary<Sprite, (Vector4 x, Vector4 y)> _maskCoordinates = new Dictionary<Sprite, (Vector4, Vector4)>();
        private MaterialPropertyBlock _properties;
        private SpriteRenderer _body, _heat, _flame, _frontFire;
        private Transform _root;
        private LocalLight2DSource _light;
        private EnemyHealth _health;
        private bool _burning;
        private float _phase;
        private float _configuredLightStrength = -1, _configuredLightRadius = -1;
        private Sprite _currentMaskSprite, _previousMaskSprite;
        private float _maskChangedAt;
        public bool IsBurning => _burning;

        private void OnDisable() => SetBurning(false);

        public void SetBurning(bool burning)
        {
            if (burning)
            {
                if (_health == null) _health = GetComponent<EnemyHealth>();
                if (_health != null && _health.IsDead) burning = false;
            }
            if (_burning == burning) return;
            _burning = burning;
            if (!burning)
            {
                if (_root != null) _root.gameObject.SetActive(false);
                return;
            }
            _health = GetComponent<EnemyHealth>();
            _currentMaskSprite = _previousMaskSprite = null;
            if (_body == null) _body = FindBody();
            EnsureVisuals();
            if (_root != null) _root.gameObject.SetActive(true);
            enabled = true;
            LateUpdate();
        }

        private SpriteRenderer FindBody()
        {
            var entity = GetComponent<EnemyEntity>();
            if (entity != null && entity.VisualRenderer != null) return entity.VisualRenderer;
            SpriteRenderer best = null;
            float size = -1;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.GetComponentInParent<Canvas>() != null || renderer.name.Contains("Shadow") ||
                    renderer.GetComponentInParent<LocalLight2DSource>() != null ||
                    (renderer.transform.parent != null && renderer.transform.parent.name == "IgniteFlames")) continue;
                float candidate = renderer.sprite != null ? renderer.bounds.size.sqrMagnitude : 0;
                if (candidate > size) { best = renderer; size = candidate; }
            }
            return best;
        }

        private void EnsureVisuals()
        {
            if (_root != null) return;
            var material = Resources.Load<Material>("Visuals/IgniteBurn");
            if (material == null || material.shader == null || !material.shader.isSupported) return;
            var host = new GameObject("IgniteFlames");
            _root = host.transform; _root.SetParent(_body != null ? _body.transform : transform, false);
            host.layer = gameObject.layer;
            _phase = Mathf.Repeat(transform.position.x * 7.13f + GetInstanceID() * .017f, 97f);
            _heat = CreateRenderer("Ignite body heat", material);
            _flame = CreateRenderer("Ignite rising fire", material);
            _frontFire = CreateRenderer("Ignite foreground tongues", material);
            if (_quad == null)
            {
                _quad = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, 0), 1);
                _quad.name = "Ignite shader quad"; _quad.hideFlags = HideFlags.HideAndDontSave;
            }
            _flame.sprite = _quad;
            _frontFire.sprite = _quad;
            var lightHost = new GameObject("Ignite light"); lightHost.layer = gameObject.layer;
            lightHost.transform.SetParent(_root, false);
            _light = lightHost.AddComponent<LocalLight2DSource>();
            _light.ConfigureFlame(_phase); _light.ConfigureTint(new Color(1f, .42f, .09f));
            host.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Characters, 1, true);
        }

        private SpriteRenderer CreateRenderer(string name, Material material)
        {
            var host = new GameObject(name); host.layer = gameObject.layer; host.transform.SetParent(_root, false);
            var renderer = host.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
            return renderer;
        }

        private void LateUpdate()
        {
            if (!_burning) return;
            if (_health != null && _health.IsDead) { SetBurning(false); return; }
            if (_root == null) return;
            bool visible = _body != null && _body.enabled && _body.gameObject.activeInHierarchy && _body.sprite != null;
            _heat.enabled = _flame.enabled = _frontFire.enabled = visible;
            _light.gameObject.SetActive(visible);
            if (!visible) return;
            bool reduced = GameplayPresentationSettings.ReducedCombatEffects;
            var sprite = _body.sprite; var bounds = sprite.bounds;
            _heat.sprite = sprite; _heat.flipX = _body.flipX; _heat.flipY = _body.flipY;
            _heat.color = _flame.color = _frontFire.color = new Color(1, 1, 1, _body.color.a);
            _heat.sortingLayerID = _flame.sortingLayerID = _frontFire.sortingLayerID = _body.sortingLayerID;
            // The opaque body occludes the large fire. Only small heat ribbons sit in front.
            _heat.sortingOrder = _body.sortingOrder + 1; _flame.sortingOrder = _body.sortingOrder - 1;
            _frontFire.sortingOrder = _body.sortingOrder + 2;
            _heat.maskInteraction = _flame.maskInteraction = _frontFire.maskInteraction = _body.maskInteraction;
            _heat.spriteSortPoint = _body.spriteSortPoint;
            SetProperties(_heat, 0, bounds, sprite.rect.size, _strength * (reduced ? .6f : 1));
            float padding = Mathf.Max(2f / sprite.pixelsPerUnit, bounds.size.x * .12f) * (reduced ? .65f : 1);
            float bottom = bounds.min.y + bounds.size.y * .08f;
            float height = bounds.size.y * (1 + _flameHeight * (reduced ? .5f : 1) - .08f);
            float width = Mathf.Max(.15f, bounds.size.x + padding * 2);
            _flame.transform.localPosition = new Vector3(_body.flipX ? -bounds.center.x : bounds.center.x,
                _body.flipY ? -bounds.max.y + bounds.size.y * .08f : bottom, 0);
            _flame.transform.localScale = new Vector3(width, height, 1);
            _flame.flipX = false;
            SetProperties(_flame, 1, _quad.bounds, new Vector2(width, height) * sprite.pixelsPerUnit,
                _strength * (reduced ? .6f : .95f));
            SetSilhouette(sprite, bounds, new Vector4(bounds.min.x - padding, bottom, width, height));
            _frontFire.transform.localPosition = _flame.transform.localPosition;
            _frontFire.transform.localScale = _flame.transform.localScale;
            _properties.SetFloat(ModeId, 2);
            _properties.SetFloat(StrengthId, _strength * (reduced ? .2f : .42f));
            _frontFire.SetPropertyBlock(_properties);
            _light.transform.localPosition = new Vector3(_flame.transform.localPosition.x,
                _body.flipY ? -bounds.center.y : bounds.center.y, 0);
            float lightStrength = _lightStrength * (reduced ? .55f : 1) * _body.color.a;
            _light.ConfigureBreathing(_lightBreath * (reduced ? .5f : 1), _breathSpeed);
            if (_configuredLightStrength != lightStrength || _configuredLightRadius != _lightRadius)
            {
                _light.Configure(lightStrength, _lightRadius);
                _configuredLightStrength = lightStrength; _configuredLightRadius = _lightRadius;
            }
            _light.ConfigureGlow(new SourceGlowSettings { enabled = true, strength = _glowStrength * (reduced ? .5f : 1),
                radius = Mathf.Clamp(_body.bounds.extents.magnitude * .9f, .3f, 1.2f), softness = 1.3f, color = Color.white });
        }

        private void SetProperties(SpriteRenderer renderer, float mode, Bounds bounds, Vector2 pixels, float strength)
        {
            if (_properties == null) _properties = new MaterialPropertyBlock();
            _properties.Clear();
            _properties.SetVector(RectId, new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            _properties.SetVector(PixelsId, new Vector4(Mathf.Max(1, pixels.x), Mathf.Max(1, pixels.y), 0, 0));
            _properties.SetFloat(ModeId, mode); _properties.SetFloat(TimeId, Time.time + _phase);
            _properties.SetFloat(StrengthId, strength);
            _properties.SetFloat(HeightId, _flameHeight * 2 * (GameplayPresentationSettings.ReducedCombatEffects ? .65f : 1));
            renderer.SetPropertyBlock(_properties);
        }

        private void SetSilhouette(Sprite sprite, Bounds bounds, Vector4 area)
        {
            if (!_maskCoordinates.TryGetValue(sprite, out var coordinates))
            {
                // Derive the actual sprite-to-atlas mapping. Outer UV rectangles alone
                // lose rotated packing and assume a full-rectangle sprite mesh.
                var vertices = sprite.vertices; var uv = sprite.uv;
                Vector4 rowX = Vector4.zero, rowY = Vector4.zero;
                for (int i = 2; i < vertices.Length; i++)
                {
                    var p = vertices[1] - vertices[0]; var q = vertices[i] - vertices[0];
                    float determinant = p.x * q.y - p.y * q.x;
                    if (Mathf.Abs(determinant) < .000001f) continue;
                    var u = uv[1] - uv[0]; var v = uv[i] - uv[0];
                    rowX = new Vector4((u.x * q.y - v.x * p.y) / determinant, (p.x * v.x - q.x * u.x) / determinant, 0, 0);
                    rowY = new Vector4((u.y * q.y - v.y * p.y) / determinant, (p.x * v.y - q.x * u.y) / determinant, 0, 0);
                    rowX.z = uv[0].x - rowX.x * vertices[0].x - rowX.y * vertices[0].y;
                    rowY.z = uv[0].y - rowY.x * vertices[0].x - rowY.y * vertices[0].y;
                    break;
                }
                coordinates = (rowX, rowY); _maskCoordinates.Add(sprite, coordinates);
            }
            _properties.SetTexture(MaskTexId, sprite.texture);
            _properties.SetVector(MaskRectId, new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            _properties.SetVector(MaskXId, coordinates.x); _properties.SetVector(MaskYId, coordinates.y);
            if (_currentMaskSprite != sprite)
            {
                _previousMaskSprite = _currentMaskSprite != null ? _currentMaskSprite : sprite;
                _currentMaskSprite = sprite; _maskChangedAt = Time.time;
            }
            if (_previousMaskSprite == null) _previousMaskSprite = sprite;
            var previousBounds = _previousMaskSprite.bounds;
            var previousUV = _maskCoordinates[_previousMaskSprite];
            _properties.SetTexture("_PreviousMaskTex", _previousMaskSprite.texture);
            _properties.SetVector("_PreviousMaskRect", new Vector4(previousBounds.min.x,previousBounds.min.y,previousBounds.size.x,previousBounds.size.y));
            _properties.SetVector("_PreviousMaskUVX", previousUV.x); _properties.SetVector("_PreviousMaskUVY", previousUV.y);
            _properties.SetFloat("_MaskBlend", _previousMaskSprite == sprite ? 1 : Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time - _maskChangedAt) / .08f)));
            // Mirror the mask explicitly, rather than mirroring a quad about its
            // pivot. Asymmetric attack/weapon frames must remain centered on the actor.
            if (_body.flipX) { area.x += area.z; area.z = -area.z; }
            if (_body.flipY) { area.y = bounds.max.y - (area.y - bounds.min.y); area.w = -area.w; }
            _properties.SetVector(AreaId, area);
            _flame.SetPropertyBlock(_properties);
        }
    }
}
