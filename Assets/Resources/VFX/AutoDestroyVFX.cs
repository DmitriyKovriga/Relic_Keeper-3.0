using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");
    private const float DefaultFadeStartAlphaMultiplier = 0.5f;

    private float _duration;
    private float _timer;
    private bool _fadeOutEnabled;
    private float _fadeOutStartLifePercent;
    private float _fadeStartAlphaMultiplier;
    private SpriteRenderer[] _renderers;
    private Color[] _startColors;
    private MaterialPropertyBlock _propertyBlock;
    private bool _initialized;

    public static AutoDestroyVFX Ensure(GameObject target)
    {
        if (target == null)
            return null;

        var autoDestroy = target.GetComponent<AutoDestroyVFX>();
        if (autoDestroy == null)
            autoDestroy = target.AddComponent<AutoDestroyVFX>();

        return autoDestroy;
    }

    public void Initialize(
        float duration,
        bool fadeOutEnabled = true,
        float fadeOutStartLifePercent = 0.5f,
        float fadeStartAlphaMultiplier = DefaultFadeStartAlphaMultiplier)
    {
        _duration = Mathf.Max(0.0001f, duration);
        _timer = 0f;
        _fadeOutEnabled = fadeOutEnabled;
        _fadeOutStartLifePercent = Mathf.Clamp01(fadeOutStartLifePercent);
        _fadeStartAlphaMultiplier = Mathf.Clamp01(fadeStartAlphaMultiplier);
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _startColors = new Color[_renderers.Length];
        _propertyBlock ??= new MaterialPropertyBlock();

        for (int i = 0; i < _renderers.Length; i++)
        {
            _startColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
        }

        ApplyAlphaMultiplier(1f);
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized)
            return;

        _timer += Time.deltaTime;

        if (_fadeOutEnabled && _fadeOutStartLifePercent < 1f)
        {
            float lifeProgress = Mathf.Clamp01(_timer / _duration);
            float alphaMultiplier = 1f;

            if (lifeProgress >= _fadeOutStartLifePercent)
            {
                float fadeProgress = Mathf.InverseLerp(_fadeOutStartLifePercent, 1f, lifeProgress);
                alphaMultiplier = Mathf.Lerp(_fadeStartAlphaMultiplier, 0f, Mathf.SmoothStep(0f, 1f, fadeProgress));
            }

            ApplyAlphaMultiplier(alphaMultiplier);
        }

        if (_timer >= _duration)
            Destroy(gameObject);
    }

    private void ApplyAlphaMultiplier(float alphaMultiplier)
    {
        if (_renderers == null || _startColors == null)
            return;

        alphaMultiplier = Mathf.Clamp01(alphaMultiplier);

        for (int i = 0; i < _renderers.Length; i++)
        {
            SpriteRenderer renderer = _renderers[i];
            if (renderer == null)
                continue;

            Color startColor = i < _startColors.Length ? _startColors[i] : renderer.color;
            Color fadedColor = new Color(startColor.r, startColor.g, startColor.b, startColor.a * alphaMultiplier);
            renderer.color = fadedColor;
            ApplyMaterialColor(renderer, fadedColor);
        }
    }

    private void ApplyMaterialColor(SpriteRenderer renderer, Color color)
    {
        if (renderer == null)
            return;

        var sharedMaterial = renderer.sharedMaterial;
        if (sharedMaterial == null)
            return;

        renderer.GetPropertyBlock(_propertyBlock);

        bool changed = false;
        if (sharedMaterial.HasProperty(ColorId))
        {
            _propertyBlock.SetColor(ColorId, color);
            changed = true;
        }

        if (sharedMaterial.HasProperty(BaseColorId))
        {
            _propertyBlock.SetColor(BaseColorId, color);
            changed = true;
        }

        if (sharedMaterial.HasProperty(RendererColorId))
        {
            _propertyBlock.SetColor(RendererColorId, color);
            changed = true;
        }

        if (changed)
            renderer.SetPropertyBlock(_propertyBlock);
    }
}
