using UnityEngine;
using UnityEngine.UIElements;

public static class UIFontApplier
{
    public static bool ApplyToRoot(VisualElement root, Font explicitFont = null)
    {
        if (root == null)
            return false;

        var font = explicitFont ?? UIFontResolver.ResolveUIToolkitFont();
        if (font == null)
            return false;

        root.style.unityFontDefinition = FontDefinition.FromFont(font);
        return true;
    }
}
