using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerStats _playerStats;

    [Header("Bars (Images)")]
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _manaFill;
    [SerializeField] private Image _xpFill;

    [Header("Value Texts (TMP)")] // <--- НОВОЕ: Сюда перетащить текстовые поля
    [SerializeField] private TextMeshProUGUI _healthValueText;
    [SerializeField] private TextMeshProUGUI _manaValueText;
    [SerializeField] private TextMeshProUGUI _xpValueText;
    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("Skill Slots")]
    [SerializeField] private UISkillSlot[] _skillSlots;

    private void Start()
    {
        if (_playerStats != null)
        {
            _playerStats.OnAnyStatChanged += UpdateUI;
            UpdateUI();
        }
    }

    private void OnDestroy()
    {
        if (_playerStats != null) _playerStats.OnAnyStatChanged -= UpdateUI;
    }

    public void SetPlayer(PlayerStats stats)
    {
        if (_playerStats != null) _playerStats.OnAnyStatChanged -= UpdateUI;
        _playerStats = stats;
        
        if (_playerStats != null)
        {
            _playerStats.OnAnyStatChanged += UpdateUI;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (_playerStats == null) return;
        
        // --- 1. HEALTH ---
        if (_playerStats.Health != null)
        {
            // Полоска
            if (_healthFill != null)
                _healthFill.fillAmount = _playerStats.Health.Percent;

            // Текст: "50 / 100" (округляем до целого через :0)
            if (_healthValueText != null)
                _healthValueText.text = $"{_playerStats.Health.Current:0} / {_playerStats.Health.Max:0}";
        }

        // --- 2. MANA ---
        if (_playerStats.Mana != null)
        {
            // Полоска
            if (_manaFill != null)
                _manaFill.fillAmount = _playerStats.Mana.Percent;

            // Текст: "25 / 50"
            if (_manaValueText != null)
                _manaValueText.text = $"{_playerStats.Mana.Current:0} / {_playerStats.Mana.Max:0}";
        }

        // --- 3. EXPERIENCE ---
        if (_playerStats.Leveling != null)
        {
            float currentXP = _playerStats.Leveling.CurrentXP;
            float reqXP = _playerStats.Leveling.RequiredXP;

            // Полоска
            if (_xpFill != null)
            {
                if (reqXP > 0)
                    _xpFill.fillAmount = currentXP / reqXP;
                else
                    _xpFill.fillAmount = 1f;
            }

            // Текст: "1250 / 2000"
            if (_xpValueText != null)
            {
                if (reqXP > 0)
                    _xpValueText.text = $"{currentXP:0} / {reqXP:0}";
                else
                    _xpValueText.text = "MAX";
            }
        }

        // --- 4. LEVEL TEXT ---
        if (_levelText != null && _playerStats.Leveling != null)
        {
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
}