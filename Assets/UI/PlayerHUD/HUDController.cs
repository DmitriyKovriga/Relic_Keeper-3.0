using UnityEngine;
using UnityEngine.UI;
using TMPro; // <--- ВАЖНО: Добавь эту строчку, чтобы работал TextMeshPro

public class HUDController : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerStats _playerStats;

    [Header("Bars")]
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _manaFill;
    [SerializeField] private Image _xpFill;

    [Header("Text Info")] // <--- НОВЫЙ РАЗДЕЛ
    [SerializeField] private TextMeshProUGUI _levelText; // Ссылка на текст уровня

    [Header("Skill Slots")]
    [SerializeField] private UISkillSlot[] _skillSlots;

    private void Start()
    {
        if (_playerStats != null)
        {
            _playerStats.OnStatsChanged += UpdateUI;
            // Ждем инициализации данных, не обновляем сразу
        }
    }

    private void OnDestroy()
    {
        if (_playerStats != null) _playerStats.OnStatsChanged -= UpdateUI;
    }

    public void SetPlayer(PlayerStats stats)
    {
        if (_playerStats != null) _playerStats.OnStatsChanged -= UpdateUI;
        _playerStats = stats;
        
        if (_playerStats != null)
        {
            _playerStats.OnStatsChanged += UpdateUI;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (_playerStats == null) return;
        if (_playerStats.MaxHealth == null || _playerStats.MaxMana == null) return;

        // --- BARS ---
        if (_healthFill != null)
        {
            float maxHp = _playerStats.MaxHealth.Value;
            _healthFill.fillAmount = maxHp > 0 ? _playerStats.CurrentHealth / maxHp : 0;
        }

        if (_manaFill != null)
        {
            float maxMana = _playerStats.MaxMana.Value;
            _manaFill.fillAmount = maxMana > 0 ? _playerStats.CurrentMana / maxMana : 0;
        }

        if (_xpFill != null)
        {
            float requiredXp = _playerStats.RequiredXP;
            // Если макс уровень (reqXP = 0), рисуем полную полоску
            if (requiredXp > 0)
                _xpFill.fillAmount = _playerStats.CurrentXP / requiredXp;
            else
                _xpFill.fillAmount = 1f;
        }

        // --- LEVEL TEXT (НОВОЕ) ---
        if (_levelText != null)
        {
            // Просто берем цифру уровня из статов
            _levelText.text = _playerStats.Level.ToString();
        }
    }
    
    // ... (Метод UpdateSkillSlot остается без изменений) ...
    public void UpdateSkillSlot(int index, Sprite icon)
    {
        if (index >= 0 && index < _skillSlots.Length && _skillSlots[index] != null)
        {
            _skillSlots[index].Setup(icon);
        }
    }
}