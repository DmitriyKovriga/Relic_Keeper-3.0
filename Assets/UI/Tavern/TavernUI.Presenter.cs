using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class TavernUI
{
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
        _windowRoot.style.left = 0;
        _windowRoot.style.width = ScreenWidth;
        _windowRoot.style.top = 0;
        _windowRoot.style.height = ScreenHeight;
        _windowRoot.style.backgroundColor = new Color(0.08f, 0.06f, 0.05f, 0.95f);
        _windowRoot.style.display = DisplayStyle.None;
        root.Add(_windowRoot);

        _overlay = new VisualElement { name = "Overlay" };
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0;
        _overlay.style.width = ScreenWidth;
        _overlay.style.top = 0;
        _overlay.style.height = ScreenHeight;
        _overlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _overlay && !_isNewGameMode) Close();
        });
        _windowRoot.Add(_overlay);

        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = PanelMargin;
        panel.style.width = panelW;
        panel.style.top = PanelMargin;
        panel.style.height = panelH;
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.backgroundColor = new Color(0.15f, 0.12f, 0.1f, 1f);
        panel.style.borderLeftWidth = panel.style.borderRightWidth = panel.style.borderTopWidth = panel.style.borderBottomWidth = 2;
        panel.style.borderLeftColor = panel.style.borderRightColor = panel.style.borderTopColor = panel.style.borderBottomColor = new Color(0.4f, 0.35f, 0.25f);
        panel.style.paddingLeft = panel.style.paddingRight = panel.style.paddingTop = panel.style.paddingBottom = 4;
        _windowRoot.Add(panel);

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
        SetLocalizedLabel(title, TavernLocKeys.Title, "Tavern");

        var tabHostel = new Button(() => { _activeTabIndex = 0; ShowTab(0); UpdateTabStyles(); }) { text = "Hostel" };
        var tabRecruit = new Button(() => { _activeTabIndex = 1; ShowTab(1); UpdateTabStyles(); }) { text = "Recruit" };
        SetLocalizedButton(tabHostel, TavernLocKeys.Hostel, "Hostel");
        SetLocalizedButton(tabRecruit, TavernLocKeys.Recruit, "Recruit");
        _tabHostel = tabHostel;
        _tabRecruit = tabRecruit;
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
        _closeButton = new Button(Close) { text = "X" };
        SetLocalizedButton(_closeButton, TavernLocKeys.Close, "X");
        _closeButton.style.fontSize = 10;
        _closeButton.style.width = 20;
        _closeButton.style.height = 14;
        headerRow.Add(_closeButton);
        panel.Add(headerRow);

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
        SetLocalizedLabel(hireLabel, TavernLocKeys.PickOne, "Pick one:");
        _rerollButton = new Button(RerollHireChoices) { text = "Reroll" };
        SetLocalizedButton(_rerollButton, TavernLocKeys.Reroll, "Reroll");
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
        if (all.Count == 0)
        {
            PopulateHireChoices();
            return;
        }

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
            if (ch == null) continue;

            var card = CreateHeroCard(ch, isHire: false, isHostel: true);
            _hostelListContainer.Add(card);
        }
    }
}
