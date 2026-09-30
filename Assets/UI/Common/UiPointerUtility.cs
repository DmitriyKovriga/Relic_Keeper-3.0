using UnityEngine;
using UnityEngine.UIElements;

public static class UiPointerUtility
{
    public static bool IsPointerOverHudShortcuts()
    {
        return HudShortcutBar.IsPointerOverBar();
    }

    /// <summary>
    /// Last laid-out rectangle in the ancestor's space. Reads cached layout only,
    /// so it is safe from Update and LateUpdate.
    /// </summary>
    public static Rect RectInAncestor(VisualElement element, VisualElement ancestor)
    {
        if (element == null)
            return default;

        Rect rect = element.layout;
        VisualElement parent = element.hierarchy.parent;
        while (parent != null && parent != ancestor)
        {
            rect.x += parent.layout.x;
            rect.y += parent.layout.y;
            parent = parent.hierarchy.parent;
        }

        return rect;
    }

    public static bool ContainsPanelPoint(VisualElement element, Vector2 panelPoint)
    {
        Rect rect = RectInAncestor(element, null);
        if (rect.width < 1f || rect.height < 1f)
            return false;

        return panelPoint.x >= rect.xMin && panelPoint.x <= rect.xMax
            && panelPoint.y >= rect.yMin && panelPoint.y <= rect.yMax;
    }
}
