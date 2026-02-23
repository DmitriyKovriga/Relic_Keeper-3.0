using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Skills;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Text;

public class ItemTooltipController : MonoBehaviour
{
    public static ItemTooltipController Instance { get; private set; }

    [Header("UI Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private Font _customFont;

    [Header("Layout Settings (Pixel Perfect)")]
    [SerializeField] private float _tooltipWidth = 150f; // Чуть уже (было 160)
    [SerializeField] private float _gap = 5f; 
    [SerializeField] private float _screenPadding = 4f; 
    
    [SerializeField, Tooltip("Задержка в миллисекундах перед скрытием тултипа (увеличена против мерцания при наведении на экипировку)")]
    private long _hideDelayMs = 180;
    
    private const float SLOT_SIZE = 24f; 

    // --- Localization Tables ---
    private const string TABLE_MENU = "MenuLabels";
    private const string TABLE_AFFIXES = "AffixesLabels";
    private const string TABLE_ITEMS = "ItemsLabels";
    private const string TABLE_SKILLS = "SkillsLabels";

    // --- UI Elements ---
    private VisualElement _root;
    
    // 1. Основной (Item)
    private VisualElement _itemTooltipBox;
    private Label _headerLabel;
    private VisualElement _headerDivider; 
    private VisualElement _statsContainer;

    // 2. Вторичный (Skill)
    private VisualElement _skillTooltipBox;

    // --- State ---
    private InventoryItem _currentTargetItem;
    private CraftingOrbSO _currentTargetOrb;
    private VisualElement _targetAnchorSlot;
    private IVisualElementScheduledItem _hideScheduler;

    // --- Orb Tooltip ---
    private VisualElement _orbTooltipBox;
    private Label _orbTitleLabel;
    private Label _orbDescLabel;

    // --- Colors ---
    private readonly Color _colBg = new Color(0.05f, 0.05f, 0.05f, 0.98f); 
    private readonly Color _colSkillBg = new Color(0.05f, 0.1f, 0.15f, 0.98f);
    
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

    private readonly Color _colSkillType = new Color(0.6f, 0.6f, 0.6f); 

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        // Тултип должен жить в том же UIDocument, что и инвентарь — иначе WorldToLocal даёт неверные координаты (другая панель).
        var inv = UnityEngine.Object.FindObjectOfType<InventoryUI>(true);
        if (inv != null && inv.RootVisualElement != null)
            _root = inv.RootVisualElement;
        else if (_uiDoc != null)
            _root = _uiDoc.rootVisualElement;
        UIFontApplier.ApplyToRoot(_root);
        if (_root != null)
            _root.schedule.Execute(RebuildTooltipStructure).ExecuteLater(50);

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        if (_currentTargetItem != null && _itemTooltipBox.style.display == DisplayStyle.Flex)
        {
            FillItemData(_currentTargetItem);
            FillSkillData(_currentTargetItem);
            _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
        }
        else if (_currentTargetOrb != null && _orbTooltipBox != null && _orbTooltipBox.style.display == DisplayStyle.Flex)
        {
            string nameKey = string.IsNullOrEmpty(_currentTargetOrb.NameKey) ? $"crafting_orb.{_currentTargetOrb.ID}.name" : _currentTargetOrb.NameKey;
            string descKey = string.IsNullOrEmpty(_currentTargetOrb.DescriptionKey) ? $"crafting_orb.{_currentTargetOrb.ID}.description" : _currentTargetOrb.DescriptionKey;
            LocalizeLabel(_orbTitleLabel, TABLE_MENU, nameKey, _currentTargetOrb.name);
            LocalizeLabel(_orbDescLabel, TABLE_MENU, descKey, "");
            _root.schedule.Execute(RecalculateOrbPosition).ExecuteLater(1);
        }
    }

    private void RebuildTooltipStructure()
    {
        // Тултип в том же корне, что и инвентарь — иначе позиция считается в другой панели и "ничего не меняется".
        var inv = UnityEngine.Object.FindObjectOfType<InventoryUI>(true);
        if (inv != null && inv.RootVisualElement != null)
            _root = inv.RootVisualElement;
        else if (_root == null && _uiDoc != null)
            _root = _uiDoc.rootVisualElement;
        if (_root == null) return;

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
        _skillTooltipBox.style.borderTopColor = new Color(0, 0.5f, 0.5f); 
        _skillTooltipBox.style.borderBottomColor = new Color(0, 0.5f, 0.5f);
        _skillTooltipBox.style.borderLeftColor = new Color(0, 0.5f, 0.5f); 
        _skillTooltipBox.style.borderRightColor = new Color(0, 0.5f, 0.5f);
        
        _root.Add(_skillTooltipBox);

        // --- 3. Orb Tooltip (crafting orbs) ---
        _orbTooltipBox = CreateContainer("GlobalOrbTooltip", _colSkillBg);
        _orbTooltipBox.style.borderTopColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTooltipBox.style.borderBottomColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTooltipBox.style.borderLeftColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTooltipBox.style.borderRightColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTitleLabel = CreateLabel("", 9, FontStyle.Bold, TextAnchor.MiddleCenter);
        _orbTitleLabel.style.color = new StyleColor(_colTitleRare);
        _orbDescLabel = CreateLabel("", 8, FontStyle.Normal, TextAnchor.MiddleCenter);
        _orbTooltipBox.Add(_orbTitleLabel);
        _orbTooltipBox.Add(CreateDivider());
        _orbTooltipBox.Add(_orbDescLabel);
        _root.Add(_orbTooltipBox);

        _itemTooltipBox.RegisterCallback<GeometryChangedEvent>(OnItemTooltipGeometryChangedOnce);
    }

    private void OnItemTooltipGeometryChangedOnce(GeometryChangedEvent evt)
    {
        _itemTooltipBox.UnregisterCallback<GeometryChangedEvent>(OnItemTooltipGeometryChangedOnce);
        if (_itemTooltipBox.style.display == DisplayStyle.Flex && _currentTargetItem != null)
            RecalculatePosition();
    }

    private VisualElement CreateContainer(string name, Color bg)
    {
        var el = new VisualElement { name = name };
        el.style.position = Position.Absolute;
        el.style.width = _tooltipWidth;
        el.style.backgroundColor = new StyleColor(bg);
        
        // C# совместимые бордеры
        el.style.borderTopWidth = 1; el.style.borderBottomWidth = 1;
        el.style.borderLeftWidth = 1; el.style.borderRightWidth = 1;
        
        // --- ИСПРАВЛЕНИЕ 1: УМЕНЬШИЛ ОТСТУПЫ (БЫЛО 4) ---
        el.style.paddingTop = 2; el.style.paddingBottom = 2;
        el.style.paddingLeft = 3; el.style.paddingRight = 3;
        
        el.style.visibility = Visibility.Hidden; 
        el.style.display = DisplayStyle.None;
        el.pickingMode = PickingMode.Ignore; 
        
        var resolvedFont = _customFont != null
            ? _customFont
            : UIFontResolver.ResolveUIToolkitFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        if (resolvedFont != null)
            el.style.unityFontDefinition = FontDefinition.FromFont(resolvedFont);
        
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
        // Уменьшил внутренние отступы лейблов
        lbl.style.paddingTop = 0; lbl.style.paddingBottom = 1;
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

    public void ShowOrbTooltip(CraftingOrbSO orb, VisualElement anchorSlot)
    {
        if (_orbTooltipBox == null || orb == null) return;
        if (_hideScheduler != null) { _hideScheduler.Pause(); _hideScheduler = null; }

        _currentTargetItem = null;
        _currentTargetOrb = orb;
        _targetAnchorSlot = anchorSlot;

        if (_itemTooltipBox != null) { _itemTooltipBox.style.display = DisplayStyle.None; }
        if (_skillTooltipBox != null) { _skillTooltipBox.style.display = DisplayStyle.None; }

        string nameKey = string.IsNullOrEmpty(orb.NameKey) ? $"crafting_orb.{orb.ID}.name" : orb.NameKey;
        string descKey = string.IsNullOrEmpty(orb.DescriptionKey) ? $"crafting_orb.{orb.ID}.description" : orb.DescriptionKey;
        _orbTitleLabel.text = orb.name;
        _orbDescLabel.text = "";

        LocalizeLabel(_orbTitleLabel, TABLE_MENU, nameKey, orb.name);
        LocalizeLabel(_orbDescLabel, TABLE_MENU, descKey, "");

        _orbTooltipBox.style.display = DisplayStyle.Flex;
        _orbTooltipBox.style.visibility = Visibility.Hidden;
        _orbTooltipBox.MarkDirtyRepaint();
        _root.schedule.Execute(RecalculateOrbPosition).ExecuteLater(1);
    }

    public void ShowTooltip(InventoryItem item, VisualElement anchorSlot)
    {
        if (_itemTooltipBox == null || item == null || item.Data == null) return;
        
        if (_hideScheduler != null)
        {
            _hideScheduler.Pause(); 
            _hideScheduler = null;
        }

        _currentTargetOrb = null;
        if (_orbTooltipBox != null) _orbTooltipBox.style.display = DisplayStyle.None;

        if (_currentTargetItem == item && _itemTooltipBox.style.display == DisplayStyle.Flex) return;

        _currentTargetItem = item;
        _targetAnchorSlot = anchorSlot;

        FillItemData(item);
        FillSkillData(item);

        _itemTooltipBox.style.display = DisplayStyle.Flex;
        _itemTooltipBox.style.visibility = Visibility.Hidden;

        bool hasSkill = _skillTooltipBox.userData != null; // userData "true" если есть скиллы
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
        RecalculatePosition();
        _root.schedule.Execute(RecalculatePosition).ExecuteLater(50);
    }

    /// <summary>
    /// Refreshes the item tooltip content and position if it is currently showing (e.g. after orb reroll).
    /// </summary>
    public void RefreshCurrentItemTooltip()
    {
        if (_currentTargetItem == null || _itemTooltipBox == null || _itemTooltipBox.style.display != DisplayStyle.Flex)
            return;
        FillItemData(_currentTargetItem);
        FillSkillData(_currentTargetItem);
        _itemTooltipBox.MarkDirtyRepaint();
        _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
    }

    public void HideTooltip()
    {
        if (_hideScheduler != null) return;

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
            if (_orbTooltipBox != null)
            {
                _orbTooltipBox.style.display = DisplayStyle.None;
                _orbTooltipBox.style.visibility = Visibility.Hidden;
            }
            _currentTargetItem = null;
            _currentTargetOrb = null;
            _targetAnchorSlot = null;
            _hideScheduler = null; 
        });
        
        _hideScheduler.ExecuteLater(_hideDelayMs);
    }

