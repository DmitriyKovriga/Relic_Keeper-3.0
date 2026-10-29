using Scripts.Skills;
using Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public partial class HUDController
{
    private PlayerSkillManager _skillStateManager;
    private Image _skillStateBadge;
    private TextMeshProUGUI _skillStateCaption;
    private SkillBehaviour _skillStateSource;
    private SkillSlotCue _skillStateCue;
    private int _skillStateSlot;
    private float _skillStateUntil = float.NegativeInfinity;

    private void BindSkillStateEvents()
    {
        if (_skillStateManager == _skillManager) return;
        UnbindSkillStateEvents(); _skillStateManager = _skillManager;
        if (_skillStateManager != null) _skillStateManager.OnSkillCastAttempted += OnSkillCastAttempted;
        LocalizationSettings.SelectedLocaleChanged += OnSkillStateLocaleChanged;
    }

    private void UnbindSkillStateEvents()
    {
        if (_skillStateManager != null) _skillStateManager.OnSkillCastAttempted -= OnSkillCastAttempted;
        _skillStateManager = null;
        LocalizationSettings.SelectedLocaleChanged -= OnSkillStateLocaleChanged;
    }

    private void OnSkillCastAttempted(int index, SkillBehaviour runtime, SkillCastResult result)
    {
        if (!isActiveAndEnabled || _skillSlots == null || index < 0 || index >= _skillSlots.Length || _skillSlots[index] == null ||
            !ReferenceEquals(_skillManager.GetSkillRuntime(index), runtime)) return;
        var slot = _skillSlots[index]; slot.BindFeedbackRuntime(runtime);
        var cue = slot.NotifyCastResult(runtime, result);
        if (result == SkillCastResult.Accepted && _skillStateSlot == index) ClearSkillStateMessage();
        if (cue != SkillSlotCue.None) ShowSkillStateMessage(index, runtime, cue);
    }

    private void ShowSkillStateMessage(int index, SkillBehaviour runtime, SkillSlotCue cue)
    {
        if (_skillSlots == null || index < 0 || index >= _skillSlots.Length || _skillSlots[index] == null) return;
        if (_skillStateBadge == null)
        {
            var host = new GameObject("SkillStateMessage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            host.transform.SetParent(_skillSlots[index].transform.parent, false);
            host.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = host.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -10); rect.sizeDelta = new Vector2(150, 11);
            _skillStateBadge = host.GetComponent<Image>(); _skillStateBadge.color = new Color(.04f, .05f, .06f, .88f); _skillStateBadge.raycastTarget = false;
            var textHost = new GameObject("Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); textHost.transform.SetParent(host.transform, false);
            var textRect = textHost.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2, 0); textRect.offsetMax = new Vector2(-2, 0);
            _skillStateCaption = textHost.GetComponent<TextMeshProUGUI>(); _skillStateCaption.font = UIFontResolver.ResolveTMPFontAsset();
            _skillStateCaption.fontSize = 7; _skillStateCaption.alignment = TextAlignmentOptions.Center; _skillStateCaption.enableAutoSizing = false;
            _skillStateCaption.textWrappingMode = TextWrappingModes.NoWrap; _skillStateCaption.overflowMode = TextOverflowModes.Ellipsis; _skillStateCaption.raycastTarget = false;
        }
        _skillStateSource = runtime; _skillStateSlot = index; _skillStateCue = cue;
        _skillStateUntil = Time.unscaledTime + .65f;
        _skillStateBadge.gameObject.SetActive(true); RefreshSkillStateCaption();
    }

    private void RefreshSkillStateCaption()
    {
        if (_skillStateCaption == null || _skillSlots == null || _skillStateSlot >= _skillSlots.Length) return;
        _skillStateCaption.text = SkillSlotFeedbackState.Caption(_skillStateCue, _skillSlots[_skillStateSlot].FeedbackBinding);
        _skillStateCaption.color = UISkillSlot.StateCueColor(_skillStateCue);
    }
    private void OnSkillStateLocaleChanged(Locale _) => RefreshSkillStateCaption();
    private void ClearSkillStateMessage()
    {
        _skillStateUntil = float.NegativeInfinity;
        if (_skillStateBadge != null) _skillStateBadge.gameObject.SetActive(false);
    }
    private void TickSkillStateMessage()
    {
        if (_skillStateBadge == null) return;
        if (GamePauseService.IsPaused || Time.unscaledTime >= _skillStateUntil || _skillManager == null ||
            !ReferenceEquals(_skillManager.GetSkillRuntime(_skillStateSlot), _skillStateSource)) ClearSkillStateMessage();
    }
}
