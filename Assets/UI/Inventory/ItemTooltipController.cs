using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Skills;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ItemTooltipController : MonoBehaviour
{
    public static ItemTooltipController Instance { get; private set; }

    [Header("UI Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private Font _customFont;

    [Header("Layout Settings")]
    [SerializeField] private float _tooltipWidth = 160f; 
    [SerializeField] private float _gap = 5f; 
    [SerializeField] private float _screenPadding = 2f; 
    
    [SerializeField, Tooltip("Задержка в миллисекундах перед скрытием тултипа")] 
    private long _hideDelayMs = 50;
    
    private const float SLOT_SIZE = 24f; 

    private const string TABLE_MENU = "MenuLabels";
    private const string TABLE_AFFIXES = "AffixesLabels";

    // --- UI Elements ---
    private VisualElement _root;
    
    private VisualElement _itemTooltipBox;
    private Label _headerLabel;
    private VisualElement _headerDivider; 
    private VisualElement _statsContainer;

    private VisualElement _skillTooltipBox;
    private Label _skillHeaderLabel;
    private Image _skillIconImage; 
    private Label _skillDescLabel;

    // --- State ---
    private InventoryItem _currentTargetItem;
    private VisualElement _targetAnchorSlot;
    
    // Переменная для таймера скрытия
    private IVisualElementScheduledItem _hideScheduler;

    // --- Colors ---
    private readonly Color _colBg = new Color(0.02f, 0.02f, 0.02f, 1f); 
    private readonly Color _colSkillBg = new Color(0.05f, 0.1f, 0.15f, 1f);
    private readonly Color _colNormalText = new Color(0.9f, 0.9f, 0.9f);
    private readonly Color _colModifiedText = new Color(0.5f, 0.6f, 1f);
    private readonly Color _colTitleCommon = Color.white;
    private readonly Color _colTitleMagic = new Color(0.3f, 0.3f, 1f); 
    private readonly Color _colTitleRare = new Color(1f, 1f, 0.4f); 
    private readonly Color _colMagicBorder = new Color(0.3f, 0.3f, 0.7f);
    private readonly Color _colRareBorder = new Color(0.7f, 0.6f, 0.2f);
    private readonly Color _colImplicit = new Color(0.6f, 0.8f, 1f);
    private readonly Color _colAffix = new Color(0.5f, 0.5f, 1f);
    private readonly Color _colFireText = new Color(1f, 0.5f, 0.5f);
    private readonly Color _colColdText = new Color(0.5f, 0.6f, 1f);
    private readonly Color _colLightningText = new Color(1f, 1f, 0.5f);

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
            _root.schedule.Execute(RebuildTooltipStructure).ExecuteLater(50);
        }
    }

    private void RebuildTooltipStructure()
    {
        var old1 = _root.Q<VisualElement>("GlobalItemTooltip");
        if (old1 != null) _root.Remove(old1);
        var old2 = _root.Q<VisualElement>("GlobalSkillTooltip");
        if (old2 != null) _root.Remove(old2);

        // --- 1. Item Tooltip ---
        _itemTooltipBox = CreateContainer("GlobalItemTooltip", _colBg);
        _headerLabel = CreateLabel("", 8, FontStyle.Bold, TextAnchor.MiddleCenter);
        _statsContainer = new VisualElement { style = { width = Length.Percent(100) } };
        
        _itemTooltipBox.Add(_headerLabel);
        _itemTooltipBox.Add(CreateDivider());
        _itemTooltipBox.Add(_statsContainer);
        _root.Add(_itemTooltipBox);

        // --- 2. Skill Tooltip ---
        _skillTooltipBox = CreateContainer("GlobalSkillTooltip", _colSkillBg);
        _skillTooltipBox.style.borderTopColor = Color.cyan; _skillTooltipBox.style.borderBottomColor = Color.cyan;
        _skillTooltipBox.style.borderLeftColor = Color.cyan; _skillTooltipBox.style.borderRightColor = Color.cyan;

        _skillHeaderLabel = CreateLabel("", 8, FontStyle.Bold, TextAnchor.MiddleCenter);
        _skillHeaderLabel.style.color = new StyleColor(Color.cyan);
        
        _skillIconImage = new Image();
        _skillIconImage.style.width = 32;
        _skillIconImage.style.height = 32;
        _skillIconImage.style.marginTop = 4;
        _skillIconImage.style.marginBottom = 4;
        _skillIconImage.style.alignSelf = Align.Center;
        
        _skillDescLabel = CreateLabel("", 8, FontStyle.Normal, TextAnchor.UpperLeft);
        
        _skillTooltipBox.Add(_skillHeaderLabel);
        _skillTooltipBox.Add(CreateDivider());
        _skillTooltipBox.Add(_skillIconImage);
        _skillTooltipBox.Add(_skillDescLabel);
        _root.Add(_skillTooltipBox);

        _itemTooltipBox.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private VisualElement CreateContainer(string name, Color bg)
    {
        var el = new VisualElement { name = name };
        el.style.position = Position.Absolute;
        el.style.width = _tooltipWidth;
        el.style.backgroundColor = new StyleColor(bg);
        el.style.borderTopWidth = 1; el.style.borderBottomWidth = 1;
        el.style.borderLeftWidth = 1; el.style.borderRightWidth = 1;
        el.style.paddingTop = 4; el.style.paddingBottom = 4;
        el.style.paddingLeft = 4; el.style.paddingRight = 4;
        
        el.style.visibility = Visibility.Hidden; 
        el.style.display = DisplayStyle.None;
        el.pickingMode = PickingMode.Ignore; 
        
        if (_customFont != null) 
            el.style.unityFontDefinition = FontDefinition.FromFont(_customFont);
        else 
        {
            var defFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defFont) el.style.unityFontDefinition = FontDefinition.FromFont(defFont);
        }
        
        el.style.fontSize = 8;
        el.style.alignItems = Align.Center; 
        return el;
    }

    private Label CreateLabel(string txt, int size, FontStyle style, TextAnchor align)
    {
        var lbl = new Label(txt);
        lbl.style.fontSize = size;
        lbl.style.unityFontStyleAndWeight = style;
        lbl.style.unityTextAlign = align;
        lbl.style.whiteSpace = WhiteSpace.Normal;
        lbl.style.color = new StyleColor(_colNormalText);
        return lbl;
    }

    private VisualElement CreateDivider()
    {
        var d = new VisualElement();
        d.style.height = 1;
        d.style.width = Length.Percent(100);
        d.style.marginTop = 2; d.style.marginBottom = 2;
        d.style.backgroundColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.5f));
        return d;
    }

    // --- Public API ---

    public void ShowTooltip(InventoryItem item, VisualElement anchorSlot)
    {
        if (_itemTooltipBox == null || item == null || item.Data == null) return;
        
        // --- ФИКС МОРГАНИЯ (ОТМЕНА СТАРОГО СКРЫТИЯ) ---
        if (_hideScheduler != null)
        {
            _hideScheduler.Pause(); // Отменяем запланированное скрытие
            _hideScheduler = null;
        }

        // Если тот же предмет, выходим.
        if (_currentTargetItem == item && _itemTooltipBox.style.display == DisplayStyle.Flex) return;

        _currentTargetItem = item;
        _targetAnchorSlot = anchorSlot;

        FillItemData(item);
        FillSkillData(item);

        _itemTooltipBox.style.display = DisplayStyle.Flex;
        _itemTooltipBox.style.visibility = Visibility.Hidden;

        bool hasSkill = _skillTooltipBox.userData != null;
        if (hasSkill)
        {
            _skillTooltipBox.style.display = DisplayStyle.Flex;
            _skillTooltipBox.style.visibility = Visibility.Hidden;
        }
        else
        {
            _skillTooltipBox.style.display = DisplayStyle.None;
        }

        _itemTooltipBox.MarkDirtyRepaint();
        _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
    }

    public void HideTooltip()
    {
        // --- ФИКС МОРГАНИЯ (ОТЛОЖЕННОЕ СКРЫТИЕ) ---
        // Если уже запланировано скрытие, не дублируем
        if (_hideScheduler != null) return;

        // Создаем таймер
        _hideScheduler = _root.schedule.Execute(() =>
        {
            if (_itemTooltipBox != null) 
            {
                _itemTooltipBox.style.display = DisplayStyle.None;
                _itemTooltipBox.style.visibility = Visibility.Hidden;
            }
            if (_skillTooltipBox != null) 
            {
                _skillTooltipBox.style.display = DisplayStyle.None;
                _skillTooltipBox.style.visibility = Visibility.Hidden;
            }
            _currentTargetItem = null;
            _targetAnchorSlot = null;
            _hideScheduler = null; // Очищаем ссылку после выполнения
        });
        
        // Запускаем через N мс
        _hideScheduler.ExecuteLater(_hideDelayMs);
    }

    // --- Positioning Logic ---

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (_itemTooltipBox.style.display == DisplayStyle.Flex)
        {
            RecalculatePosition();
        }
    }

    private void RecalculatePosition()
    {
        if (_targetAnchorSlot == null || _currentTargetItem == null) return;

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;

        Rect r = _targetAnchorSlot.worldBound;
        Vector2 slotPos = _root.WorldToLocal(r.position);
        
        float itemPhysicalWidth = _currentTargetItem.Data.Width * SLOT_SIZE;

        float itemRightEdge = slotPos.x + itemPhysicalWidth + _gap;
        float itemLeftEdge = slotPos.x - _gap;

        float itemW = _itemTooltipBox.resolvedStyle.width;
        if (float.IsNaN(itemW) || itemW < 10) itemW = _tooltipWidth;
        float itemH = _itemTooltipBox.resolvedStyle.height;

        bool hasSkill = _skillTooltipBox.style.display == DisplayStyle.Flex;
        float skillW = hasSkill ? _skillTooltipBox.resolvedStyle.width : 0;
        if (hasSkill && (float.IsNaN(skillW) || skillW < 10)) skillW = _tooltipWidth;

        float finalItemX, finalSkillX;
        float y = slotPos.y;

        float widthNeededRight = itemW + (hasSkill ? (_gap + skillW) : 0) + _screenPadding;
        
        if (itemRightEdge + widthNeededRight < screenW)
        {
            finalItemX = itemRightEdge;
            finalSkillX = finalItemX + itemW + _gap;
        }
        else
        {
            float widthNeededLeft = itemW + (hasSkill ? (_gap + skillW) : 0) + _screenPadding;
            if (itemLeftEdge - widthNeededLeft > 0)
            {
                finalItemX = itemLeftEdge - itemW;
                finalSkillX = finalItemX - _gap - skillW;
            }
            else
            {
                float spaceRight = screenW - itemRightEdge;
                float spaceLeft = itemLeftEdge;

                if (spaceRight > spaceLeft)
                {
                    finalItemX = itemRightEdge;
                    finalSkillX = itemLeftEdge - skillW;
                }
                else
                {
                    finalItemX = itemLeftEdge - itemW;
                    finalSkillX = itemRightEdge;
                }
            }
        }

        float maxHeight = Mathf.Max(itemH, hasSkill ? _skillTooltipBox.resolvedStyle.height : 0);
        if (y + maxHeight > screenH - _screenPadding)
        {
            y = screenH - maxHeight - _screenPadding;
        }
        if (y < _screenPadding) y = _screenPadding;

        _itemTooltipBox.style.left = finalItemX;
        _itemTooltipBox.style.top = y;
        _itemTooltipBox.style.visibility = Visibility.Visible;

        if (hasSkill)
        {
            _skillTooltipBox.style.left = finalSkillX;
            _skillTooltipBox.style.top = y;
            _skillTooltipBox.style.visibility = Visibility.Visible;
        }
    }

    // --- Fill Data Logic ---

    private void FillSkillData(InventoryItem item)
    {
        bool hasSkill = item.GrantedSkills != null && item.GrantedSkills.Count > 0 && item.GrantedSkills[0] != null;
        _skillTooltipBox.userData = hasSkill ? "true" : null;

        if (hasSkill)
        {
            var skill = item.GrantedSkills[0];
            _skillHeaderLabel.text = $"Grants Skill: {skill.SkillName}";
            
            _skillIconImage.sprite = skill.Icon;
            _skillIconImage.style.display = skill.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;
            
            string desc = skill.Description;
            if (skill.Cooldown > 0) desc += $"\n\n<color=#aaaaaa>Cooldown: {skill.Cooldown}s</color>";
            if (skill.ManaCost > 0) desc += $"\nMana Cost: {skill.ManaCost}</color>";
            
            _skillDescLabel.text = desc;
        }
    }

    private void FillItemData(InventoryItem item)
    {
        int affixes = item.Affixes != null ? item.Affixes.Count : 0;
        Color rarityCol = affixes >= 3 ? _colTitleRare : (affixes > 0 ? _colTitleMagic : _colTitleCommon);
        
        _headerLabel.text = item.Data.ItemName;
        _headerLabel.style.color = new StyleColor(rarityCol);
        
        Color borderCol = affixes >= 3 ? _colRareBorder : (affixes > 0 ? _colMagicBorder : Color.gray);
        _itemTooltipBox.style.borderTopColor = borderCol; _itemTooltipBox.style.borderBottomColor = borderCol;
        _itemTooltipBox.style.borderLeftColor = borderCol; _itemTooltipBox.style.borderRightColor = borderCol;

        _statsContainer.Clear();

        if (item.Data is WeaponItemSO weapon)
        {
            AddRow(StatType.DamagePhysical, item, weapon.MinPhysicalDamage, weapon.MaxPhysicalDamage);
            AddRow(StatType.DamageFire, item, weapon.MinFireDamage, weapon.MaxFireDamage, _colFireText);
            AddRow(StatType.DamageCold, item, weapon.MinColdDamage, weapon.MaxColdDamage, _colColdText);
            AddRow(StatType.DamageLightning, item, weapon.MinLightningDamage, weapon.MaxLightningDamage, _colLightningText);
            
            AddSimpleRow(StatType.AttackSpeed, item, weapon.AttacksPerSecond, "{0:F2}");
            AddSimpleRow(StatType.CritChance, item, weapon.BaseCritChance, "{0}%");
        }
        else if (item.Data is ArmorItemSO armor)
        {
            if (armor.BaseArmor > 0) AddSimpleRow(StatType.Armor, item, armor.BaseArmor);
            if (armor.BaseEvasion > 0) AddSimpleRow(StatType.Evasion, item, armor.BaseEvasion);
            if (armor.BaseBubbles > 0) AddSimpleRow(StatType.MaxBubbles, item, armor.BaseBubbles);
        }

        if (_statsContainer.childCount > 0) AddDivToContainer();

        if (item.Data.ImplicitModifiers != null)
        {
            foreach(var mod in item.Data.ImplicitModifiers)
                AddModRow(mod.Stat, mod.Value, mod.Type, _colImplicit);
        }

        if (item.Data.ImplicitModifiers != null && item.Data.ImplicitModifiers.Count > 0 && affixes > 0)
            AddDivToContainer();

        if (item.Affixes != null)
        {
            foreach(var aff in item.Affixes)
            {
                if(aff.Modifiers.Count == 0) continue;
                string key = aff.Data.TranslationKey;
                float val = aff.Modifiers[0].Mod.Value;
                if (string.IsNullOrEmpty(key)) key = $"stats.{aff.Modifiers[0].Type}";
                AddAffixRow(key, val, _colAffix);
            }
        }
    }

    // --- Helpers ---

    private void AddRow(StatType type, InventoryItem item, float min, float max, Color? c = null)
    {
        float fMin = item.GetCalculatedStat(type, min);
        float fMax = item.GetCalculatedStat(type, max);
        if (fMax <= 0) return;
        bool mod = Mathf.Abs(fMax - max) > 0.01f;
        CreateAsyncLabel(type.ToString(), (n) => $"{n}: {Mathf.Round(fMin)}-{Mathf.Round(fMax)}", c ?? (mod ? _colModifiedText : _colNormalText));
    }

    private void AddSimpleRow(StatType type, InventoryItem item, float baseVal, string fmt = "{0}")
    {
        float f = item.GetCalculatedStat(type, baseVal);
        CreateAsyncLabel(type.ToString(), (n) => $"{n}: {string.Format(fmt, f)}", Mathf.Abs(f - baseVal) > 0.01f ? _colModifiedText : _colNormalText);
    }

    private void AddModRow(StatType type, float val, StatModType mt, Color c)
    {
        string sign = (mt != StatModType.Flat || val < 0) ? "" : "+";
        string end = (mt != StatModType.Flat) ? "%" : "";
        CreateAsyncLabel($"stats.{type}", (n) => $"{n}: {sign}{val}{end}", c);
    }

    private void AddAffixRow(string key, float val, Color c)
    {
        var lbl = CreateLabel("...", 8, FontStyle.Normal, TextAnchor.MiddleCenter);
        lbl.style.color = new StyleColor(c);
        _statsContainer.Add(lbl);
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_AFFIXES, key, new object[]{ val });
        op.Completed += (h) => { if(lbl!=null) lbl.text = h.Result; };
    }

    private void CreateAsyncLabel(string key, System.Func<string, string> fmt, Color c)
    {
        var lbl = CreateLabel("...", 8, FontStyle.Normal, TextAnchor.MiddleCenter);
        lbl.style.color = new StyleColor(c);
        _statsContainer.Add(lbl);
        var k = key.Contains(".") ? key : $"stats.{key}";
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_MENU, k);
        op.Completed += (h) => { if(lbl!=null) lbl.text = fmt(h.Status == AsyncOperationStatus.Succeeded ? h.Result : key); };
    }

    private void AddDivToContainer()
    {
        var d = new VisualElement();
        d.style.height = 1;
        d.style.width = Length.Percent(100);
        d.style.marginTop = 2; d.style.marginBottom = 2;
        d.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
        _statsContainer.Add(d);
    }
}