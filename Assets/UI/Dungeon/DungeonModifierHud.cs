using System.Collections.Generic;
using System.Text;
using Scripts.Dungeon;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DungeonModifierHud : MonoBehaviour
{
    private const float SortingOrder = 500f;
    public const int PanelWidth = 112;
    public const int PanelPaddingLeft = 4;
    public const int PanelPaddingRight = 2;
    public const int ModifierFontSize = 5;
    public const int ModifierLineHeight = 7;
    public const int MaxCharactersPerLine = 34;

    private static readonly Color PanelBackground = new Color(0.055f, 0.045f, 0.035f, 0.52f);
    private static readonly Color BorderColor = new Color(0.43f, 0.33f, 0.19f, 0.45f);
    private static readonly Color HeaderColor = new Color(0.91f, 0.76f, 0.45f, 1f);
    private static readonly Color TextColor = new Color(0.88f, 0.84f, 0.76f, 1f);
    private static DungeonModifierHud _instance;

    private UIDocument _document;
    private VisualElement _panel;
    private Label _location;
    private Label _room;
    private VisualElement _modifierList;

    public static DungeonModifierHud GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("DungeonModifierHUD");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<DungeonModifierHud>();
        _instance._document = document;
        host.SetActive(true);
        _instance.Build();
        return _instance;
    }

    public void Show(
        string locationName,
        int roomIndex,
        int roomCount,
        int roomLevel,
        IReadOnlyList<string> globalModifiers,
        IReadOnlyList<string> localModifiers)
    {
        Build();
        if (_panel == null)
            return;

        _location.text = string.IsNullOrWhiteSpace(locationName) ? "Подземелье" : locationName;
        _room.text = $"Комната {roomIndex}/{Mathf.Max(roomIndex, roomCount)}  •  Уровень {roomLevel}";
        _modifierList.Clear();
        AddGroup(null, globalModifiers, true);
        AddGroup("ЭТА КОМНАТА", localModifiers, false);
        float contentHeight = 23f + GetGroupHeight(globalModifiers, false) + GetGroupHeight(localModifiers, true);
        _panel.style.height = contentHeight;
        _panel.style.minHeight = contentHeight;
        _panel.style.maxHeight = contentHeight;
        _panel.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.style.display = DisplayStyle.None;
    }

    private void AddGroup(string title, IReadOnlyList<string> values, bool isGlobal)
    {
        if (values == null || values.Count == 0)
            return;

        bool showHeader = !string.IsNullOrWhiteSpace(title);
        DungeonUIPresentationSO presentation = DungeonUIPresentationSO.Load();
        var section = new VisualElement
        {
            name = isGlobal ? "GlobalModifiersSection" : "RoomModifiersSection",
            pickingMode = PickingMode.Ignore
        };
        section.style.marginTop = showHeader ? 2 : 1;
        section.style.marginBottom = 0;
        section.style.marginLeft = 0;
        section.style.marginRight = 0;
        section.style.paddingLeft = 0;
        section.style.paddingRight = 0;
        section.style.paddingTop = 0;
        section.style.paddingBottom = 0;
        section.style.flexGrow = 0;
        section.style.flexShrink = 0;
        section.style.justifyContent = Justify.FlexStart;
        AddDecorativeSprite(section, "SectionFrameArt", presentation != null ? presentation.HudSectionFrame : null);
        _modifierList.Add(section);

        if (showHeader)
        {
            var header = new Label(title);
            header.name = "SectionTitle";
            header.pickingMode = PickingMode.Ignore;
            header.style.fontSize = 5;
            header.style.height = 6;
            header.style.minHeight = 6;
            header.style.maxHeight = 6;
            header.style.flexGrow = 0;
            header.style.flexShrink = 0;
            ResetSpacing(header);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.unityTextAlign = TextAnchor.MiddleRight;
            header.style.color = HeaderColor;
            header.style.opacity = 0.82f;
            section.Add(header);
        }

        for (int i = 0; i < values.Count; i++)
        {
            string wrappedText = WrapForHud(values[i], out int lineCount);
            float rowHeight = lineCount * ModifierLineHeight;
            var label = new Label(wrappedText) { pickingMode = PickingMode.Ignore };
            label.name = "ModifierEntry";
            label.style.fontSize = ModifierFontSize;
            label.style.height = rowHeight;
            label.style.minHeight = rowHeight;
            label.style.maxHeight = rowHeight;
            label.style.flexGrow = 0;
            label.style.flexShrink = 0;
            ResetSpacing(label);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.overflow = Overflow.Hidden;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            label.style.color = TextColor;
            section.Add(label);
        }
    }

    private static float GetGroupHeight(IReadOnlyList<string> values, bool showHeader)
    {
        if (values == null || values.Count == 0)
            return 0f;

        int lineCount = 0;
        for (int i = 0; i < values.Count; i++)
        {
            WrapForHud(values[i], out int entryLines);
            lineCount += entryLines;
        }
        return (showHeader ? 8f : 1f) + lineCount * ModifierLineHeight;
    }

    public static string WrapForHud(string text, out int lineCount)
    {
        string[] words = (text ?? string.Empty).Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            lineCount = 1;
            return string.Empty;
        }

        var result = new StringBuilder();
        int currentLength = 0;
        lineCount = 1;
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (currentLength > 0 && currentLength + 1 + word.Length > MaxCharactersPerLine)
            {
                result.Append('\n');
                currentLength = 0;
                lineCount++;
            }
            else if (currentLength > 0)
            {
                result.Append(' ');
                currentLength++;
            }

            result.Append(word);
            currentLength += word.Length;
        }
        return result.ToString();
    }

    private void Build()
    {
        if (_panel != null || _document == null || _document.panelSettings == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;

        _panel = new VisualElement { name = "DungeonModifierHUDPanel", pickingMode = PickingMode.Ignore };
        _panel.style.position = Position.Absolute;
        _panel.style.right = 3;
        _panel.style.top = 3;
        _panel.style.width = PanelWidth;
        _panel.style.flexDirection = FlexDirection.Column;
        _panel.style.alignItems = Align.Stretch;
        _panel.style.justifyContent = Justify.FlexStart;
        _panel.style.flexGrow = 0;
        _panel.style.flexShrink = 0;
        _panel.style.paddingLeft = PanelPaddingLeft;
        _panel.style.paddingRight = PanelPaddingRight;
        _panel.style.paddingTop = 2;
        _panel.style.paddingBottom = 3;
        _panel.style.backgroundColor = PanelBackground;
        _panel.style.borderLeftWidth = 0;
        _panel.style.borderRightWidth = 0;
        _panel.style.borderTopWidth = 0;
        _panel.style.borderBottomWidth = 0;
        _panel.style.borderTopLeftRadius = 0;
        _panel.style.borderTopRightRadius = 0;
        _panel.style.borderBottomLeftRadius = 0;
        _panel.style.borderBottomRightRadius = 0;
        _panel.style.display = DisplayStyle.None;
        root.Add(_panel);

        DungeonUIPresentationSO presentation = DungeonUIPresentationSO.Load();
        AddDecorativeSprite(_panel, "HudFrameArt", presentation != null ? presentation.HudFrame : null);

        _location = new Label { name = "LocationName", pickingMode = PickingMode.Ignore };
        _location.style.fontSize = 7;
        _location.style.height = 9;
        _location.style.minHeight = 9;
        _location.style.maxHeight = 9;
        _location.style.flexGrow = 0;
        _location.style.flexShrink = 0;
        ResetSpacing(_location);
        _location.style.unityFontStyleAndWeight = FontStyle.Bold;
        _location.style.unityTextAlign = TextAnchor.MiddleRight;
        _location.style.whiteSpace = WhiteSpace.Normal;
        _location.style.color = HeaderColor;
        _panel.Add(_location);

        _room = new Label { name = "RoomSummary", pickingMode = PickingMode.Ignore };
        _room.style.fontSize = 5;
        _room.style.height = 7;
        _room.style.minHeight = 7;
        _room.style.maxHeight = 7;
        _room.style.flexGrow = 0;
        _room.style.flexShrink = 0;
        ResetSpacing(_room);
        _room.style.unityTextAlign = TextAnchor.MiddleRight;
        _room.style.color = TextColor;
        _room.style.marginBottom = 1;
        _room.style.opacity = 0.8f;
        _panel.Add(_room);

        var divider = new VisualElement { name = "HeaderDivider", pickingMode = PickingMode.Ignore };
        divider.style.height = 1;
        divider.style.minHeight = 1;
        divider.style.maxHeight = 1;
        divider.style.flexShrink = 0;
        divider.style.backgroundColor = BorderColor;
        _panel.Add(divider);

        _modifierList = new VisualElement { name = "ModifiersContent", pickingMode = PickingMode.Ignore };
        _modifierList.style.flexGrow = 0;
        _modifierList.style.flexShrink = 0;
        _modifierList.style.justifyContent = Justify.FlexStart;
        _panel.Add(_modifierList);
    }

    private static void ResetSpacing(VisualElement element)
    {
        element.style.marginLeft = 0;
        element.style.marginRight = 0;
        element.style.marginTop = 0;
        element.style.marginBottom = 0;
        element.style.paddingLeft = 0;
        element.style.paddingRight = 0;
        element.style.paddingTop = 0;
        element.style.paddingBottom = 0;
    }

    private static void AddDecorativeSprite(VisualElement parent, string name, Sprite sprite)
    {
        if (parent == null || sprite == null)
            return;

        var art = new Image
        {
            name = name,
            sprite = sprite,
            scaleMode = ScaleMode.StretchToFill,
            pickingMode = PickingMode.Ignore
        };
        art.style.position = Position.Absolute;
        art.style.left = 0;
        art.style.right = 0;
        art.style.top = 0;
        art.style.bottom = 0;
        parent.Add(art);
        art.SendToBack();
    }

    private static PanelSettings ResolvePanelSettings()
    {
        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UIDocument document in documents)
        {
            if (document != null && document.panelSettings != null)
                return document.panelSettings;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
