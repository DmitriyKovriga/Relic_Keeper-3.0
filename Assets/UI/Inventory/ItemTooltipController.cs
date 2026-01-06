using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using UnityEngine.Localization.Settings;

public class ItemTooltipController : MonoBehaviour
{
    public static ItemTooltipController Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private UIDocument _uiDoc; 
    
    [Header("Tooltip Config")]
    [SerializeField] private float _tooltipWidth = 140f; 
    [SerializeField] private float _gap = 5f; 
    [SerializeField] private float _screenPadding = 2f; 

    private VisualElement _root;
    private VisualElement _container;
    private Label _headerLabel;
    private VisualElement _headerDivider; 
    private VisualElement _statsContainer;

    // --- ЦВЕТА ---
    private readonly Color _colNormalText = new Color(0.8f, 0.8f, 0.8f); 
    private readonly Color _colNormalBorder = new Color(0.5f, 0.5f, 0.5f); 

    private readonly Color _colMagicText = new Color(0.53f, 0.53f, 1f); 
    private readonly Color _colMagicBorder = new Color(0.3f, 0.3f, 0.7f); 

    private readonly Color _colRareText = new Color(1f, 1f, 0.46f); 
    private readonly Color _colRareBorder = new Color(0.7f, 0.6f, 0.2f); 

