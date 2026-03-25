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

    private void Awake()
    {
        ApplyConfiguredFont();
        CacheAdaptiveTextSettings();
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
            if (_healthFill != null)
                _healthFill.fillAmount = _playerStats.Health.Percent;

            if (_healthValueText != null)
            {
                _healthValueText.text = $"{_playerStats.Health.Current:0} / {_playerStats.Health.Max:0}";
                FitTextToContainer(_healthValueText, _healthTextBaseFontSize);
            }
        }

        // --- 2. MANA ---
        if (_playerStats.Mana != null)
        {
            if (_manaFill != null)
                _manaFill.fillAmount = _playerStats.Mana.Percent;

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
}
