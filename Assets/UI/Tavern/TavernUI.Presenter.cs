using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

public partial class TavernUI
{
    private void BuildUI()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc?.rootVisualElement == null) return;

        var root = _uiDoc.rootVisualElement;
        root.Clear();
        UIFontApplier.ApplyToRoot(root);

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
        _hostelScrollView = new ScrollView(ScrollViewMode.Vertical);
        _hostelScrollView.style.flexGrow = 1;
        _hostelScrollView.style.minHeight = 0;
        _hostelScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _hostelScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        _hostelScrollView.style.marginTop = 1;
        _hostelScrollView.contentContainer.style.flexDirection = FlexDirection.Row;
        _hostelScrollView.contentContainer.style.flexWrap = Wrap.Wrap;
        _hostelScrollView.contentContainer.style.alignItems = Align.Stretch;
        _hostelScrollView.contentContainer.style.paddingRight = 2;
        _hostelListContainer = new VisualElement();
        _hostelListContainer.style.flexDirection = FlexDirection.Row;
        _hostelListContainer.style.flexWrap = Wrap.Wrap;
        _hostelListContainer.style.alignItems = Align.Stretch;
        _hostelListContainer.style.minHeight = 0;
        _hostelScrollView.Add(_hostelListContainer);
        _hostelContent.Add(_hostelScrollView);
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

        BuildDeleteDialog();

        _activeTabIndex = 1;
        ShowTab(1);
    }

    private void BuildDeleteDialog()
    {
        _deleteDialogOverlay = new VisualElement { name = "DeleteDialogOverlay" };
        _deleteDialogOverlay.style.position = Position.Absolute;
        _deleteDialogOverlay.style.left = 0;
        _deleteDialogOverlay.style.right = 0;
        _deleteDialogOverlay.style.top = 0;
        _deleteDialogOverlay.style.bottom = 0;
        _deleteDialogOverlay.style.justifyContent = Justify.Center;
        _deleteDialogOverlay.style.alignItems = Align.Center;
        _deleteDialogOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        _deleteDialogOverlay.style.display = DisplayStyle.None;
        _deleteDialogOverlay.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _deleteDialogOverlay)
                HideDeleteDialog();
        });
        _windowRoot.Add(_deleteDialogOverlay);

        var dialog = new VisualElement();
        dialog.style.width = 244;
        dialog.style.minHeight = 94;
        dialog.style.paddingLeft = 10;
        dialog.style.paddingRight = 10;
        dialog.style.paddingTop = 8;
        dialog.style.paddingBottom = 8;
        dialog.style.backgroundColor = new Color(0.12f, 0.09f, 0.08f, 0.98f);
        dialog.style.borderLeftWidth = dialog.style.borderRightWidth = dialog.style.borderTopWidth = dialog.style.borderBottomWidth = 2;
        dialog.style.borderLeftColor = dialog.style.borderRightColor = dialog.style.borderTopColor = dialog.style.borderBottomColor = new Color(0.46f, 0.36f, 0.24f);
        _deleteDialogOverlay.Add(dialog);

        _deleteDialogTitle = new Label("Delete Hero");
        _deleteDialogTitle.style.fontSize = 10;
        _deleteDialogTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        _deleteDialogTitle.style.color = new Color(0.92f, 0.82f, 0.64f);
        _deleteDialogTitle.style.marginBottom = 6;
        dialog.Add(_deleteDialogTitle);
        SetLocalizedLabel(_deleteDialogTitle, TavernLocKeys.DeleteTitle, "Delete Hero");

        _deleteDialogMessage = new Label();
        _deleteDialogMessage.style.fontSize = 8;
        _deleteDialogMessage.style.color = new Color(0.86f, 0.80f, 0.74f);
        _deleteDialogMessage.style.whiteSpace = WhiteSpace.Normal;
        _deleteDialogMessage.style.marginBottom = 8;
        dialog.Add(_deleteDialogMessage);

        var buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.justifyContent = Justify.FlexEnd;
        buttonsRow.style.alignItems = Align.Center;
        dialog.Add(buttonsRow);

        _deleteDialogCancelButton = new Button(HideDeleteDialog) { text = "Cancel" };
        SetLocalizedButton(_deleteDialogCancelButton, TavernLocKeys.Cancel, "Cancel");
        _deleteDialogCancelButton.style.width = 58;
        _deleteDialogCancelButton.style.height = 16;
        _deleteDialogCancelButton.style.fontSize = 8;
        _deleteDialogCancelButton.style.marginRight = 4;
        buttonsRow.Add(_deleteDialogCancelButton);

        _deleteDialogConfirmButton = new Button(OnDeleteDialogConfirmClicked) { text = "Delete" };
        _deleteDialogConfirmButton.style.width = 92;
        _deleteDialogConfirmButton.style.height = 16;
        _deleteDialogConfirmButton.style.fontSize = 8;
        _deleteDialogConfirmButton.style.backgroundColor = new Color(0.48f, 0.16f, 0.16f);
        buttonsRow.Add(_deleteDialogConfirmButton);
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
        if (_hostelScrollView != null)
            _hostelScrollView.scrollOffset = Vector2.zero;
        var hostel = CharacterPartyManager.Instance?.HostelCharacterIDs ?? new List<string>();
        foreach (var instanceId in hostel)
        {
            var saveData = CharacterPartyManager.Instance?.GetCharacterData(instanceId);
            var ch = _characterDB?.GetCharacterByID(saveData?.CharacterClassID);
            if (ch == null) continue;

            var card = CreateHeroCard(ch, isHire: false, isHostel: true, characterInstanceId: instanceId);
            _hostelListContainer.Add(card);
        }
    }

    private void ShowDeleteDialog(CharacterDataSO ch, string characterInstanceId)
    {
        _pendingDeleteCharacter = ch;
        _pendingDeleteCharacterInstanceId = characterInstanceId;
        _deleteNeedsFinalConfirmation = false;
        UpdateDeleteDialogText();
        if (_deleteDialogOverlay != null)
            _deleteDialogOverlay.style.display = DisplayStyle.Flex;
    }

    private void HideDeleteDialog()
    {
        _pendingDeleteCharacter = null;
        _pendingDeleteCharacterInstanceId = null;
        _deleteNeedsFinalConfirmation = false;
        if (_deleteDialogOverlay != null)
            _deleteDialogOverlay.style.display = DisplayStyle.None;
    }

    private void OnDeleteDialogConfirmClicked()
    {
        if (_pendingDeleteCharacter == null)
        {
            HideDeleteDialog();
            return;
        }

        if (!_deleteNeedsFinalConfirmation)
        {
            _deleteNeedsFinalConfirmation = true;
            UpdateDeleteDialogText();
            return;
        }

        OnDeleteHostelCharacterConfirmed(_pendingDeleteCharacter, _pendingDeleteCharacterInstanceId);
        HideDeleteDialog();
    }

    private void UpdateDeleteDialogText()
    {
        if (_deleteDialogMessage == null)
            return;

        string heroName = _pendingDeleteCharacter != null ? GetLocalizedName(_pendingDeleteCharacter) : "Hero";
        bool isRu = LocalizationSettings.SelectedLocale != null &&
                    LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ru");
        if (_deleteNeedsFinalConfirmation)
        {
            _deleteDialogMessage.text = isRu
                ? $"Удалить {heroName} навсегда? Инвентарь, уровень и прогресс этого героя будут удалены."
                : $"Delete {heroName} permanently? Inventory items and progress for this hero will be removed.";
            SetLocalizedButton(_deleteDialogConfirmButton, TavernLocKeys.DeleteFinalConfirm, "Confirm Delete");
            _deleteDialogConfirmButton.style.backgroundColor = new Color(0.60f, 0.12f, 0.12f);
        }
        else
        {
            _deleteDialogMessage.text = isRu
                ? $"Вы собираетесь удалить {heroName} из хостела. Все вещи в инвентаре этого героя тоже будут удалены."
                : $"You are about to remove {heroName} from the hostel. This also deletes all items in this hero's inventory.";
            SetLocalizedButton(_deleteDialogConfirmButton, TavernLocKeys.DeleteConfirm, "Delete Hero");
            _deleteDialogConfirmButton.style.backgroundColor = new Color(0.48f, 0.16f, 0.16f);
        }
    }
}
