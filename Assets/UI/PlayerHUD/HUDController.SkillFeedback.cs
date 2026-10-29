using Scripts.Skills;

public partial class HUDController
{
    private PlayerSkillManager _skillStateManager;

    private void BindSkillStateEvents()
    {
        if (_skillStateManager == _skillManager) return;
        UnbindSkillStateEvents(); _skillStateManager = _skillManager;
        if (_skillStateManager != null) _skillStateManager.OnSkillCastAttempted += OnSkillCastAttempted;
    }

    private void UnbindSkillStateEvents()
    {
        if (_skillStateManager != null) _skillStateManager.OnSkillCastAttempted -= OnSkillCastAttempted;
        _skillStateManager = null;
    }

    private void OnSkillCastAttempted(int index, SkillBehaviour runtime, SkillCastResult result)
    {
        if (!isActiveAndEnabled || _skillSlots == null || index < 0 || index >= _skillSlots.Length || _skillSlots[index] == null ||
            !ReferenceEquals(_skillManager.GetSkillRuntime(index), runtime)) return;
        var slot = _skillSlots[index]; slot.BindFeedbackRuntime(runtime);
        slot.NotifyCastResult(runtime, result);
    }
}
