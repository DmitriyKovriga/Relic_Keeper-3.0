using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scripts.Skills;

public class UISkillSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _cooldownOverlayImage;
    [SerializeField] private TextMeshProUGUI _inputText;
    [SerializeField] private Image _inputBackgroundImage;
    [SerializeField] private TextMeshProUGUI _manaCostText;
    [SerializeField] private TextMeshProUGUI _cooldownText;

    [Header("Runtime Labels")]
    [SerializeField, Min(1f)] private float _inputFontSize = 8f;
    [SerializeField, Min(1f)] private float _manaFontSize = 8f;
    [SerializeField, Min(1f)] private float _cooldownFontSize = 12f;
    [SerializeField] private Color _inputBackgroundColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color _inputTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color _manaTextColor = new Color(0.35f, 0.68f, 1f, 1f);
    [SerializeField] private Color _cooldownTextColor = Color.white;

    private static Sprite _runtimeWhiteSprite;

    private void Awake()
    {
        if (_iconImage == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                _iconImage = iconTransform.GetComponent<Image>();
        }

        if (_iconImage == null)
        {
            Debug.LogError($"[UISkillSlot] Icon image was not found on {gameObject.name}. Add child Image 'Icon' or assign it manually.");
            return;
        }

        _iconImage.raycastTarget = false;
        EnsureCooldownOverlay();
        EnsureInfoLabels();
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

    public void Setup(SkillDataSO skill, string inputLabel)
    {
        Setup(skill != null ? skill.Icon : null);
        SetInputLabel(inputLabel);
        SetManaCost(skill != null ? skill.ManaCost : 0f);
        SetCooldownText(0f, false);
    }

    public void Clear()
    {
        if (_iconImage == null)
            return;

        _iconImage.sprite = null;
        _iconImage.enabled = false;
        SetCooldownOverlay(0f, false);
        SetManaCost(0f);
        SetCooldownText(0f, false);
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

    public void SetCooldownText(float secondsRemaining, bool visible)
    {
        if (_cooldownText == null)
            return;

        bool shouldShow = visible && secondsRemaining > 0.01f;
        _cooldownText.enabled = shouldShow;
        if (!shouldShow)
        {
            _cooldownText.text = "";
            return;
        }

        _cooldownText.text = Mathf.CeilToInt(secondsRemaining).ToString();
    }

    public void SetInputLabel(string label)
    {
        bool hasLabel = !string.IsNullOrWhiteSpace(label);
        if (_inputText != null)
        {
            _inputText.text = hasLabel ? label.Trim().ToUpperInvariant() : "";
            _inputText.enabled = hasLabel;
        }

        if (_inputBackgroundImage != null)
            _inputBackgroundImage.enabled = hasLabel;
    }

    private void SetManaCost(float manaCost)
    {
        if (_manaCostText == null)
            return;

        bool hasManaCost = manaCost > 0.01f;
        _manaCostText.enabled = hasManaCost;
        _manaCostText.text = hasManaCost ? Mathf.CeilToInt(manaCost).ToString() : "";
    }

    private void EnsureCooldownOverlay()
    {
        if (_cooldownOverlayImage != null)
            return;

        Transform overlayTransform = transform.Find("CooldownOverlay");
        if (overlayTransform != null)
        {
            _cooldownOverlayImage = overlayTransform.GetComponent<Image>();
            if (_cooldownOverlayImage != null)
                return;
        }

        var overlayGO = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGO.transform.SetParent(transform, false);
        overlayGO.transform.SetAsLastSibling();

        RectTransform rect = overlayGO.GetComponent<RectTransform>();
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

    private void EnsureInfoLabels()
    {
        EnsureInputLabel();
        EnsureManaCostLabel();
        EnsureCooldownLabel();
    }

    private void EnsureInputLabel()
    {
        Transform existing = transform.Find("InputBind");
        GameObject root = existing != null ? existing.gameObject : new GameObject("InputBind", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        root.transform.SetAsLastSibling();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, -2f);
        rootRect.sizeDelta = new Vector2(14f, 8f);

        _inputBackgroundImage = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        _inputBackgroundImage.sprite = GetRuntimeWhiteSprite();
        _inputBackgroundImage.color = _inputBackgroundColor;
        _inputBackgroundImage.raycastTarget = false;

        Transform textTransform = root.transform.Find("Text");
        GameObject textGo = textTransform != null ? textTransform.gameObject : new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _inputText = textGo.GetComponent<TextMeshProUGUI>() ?? textGo.AddComponent<TextMeshProUGUI>();
        ConfigureText(_inputText, _inputFontSize, _inputTextColor, TextAlignmentOptions.Center);
    }

    private void EnsureManaCostLabel()
    {
        Transform existing = transform.Find("ManaCost");
        GameObject textGo = existing != null ? existing.gameObject : new GameObject("ManaCost", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        textGo.transform.SetAsLastSibling();

        RectTransform rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-2f, 2f);
        rect.sizeDelta = new Vector2(16f, 10f);

        _manaCostText = textGo.GetComponent<TextMeshProUGUI>() ?? textGo.AddComponent<TextMeshProUGUI>();
        ConfigureText(_manaCostText, _manaFontSize, _manaTextColor, TextAlignmentOptions.BottomRight);
    }

    private void EnsureCooldownLabel()
    {
        Transform existing = transform.Find("CooldownText");
        GameObject textGo = existing != null ? existing.gameObject : new GameObject("CooldownText", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        textGo.transform.SetAsLastSibling();

        RectTransform rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _cooldownText = textGo.GetComponent<TextMeshProUGUI>() ?? textGo.AddComponent<TextMeshProUGUI>();
        ConfigureText(_cooldownText, _cooldownFontSize, _cooldownTextColor, TextAlignmentOptions.Center);
        _cooldownText.fontStyle = FontStyles.Bold;
    }

    private static void ConfigureText(TextMeshProUGUI text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.font = UIFontResolver.ResolveTMPFontAsset();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
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
