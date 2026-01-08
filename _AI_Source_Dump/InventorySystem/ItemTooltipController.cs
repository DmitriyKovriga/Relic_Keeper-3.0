using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ItemTooltipController : MonoBehaviour
{
    public static ItemTooltipController Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private UIDocument _uiDoc; 
    
    [Header("Tooltip Config")]
    [SerializeField] private float _tooltipWidth = 160f; 
    [SerializeField] private float _gap = 5f; 
    [SerializeField] private float _screenPadding = 2f; 

    private const string TABLE_MENU = "MenuLabels";
    private const string TABLE_AFFIXES = "AffixesLabels";

    private VisualElement _root;
    private VisualElement _container;
    private Label _headerLabel;
    private VisualElement _headerDivider; 
    private VisualElement _statsContainer;

    // --- State для пересчета позиции ---
    private VisualElement _targetSlot;
    private InventoryItem _targetItem;

    // --- ЦВЕТА ---
    private readonly Color _colNormalText = new Color(0.8f, 0.8f, 0.8f); 
    private readonly Color _colModifiedText = new Color(0.53f, 0.53f, 1f); 
    
    private readonly Color _colFireText = new Color(1f, 0.5f, 0.5f); 
    private readonly Color _colColdText = new Color(0.5f, 0.6f, 1f); 
    private readonly Color _colLightningText = new Color(1f, 1f, 0.5f); 

    private readonly Color _colMagicBorder = new Color(0.3f, 0.3f, 0.7f); 
    private readonly Color _colRareText = new Color(1f, 1f, 0.46f); 
    private readonly Color _colRareBorder = new Color(0.7f, 0.6f, 0.2f); 
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
        _container.style.position = Position.Absolute;
        _container.style.width = _tooltipWidth;
        _container.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.95f)); 

        _container.style.borderTopWidth = 1; _container.style.borderBottomWidth = 1;
        _container.style.borderLeftWidth = 1; _container.style.borderRightWidth = 1;
        _container.style.paddingTop = 4; _container.style.paddingBottom = 4;
        _container.style.paddingLeft = 4; _container.style.paddingRight = 4;

        _container.style.display = DisplayStyle.None; 
        _container.pickingMode = PickingMode.Ignore; 
        _container.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        _container.style.fontSize = 8;
        _container.style.alignItems = Align.Center; 

        // ВАЖНО: Подписываемся на изменение геометрии. 
        // Это сработает, когда UI Toolkit рассчитает реальные размеры после появления.
        _container.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        _headerLabel = new Label();
        _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _headerLabel.style.whiteSpace = WhiteSpace.Normal;
        _headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _container.Add(_headerLabel);

        _headerDivider = new VisualElement();
        _headerDivider.style.height = 1;
        _headerDivider.style.width = Length.Percent(100);
        _headerDivider.style.marginTop = 2; _headerDivider.style.marginBottom = 2;
        _container.Add(_headerDivider);

        _statsContainer = new VisualElement();
        _statsContainer.style.width = Length.Percent(100);
        _statsContainer.style.alignItems = Align.Center; 
        _container.Add(_statsContainer);

        _root.Add(_container);
    }

    public void ShowTooltip(InventoryItem item, VisualElement slot)
    {
        if (_container == null || item == null || item.Data == null) return;

        // Сохраняем цели для пересчета
        _targetSlot = slot;
        _targetItem = item;

        // 1. Стиль рамки
        int affixCount = item.Affixes != null ? item.Affixes.Count : 0;
        Color borderColor = (affixCount >= 3) ? _colRareBorder : (affixCount > 0 ? _colMagicBorder : _colNormalText);
        Color nameColor = (affixCount >= 3) ? _colRareText : (affixCount > 0 ? _colModifiedText : _colNormalText);

        _container.style.borderTopColor = borderColor; _container.style.borderBottomColor = borderColor;
        _container.style.borderLeftColor = borderColor; _container.style.borderRightColor = borderColor;
        _headerDivider.style.backgroundColor = borderColor;
        _headerLabel.style.color = nameColor;
        _headerLabel.text = item.Data.ItemName;
        
        _statsContainer.Clear();

        // === 1. БАЗОВЫЕ СТАТЫ ===
        bool hasBase = false;

        if (item.Data is WeaponItemSO weapon)
        {
            float baseMin = weapon.MinPhysicalDamage;
            float baseMax = weapon.MaxPhysicalDamage;
            float finalMin = item.GetCalculatedStat(StatType.DamagePhysical, baseMin);
            float finalMax = item.GetCalculatedStat(StatType.DamagePhysical, baseMax);
            
            AddCalculatedRangeRow(StatType.DamagePhysical, finalMin, finalMax, baseMin, baseMax);

            CheckAndAddElementalRow(item, StatType.DamageFire, _colFireText);
            CheckAndAddElementalRow(item, StatType.DamageCold, _colColdText);
            CheckAndAddElementalRow(item, StatType.DamageLightning, _colLightningText);

            float baseCrit = weapon.BaseCritChance; 
            float finalCrit = item.GetCalculatedStat(StatType.CritChance, weapon.BaseCritChance);
            AddCalculatedStatRow(StatType.CritChance, finalCrit, baseCrit, "{0}%");

            float baseAps = weapon.AttacksPerSecond;
            float finalAps = item.GetCalculatedStat(StatType.AttackSpeed, baseAps);
            AddCalculatedStatRow(StatType.AttackSpeed, finalAps, baseAps, "{0}");

            hasBase = true;
        }
        else if (item.Data is ArmorItemSO armor)
        {
            if (armor.BaseArmor > 0)
                AddCalculatedStatRow(StatType.Armor, item.GetCalculatedStat(StatType.Armor, armor.BaseArmor), armor.BaseArmor);
            if (armor.BaseEvasion > 0)
                AddCalculatedStatRow(StatType.Evasion, item.GetCalculatedStat(StatType.Evasion, armor.BaseEvasion), armor.BaseEvasion);
            if (armor.BaseBubbles > 0)
                AddCalculatedStatRow(StatType.MaxBubbles, item.GetCalculatedStat(StatType.MaxBubbles, armor.BaseBubbles), armor.BaseBubbles);

            hasBase = true;
        }

        bool hasMods = (item.Data.ImplicitModifiers != null && item.Data.ImplicitModifiers.Count > 0) || affixCount > 0;
        if (hasBase && hasMods) AddDivider(borderColor);

        // === 2. ИМПЛИСИТЫ ===
        if (item.Data.ImplicitModifiers != null)
        {
            foreach (var mod in item.Data.ImplicitModifiers)
                AddSimpleStatRow(mod.Stat, mod.Value, mod.Type, _colImplicit);
        }

        if (item.Data.ImplicitModifiers != null && item.Data.ImplicitModifiers.Count > 0 && affixCount > 0)
            AddDivider(borderColor);

        // === 3. АФФИКСЫ ===
        if (item.Affixes != null)
        {
            foreach (var affix in item.Affixes)
            {
                if (affix.Modifiers.Count == 0) continue;
                string key = affix.Data.TranslationKey;
                float value = affix.Modifiers[0].Mod.Value;
                
                if (string.IsNullOrEmpty(key))
                {
                     var m = affix.Modifiers[0];
                     string suff = m.Mod.Type == StatModType.PercentAdd ? "Increase" : "Flat";
                     key = $"affix_{suff.ToLower()}_{m.Type.ToString().ToLower()}";
                }
                AddAffixRow(key, value, _colAffix);
            }
        }

        // --- ВАЖНОЕ ИЗМЕНЕНИЕ: СКРЫВАЕМ, ПОКА НЕ ПОСЧИТАЕМ ---
        // Делаем невидимым, но layout активным (Flex), чтобы движок посчитал ширину/высоту.
        _container.style.visibility = Visibility.Hidden; 
        _container.style.display = DisplayStyle.Flex;
        
        // Помечаем, что нужно перерисовать
        _container.MarkDirtyRepaint();
        
        // Запускаем принудительный пересчет на следующем кадре, на случай если Event не сработает
        _container.schedule.Execute(() => {
            UpdatePosition();
        }).ExecuteLater(1);
    }

    public void HideTooltip()
    {
        if (_container != null) 
        {
            _container.style.display = DisplayStyle.None;
            _targetSlot = null;
            _targetItem = null;
        }
    }

    // Этот метод вызывается автоматически, когда меняется размер тултипа (текст загрузился, layout сработал)
    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // Если размеры изменились (ширина или высота) и тултип активен
        if (_container.style.display == DisplayStyle.Flex && 
           (evt.oldRect.width != evt.newRect.width || evt.oldRect.height != evt.newRect.height))
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        if (_targetSlot == null || _targetItem == null) return;

        CalculateSmartPosition(_targetSlot, _targetItem);
        
        // Теперь, когда позиция верная, показываем
        _container.style.visibility = Visibility.Visible;
    }

    // ================= HELPER METHODS =================

    private void CheckAndAddElementalRow(InventoryItem item, StatType type, Color color)
    {
        float val = item.GetCalculatedStat(type, 0f);
        if (val > 0) CreateAsyncRow(type, val, color, "{0}");
    }

    private void AddCalculatedStatRow(StatType type, float finalVal, float baseVal, string format = "{0}")
    {
        bool isModified = Mathf.Abs(finalVal - baseVal) > 0.01f;
        Color valColor = isModified ? _colModifiedText : _colNormalText;
        CreateAsyncRow(type, finalVal, valColor, format);
    }

    private void AddCalculatedRangeRow(StatType type, float finalMin, float finalMax, float baseMin, float baseMax)
    {
        bool isModified = Mathf.Abs(finalMin - baseMin) > 0.01f || Mathf.Abs(finalMax - baseMax) > 0.01f;
        Color valColor = isModified ? _colModifiedText : _colNormalText;

        string key = $"stats.{type}";
        Label lbl = CreateLabel("...", valColor, false);
        _statsContainer.Add(lbl);

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_MENU, key);
        op.Completed += (h) =>
        {
            string name = h.Status == AsyncOperationStatus.Succeeded ? h.Result : type.ToString();
            lbl.text = $"{name}: {Mathf.Round(finalMin)}-{Mathf.Round(finalMax)}";
            // После загрузки текста размер может измениться -> вызываем апдейт
            UpdatePosition();
        };
    }

    private void CreateAsyncRow(StatType type, float value, Color valColor, string valueFormat)
    {
        string key = $"stats.{type}";
        Label lbl = CreateLabel("...", valColor, false);
        _statsContainer.Add(lbl);

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_MENU, key);
        op.Completed += (h) =>
        {
            string name = h.Status == AsyncOperationStatus.Succeeded ? h.Result : type.ToString();
            string valStr = string.Format(valueFormat, value); 
            lbl.text = $"{name}: {valStr}";
            UpdatePosition();
        };
    }

    private void AddSimpleStatRow(StatType type, float value, StatModType modType, Color color)
    {
        string key = $"stats.{type}";
        Label lbl = CreateLabel("...", color, false);
        _statsContainer.Add(lbl);

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_MENU, key);
        op.Completed += (h) =>
        {
            string name = h.Status == AsyncOperationStatus.Succeeded ? h.Result : type.ToString();
            string sign = (modType != StatModType.Flat || value < 0) ? "" : "+"; 
            string end = (modType == StatModType.PercentAdd || modType == StatModType.PercentMult) ? "%" : "";
            lbl.text = $"{name}: {sign}{value}{end}";
            UpdatePosition();
        };
    }

    private void AddAffixRow(string key, float value, Color color)
    {
        Label lbl = CreateLabel("...", color, false);
        _statsContainer.Add(lbl);

        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_AFFIXES, key, new object[] { value });
        op.Completed += (h) =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded) lbl.text = h.Result;
            else lbl.text = $"[{key}] {value}";
            UpdatePosition();
        };
    }

    private void AddDivider(Color color)
    {
        VisualElement div = new VisualElement();
        div.style.height = 1; div.style.width = Length.Percent(100);
        div.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.4f));
        div.style.marginTop = 2; div.style.marginBottom = 2;
        _statsContainer.Add(div);
    }

    private Label CreateLabel(string text, Color color, bool isBold)
    {
        Label lbl = new Label(text);
        lbl.style.color = new StyleColor(color);
        lbl.style.fontSize = 8;
        lbl.style.whiteSpace = WhiteSpace.Normal;
        lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
        lbl.style.paddingTop = 0; lbl.style.paddingBottom = 2;
        if (isBold) lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        return lbl;
    }

    private void CalculateSmartPosition(VisualElement slot, InventoryItem item)
    {
        if (_container == null || _root == null) return;
        
        float tipW = _container.resolvedStyle.width;
        float tipH = _container.resolvedStyle.height;
        if (float.IsNaN(tipW) || tipW < 10) tipW = _tooltipWidth;
        if (float.IsNaN(tipH) || tipH < 10) tipH = 50f; 

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;

        Rect r = slot.worldBound;
        Vector2 slotPos = _root.WorldToLocal(r.position);
        
        float itemPixelW = r.width * item.Data.Width;
        float itemPixelH = r.height * item.Data.Height;
        float centerX = slotPos.x + (itemPixelW / 2f);
        
        float tryY = slotPos.y - tipH - _gap;
        float finalX, finalY;

        if (tryY >= _screenPadding)
        {
            finalY = tryY;
            finalX = centerX - (tipW / 2f);
        }
        else
        {
            float spaceLeft = slotPos.x;
            float spaceRight = screenW - (slotPos.x + itemPixelW);

            if (spaceRight > spaceLeft) finalX = slotPos.x + itemPixelW + _gap;
            else finalX = slotPos.x - tipW - _gap;
            
            finalY = slotPos.y + (itemPixelH / 2f) - (tipH / 2f);
        }

        finalX = Mathf.Clamp(finalX, _screenPadding, screenW - tipW - _screenPadding);
        finalY = Mathf.Clamp(finalY, _screenPadding, screenH - tipH - _screenPadding);

        _container.style.left = finalX;
        _container.style.top = finalY;
    }
}