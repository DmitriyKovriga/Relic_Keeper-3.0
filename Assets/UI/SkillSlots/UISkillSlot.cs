using UnityEngine;
using UnityEngine.UI;

public class UISkillSlot : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Перетащи сюда дочерний объект Icon (Image). Не фон слота.")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _cooldownOverlayImage;

    private static Sprite _runtimeWhiteSprite;

    private void Awake()
    {
        if (_iconImage == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                _iconImage = iconTransform.GetComponent<Image>();
        }

        if (_iconImage == null)
        {
            Debug.LogError($"[UISkillSlot] В объекте {gameObject.name} не найдена иконка. Создай дочерний Image 'Icon' или назначь его вручную.");
            return;
        }

        _iconImage.raycastTarget = false;
        EnsureCooldownOverlay();
        Clear();
    }

    public void Setup(Sprite icon)
    {
        if (_iconImage == null)
            return;

        if (icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.enabled = true;
            _iconImage.color = Color.white;
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        if (_iconImage == null)
            return;

        _iconImage.sprite = null;
        _iconImage.enabled = false;
        SetCooldownOverlay(0f, false);
    }

    public void SetCooldownOverlay(float normalizedRemaining, bool visible)
    {
        if (_cooldownOverlayImage == null)
            return;

        bool shouldShow = visible && normalizedRemaining > 0.001f;
        _cooldownOverlayImage.enabled = shouldShow;

        if (!shouldShow)
            return;

        _cooldownOverlayImage.fillAmount = Mathf.Clamp01(normalizedRemaining);
    }

    private void EnsureCooldownOverlay()
    {
        if (_cooldownOverlayImage != null)
            return;

        var overlayTransform = transform.Find("CooldownOverlay");
        if (overlayTransform != null)
        {
            _cooldownOverlayImage = overlayTransform.GetComponent<Image>();
            if (_cooldownOverlayImage != null)
                return;
        }

        var overlayGO = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGO.transform.SetParent(transform, false);
        overlayGO.transform.SetAsLastSibling();

        var rect = overlayGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _cooldownOverlayImage = overlayGO.GetComponent<Image>();
        _cooldownOverlayImage.sprite = GetRuntimeWhiteSprite();
        _cooldownOverlayImage.color = new Color(0.35f, 0.35f, 0.35f, 0.58f);
        _cooldownOverlayImage.raycastTarget = false;
        _cooldownOverlayImage.type = Image.Type.Filled;
        _cooldownOverlayImage.fillMethod = Image.FillMethod.Radial360;
        _cooldownOverlayImage.fillOrigin = 2;
        _cooldownOverlayImage.fillClockwise = true;
        _cooldownOverlayImage.fillAmount = 0f;
        _cooldownOverlayImage.enabled = false;
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
