using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scripts.Stats; // Не забудь подключить namespace со статами
using Scripts.Skills;

public class HUDController : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private PlayerSkillManager _skillManager;

    [Header("Bars")]
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _manaFill;
    [SerializeField] private Image _xpFill;

    [Header("Resource Bar Effects")]
    [SerializeField, Min(0.01f)] private float _resourceBarFxDuration = 0.24f;
    [SerializeField] private Color _healthDamageFxColor = new Color(0.9f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private Color _healthRegenFxColor = new Color(0.25f, 0.95f, 0.35f, 0.8f);
    [SerializeField] private Color _manaSpendFxColor = new Color(0.3f, 0.55f, 1f, 0.8f);
    [SerializeField] private Color _manaRegenFxColor = new Color(0.2f, 1f, 0.95f, 0.75f);

    [Header("Value Texts")]
    [SerializeField] private TextMeshProUGUI _healthValueText;
    [SerializeField] private TextMeshProUGUI _manaValueText;
    [SerializeField] private TextMeshProUGUI _xpValueText;
    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("Adaptive Resource Text")]
    [SerializeField, Min(1f)] private float _resourceTextMinFontSize = 10f;
    [SerializeField, Min(0f)] private float _resourceTextHorizontalPadding = 6f;
    [SerializeField, Min(0f)] private float _resourceTextVerticalPadding = 2f;

    [Header("Skill Slots")]
    [SerializeField] private UISkillSlot[] _skillSlots;

    private float _healthTextBaseFontSize;
    private float _manaTextBaseFontSize;
    private Vector2 _lastHealthTextRectSize = Vector2.negativeInfinity;
    private Vector2 _lastManaTextRectSize = Vector2.negativeInfinity;
    private ResourceBarEffect _healthBarEffect;
    private ResourceBarEffect _manaBarEffect;
    private float _previousHealthNormalized = -1f;
    private float _previousManaNormalized = -1f;

    private void Awake()
    {
        ApplyConfiguredFont();
        CacheAdaptiveTextSettings();
        InitializeResourceBarEffects();
    }

    private void Start()
    {
        // Если игрок уже привязан в инспекторе
        if (_playerStats != null)
        {
            SetupEvents();
            UpdateUI();
        }
        if (_skillManager != null)
        {
            _skillManager.OnSkillSlotUpdated += UpdateSkillSlotUI;
        }
        else
        {
            // Пытаемся найти, если не назначено
            _skillManager = FindFirstObjectByType<PlayerSkillManager>();
            if (_skillManager != null) _skillManager.OnSkillSlotUpdated += UpdateSkillSlotUI;
        }
    }

    private void OnDestroy()
    {
        if (_playerStats != null) _playerStats.OnAnyStatChanged -= UpdateUI;
        if (_skillManager != null) _skillManager.OnSkillSlotUpdated -= UpdateSkillSlotUI;
    }

    private void Update()
    {
        UpdateCooldownOverlays();
        RefreshAdaptiveResourceTextIfNeeded();
        TickResourceBarEffects();
    }

    private void UpdateSkillSlotUI(int index, SkillDataSO skill)
    {
        if (index < 0 || index >= _skillSlots.Length) return;

        if (skill != null)
        {
            _skillSlots[index].Setup(skill.Icon);
        }
        else
        {
            _skillSlots[index].Clear();
        }

        if (index == 0)
            _skillSlots[index].SetCooldownOverlay(0f, false);
    }

    public void SetPlayer(PlayerStats stats)
    {
        if (_playerStats != null) _playerStats.OnAnyStatChanged -= UpdateUI;
        _playerStats = stats;
        _previousHealthNormalized = -1f;
        _previousManaNormalized = -1f;
        
        if (_playerStats != null)
        {
            SetupEvents();
            UpdateUI();
        }
    }

    private void SetupEvents()
    {
        _playerStats.OnAnyStatChanged += UpdateUI;
    }

    private void UpdateUI()
    {
        if (_playerStats == null) return;
        
        // --- 1. HEALTH ---
        // Используем ресурсы, они уже знают про свой Максимум
        if (_playerStats.Health != null)
        {
            float healthNormalized = _playerStats.Health.Percent;
            TriggerBarEffect(_healthBarEffect, ref _previousHealthNormalized, healthNormalized);

            if (_healthFill != null)
                _healthFill.fillAmount = healthNormalized;

            if (_healthValueText != null)
            {
                _healthValueText.text = $"{_playerStats.Health.Current:0} / {_playerStats.Health.Max:0}";
                FitTextToContainer(_healthValueText, _healthTextBaseFontSize);
            }
        }

        // --- 2. MANA ---
        if (_playerStats.Mana != null)
        {
            float manaNormalized = _playerStats.Mana.Percent;
            TriggerBarEffect(_manaBarEffect, ref _previousManaNormalized, manaNormalized);

            if (_manaFill != null)
                _manaFill.fillAmount = manaNormalized;

            if (_manaValueText != null)
            {
                _manaValueText.text = $"{_playerStats.Mana.Current:0} / {_playerStats.Mana.Max:0}";
                FitTextToContainer(_manaValueText, _manaTextBaseFontSize);
            }
        }

        // --- 3. EXPERIENCE ---
        if (_playerStats.Leveling != null)
        {
            float currentXP = _playerStats.Leveling.CurrentXP;
            float reqXP = _playerStats.Leveling.RequiredXP;

            if (_xpFill != null)
                _xpFill.fillAmount = (reqXP > 0) ? currentXP / reqXP : 1f;

            if (_xpValueText != null)
                _xpValueText.text = (reqXP > 0) ? $"{currentXP:0} / {reqXP:0}" : "MAX";
            
            if (_levelText != null)
                _levelText.text = _playerStats.Leveling.Level.ToString();
        }
    }
    
    public void UpdateSkillSlot(int index, Sprite icon)
    {
        if (index >= 0 && index < _skillSlots.Length && _skillSlots[index] != null)
        {
            _skillSlots[index].Setup(icon);
        }
    }

    private void UpdateCooldownOverlays()
    {
        if (_skillManager == null || _skillSlots == null)
            return;

        for (int i = 0; i < _skillSlots.Length; i++)
        {
            var slot = _skillSlots[i];
            if (slot == null)
                continue;

            if (i == 0)
            {
                slot.SetCooldownOverlay(0f, false);
                continue;
            }

            bool hasCooldownSkill = _skillManager.SlotHasCooldownSkill(i);
            float normalized = _skillManager.GetSkillCooldownNormalized(i);
            slot.SetCooldownOverlay(normalized, hasCooldownSkill);
        }
    }

    private void ApplyConfiguredFont()
    {
        var configuredFont = UIFontResolver.ResolveTMPFontAsset();
        if (configuredFont == null)
            return;

        if (_healthValueText != null) _healthValueText.font = configuredFont;
        if (_manaValueText != null) _manaValueText.font = configuredFont;
        if (_xpValueText != null) _xpValueText.font = configuredFont;
        if (_levelText != null) _levelText.font = configuredFont;
    }

    private void CacheAdaptiveTextSettings()
    {
        _healthTextBaseFontSize = _healthValueText != null ? Mathf.Max(_healthValueText.fontSize, _resourceTextMinFontSize) : _resourceTextMinFontSize;
        _manaTextBaseFontSize = _manaValueText != null ? Mathf.Max(_manaValueText.fontSize, _resourceTextMinFontSize) : _resourceTextMinFontSize;

        PrepareAdaptiveText(_healthValueText);
        PrepareAdaptiveText(_manaValueText);
    }

    private void PrepareAdaptiveText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.margin = Vector4.zero;
    }

    private void RefreshAdaptiveResourceTextIfNeeded()
    {
        if (_healthValueText != null)
        {
            Vector2 rectSize = _healthValueText.rectTransform.rect.size;
            if (!Approximately(rectSize, _lastHealthTextRectSize))
            {
                FitTextToContainer(_healthValueText, _healthTextBaseFontSize);
                _lastHealthTextRectSize = rectSize;
            }
        }

        if (_manaValueText != null)
        {
            Vector2 rectSize = _manaValueText.rectTransform.rect.size;
            if (!Approximately(rectSize, _lastManaTextRectSize))
            {
                FitTextToContainer(_manaValueText, _manaTextBaseFontSize);
                _lastManaTextRectSize = rectSize;
            }
        }
    }

    private void FitTextToContainer(TextMeshProUGUI text, float baseFontSize)
    {
        if (text == null)
            return;

        Rect rect = text.rectTransform.rect;
        float availableWidth = Mathf.Max(1f, rect.width - _resourceTextHorizontalPadding);
        float availableHeight = Mathf.Max(1f, rect.height - _resourceTextVerticalPadding);
        float minFont = Mathf.Max(1f, _resourceTextMinFontSize);
        float fontSize = Mathf.Max(baseFontSize, minFont);

        text.fontSize = fontSize;

        while (fontSize > minFont)
        {
            Vector2 preferred = text.GetPreferredValues(text.text, availableWidth, availableHeight);
            if (preferred.x <= availableWidth && preferred.y <= availableHeight)
                break;

            fontSize -= 1f;
            text.fontSize = fontSize;
        }

        if (fontSize < minFont)
            text.fontSize = minFont;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
    }

    private void InitializeResourceBarEffects()
    {
        _healthBarEffect = CreateResourceBarEffect(_healthFill, "HealthBarFx", _healthDamageFxColor, _healthRegenFxColor);
        _manaBarEffect = CreateResourceBarEffect(_manaFill, "ManaBarFx", _manaSpendFxColor, _manaRegenFxColor);
    }

    private void TickResourceBarEffects()
    {
        _healthBarEffect?.Tick(Time.unscaledDeltaTime, _resourceBarFxDuration);
        _manaBarEffect?.Tick(Time.unscaledDeltaTime, _resourceBarFxDuration);
    }

    private void TriggerBarEffect(ResourceBarEffect effect, ref float previousNormalized, float currentNormalized)
    {
        if (effect == null)
        {
            previousNormalized = currentNormalized;
            return;
        }

        float clampedCurrent = Mathf.Clamp01(currentNormalized);
        if (previousNormalized < 0f)
        {
            previousNormalized = clampedCurrent;
            effect.SyncToCurrent(clampedCurrent);
            return;
        }

        if (clampedCurrent < previousNormalized - 0.0001f)
        {
            effect.PlayDecrease(previousNormalized, clampedCurrent);
        }
        else if (clampedCurrent > previousNormalized + 0.0001f)
        {
            effect.PlayIncrease(previousNormalized, clampedCurrent);
        }

        previousNormalized = clampedCurrent;
    }

    private static ResourceBarEffect CreateResourceBarEffect(Image sourceFill, string name, Color decreaseColor, Color increaseColor)
    {
        if (sourceFill == null || sourceFill.rectTransform == null)
            return null;

        RectTransform overlayRoot = CreateOverlayRoot(sourceFill.rectTransform, $"{name}_OverlayRoot");
        if (overlayRoot == null)
            return null;

        var decreaseImage = CreateFxImage(overlayRoot, $"{name}_Decrease", decreaseColor);
        var increaseImage = CreateFxImage(overlayRoot, $"{name}_Increase", increaseColor);
        return new ResourceBarEffect(overlayRoot, decreaseImage, increaseImage);
    }

    private static RectTransform CreateOverlayRoot(RectTransform sourceRect, string objectName)
    {
        if (sourceRect == null)
            return null;

        var go = new GameObject(objectName, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(sourceRect, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetSiblingIndex(0);
        return rect;
    }

    private static Image CreateFxImage(RectTransform parent, string objectName, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private sealed class ResourceBarEffect
    {
        private readonly RectTransform _parent;
        private readonly Image _decreaseImage;
        private readonly Image _increaseImage;
        private readonly Color _decreaseBaseColor;
        private readonly Color _increaseBaseColor;

        private bool _decreaseActive;
        private bool _increaseActive;
        private float _decreaseElapsed;
        private float _increaseElapsed;

        public ResourceBarEffect(RectTransform parent, Image decreaseImage, Image increaseImage)
        {
            _parent = parent;
            _decreaseImage = decreaseImage;
            _increaseImage = increaseImage;
            _decreaseBaseColor = decreaseImage != null ? decreaseImage.color : Color.clear;
            _increaseBaseColor = increaseImage != null ? increaseImage.color : Color.clear;
        }

        public void SyncToCurrent(float normalized)
        {
            HideDecrease();
            HideIncrease();
            if (_parent == null)
                return;

            float width = GetParentWidth() * Mathf.Clamp01(normalized);
            SetSegment(_decreaseImage, 0f, width, _decreaseBaseColor, 0f);
            SetSegment(_increaseImage, 0f, width, _increaseBaseColor, 0f);
        }

        public void PlayDecrease(float previousNormalized, float currentNormalized)
        {
            float maxWidth = GetParentWidth();
            float start = maxWidth * Mathf.Clamp01(currentNormalized);
            float end = maxWidth * Mathf.Clamp01(previousNormalized);
            float width = Mathf.Max(0f, end - start);

            HideIncrease();
            if (width <= 0.01f)
            {
                HideDecrease();
                return;
            }

            _decreaseActive = true;
            _decreaseElapsed = 0f;
            SetSegment(_decreaseImage, start, width, _decreaseBaseColor, _decreaseBaseColor.a);
        }

        public void PlayIncrease(float previousNormalized, float currentNormalized)
        {
            float maxWidth = GetParentWidth();
            float start = maxWidth * Mathf.Clamp01(previousNormalized);
            float end = maxWidth * Mathf.Clamp01(currentNormalized);
            float width = Mathf.Max(0f, end - start);

            HideDecrease();
            if (width <= 0.01f)
            {
                HideIncrease();
                return;
            }

            _increaseActive = true;
            _increaseElapsed = 0f;
            SetSegment(_increaseImage, start, width, _increaseBaseColor, _increaseBaseColor.a);
        }

        public void Tick(float dt, float duration)
        {
            float safeDuration = Mathf.Max(0.01f, duration);

            if (_decreaseActive)
            {
                _decreaseElapsed += Mathf.Max(0f, dt);
                float alpha = 1f - Mathf.Clamp01(_decreaseElapsed / safeDuration);
                SetAlpha(_decreaseImage, _decreaseBaseColor, alpha);
                if (alpha <= 0.001f)
                    HideDecrease();
            }

            if (_increaseActive)
            {
                _increaseElapsed += Mathf.Max(0f, dt);
                float alpha = 1f - Mathf.Clamp01(_increaseElapsed / safeDuration);
                SetAlpha(_increaseImage, _increaseBaseColor, alpha);
                if (alpha <= 0.001f)
                    HideIncrease();
            }
        }

        private float GetParentWidth()
        {
            return _parent != null ? Mathf.Max(0f, _parent.rect.width) : 0f;
        }

        private static void SetSegment(Image image, float startX, float width, Color baseColor, float alpha)
        {
            if (image == null || image.rectTransform == null)
                return;

            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(Mathf.Round(startX), 0f);
            rect.sizeDelta = new Vector2(Mathf.Round(width), 0f);

            var color = baseColor;
            color.a = alpha;
            image.color = color;
            image.enabled = alpha > 0.001f && width > 0.01f;
        }

        private static void SetAlpha(Image image, Color baseColor, float alpha)
        {
            if (image == null)
                return;

            var color = baseColor;
            color.a = baseColor.a * Mathf.Clamp01(alpha);
            image.color = color;
            image.enabled = color.a > 0.001f && image.rectTransform.sizeDelta.x > 0.01f;
        }

        private void HideDecrease()
        {
            _decreaseActive = false;
            _decreaseElapsed = 0f;
            SetSegment(_decreaseImage, 0f, 0f, _decreaseBaseColor, 0f);
        }

        private void HideIncrease()
        {
            _increaseActive = false;
            _increaseElapsed = 0f;
            SetSegment(_increaseImage, 0f, 0f, _increaseBaseColor, 0f);
        }
    }
}
