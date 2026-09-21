using UnityEngine;
using UnityEngine.Sprites;

namespace Scripts.Visuals.SpriteFX
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Relic Keeper/Visuals/Sprite FX Controller")]
    public sealed class SpriteFxController : MonoBehaviour
    {
        private static readonly int UvRect = Shader.PropertyToID("_FxUvRect");
        private static readonly int Neighbours = Shader.PropertyToID("_FxNeighboursAllowed");
        private static readonly int TintId = Shader.PropertyToID("_FxRuntimeTint");
        private static readonly int FlashId = Shader.PropertyToID("_FxRuntimeFlash");
        private static readonly int FlashColorId = Shader.PropertyToID("_FxRuntimeFlashColor");
        private static readonly int EmissionId = Shader.PropertyToID("_FxRuntimeEmission");
        private static readonly int EmissionColorId = Shader.PropertyToID("_FxRuntimeEmissionColor");
        private static readonly int DissolveId = Shader.PropertyToID("_FxRuntimeDissolve");
        private static readonly int FadeId = Shader.PropertyToID("_FxRuntimeFade");

        private SpriteRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Sprite _lastSprite;
        private Texture _lastTexture;
        private Vector4 _uvRect = new Vector4(0f, 0f, 1f, 1f);
        private bool _neighbours;
        private Color _tint = Color.white, _flashColor = Color.white, _emissionColor = Color.white;
        private float _flash, _flashDuration, _flashRemaining, _tintRemaining, _emission, _dissolve, _fade = 1f;
        private float _dissolveStart, _dissolveTarget, _dissolveDuration, _dissolveElapsed;
        private bool _dissolving;

        public float DissolveAmount => _dissolve;
        public float Fade => _fade;

        private void OnEnable()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _block ??= new MaterialPropertyBlock();
            RefreshSprite();
            Apply();
        }

        private void LateUpdate()
        {
            bool changed = RefreshSprite();
            if (Application.isPlaying)
            {
                float dt = Time.deltaTime;
                if (_flashRemaining > 0f)
                {
                    _flashRemaining = Mathf.Max(0f, _flashRemaining - dt);
                    _flash = _flashRemaining / _flashDuration;
                    changed = true;
                }
                if (_tintRemaining > 0f)
                {
                    _tintRemaining = Mathf.Max(0f, _tintRemaining - dt);
                    if (_tintRemaining == 0f) _tint = Color.white;
                    changed = true;
                }
                if (_dissolving)
                {
                    _dissolveElapsed += dt;
                    float t = Mathf.Clamp01(_dissolveElapsed / _dissolveDuration);
                    _dissolve = Mathf.Lerp(_dissolveStart, _dissolveTarget, t);
                    _dissolving = t < 1f;
                    changed = true;
                }
            }
            if (changed) Apply();
        }

        public void Flash(Color color, float duration = 0.12f)
        {
            _flashColor = color;
            _flashDuration = Mathf.Max(0.0001f, duration);
            _flashRemaining = duration > 0f ? duration : 0f;
            _flash = duration > 0f ? 1f : 0f;
            Apply();
        }

        public void TemporaryTint(Color color, float duration)
        {
            _tintRemaining = Mathf.Max(0f, duration);
            _tint = duration > 0f ? color : Color.white;
            Apply();
        }

        public void SetEmission(Color color, float strength)
        {
            _emissionColor = color;
            _emission = Mathf.Max(0f, strength);
            Apply();
        }

        public void SetDissolve(float amount)
        {
            _dissolving = false;
            _dissolve = Mathf.Clamp01(amount);
            Apply();
        }

        public void DissolveTo(float target, float duration)
        {
            if (duration <= 0f) { SetDissolve(target); return; }
            _dissolveStart = _dissolve;
            _dissolveTarget = Mathf.Clamp01(target);
            _dissolveDuration = duration;
            _dissolveElapsed = 0f;
            _dissolving = true;
        }

        public void SetFade(float alpha) { _fade = Mathf.Clamp01(alpha); Apply(); }

        public void ResetEffects()
        {
            _tint = Color.white;
            _flash = _flashRemaining = _tintRemaining = _emission = _dissolve = 0f;
            _fade = 1f;
            _dissolving = false;
            Apply();
        }

        private void OnDisable() => ResetEffects();

        private bool RefreshSprite()
        {
            if (_renderer == null) return false;
            Sprite sprite = _renderer.sprite;
            Texture texture = sprite != null ? sprite.texture : null;
            if (sprite == _lastSprite && texture == _lastTexture) return false;
            _lastSprite = sprite;
            _lastTexture = texture;
            _uvRect = sprite != null ? DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
            // Tight atlas packing may interleave polygons within UV bounding rectangles.
            _neighbours = sprite != null && (!sprite.packed || sprite.packingMode == SpritePackingMode.Rectangle);
            return true;
        }

        private void Apply()
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_block); // Preserve AutoDestroyVFX and other owners' properties.
            _block.SetVector(UvRect, _uvRect);
            _block.SetFloat(Neighbours, _neighbours ? 1f : 0f);
            _block.SetColor(TintId, _tint);
            _block.SetColor(FlashColorId, _flashColor);
            _block.SetFloat(FlashId, _flash);
            _block.SetColor(EmissionColorId, _emissionColor);
            _block.SetFloat(EmissionId, _emission);
            _block.SetFloat(DissolveId, _dissolve);
            _block.SetFloat(FadeId, _fade);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
