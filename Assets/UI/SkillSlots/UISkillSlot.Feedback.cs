using Scripts.Skills;
using Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

public partial class UISkillSlot
{
    private readonly SkillSlotFeedbackState _stateFeedback = new SkillSlotFeedbackState();
    private SkillBehaviour _feedbackRuntime;
    private Image _feedbackFrame;
    private Image[] _feedbackEdges;
    private SkillSlotCue _feedbackCue;
    private float _feedbackStarted, _feedbackUntil = float.NegativeInfinity;
    private bool _canAffordMana = true;

    public string FeedbackBinding => _inputText != null ? _inputText.text : "";

    public void ResetStateFeedback()
    {
        _stateFeedback.Reset(); _feedbackRuntime = null;
        _canAffordMana = true;
        _feedbackUntil = float.NegativeInfinity;
        if (_feedbackFrame != null) { _feedbackFrame.enabled = false; _feedbackFrame.gameObject.SetActive(false); }
        if (_manaCostText != null) _manaCostText.color = _manaTextColor;
    }

    public void BindFeedbackRuntime(SkillBehaviour runtime)
    {
        if (ReferenceEquals(_feedbackRuntime, runtime)) return;
        ResetStateFeedback(); _feedbackRuntime = runtime;
    }

    public SkillSlotCue NotifyCastResult(SkillBehaviour runtime, SkillCastResult result)
    {
        if (!ReferenceEquals(_feedbackRuntime, runtime) || runtime == null || runtime.Data != _skill) return SkillSlotCue.None;
        var cue = _stateFeedback.RecordAttempt(result, runtime.CooldownDuration, Time.unscaledTime);
        if (result == SkillCastResult.Accepted)
        {
            _feedbackUntil = float.NegativeInfinity;
            if (_feedbackFrame != null) { _feedbackFrame.enabled = false; _feedbackFrame.gameObject.SetActive(false); }
        }
        if (cue != SkillSlotCue.None) ShowStateCue(cue);
        return cue;
    }

    public bool ObserveFeedbackCooldown(float remaining, bool canAfford, bool casting, bool paused)
    {
        _canAffordMana = canAfford;
        if (!_stateFeedback.ObserveCooldown(remaining, canAfford, casting, paused)) return false;
        ShowStateCue(SkillSlotCue.Ready); return true;
    }

    public static Color StateCueColor(SkillSlotCue cue)
    {
        switch (cue)
        {
            case SkillSlotCue.Ready: return new Color(.7f, .95f, 1f);
            case SkillSlotCue.Cooldown: return new Color(1f, .73f, .28f);
            case SkillSlotCue.NoMana: return new Color(.35f, .7f, 1f);
            case SkillSlotCue.Busy: return new Color(.84f, .84f, .84f);
            default: return new Color(1f, .42f, .34f);
        }
    }

    private void ShowStateCue(SkillSlotCue cue)
    {
        if (_feedbackFrame == null)
        {
            var host = new GameObject("SkillStateFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            host.transform.SetParent(transform, false); host.transform.SetAsLastSibling();
            var rect = host.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one; rect.offsetMax = -Vector2.one;
            _feedbackFrame = host.GetComponent<Image>(); _feedbackFrame.sprite = GetRuntimeWhiteSprite(); _feedbackFrame.raycastTarget = false;
            _feedbackEdges = new Image[4];
            for (int i = 0; i < _feedbackEdges.Length; i++)
            {
                var edge = new GameObject("Edge" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                edge.transform.SetParent(host.transform, false);
                var r = edge.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
                if (i == 0) { r.anchorMin = new Vector2(0, 1); r.offsetMin = new Vector2(0, -1); }
                else if (i == 1) { r.anchorMax = new Vector2(1, 0); r.offsetMax = new Vector2(0, 1); }
                else if (i == 2) { r.anchorMax = new Vector2(0, 1); r.offsetMax = new Vector2(1, 0); }
                else { r.anchorMin = new Vector2(1, 0); r.offsetMin = new Vector2(-1, 0); }
                var image = edge.GetComponent<Image>(); image.sprite = GetRuntimeWhiteSprite(); image.raycastTarget = false; _feedbackEdges[i] = image;
            }
        }
        _feedbackCue = cue; _feedbackStarted = Time.unscaledTime;
        _feedbackUntil = _feedbackStarted + (cue == SkillSlotCue.Ready && !GameplayPresentationSettings.ReducedCombatEffects ? .45f : .3f);
        TickStateFeedback();
    }

    private void TickStateFeedback()
    {
        bool visible = !GamePauseService.IsPaused && Time.unscaledTime < _feedbackUntil;
        if (_feedbackFrame != null)
        {
            _feedbackFrame.enabled = visible;
            _feedbackFrame.gameObject.SetActive(visible);
            if (visible)
            {
                float alpha = Mathf.Clamp01((_feedbackUntil - Time.unscaledTime) / (_feedbackUntil - _feedbackStarted));
                if (GameplayPresentationSettings.ReducedCombatEffects) alpha *= .6f;
                var color = StateCueColor(_feedbackCue);
                _feedbackFrame.color = new Color(color.r, color.g, color.b, .025f * alpha);
                foreach (var edge in _feedbackEdges) edge.color = new Color(color.r, color.g, color.b, alpha);
            }
        }
        if (_manaCostText != null)
            _manaCostText.color = visible && _feedbackCue == SkillSlotCue.NoMana ? StateCueColor(_feedbackCue) :
                _canAffordMana ? _manaTextColor : new Color(.36f, .43f, .52f);
    }
}
