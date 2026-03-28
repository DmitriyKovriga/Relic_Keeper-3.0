using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    private float _duration;
    private float _timer;
    private bool _fadeOutEnabled;
    private float _fadeOutStartLifePercent;
    private SpriteRenderer[] _renderers;
    private Color[] _startColors;
    private bool _initialized;

    public void Initialize(float duration, bool fadeOutEnabled = true, float fadeOutStartLifePercent = 0.5f)
    {
        _duration = Mathf.Max(0.0001f, duration);
        _timer = 0f;
        _fadeOutEnabled = fadeOutEnabled;
        _fadeOutStartLifePercent = Mathf.Clamp01(fadeOutStartLifePercent);
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _startColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            _startColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
        }

        ApplyAlphaMultiplier(1f);
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized)
            return;

        _timer += Time.deltaTime;

        if (_fadeOutEnabled && _fadeOutStartLifePercent < 1f)
        {
            float lifeProgress = Mathf.Clamp01(_timer / _duration);
            float fadeProgress = Mathf.InverseLerp(_fadeOutStartLifePercent, 1f, lifeProgress);
            float alphaMultiplier = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
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
            renderer.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * alphaMultiplier);
        }
    }
}
