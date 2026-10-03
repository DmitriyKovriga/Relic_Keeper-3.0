using System.Collections.Generic;
using Scripts.Visuals;
using Scripts.Visuals.SpriteFX;
using UnityEngine;

namespace Scripts.StatusEffects
{
    /// <summary>
    /// A barely-there tint inside the player sprite and a wide, transparent halo outside it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    public sealed class StatusAuraPresenter : MonoBehaviour
    {
        private const float InnerEmission = 0.03f;
        private const string HaloShaderName = "RelicKeeper/Sprites/Aura Halo";

        private static readonly Vector2[] Directions =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(0.7071f, -0.7071f),
            new Vector2(-0.7071f, 0.7071f), new Vector2(-0.7071f, -0.7071f)
        };

        private static readonly float[] RingDistance = { 1f, 2f, 3f, 4f };
        private static readonly float[] RingAlpha = { 0.1f, 0.065f, 0.04f, 0.022f };

        private StatusEffectController _controller;
        private StatusEffectsHudSettingsSO _settings;
        private Material _auraMaterial;
        private Material _haloMaterial;
        private Material _originalBodyMaterial;
        private SpriteRenderer _body;
        private bool _bodyMaterialSwapped;
        private Transform _outlineRoot;
        private readonly List<SpriteRenderer> _outlines = new List<SpriteRenderer>(12);
        private readonly Dictionary<StatusEffectController.ActiveEffectInstance, GameObject> _spawnedVfx =
            new Dictionary<StatusEffectController.ActiveEffectInstance, GameObject>();
        private readonly List<StatusEffectController.ActiveEffectInstance> _retired = new List<StatusEffectController.ActiveEffectInstance>();
        private StatusAuraColor _color = StatusAuraColor.None;

        private void Awake()
        {
            _controller = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            if (_controller == null)
                _controller = GetComponent<StatusEffectController>();

            if (_settings == null)
                _settings = Resources.Load<StatusEffectsHudSettingsSO>(ProjectPaths.ResourcesStatusEffectsHudSettings);

            if (_controller != null)
                _controller.OnActiveEffectsChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnActiveEffectsChanged -= Refresh;

            ClearVfx();
            ApplyGlow(StatusAuraColor.None);
            if (_auraMaterial != null)
            {
                Destroy(_auraMaterial);
                _auraMaterial = null;
            }

            if (_haloMaterial != null)
            {
                Destroy(_haloMaterial);
                _haloMaterial = null;
            }
        }

        private void LateUpdate()
        {
            if (_color == StatusAuraColor.None)
                return;

            PlaceOutlines();
        }

        private void Refresh()
        {
            ApplyGlow(ResolveAuraColor());
            SyncVfx();
        }

        private StatusAuraColor ResolveAuraColor()
        {
            if (_controller == null || _controller.ActiveEffects == null)
                return StatusAuraColor.None;

            for (int i = _controller.ActiveEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectController.ActiveEffectInstance effect = _controller.ActiveEffects[i];
                if (effect != null && effect.AuraColor != StatusAuraColor.None)
                    return effect.AuraColor;
            }

            return StatusAuraColor.None;
        }

        private void ApplyGlow(StatusAuraColor color)
        {
            _color = color;
            bool active = color != StatusAuraColor.None;
            SpriteRenderer body = BodyRenderer();
            if (body != null)
            {
                if (active)
                {
                    Material aura = EnsureAuraMaterial();
                    if (aura != null && body.sharedMaterial != aura)
                    {
                        if (!_bodyMaterialSwapped)
                        {
                            _originalBodyMaterial = body.sharedMaterial;
                            _bodyMaterialSwapped = true;
                            _body = body;
                        }

                        body.sharedMaterial = aura;
                    }

                    SpriteFxController fx = body.GetComponent<SpriteFxController>();
                    if (fx == null)
                        fx = body.gameObject.AddComponent<SpriteFxController>();
                    fx.SetEmission(StatusEffectPresentation.AuraTint(color), InnerEmission);
                }
                else if (_bodyMaterialSwapped && _body != null)
                {
                    SpriteFxController fx = _body.GetComponent<SpriteFxController>();
                    if (fx != null)
                        fx.SetEmission(Color.white, 0f);
                    _body.sharedMaterial = _originalBodyMaterial;
                    _bodyMaterialSwapped = false;
                    _body = null;
                }
            }

            if (active)
            {
                EnsureOutlines();
                Color tint = StatusEffectPresentation.AuraTint(color);
                for (int i = 0; i < _outlines.Count; i++)
                {
                    if (_outlines[i] == null)
                        continue;

                    int ring = i / Directions.Length;
                    float alpha = ring < RingAlpha.Length ? RingAlpha[ring] : RingAlpha[RingAlpha.Length - 1];
                    _outlines[i].color = new Color(tint.r, tint.g, tint.b, alpha);
                    _outlines[i].enabled = true;
                }

                PlaceOutlines();
                return;
            }

            for (int i = 0; i < _outlines.Count; i++)
            {
                if (_outlines[i] != null)
                    _outlines[i].enabled = false;
            }
        }

        private SpriteRenderer BodyRenderer()
        {
            return GetComponent<SpriteRenderer>();
        }

        private SpriteRenderer VisibleRenderer()
        {
            PlayerMovementVisual movement = GetComponent<PlayerMovementVisual>();
            if (movement != null && movement.DisplayRenderer != null)
                return movement.DisplayRenderer;

            return GetComponent<SpriteRenderer>();
        }

