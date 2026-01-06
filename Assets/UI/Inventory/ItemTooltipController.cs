using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;

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
    private VisualElement _divider; // Вынесли в поле класса, чтобы менять цвет
    private VisualElement _statsContainer;

    // Цвета (PoE Palette)
    private readonly Color _colNormalText = new Color(0.8f, 0.8f, 0.8f); // C8C8C8
    private readonly Color _colNormalBorder = new Color(0.5f, 0.5f, 0.5f); // Grey

    private readonly Color _colMagicText = new Color(0.53f, 0.53f, 1f); // 8888FF
    private readonly Color _colMagicBorder = new Color(0.3f, 0.3f, 0.7f); // Blue

    private readonly Color _colRareText = new Color(1f, 1f, 0.46f); // FFFF77
    private readonly Color _colRareBorder = new Color(0.7f, 0.6f, 0.2f); // Gold

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

        // --- ГЕОМЕТРИЯ ---
        _container.style.position = Position.Absolute;
        _container.style.width = _tooltipWidth;
        _container.style.minWidth = 100f; 
        _container.style.maxWidth = 180f; 
        _container.style.flexShrink = 0; 
        
        // Фон всегда темный
        _container.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.98f)); 
        
        // Бордюры (цвета назначим в ShowTooltip)
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
        _container.Add(_headerLabel);

        // DIVIDER
        _divider = new VisualElement();
        _divider.style.height = 1;
        _divider.style.width = Length.Percent(100);
        _divider.style.marginTop = 2; _divider.style.marginBottom = 2;
        _container.Add(_divider);

        // STATS
        _statsContainer = new VisualElement();
        _statsContainer.style.width = Length.Percent(100);
        _statsContainer.style.alignItems = Align.Center; 
        _container.Add(_statsContainer);

        _root.Add(_container);
    }

    public void ShowTooltip(InventoryItem item, VisualElement slot)
    {
        if (_container == null || item == null || item.Data == null || slot == null) return;

        // 1. Определяем редкость и цвета
        // 0 аффиксов = Normal
        // 1-2 аффикса = Magic
        // 3+ аффикса = Rare
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

        // 2. Применяем цвета
        _headerLabel.style.color = targetTextColor;
        
        _container.style.borderTopColor = targetBorderColor;
        _container.style.borderBottomColor = targetBorderColor;
        _container.style.borderLeftColor = targetBorderColor;
        _container.style.borderRightColor = targetBorderColor;
        
        // Разделитель тоже красим в цвет редкости (опционально, но стильно)
        _divider.style.backgroundColor = targetBorderColor;

        // 3. Заполняем контент
        _headerLabel.text = item.Data.ItemName;
        _statsContainer.Clear();
        List<string> lines = item.GetDescriptionLines(); 
        
        if (lines != null)
        {
            foreach (var line in lines)
            {
                Label stat = new Label(line);
                // Статы всегда синевато-серые, чтобы не отвлекать, или белые
                stat.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.9f));
                stat.style.fontSize = 8;
                stat.style.whiteSpace = WhiteSpace.Normal;
                stat.style.unityTextAlign = TextAnchor.MiddleCenter; 
                stat.style.marginBottom = 0; 
                _statsContainer.Add(stat);
            }
        }

        // 4. Отображение и Позиционирование
        _container.style.display = DisplayStyle.Flex;
        _container.MarkDirtyRepaint();
        
        // Расчет позиции
        _container.schedule.Execute(() => CalculateSmartPosition(slot, item));
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

        // --- Приоритет: СВЕРХУ ---
        float tryTopY = slotPos.y - tipH - _gap;

        if (tryTopY >= _screenPadding)
        {
            finalY = tryTopY;
            finalX = itemCenterX - (tipW / 2f);
        }
        else 
        {
            // --- СБОКУ ---
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

        // --- ЖЕСТКИЙ CLAMP (Чтобы не вылез за экран) ---
        finalX = Mathf.Clamp(finalX, _screenPadding, screenW - tipW - _screenPadding);
        finalY = Mathf.Clamp(finalY, _screenPadding, screenH - tipH - _screenPadding);

        _container.style.left = finalX;
        _container.style.top = finalY;
    }
}