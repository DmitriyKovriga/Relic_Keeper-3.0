using System.Collections.Generic;
using System.Linq;
using Scripts.Skills.PassiveTree;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

public partial class TavernUI
{
    private PassiveSkillTreeSO _savedTreeForRestore;
    private List<string> _savedAllocationsForRestore;

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
            img.style.width = 28;
            img.style.height = 28;
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
            op.Completed += _ =>
            {
                if (descLabel != null && descLabel.panel != null)
                    descLabel.text = op.Result;
            };
        }

        var btnRow = new VisualElement();
        btnRow.style.flexDirection = FlexDirection.Row;
        btnRow.style.marginTop = 1;
        btnRow.style.flexShrink = 0;

        if (ch.PassiveTree != null)
        {
            var treeBtn = new Button(() => ShowTreePreview(ch)) { text = "Tree" };
            SetLocalizedButton(treeBtn, TavernLocKeys.Tree, "Tree");
            treeBtn.style.fontSize = 8;
            treeBtn.style.width = 36;
            treeBtn.style.height = 14;
            treeBtn.style.marginRight = 2;
            btnRow.Add(treeBtn);
        }

        if (isHire)
        {
            var hireBtn = new Button(() => OnHireClicked(ch)) { text = "Hire" };
            SetLocalizedButton(hireBtn, TavernLocKeys.Hire, "Hire");
            hireBtn.style.fontSize = 9;
            hireBtn.style.width = 44;
            hireBtn.style.height = 16;
            hireBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            btnRow.Add(hireBtn);
        }
        else if (isHostel)
        {
            var swapBtn = new Button(() => OnSwapToHostelClicked(ch)) { text = "Swap" };
            SetLocalizedButton(swapBtn, TavernLocKeys.Swap, "Swap");
            swapBtn.style.fontSize = 9;
            swapBtn.style.width = 44;
            swapBtn.style.height = 16;
            swapBtn.style.backgroundColor = new Color(0.3f, 0.4f, 0.5f);
            btnRow.Add(swapBtn);
        }

        card.Add(btnRow);
        return card;
    }

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

        var chData = CharacterPartyManager.Instance?.GetCharacterData(ch.ID);
        if (chData?.AllocatedPassiveNodes != null && chData.AllocatedPassiveNodes.Count > 0)
            treeMgr.LoadState(chData.AllocatedPassiveNodes);

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
}