    // --- Positioning Logic ---

    private void RecalculatePosition()
    {
        if (_targetAnchorSlot == null || _currentTargetItem == null) return;

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;

        // Якорь = слот или иконка; границы предмета берём по worldBound якоря (для иконки это весь предмет)
        Rect r = _targetAnchorSlot.worldBound;
        Vector2 pMin = _root.WorldToLocal(r.min);
        Vector2 pMax = _root.WorldToLocal(r.max);
        float itemLeft = pMin.x;
        float itemRight = pMax.x;
        float itemCenterY = (pMin.y + pMax.y) * 0.5f;

        // Размеры тултипов
        float itemW = _itemTooltipBox.resolvedStyle.width;
        if (float.IsNaN(itemW) || itemW < 10) itemW = _tooltipWidth;
        float itemH = _itemTooltipBox.resolvedStyle.height;
        if (float.IsNaN(itemH) || itemH < 10) itemH = 100f;

        bool hasSkill = _skillTooltipBox.style.display == DisplayStyle.Flex;
        float skillW = hasSkill ? _skillTooltipBox.resolvedStyle.width : 0;
        if (hasSkill && (float.IsNaN(skillW) || skillW < 10)) skillW = _tooltipWidth;
        float skillH = hasSkill ? _skillTooltipBox.resolvedStyle.height : 0;
        if (hasSkill && (float.IsNaN(skillH) || skillH < 10)) skillH = 100f;

        float maxHeight = Mathf.Max(itemH, skillH);
        float finalItemX;
        float finalSkillX;
        float y;
        bool isEquipmentSlot = _targetAnchorSlot.userData is int sid && sid >= 100;

        if (isEquipmentSlot)
        {
            // Экипировка: тултип слева от предмета
            y = pMin.y;
            finalItemX = itemLeft - itemW - _gap;
            finalSkillX = hasSkill ? (finalItemX - _gap - skillW) : 0;
        }
        else
        {
            // Рюкзак/склад: сначала пробуем сверху (центр по горизонтали), иначе справа/слева (центр по вертикали)
            float itemTop = pMin.y;
            float itemCenterX = (itemLeft + itemRight) * 0.5f;
            float totalW = itemW + (hasSkill ? (_gap + skillW) : 0);
            float yAbove = itemTop - maxHeight - _gap;

            if (yAbove >= _screenPadding)
            {
                // Место сверху есть — тултип над предметом, центрирован по горизонтали
                y = yAbove;
                finalItemX = Mathf.Clamp(itemCenterX - itemW * 0.5f, _screenPadding, screenW - itemW - _screenPadding);
                finalSkillX = hasSkill ? Mathf.Clamp(finalItemX + itemW + _gap, _screenPadding, screenW - skillW - _screenPadding) : 0;
                if (hasSkill && finalSkillX + skillW > screenW - _screenPadding)
                    finalSkillX = Mathf.Clamp(finalItemX - _gap - skillW, _screenPadding, screenW - skillW - _screenPadding);
            }
            else
            {
                // Сверху не влезает — справа или слева, центрирован по вертикали
                y = itemCenterY - maxHeight * 0.5f;
                if (itemRight + totalW + _screenPadding <= screenW)
                {
                    finalItemX = itemRight + _gap;
                    finalSkillX = hasSkill ? (finalItemX + itemW + _gap) : 0;
                }
                else if (itemLeft - totalW - _screenPadding >= 0)
                {
                    finalItemX = itemLeft - itemW - _gap;
                    finalSkillX = hasSkill ? (finalItemX - _gap - skillW) : 0;
                }
                else
                {
                    finalItemX = itemRight + _gap;
                    finalSkillX = hasSkill ? Mathf.Clamp(finalItemX - _gap - skillW, _screenPadding, screenW - skillW - _screenPadding) : 0;
                }
            }
        }

        // Ограничение по вертикали
        if (y + maxHeight > screenH - _screenPadding)
            y = screenH - maxHeight - _screenPadding;
        if (y < _screenPadding)
            y = _screenPadding;
        finalItemX = Mathf.Clamp(finalItemX, _screenPadding, screenW - itemW - _screenPadding);

        // Скилл-тултип строго слева или справа: не перекрывать ни тултип предмета, ни иконку предмета (itemLeft..itemRight)
        if (hasSkill)
        {
            float zoneLeft = Mathf.Min(finalItemX, itemLeft);
            float zoneRight = Mathf.Max(finalItemX + itemW, itemRight);
            float skillRightX = Mathf.Max(finalItemX + itemW + _gap, itemRight + _gap);
            float skillLeftX = Mathf.Min(finalItemX - _gap - skillW, itemLeft - _gap - skillW);
            bool fitsRight = skillRightX + skillW <= screenW - _screenPadding;
            bool fitsLeft = skillLeftX >= _screenPadding;
            if (fitsRight)
                finalSkillX = skillRightX;
            else if (fitsLeft)
                finalSkillX = skillLeftX;
            else
            {
                float spaceRight = screenW - _screenPadding - skillRightX;
                float spaceLeft = skillLeftX - _screenPadding;
                if (spaceRight >= spaceLeft)
                    finalSkillX = Mathf.Clamp(skillRightX, skillRightX, screenW - skillW - _screenPadding);
                else
                    finalSkillX = Mathf.Clamp(skillLeftX, _screenPadding, skillLeftX);
            }
            finalSkillX = Mathf.Clamp(finalSkillX, _screenPadding, screenW - skillW - _screenPadding);
            // Жёстко: не заходить в зону предмета и тултипа (даже после clamp)
            if (finalSkillX + skillW > zoneLeft - _gap && finalSkillX < zoneRight + _gap)
            {
                if (finalSkillX >= zoneLeft)
                    finalSkillX = zoneRight + _gap;
                else
                    finalSkillX = zoneLeft - _gap - skillW;
                finalSkillX = Mathf.Clamp(finalSkillX, _screenPadding, screenW - skillW - _screenPadding);
            }
        }

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

    private void RecalculateOrbPosition()
    {
        if (_targetAnchorSlot == null || _currentTargetOrb == null || _orbTooltipBox == null) return;
        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;
        Rect r = _targetAnchorSlot.worldBound;
        Vector2 slotPos = _root.WorldToLocal(r.position);
        float orbW = _orbTooltipBox.resolvedStyle.width;
        if (float.IsNaN(orbW) || orbW < 10) orbW = _tooltipWidth;
        float orbH = _orbTooltipBox.resolvedStyle.height;
        if (float.IsNaN(orbH) || orbH < 10) orbH = 40f;
        float slotW = 32f;
        float x = slotPos.x + slotW + _gap;
        if (x + orbW + _screenPadding > screenW) x = slotPos.x - orbW - _gap;
        if (x < _screenPadding) x = _screenPadding;
        float y = slotPos.y;
        if (y + orbH > screenH - _screenPadding) y = screenH - orbH - _screenPadding;
        if (y < _screenPadding) y = _screenPadding;
        _orbTooltipBox.style.left = x;
        _orbTooltipBox.style.top = y;
        _orbTooltipBox.style.visibility = Visibility.Visible;
    }

    // --- Fill Data Logic (SKILLS) - ТВОЙ КОД ---

    private void FillSkillData(InventoryItem item)
    {
        _skillTooltipBox.Clear(); 
        bool hasSkill = item.GrantedSkills != null && item.GrantedSkills.Count > 0;
        _skillTooltipBox.userData = hasSkill ? "true" : null; // Маркер для ShowTooltip

        if (hasSkill)
        {
            for (int i = 0; i < item.GrantedSkills.Count; i++)
            {
                var skill = item.GrantedSkills[i];
                if (skill == null) continue;

                if (i > 0) 
                {
                    var div = CreateDivider();
                    div.style.marginTop = 4; div.style.marginBottom = 4;
                    _skillTooltipBox.Add(div);
                }

                // 1. Тип
                string slotKey = "skill_type_granted";
                if (item.Data is WeaponItemSO weapon)
                {
                    if (weapon.IsTwoHanded) slotKey = (i == 0) ? "skill_type_mainhand" : "skill_type_offhand";
                    else slotKey = "skill_type_weapon"; 
                }

                var typeLabel = CreateLabel("", 7, FontStyle.Italic, TextAnchor.UpperLeft);
                typeLabel.style.color = new StyleColor(_colSkillType);
                _skillTooltipBox.Add(typeLabel);
                LocalizeLabel(typeLabel, TABLE_MENU, slotKey, (i == 0 ? "Primary Action" : "Secondary Action"));

                // 2. Имя
                var nameLabel = CreateLabel("", 8, FontStyle.Bold, TextAnchor.MiddleCenter);
                nameLabel.style.color = new StyleColor(Color.cyan);
                nameLabel.style.marginTop = 2;
                _skillTooltipBox.Add(nameLabel);
                LocalizeLabel(nameLabel, TABLE_SKILLS, $"skills.{skill.ID}", skill.SkillName);

                // 3. Иконка
                if (skill.Icon != null)
                {
                    var icon = new Image();
                    icon.sprite = skill.Icon;
                    icon.style.width = 24; // Чуть меньше для компактности (было 32)
                    icon.style.height = 24;
                    icon.style.alignSelf = Align.Center;
                    icon.style.marginTop = 2;
                    _skillTooltipBox.Add(icon);
                }

                // 4. Описание
                var descLabel = CreateLabel("", 8, FontStyle.Normal, TextAnchor.UpperLeft);
                descLabel.style.marginTop = 4;
                _skillTooltipBox.Add(descLabel);
                LocalizeSkillBody(descLabel, skill);
            }
        }
    }

    private void LocalizeSkillBody(Label label, SkillDataSO skill)
    {
        label.text = skill.Description; 

        var opDesc = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_SKILLS, $"skills.{skill.ID}.description");
        opDesc.Completed += (hDesc) =>
        {
            if (label == null) return;
            StringBuilder sb = new StringBuilder();
            sb.Append(hDesc.Status == AsyncOperationStatus.Succeeded ? hDesc.Result : skill.Description);

            var opCD = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_SKILLS, "skills.cooldown");
            opCD.Completed += (hCD) =>
            {
                if (skill.Cooldown > 0)
                {
                    string cdLabel = hCD.Status == AsyncOperationStatus.Succeeded ? hCD.Result : "Cooldown";
                    sb.Append($"\n\n<color=#aaaaaa>{cdLabel}: {skill.Cooldown}s</color>");
                }

                var opMana = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_SKILLS, "skills.manaCost");
                opMana.Completed += (hMana) =>
                {
                    if (skill.ManaCost > 0)
                    {
                        string manaLabel = hMana.Status == AsyncOperationStatus.Succeeded ? hMana.Result : "Mana Cost";
                        sb.Append($"\n<color=#aaaaaa>{manaLabel}: {skill.ManaCost}</color>");
                    }

                    if (label != null) 
                    {
                        label.text = sb.ToString();
                        if (_root != null) 
                            _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
                    }
                };
            };
        };
    }

    // --- Fill Data Logic (ITEMS) ---

    private void FillItemData(InventoryItem item)
    {
        int affixes = item.Affixes != null ? item.Affixes.Count : 0;
        Color rarityCol = affixes >= 3 ? _colTitleRare : (affixes > 0 ? _colTitleMagic : _colTitleCommon);
        
        LocalizeLabel(_headerLabel, TABLE_ITEMS, $"items.{item.Data.ID}", item.Data.ItemName);
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

    // --- Helpers (ТВОЙ КОД) ---

    private void LocalizeLabel(Label label, string table, string key, string fallback)
    {
        label.text = fallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
        op.Completed += (h) => 
        {
            if (label == null) return;
            if (h.Status == AsyncOperationStatus.Succeeded && !IsMissingTranslationResult(h.Result))
                label.text = h.Result;
        };
    }

    private static bool IsMissingTranslationResult(string result)
    {
        return string.IsNullOrEmpty(result) || (result != null && result.Contains("No translation found"));
    }

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
