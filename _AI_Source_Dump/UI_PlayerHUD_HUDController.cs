using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scripts.Stats; // Не забудь подключить namespace со статами

public class HUDController : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerStats _playerStats;

    [Header("Bars")]
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _manaFill;
    [SerializeField] private Image _xpFill;

    [Header("Value Texts")]
    [SerializeField] private TextMeshProUGUI _healthValueText;
    [SerializeField] private TextMeshProUGUI _manaValueText;
    [SerializeField] private TextMeshProUGUI _xpValueText;
    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("Skill Slots")]
    [SerializeField] private UISkillSlot[] _skillSlots;

    private void Start()
    {
        // Если игрок уже привязан в инспекторе
        if (_playerStats != null)
        {
            SetupEvents();
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
                _healthValueText.text = $"{_playerStats.Health.Current:0} / {_playerStats.Health.Max:0}";
        }

        // --- 2. MANA ---
        if (_playerStats.Mana != null)
        {
            if (_manaFill != null)
                _manaFill.fillAmount = _playerStats.Mana.Percent;

            if (_manaValueText != null)
                _manaValueText.text = $"{_playerStats.Mana.Current:0} / {_playerStats.Mana.Max:0}";
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
}