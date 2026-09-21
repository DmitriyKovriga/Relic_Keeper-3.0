/// <summary>
/// Tooltips remain pinned only while the dedicated lock input is held.
/// Without that input they follow ordinary hover ownership and hide immediately
/// after the pointer leaves the owner.
/// </summary>
public sealed class TooltipPinPolicy
{
    public bool IsVisible { get; private set; }
    public bool IsPinned { get; private set; }
    public bool JustPinned { get; private set; }
    public bool BlocksReplacement => IsPinned;

    public void Show()
    {
        IsVisible = true;
        IsPinned = false;
        JustPinned = false;
    }

    public void Hide()
    {
        IsVisible = false;
        IsPinned = false;
        JustPinned = false;
    }

    public bool Tick(bool lockHeld, bool overOwner, bool overTooltip, bool clickOutsideTooltip)
    {
        JustPinned = false;
        if (!IsVisible)
            return false;

        if (clickOutsideTooltip)
        {
            Hide();
            return true;
        }

        if (lockHeld)
        {
            if (!IsPinned)
            {
                IsPinned = true;
                JustPinned = true;
            }

            return false;
        }

        IsPinned = false;
        if (overOwner)
            return false;

        Hide();
        return true;
    }
}
