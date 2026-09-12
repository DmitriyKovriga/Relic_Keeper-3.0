using Scripts.StatusEffects;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIStatusEffectSlot : MonoBehaviour
{
    [SerializeField] private Image _frameImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _durationOverlayImage;

    private static Sprite _runtimeWhiteSprite;
    private StatusEffectController.ActiveEffectInstance _boundEffect;

    private void Awake()
    {
        EnsureVisuals();
        Clear();
    }

    public void Bind(StatusEffectController.ActiveEffectInstance effect, Color frameColor)
    {
        EnsureVisuals();
        _boundEffect = effect;

        if (_frameImage != null)
            _frameImage.color = frameColor;

        if (_iconImage != null)
        {
            Sprite icon = effect != null && effect.Effect != null ? effect.Effect.Icon : null;
            _iconImage.sprite = icon;
            _iconImage.enabled = icon != null;
            _iconImage.color = Color.white;
        }

        gameObject.SetActive(effect != null);
        UpdateRuntime();
    }

    public void UpdateRuntime()
    {
        if (_boundEffect == null)
        {
            Clear();
            return;
        }

        SetDurationFill(_boundEffect.RemainingNormalized);
    }

    public void Clear()
    {
        EnsureVisuals();
        _boundEffect = null;

        if (_iconImage != null)
        {
            _iconImage.sprite = null;
            _iconImage.enabled = false;
        }

        SetDurationFill(0f);
        gameObject.SetActive(false);
    }

    private void EnsureVisuals()
    {
        if (_frameImage == null)
        {
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = frameGo.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _frameImage = frameGo.GetComponent<Image>();
            _frameImage.sprite = GetRuntimeWhiteSprite();
            _frameImage.raycastTarget = false;
        }

        if (_iconImage == null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = iconGo.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _iconImage = iconGo.GetComponent<Image>();
            _iconImage.raycastTarget = false;
            _iconImage.preserveAspect = true;
        }

        if (_durationOverlayImage == null)
        {
            var overlayGo = new GameObject("DurationOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = overlayGo.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _durationOverlayImage = overlayGo.GetComponent<Image>();
            _durationOverlayImage.sprite = GetRuntimeWhiteSprite();
            _durationOverlayImage.color = new Color(0.1f, 0.1f, 0.1f, 0.58f);
            _durationOverlayImage.raycastTarget = false;
            _durationOverlayImage.type = Image.Type.Filled;
            _durationOverlayImage.fillMethod = Image.FillMethod.Radial360;
            _durationOverlayImage.fillOrigin = 2;
            _durationOverlayImage.fillClockwise = true;
        }
    }

    private void SetDurationFill(float normalized)
    {
        if (_durationOverlayImage == null)
            return;

        float clamped = Mathf.Clamp01(normalized);
        _durationOverlayImage.fillAmount = clamped;
        _durationOverlayImage.enabled = clamped > 0.001f && clamped < 0.999f;
    }

    private static Sprite GetRuntimeWhiteSprite()
    {
        if (_runtimeWhiteSprite != null)
            return _runtimeWhiteSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        _runtimeWhiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _runtimeWhiteSprite;
    }
}
