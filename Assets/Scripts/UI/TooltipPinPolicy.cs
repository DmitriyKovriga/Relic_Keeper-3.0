/// <summary>
/// Unpinned tooltips hide the instant the cursor leaves the owner.
/// After 2s on the owner they pin. A pinned tooltip hides on click outside
/// its cluster, or after 2s away from both owner and cluster.
/// </summary>
public sealed class TooltipPinPolicy
{
    public const float PinAfterSeconds = 2f;
    public const float UnpinAwaySeconds = 2f;

    public bool IsVisible { get; private set; }
    public bool IsPinned { get; private set; }
    public bool JustPinned { get; private set; }
    public bool BlocksReplacement => IsPinned;

    public float PinProgress
    {
        get
        {
            if (!IsVisible)
                return 0f;
            if (IsPinned)
                return 1f;
            if (PinAfterSeconds <= 0f)
                return 1f;
            float progress = _ownerSeconds / PinAfterSeconds;
            if (progress < 0f)
                return 0f;
            if (progress > 1f)
                return 1f;
            return progress;
        }
    }

    private float _ownerSeconds;
    private float _awaySeconds;

    public void Show()
    {
        IsVisible = true;
        IsPinned = false;
        JustPinned = false;
        _ownerSeconds = 0f;
        _awaySeconds = 0f;
    }

    public void Hide()
    {
        IsVisible = false;
        IsPinned = false;
        JustPinned = false;
        _ownerSeconds = 0f;
        _awaySeconds = 0f;
    }

    public bool Tick(float dt, bool overOwner, bool overTooltip, bool clickOutsideTooltip)
    {
        JustPinned = false;
        if (!IsVisible)
            return false;

        if (dt < 0f)
            dt = 0f;

        if (IsPinned)
        {
            if (clickOutsideTooltip)
            {
                Hide();
                return true;
            }

            if (overOwner || overTooltip)
            {
                _awaySeconds = 0f;
                return false;
            }

            _awaySeconds += dt;
            if (_awaySeconds >= UnpinAwaySeconds)
            {
                Hide();
                return true;
            }

            return false;
        }

        if (overOwner)
        {
            _ownerSeconds += dt;
            if (_ownerSeconds >= PinAfterSeconds)
            {
                IsPinned = true;
                JustPinned = true;
                _awaySeconds = 0f;
            }

            return false;
        }

        Hide();
        return true;
    }
}
