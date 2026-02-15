using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;
using System.Linq;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;

public class TavernUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private CharacterDatabaseSO _characterDB;
    [SerializeField] private ItemDatabaseSO _itemDatabase;
    [Tooltip("Для кнопки Tree: окно дерева пассивок (WindowView на PassiveTree_Canvas)")]
    [SerializeField] private WindowView _passiveTreeWindow;
    [Tooltip("Для кнопки Tree: PassiveTreeManager на игроке")]
    [SerializeField] private PassiveTreeManager _passiveTreeManager;
    [Tooltip("Для интеграции с WindowManager (Escape, порядок окон). Если задан — открытие/закрытие через него.")]
    [SerializeField] private WindowView _windowView;

    public WindowView WindowView => _windowView;

    private const string MenuLabelsTable = "MenuLabels";
    private const int HireChoiceCount = 3;

    // Фиксированное разрешение 480x270 px
    private const int ScreenWidth = 480;
    private const int ScreenHeight = 270;
    private const int PanelMargin = 8;
    private const int CardWidth = 142;
    private const int CardGap = 4;

    private VisualElement _windowRoot;
    private VisualElement _overlay;
    private VisualElement _hireChoicesContainer;
    private VisualElement _hostelListContainer;
    private VisualElement _hostelContent;
    private VisualElement _recruitContent;
    private Button _tabHostel;
    private Button _tabRecruit;
    private Button _rerollButton;
    private Button _closeButton;
    private List<CharacterDataSO> _currentHireChoices = new List<CharacterDataSO>();
    private bool _isNewGameMode;
    private int _activeTabIndex; // 0 = Hostel, 1 = Recruitment

    public event System.Action OnClosed;
    public bool IsOpen => _windowRoot != null && _windowRoot.style.display == DisplayStyle.Flex;

    private void OnEnable()
    {
        if (_characterDB != null) _characterDB.Init();
        if (_windowView == null) _windowView = GetComponent<WindowView>();
        BuildUI();
        if (_windowView != null)
            _windowView.OnClosed += OnTavernWindowClosed;
    }

    private void OnDisable()
    {
        if (_windowView != null)
            _windowView.OnClosed -= OnTavernWindowClosed;
    }

    private void OnTavernWindowClosed() => OnClosed?.Invoke();

    public void Open(bool forNewGame = false)
    {
        _isNewGameMode = forNewGame;
        if (_closeButton != null)
            _closeButton.style.display = forNewGame ? DisplayStyle.None : DisplayStyle.Flex;
        RefreshHireChoices();
        RefreshHostelList();
        ShowTab(_activeTabIndex);
        if (_windowView != null)
            _windowView.Open();
        else
        {
            if (_windowRoot != null) _windowRoot.style.display = DisplayStyle.Flex;
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Disable();
        }
    }

    public void Close()
    {
        if (_windowView != null)
            _windowView.Close();
        else
        {
            if (_windowRoot != null) _windowRoot.style.display = DisplayStyle.None;
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Enable();
            OnClosed?.Invoke();
        }
    }

    private void BuildUI()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc?.rootVisualElement == null) return;

        var root = _uiDoc.rootVisualElement;
        root.Clear();

        int panelW = ScreenWidth - PanelMargin * 2;
        int panelH = ScreenHeight - PanelMargin * 2;

        _windowRoot = new VisualElement { name = "WindowRoot" };
        _windowRoot.style.position = Position.Absolute;
        _windowRoot.style.left = 0; _windowRoot.style.width = ScreenWidth;
        _windowRoot.style.top = 0; _windowRoot.style.height = ScreenHeight;
        _windowRoot.style.backgroundColor = new Color(0.08f, 0.06f, 0.05f, 0.95f);
        _windowRoot.style.display = DisplayStyle.None;
        root.Add(_windowRoot);

        _overlay = new VisualElement { name = "Overlay" };
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0; _overlay.style.width = ScreenWidth;
        _overlay.style.top = 0; _overlay.style.height = ScreenHeight;
        _overlay.RegisterCallback<ClickEvent>(evt => { if (evt.target == _overlay && !_isNewGameMode) Close(); });
        _windowRoot.Add(_overlay);

        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = PanelMargin; panel.style.width = panelW;
        panel.style.top = PanelMargin; panel.style.height = panelH;
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.backgroundColor = new Color(0.15f, 0.12f, 0.1f, 1f);
        panel.style.borderLeftWidth = panel.style.borderRightWidth = panel.style.borderTopWidth = panel.style.borderBottomWidth = 2;
        panel.style.borderLeftColor = panel.style.borderRightColor = panel.style.borderTopColor = panel.style.borderBottomColor = new Color(0.4f, 0.35f, 0.25f);
        panel.style.paddingLeft = panel.style.paddingRight = panel.style.paddingTop = panel.style.paddingBottom = 4;
        _windowRoot.Add(panel);

        // Компактная строка: Tavern | Hostel | Recruitment | Close
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.height = 14;
        headerRow.style.marginBottom = 2;

        var title = new Label("Tavern");
        title.style.fontSize = 9;
        title.style.color = new Color(0.9f, 0.8f, 0.6f);
        title.style.marginRight = 8;
        headerRow.Add(title);

        var tabHostel = new Button(() => { _activeTabIndex = 0; ShowTab(0); UpdateTabStyles(); }) { text = "Hostel" };
        var tabRecruit = new Button(() => { _activeTabIndex = 1; ShowTab(1); UpdateTabStyles(); }) { text = "Recruit" };
        _tabHostel = tabHostel; _tabRecruit = tabRecruit;
        foreach (var btn in new[] { tabHostel, tabRecruit })
        {
            btn.style.fontSize = 7;
            btn.style.width = 44;
            btn.style.height = 12;
            btn.style.marginRight = 2;
            btn.style.paddingLeft = btn.style.paddingRight = 2;
            headerRow.Add(btn);
        }

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        headerRow.Add(spacer);
        _closeButton = new Button(Close) { text = "×" };
        _closeButton.style.fontSize = 10;
        _closeButton.style.width = 20;
        _closeButton.style.height = 14;
        headerRow.Add(_closeButton);
        panel.Add(headerRow);

        // Контент Hostel — 90% под карточки
        _hostelContent = new VisualElement();
        _hostelContent.style.flexDirection = FlexDirection.Column;
        _hostelContent.style.flexGrow = 1;
        _hostelContent.style.minHeight = 0;
        _hostelListContainer = new VisualElement();
        _hostelListContainer.style.flexDirection = FlexDirection.Row;
        _hostelListContainer.style.flexWrap = Wrap.Wrap;
        _hostelListContainer.style.flexGrow = 1;
        _hostelListContainer.style.minHeight = 0;
        _hostelListContainer.style.alignItems = Align.Stretch;
        _hostelContent.Add(_hostelListContainer);
        panel.Add(_hostelContent);

        // Контент Recruitment — 90% под карточки
        _recruitContent = new VisualElement();
        _recruitContent.style.flexDirection = FlexDirection.Column;
        _recruitContent.style.flexGrow = 1;
        _recruitContent.style.minHeight = 0;
        var recruitTop = new VisualElement();
        recruitTop.style.flexDirection = FlexDirection.Row;
        recruitTop.style.justifyContent = Justify.SpaceBetween;
        recruitTop.style.alignItems = Align.Center;
        recruitTop.style.height = 14;
        recruitTop.style.marginBottom = 2;
        var hireLabel = new Label("Pick one:");
        hireLabel.style.fontSize = 8;
        hireLabel.style.color = new Color(0.85f, 0.75f, 0.55f);
        recruitTop.Add(hireLabel);
        _rerollButton = new Button(RerollHireChoices) { text = "Reroll" };
        _rerollButton.style.fontSize = 7;
        _rerollButton.style.width = 36;
        _rerollButton.style.height = 12;
        recruitTop.Add(_rerollButton);
        _recruitContent.Add(recruitTop);
        _hireChoicesContainer = new VisualElement();
        _hireChoicesContainer.style.flexDirection = FlexDirection.Row;
        _hireChoicesContainer.style.flexWrap = Wrap.NoWrap;
        _hireChoicesContainer.style.flexGrow = 1;
        _hireChoicesContainer.style.minHeight = 0;
        _hireChoicesContainer.style.alignItems = Align.Stretch;
        _recruitContent.Add(_hireChoicesContainer);
        panel.Add(_recruitContent);

        _activeTabIndex = 1;
        ShowTab(1);
    }

    private void ShowTab(int index)
    {
        _activeTabIndex = index;
        if (_hostelContent != null) _hostelContent.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (_recruitContent != null) _recruitContent.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        UpdateTabStyles();
    }

    private void UpdateTabStyles()
    {
        if (_tabHostel != null)
            _tabHostel.style.backgroundColor = _activeTabIndex == 0 ? new Color(0.35f, 0.3f, 0.22f) : new Color(0.2f, 0.18f, 0.15f);
        if (_tabRecruit != null)
            _tabRecruit.style.backgroundColor = _activeTabIndex == 1 ? new Color(0.35f, 0.3f, 0.22f) : new Color(0.2f, 0.18f, 0.15f);
    }

    private void RefreshHireChoices()
    {
        if (_currentHireChoices.Count == 0)
            RerollHireChoices();
        else
            PopulateHireChoices();
    }

    private void RerollHireChoices()
    {
        var all = _characterDB?.AllCharacters?.Where(c => c != null && !string.IsNullOrEmpty(c.ID)).ToList() ?? new List<CharacterDataSO>();
        _currentHireChoices.Clear();
        if (all.Count == 0) { PopulateHireChoices(); return; }
        // Разрешаем дубликаты, если персонажей меньше 3 — всегда показываем 3 карточки
        for (int i = 0; i < HireChoiceCount; i++)
        {
            int idx = Random.Range(0, all.Count);
            _currentHireChoices.Add(all[idx]);
        }
        PopulateHireChoices();
    }

    private void PopulateHireChoices()
    {
        _hireChoicesContainer.Clear();
        foreach (var ch in _currentHireChoices)
        {
            var card = CreateHeroCard(ch, isHire: true);
            _hireChoicesContainer.Add(card);
        }
    }

    private void RefreshHostelList()
    {
        _hostelListContainer.Clear();
        var hostel = CharacterPartyManager.Instance?.HostelCharacterIDs ?? new List<string>();
        foreach (var id in hostel)
        {
            var ch = _characterDB?.GetCharacterByID(id);
            if (ch != null)
            {
                var card = CreateHeroCard(ch, isHire: false, isHostel: true);
                _hostelListContainer.Add(card);
            }
        }
    }

    private VisualElement CreateHeroCard(CharacterDataSO ch, bool isHire, bool isHostel = false)
    {
        var card = new VisualElement();
        card.style.width = CardWidth;
        card.style.minWidth = CardWidth;
        card.style.flexGrow = 1;
        card.style.flexShrink = 0;
        card.style.marginRight = CardGap;
        card.style.marginBottom = 2;
        card.style.minHeight = 0;
        card.style.paddingLeft = card.style.paddingRight = 4;
        card.style.paddingTop = card.style.paddingBottom = 4;
        card.style.backgroundColor = new Color(0.2f, 0.18f, 0.15f, 1f);
        card.style.borderLeftWidth = card.style.borderRightWidth = card.style.borderTopWidth = card.style.borderBottomWidth = 1;
        card.style.borderLeftColor = card.style.borderRightColor = card.style.borderTopColor = card.style.borderBottomColor = new Color(0.45f, 0.4f, 0.3f);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexGrow = 1;
        row.style.minHeight = 0;

        if (ch.Portrait != null)
        {
            var img = new Image { sprite = ch.Portrait };
            img.style.width = 28; img.style.height = 28;
            img.style.marginRight = 4;
            img.style.flexShrink = 0;
            row.Add(img);
        }

        var col = new VisualElement();
        col.style.flexGrow = 1;
        col.style.flexShrink = 1;
        col.style.minWidth = 0;
        col.style.minHeight = 0;
        col.style.flexDirection = FlexDirection.Column;
        col.style.overflow = Overflow.Hidden;

        var nameLabel = new Label(GetLocalizedName(ch));
        nameLabel.style.fontSize = 10;
        nameLabel.style.color = Color.white;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.overflow = Overflow.Hidden;
        nameLabel.style.textOverflow = TextOverflow.Ellipsis;
        nameLabel.style.flexShrink = 0;
        col.Add(nameLabel);

        var statsScroll = new ScrollView(ScrollViewMode.Vertical);
        statsScroll.style.flexGrow = 1;
        statsScroll.style.minHeight = 0;
        statsScroll.style.marginTop = 1;
        statsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        statsScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        statsScroll.style.overflow = Overflow.Hidden;
        var statsContent = new VisualElement();
        statsContent.style.flexDirection = FlexDirection.Column;
        foreach (var line in FormatStartingStatsLines(ch))
        {
            var statLabel = new Label(line);
            statLabel.style.fontSize = 7;
            statLabel.style.color = new Color(0.9f, 0.8f, 0.5f);
            statLabel.style.overflow = Overflow.Hidden;
            statLabel.style.textOverflow = TextOverflow.Ellipsis;
            statLabel.style.whiteSpace = WhiteSpace.Normal;
            statLabel.style.marginTop = 0;
            statLabel.style.marginBottom = 0;
            statLabel.style.paddingTop = 0;
            statLabel.style.paddingBottom = 0;
            statsContent.Add(statLabel);
        }
        statsScroll.Add(statsContent);
        col.Add(statsScroll);

        row.Add(col);
        card.Add(row);

        var descLabel = new Label(GetLocalizedDescription(ch));
        descLabel.style.fontSize = 7;
        descLabel.style.color = new Color(0.75f, 0.7f, 0.6f);
        descLabel.style.overflow = Overflow.Hidden;
        descLabel.style.textOverflow = TextOverflow.Ellipsis;
        descLabel.style.marginTop = 1;
        descLabel.style.marginBottom = 1;
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.flexShrink = 0;
        card.Add(descLabel);
        if (!string.IsNullOrEmpty(ch.DescriptionKey))
        {
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, ch.DescriptionKey);
            op.Completed += _ => { if (descLabel != null && descLabel.panel != null) descLabel.text = op.Result; };
        }

        var btnRow = new VisualElement();
        btnRow.style.flexDirection = FlexDirection.Row;
        btnRow.style.marginTop = 1;
        btnRow.style.flexShrink = 0;

        if (ch.PassiveTree != null)
        {
            var treeBtn = new Button(() => ShowTreePreview(ch)) { text = "Tree" };
            treeBtn.style.fontSize = 8;
            treeBtn.style.width = 36;
            treeBtn.style.height = 14;
            treeBtn.style.marginRight = 2;
            btnRow.Add(treeBtn);
        }

        if (isHire)
        {
            var hireBtn = new Button(() => OnHireClicked(ch)) { text = "Hire" };
            hireBtn.style.fontSize = 9;
            hireBtn.style.width = 44;
            hireBtn.style.height = 16;
            hireBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            btnRow.Add(hireBtn);
        }
        else if (isHostel)
        {
            var swapBtn = new Button(() => OnSwapToHostelClicked(ch)) { text = "Swap" };
            swapBtn.style.fontSize = 9;
            swapBtn.style.width = 44;
            swapBtn.style.height = 16;
            swapBtn.style.backgroundColor = new Color(0.3f, 0.4f, 0.5f);
            btnRow.Add(swapBtn);
        }

        card.Add(btnRow);
        return card;
    }

    private static string FormatStatName(StatType type)
    {
        var s = type.ToString();
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]))
                sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private IEnumerable<string> FormatStartingStatsLines(CharacterDataSO ch)
    {
        if (ch.StartingStats == null || ch.StartingStats.Count == 0) yield break;
        foreach (var s in ch.StartingStats)
        {
            string name = FormatStatName(s.Type);
            yield return $"{name}: {s.Value}";
        }
    }

    private string GetLocalizedName(CharacterDataSO ch)
    {
        if (string.IsNullOrEmpty(ch.NameKey)) return ch.DisplayName;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, ch.NameKey);
        return op.IsDone ? op.Result : ch.DisplayName;
    }

    private string GetLocalizedDescription(CharacterDataSO ch)
    {
        if (string.IsNullOrEmpty(ch.DescriptionKey)) return ch.DescriptionFallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, ch.DescriptionKey);
        return op.IsDone ? op.Result : ch.DescriptionFallback;
    }

    private string FormatStartingStats(CharacterDataSO ch)
    {
        if (ch.StartingStats == null || ch.StartingStats.Count == 0) return "";
        var parts = ch.StartingStats.Select(s => $"+{s.Value} {s.Type}").Take(5);
        return string.Join(", ", parts);
    }

    private PassiveSkillTreeSO _savedTreeForRestore;
    private List<string> _savedAllocationsForRestore;

    private void ShowTreePreview(CharacterDataSO ch)
    {
        if (ch.PassiveTree == null) return;
        var treeMgr = _passiveTreeManager != null ? _passiveTreeManager : FindFirstObjectByType<PassiveTreeManager>();
        var windowMgr = FindFirstObjectByType<WindowManager>();
        var treeWindow = _passiveTreeWindow;
        if (treeWindow == null)
        {
            var toggle = FindFirstObjectByType<PassiveTreeWindowToggle>();
            if (toggle != null) treeWindow = toggle.SkillTreeWindow;
        }
        if (treeMgr == null || windowMgr == null || treeWindow == null) return;
        _savedTreeForRestore = treeMgr.TreeData;
        _savedAllocationsForRestore = treeMgr.GetSaveData();
        treeMgr.IsPreviewMode = true;
        treeMgr.SetTreeData(ch.PassiveTree);
        treeWindow.OnClosed -= RestoreActiveCharacterTree;
        treeWindow.OnClosed += RestoreActiveCharacterTree;
        if (!windowMgr.IsOpen(treeWindow))
            windowMgr.OpenWindow(treeWindow);
    }

    private void RestoreActiveCharacterTree()
    {
        var treeWindow = _passiveTreeWindow ?? FindFirstObjectByType<PassiveTreeWindowToggle>()?.SkillTreeWindow;
        if (treeWindow != null) treeWindow.OnClosed -= RestoreActiveCharacterTree;
        var treeMgr = _passiveTreeManager != null ? _passiveTreeManager : FindFirstObjectByType<PassiveTreeManager>();
        if (treeMgr == null) return;
        if (_savedTreeForRestore != null)
        {
            treeMgr.IsPreviewMode = false;
            treeMgr.SetTreeData(_savedTreeForRestore);
            if (_savedAllocationsForRestore != null)
                treeMgr.LoadState(_savedAllocationsForRestore);
        }
        else
        {
            treeMgr.IsPreviewMode = false;
        }
        _savedTreeForRestore = null;
        _savedAllocationsForRestore = null;
    }

    private void OnHireClicked(CharacterDataSO ch)
    {
        if (ch == null || string.IsNullOrEmpty(ch.ID))
        {
            Debug.LogWarning("[Tavern] Hire: персонаж без ID, пропуск.");
            return;
        }
        if (CharacterPartyManager.Instance == null)
        {
            Debug.LogWarning("[Tavern] Hire: CharacterPartyManager не найден.");
            var saveMgr = FindObjectOfType<GameSaveManager>();
            if (saveMgr != null) saveMgr.SaveGame();
            return;
        }
        if (_characterDB == null || _itemDatabase == null)
        {
            Debug.LogWarning("[Tavern] Hire: Character DB или Item DB не назначены.");
            return;
        }

        CharacterPartyManager.Instance.AddCharacterToParty(ch.ID);
        // Всегда переключаемся на нанятого: текущий уходит в хостел, новый становится активным (чистый инвентарь, статы, дерево)
        if (!CharacterPartyManager.Instance.SwapToCharacter(ch.ID, _characterDB, _itemDatabase))
        {
            Debug.LogWarning($"[Tavern] Hire: SwapToCharacter не удался для {ch.ID}. Проверьте, что персонаж есть в Character Database.");
            return;
        }

        RerollHireChoices();
        Close();
    }

    private void OnSwapToHostelClicked(CharacterDataSO ch)
    {
        if (CharacterPartyManager.Instance == null) return;
        CharacterPartyManager.Instance.SwapToCharacter(ch.ID, _characterDB, _itemDatabase);
        Close();
    }
}
