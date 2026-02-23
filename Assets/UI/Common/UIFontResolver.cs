using TMPro;
using UnityEngine;

public static class UIFontResolver
{
    private static UIFontProfile _cachedProfile;

    public static UIFontProfile Profile
    {
        get
        {
            if (_cachedProfile == null)
                _cachedProfile = Resources.Load<UIFontProfile>(ProjectPaths.ResourcesUIFontProfile);
            return _cachedProfile;
        }
    }

    public static Font ResolveUIToolkitFont(Font fallback = null)
    {
        var profile = Profile;
        if (profile != null && profile.uiToolkitFont != null)
            return profile.uiToolkitFont;
        return fallback;
    }

    public static TMP_FontAsset ResolveTMPFontAsset(TMP_FontAsset fallback = null)
    {
        var profile = Profile;
        if (profile != null && profile.tmpFontAsset != null)
            return profile.tmpFontAsset;
        return fallback;
    }

    public static void InvalidateCache()
    {
        _cachedProfile = null;
    }
}