    // Цвета для контента
    private readonly Color _colBaseStat = new Color(0.9f, 0.9f, 0.9f);
    private readonly Color _colImplicit = new Color(0.6f, 0.8f, 1f);
    private readonly Color _colAffix = new Color(0.5f, 0.5f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc != null)
        {
            _root = _uiDoc.rootVisualElement;
            _root.schedule.Execute(CreateTooltipVisuals).ExecuteLater(50);
        }
    }

    private void CreateTooltipVisuals()
    {
        var old = _root.Q<VisualElement>("GlobalItemTooltip");
        if (old != null) _root.Remove(old);

        _container = new VisualElement { name = "GlobalItemTooltip" };

        // --- ГЕОМЕТРИЯ (Твоя) ---
        _container.style.position = Position.Absolute;
        _container.style.width = _tooltipWidth;
        _container.style.minWidth = 100f; 
        _container.style.maxWidth = 180f; 
        _container.style.flexShrink = 0; 
        
        _container.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.98f)); 
        
        _container.style.borderTopWidth = 1; _container.style.borderBottomWidth = 1;
        _container.style.borderLeftWidth = 1; _container.style.borderRightWidth = 1;

        _container.style.paddingTop = 4; _container.style.paddingBottom = 4;
        _container.style.paddingLeft = 4; _container.style.paddingRight = 4;
        
        _container.style.display = DisplayStyle.None; 
        _container.pickingMode = PickingMode.Ignore; 

        _container.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        _container.style.fontSize = 8;
        _container.style.alignItems = Align.Center; 

        // HEADER
        _headerLabel = new Label();
        _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _headerLabel.style.whiteSpace = WhiteSpace.Normal;
        _headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        
        // Чуть-чуть поднимаем и заголовок тоже
        _headerLabel.style.paddingTop = 0;
        _headerLabel.style.paddingBottom = 2;
        
        _container.Add(_headerLabel);

        // HEADER DIVIDER
        _headerDivider = new VisualElement();
        _headerDivider.style.height = 1;
        _headerDivider.style.width = Length.Percent(100);
        _headerDivider.style.marginTop = 2; _headerDivider.style.marginBottom = 2;
        _container.Add(_headerDivider);

        // STATS CONTAINER
        _statsContainer = new VisualElement();
        _statsContainer.style.width = Length.Percent(100);
        _statsContainer.style.alignItems = Align.Center; 
        _container.Add(_statsContainer);

        _root.Add(_container);
    }

    public void ShowTooltip(InventoryItem item, VisualElement slot)
    {
        if (_container == null || item == null || item.Data == null || slot == null) return;

        // 1. Цвета
        int affixCount = item.Affixes != null ? item.Affixes.Count : 0;
        
        Color targetTextColor;
        Color targetBorderColor;

        if (affixCount >= 3)
        {
            targetTextColor = _colRareText;
            targetBorderColor = _colRareBorder;
        }
        else if (affixCount > 0)
        {
            targetTextColor = _colMagicText;
            targetBorderColor = _colMagicBorder;
        }
        else
        {
            targetTextColor = _colNormalText;
            targetBorderColor = _colNormalBorder;
        }

        _headerLabel.style.color = targetTextColor;
        _container.style.borderTopColor = targetBorderColor;
        _container.style.borderBottomColor = targetBorderColor;
        _container.style.borderLeftColor = targetBorderColor;
        _container.style.borderRightColor = targetBorderColor;
        _headerDivider.style.backgroundColor = targetBorderColor;

        _headerLabel.text = item.Data.ItemName;
        _statsContainer.Clear();

        // === ГЕНЕРАЦИЯ КОНТЕНТА ===

        bool hasBase = false;
        bool hasImpl = false;
        bool hasAffix = false;

        // 1. БАЗОВЫЕ СТАТЫ
        if (item.Data is ArmorItemSO armor)
        {
            if (armor.BaseArmor > 0) 
            {
                AddStatRow(GetStatText(StatType.Armor, armor.BaseArmor), _colBaseStat, true);
                hasBase = true;
            }
            if (armor.BaseEvasion > 0)
            {
                AddStatRow(GetStatText(StatType.Evasion, armor.BaseEvasion), _colBaseStat, true);
                hasBase = true;
            }
            if (armor.BaseBubbles > 0)
            {
                AddStatRow(GetStatText(StatType.MaxBubbles, armor.BaseBubbles), _colBaseStat, true);
                hasBase = true;
            }
        }

        // Разделитель
        bool hasAnyMods = (item.Data.ImplicitModifiers != null && item.Data.ImplicitModifiers.Count > 0) || affixCount > 0;
        if (hasBase && hasAnyMods)
        {
            AddDivider(targetBorderColor);
        }

        // 2. ИМПЛИСИТЫ
        if (item.Data.ImplicitModifiers != null && item.Data.ImplicitModifiers.Count > 0)
        {
            foreach (var mod in item.Data.ImplicitModifiers)
            {
                AddStatRow(GetStatText(mod.Stat, mod.Value, mod.Type), _colImplicit, false);
            }
            hasImpl = true;
        }

        // Разделитель
        if (hasImpl && affixCount > 0)
        {
            AddDivider(targetBorderColor);
        }

        // 3. АФФИКСЫ
        if (item.Affixes != null)
        {
            foreach (var affix in item.Affixes)
            {
                if (affix.Modifiers.Count > 0)
                {
                    var (statType, mod) = affix.Modifiers[0];
                    AddStatRow(GetStatText(statType, mod.Value, mod.Type), _colAffix, false);
                    hasAffix = true;
                }
            }
        }

        if (!hasBase && !hasImpl && !hasAffix)
        {
            AddStatRow("No Stats", Color.gray, false);
        }

        // 4. Позиционирование
        _container.style.display = DisplayStyle.Flex;
        _container.MarkDirtyRepaint();
        
        _container.schedule.Execute(() => CalculateSmartPosition(slot, item));
    }

    // --- HELPER METHODS ---

    private string GetStatText(StatType type, float value, StatModType modType = StatModType.Flat)
    {
        string key = $"stats.{type}";
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("MenuLabels", key);
        string name = op.IsDone ? op.Result : type.ToString();

        if (modType == StatModType.PercentAdd || modType == StatModType.PercentMult)
        {
            string sign = value >= 0 ? "+" : "";
            return $"{name}: {sign}{value}%";
        }
        return $"{name}: {value}";
    }

    private void AddStatRow(string text, Color color, bool isBold)
    {
        Label stat = new Label(text);
        stat.style.color = new StyleColor(color);
        stat.style.fontSize = 8;
        stat.style.whiteSpace = WhiteSpace.Normal;
        
        // ВАЖНО: Центрирование
        stat.style.unityTextAlign = TextAnchor.MiddleCenter; 
        
        // --- ФИКС ПОЗИЦИИ ТЕКСТА ---
        // Убираем верхний отступ и добавляем нижний.
        // Это визуально поднимает текст вверх внутри его строки.
        stat.style.paddingTop = 0; 
        stat.style.paddingBottom = 2; // <-- Поднимаем текст на 2 "пикселя" вверх
        stat.style.marginTop = 0;
        stat.style.marginBottom = 0;

        if (isBold) stat.style.unityFontStyleAndWeight = FontStyle.Bold;
        
        _statsContainer.Add(stat);
    }

    private void AddDivider(Color color)
    {
        VisualElement div = new VisualElement();
        div.style.height = 1;
        div.style.width = Length.Percent(100);
        
        div.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.4f));
        
        // Отступы разделителя
        div.style.marginTop = 2;
        div.style.marginBottom = 2;
        
        _statsContainer.Add(div);
    }

    public void HideTooltip()
    {
        if (_container != null) _container.style.display = DisplayStyle.None;
    }

    private void CalculateSmartPosition(VisualElement anchorSlot, InventoryItem item)
    {
        if (_root == null || _container == null) return;

        float tipW = _container.resolvedStyle.width;
        float tipH = _container.resolvedStyle.height;
        if (float.IsNaN(tipW) || tipW < 1) tipW = _tooltipWidth;
        if (float.IsNaN(tipH) || tipH < 1) tipH = 50f;

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;

        Rect slotRect = anchorSlot.worldBound;
        Vector2 slotPos = _root.WorldToLocal(new Vector2(slotRect.x, slotRect.y));
        
        float itemW = slotRect.width * item.Data.Width;
        float itemH = slotRect.height * item.Data.Height;

        float itemCenterX = slotPos.x + (itemW / 2f);
        float itemCenterY = slotPos.y + (itemH / 2f);

        float finalX = 0;
        float finalY = 0;

        float tryTopY = slotPos.y - tipH - _gap;

        if (tryTopY >= _screenPadding)
        {
            finalY = tryTopY;
            finalX = itemCenterX - (tipW / 2f);
        }
        else 
        {
            float spaceRight = screenW - (slotPos.x + itemW);
            float spaceLeft = slotPos.x;

            if (spaceRight > spaceLeft)
            {
                finalX = slotPos.x + itemW + _gap;
            }
            else
            {
                finalX = slotPos.x - tipW - _gap;
            }
            finalY = itemCenterY - (tipH / 2f);
        }

        finalX = Mathf.Clamp(finalX, _screenPadding, screenW - tipW - _screenPadding);
        finalY = Mathf.Clamp(finalY, _screenPadding, screenH - tipH - _screenPadding);

        // ВАЖНО: Округление убирает "дробную" кривизну текста
        _container.style.left = Mathf.Round(finalX);
        _container.style.top = Mathf.Round(finalY);
    }
}