        private Material EnsureAuraMaterial()
        {
            if (_auraMaterial != null)
                return _auraMaterial;

            Material source = _settings != null ? _settings.AuraSpriteMaterial : null;
            if (source == null)
                return null;

            _auraMaterial = new Material(source);
            _auraMaterial.SetFloat("_FxFlash", 0f);
            _auraMaterial.SetColor("_FxFlashColor", Color.white);
            _auraMaterial.SetFloat("_FxEmission", 0f);
            _auraMaterial.SetFloat("_FxOutline", 0f);
            _auraMaterial.SetFloat("_FxInnerShadow", 0f);
            return _auraMaterial;
        }

        private Material EnsureHaloMaterial()
        {
            if (_haloMaterial != null)
                return _haloMaterial;

            Shader shader = Shader.Find(HaloShaderName);
            if (shader == null)
                return EnsureAuraMaterial();

            _haloMaterial = new Material(shader);
            return _haloMaterial;
        }

        private void EnsureOutlines()
        {
            if (_outlineRoot != null)
                return;

            Transform leftover = transform.Find("StatusAuraOutline");
            if (leftover != null)
                Destroy(leftover.gameObject);

            var rootObject = new GameObject("StatusAuraOutline");
            rootObject.transform.SetParent(transform, false);
            _outlineRoot = rootObject.transform;
            Material halo = EnsureHaloMaterial();

            for (int ring = 0; ring < RingDistance.Length; ring++)
            {
                for (int direction = 0; direction < Directions.Length; direction++)
                {
                    var piece = new GameObject("Rim");
                    piece.transform.SetParent(_outlineRoot, false);
                    SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
                    renderer.sharedMaterial = halo;
                    renderer.enabled = false;
                    _outlines.Add(renderer);
                }
            }
        }

        private void PlaceOutlines()
        {
            if (_outlines.Count == 0)
                return;

            SpriteRenderer visible = VisibleRenderer();
            bool show = visible != null && visible.enabled && visible.sprite != null;
            float pixel = show ? 1f / Mathf.Max(1f, visible.sprite.pixelsPerUnit) : 0f;
            Vector3 scale = show ? visible.transform.localScale : Vector3.one;

            for (int index = 0; index < _outlines.Count; index++)
            {
                SpriteRenderer rim = _outlines[index];
                if (rim == null)
                    continue;

                rim.enabled = show;
                if (!show)
                    continue;

                int ring = index / Directions.Length;
                int direction = index % Directions.Length;
                if (ring >= RingDistance.Length)
                    continue;

                Transform rimTransform = rim.transform;
                Vector2 offset = Directions[direction] * (RingDistance[ring] * pixel);
                rimTransform.localPosition = visible.transform.localPosition + new Vector3(offset.x * scale.x, offset.y * scale.y, 0f);
                rimTransform.localRotation = visible.transform.localRotation;
                rimTransform.localScale = scale;
                rim.sprite = visible.sprite;
                rim.flipX = visible.flipX;
                rim.flipY = visible.flipY;
                rim.sortingLayerID = visible.sortingLayerID;
                rim.sortingOrder = visible.sortingOrder - (RingDistance.Length - ring);
            }
        }

        private void SyncVfx()
        {
            _retired.Clear();
            foreach (KeyValuePair<StatusEffectController.ActiveEffectInstance, GameObject> pair in _spawnedVfx)
            {
                if (pair.Key == null || pair.Key.AuraVfxPrefab == null || !IsActive(pair.Key))
                    _retired.Add(pair.Key);
            }

            for (int i = 0; i < _retired.Count; i++)
                ReleaseVfx(_retired[i]);

            if (_controller == null || _controller.ActiveEffects == null)
                return;

            for (int i = 0; i < _controller.ActiveEffects.Count; i++)
            {
                StatusEffectController.ActiveEffectInstance effect = _controller.ActiveEffects[i];
                if (effect == null || effect.AuraVfxPrefab == null)
                    continue;

                if (_spawnedVfx.TryGetValue(effect, out GameObject existing) && existing != null)
                    continue;

                GameObject spawned = Instantiate(effect.AuraVfxPrefab, transform);
                spawned.transform.localPosition = Vector3.zero;
                spawned.transform.localRotation = Quaternion.identity;
                _spawnedVfx[effect] = spawned;
            }
        }

        private bool IsActive(StatusEffectController.ActiveEffectInstance effect)
        {
            if (_controller == null || _controller.ActiveEffects == null)
                return false;

            for (int i = 0; i < _controller.ActiveEffects.Count; i++)
            {
                if (_controller.ActiveEffects[i] == effect)
                    return true;
            }

            return false;
        }

        private void ReleaseVfx(StatusEffectController.ActiveEffectInstance effect)
        {
            if (effect != null && _spawnedVfx.TryGetValue(effect, out GameObject spawned) && spawned != null)
                Destroy(spawned);

            if (effect != null)
                _spawnedVfx.Remove(effect);
        }

        private void ClearVfx()
        {
            foreach (KeyValuePair<StatusEffectController.ActiveEffectInstance, GameObject> pair in _spawnedVfx)
            {
                if (pair.Value != null)
                    Destroy(pair.Value);
            }

            _spawnedVfx.Clear();
        }
    }
}
