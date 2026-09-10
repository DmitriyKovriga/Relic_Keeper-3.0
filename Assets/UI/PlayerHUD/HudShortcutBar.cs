using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Top-left HUD shortcut strip. Built in UI Toolkit so cursor clicks reach the buttons
/// (uGUI HUD sits under PixelArtPanelSettings).
/// </summary>
public sealed class HudShortcutBar : MonoBehaviour
{
    public const int ButtonSizePixels = 12;
    public const int BarPaddingPixels = 2;
    public const int ButtonSpacingPixels = 2;
    public const int ScreenInsetPixels = 2;
    public const int BorderPixels = 1;
    public const int ButtonCount = 5;
    public const float SortingOrder = 1500f;

    public static int BarWidthPixels =>
        ButtonCount * ButtonSizePixels
        + (ButtonCount - 1) * ButtonSpacingPixels
        + BarPaddingPixels * 2
        + BorderPixels * 2;

    public static int BarHeightPixels =>
        ButtonSizePixels + BarPaddingPixels * 2 + BorderPixels * 2;

    private static readonly Color BarFill = new Color(0.055f, 0.045f, 0.035f, 0.78f);
    private static readonly Color BarBorder = new Color(0.43f, 0.33f, 0.19f, 0.7f);
    private static readonly Color ButtonFill = new Color(0.12f, 0.09f, 0.07f, 1f);
    private static readonly Color ButtonHover = new Color(0.2f, 0.16f, 0.1f, 1f);
    private static readonly Color LabelColor = new Color(0.91f, 0.76f, 0.45f, 1f);
    private static readonly Color BreathColor = new Color(1f, 0.86f, 0.22f, 1f);

    private static HudShortcutBar _instance;
    private static Sprite _circleSprite;

    private UIDocument _document;
    private VisualElement _treeBreath;
    private bool _hasUnspentPoints;
    private Icons _icons;

    public readonly struct Icons
    {
        public readonly Sprite Bar;
        public readonly Sprite Inventory;
        public readonly Sprite Craft;
        public readonly Sprite PassiveTree;
        public readonly Sprite Stats;
        public readonly Sprite Pause;

        public Icons(Sprite bar, Sprite inventory, Sprite craft, Sprite passiveTree, Sprite stats, Sprite pause)
        {
            Bar = bar;
            Inventory = inventory;
            Craft = craft;
            PassiveTree = passiveTree;
            Stats = stats;
            Pause = pause;
        }
    }

    public static HudShortcutBar GetOrCreate(Icons icons = default)
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("HudShortcutBar");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<HudShortcutBar>();
        _instance._document = document;
        _instance._icons = icons;
        host.SetActive(true);
        _instance.Build();
        return _instance;
    }

    public void SetUnspentPassivePoints(bool hasPoints)
    {
        _hasUnspentPoints = hasPoints;
        if (_treeBreath == null)
            return;

        if (!hasPoints)
        {
            _treeBreath.style.display = DisplayStyle.None;
            _treeBreath.style.opacity = 0f;
        }
    }

    public void TickBreath(float unscaledTime)
    {
        if (_treeBreath == null)
            return;

        if (!_hasUnspentPoints)
        {
            _treeBreath.style.display = DisplayStyle.None;
            return;
        }

        float alpha = EvaluateBreathAlpha(unscaledTime);
        _treeBreath.style.opacity = alpha;
        _treeBreath.style.display = DisplayStyle.Flex;
    }

    public static float EvaluateBreathAlpha(float unscaledTime, float period = 1.6f)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(unscaledTime * Mathf.PI * 2f / Mathf.Max(0.2f, period));
        return Mathf.Lerp(0.18f, 0.62f, wave);
    }

    public static bool IsPointerOverBar()
    {
        if (_instance == null || Mouse.current == null)
            return false;

        VisualElement root = _instance._document != null ? _instance._document.rootVisualElement : null;
        IPanel panel = root != null ? root.panel : null;
        if (panel == null)
            return false;

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, Mouse.current.position.ReadValue());
        VisualElement picked = panel.Pick(panelPos);
        while (picked != null)
        {
            if (picked.name == "HudShortcutStrip")
                return true;
            picked = picked.parent;
        }

        return false;
    }

    public static VisualElement CreateBarElement(Icons icons, out VisualElement treeBreath)
    {
        int width = BarWidthPixels;
        int height = BarHeightPixels;

        var bar = new VisualElement { name = "HudShortcutStrip" };
        bar.pickingMode = PickingMode.Position;
        ResetBox(bar);
        bar.style.position = Position.Absolute;
        bar.style.left = ScreenInsetPixels;
        bar.style.top = ScreenInsetPixels;
        bar.style.width = width;
        bar.style.height = height;
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.alignItems = Align.Center;
        bar.style.justifyContent = Justify.FlexStart;
        bar.style.paddingLeft = BarPaddingPixels + BorderPixels;
        bar.style.paddingRight = BarPaddingPixels + BorderPixels;
        bar.style.paddingTop = BarPaddingPixels + BorderPixels;
        bar.style.paddingBottom = BarPaddingPixels + BorderPixels;
        bar.style.backgroundColor = BarFill;
        SetSquareBorder(bar, BorderPixels, BarBorder);

        if (icons.Bar != null)
            AddDecorativeSprite(bar, "BarArt", icons.Bar);

        treeBreath = null;
        AddShortcutButton(bar, "Inventory", "I", icons.Inventory, false, out _);
        AddShortcutButton(bar, "Craft", "K", icons.Craft, false, out _);
        AddShortcutButton(bar, "Passives", "T", icons.PassiveTree, true, out treeBreath);
        AddShortcutButton(bar, "Stats", "C", icons.Stats, false, out _);
        AddShortcutButton(bar, "Pause", "M", icons.Pause, false, out _);
        return bar;
    }

    private void Build()
    {
        if (_document == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        root.Clear();
        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;
        root.style.flexGrow = 1;

        VisualElement bar = CreateBarElement(_icons, out _treeBreath);
        BindClicks(bar);
        root.Add(bar);
    }

    private void BindClicks(VisualElement bar)
    {
        BindClick(bar, "Inventory", () => Object.FindFirstObjectByType<InventoryWindowToggle>()?.Toggle());
        BindClick(bar, "Craft", () => Object.FindFirstObjectByType<InventoryWindowToggle>()?.ToggleCraft());
        BindClick(bar, "Passives", () => Object.FindFirstObjectByType<PassiveTreeWindowToggle>()?.Toggle());
        BindClick(bar, "Stats", () => Object.FindFirstObjectByType<CharacterWindowToggle>()?.Toggle());
        BindClick(bar, "Pause", () => Object.FindFirstObjectByType<PauseMenuToggle>()?.HandleEscape());
    }

    private static void BindClick(VisualElement bar, string name, System.Action onClick)
    {
        VisualElement button = bar.Q<VisualElement>(name);
        if (button == null)
            return;

        button.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            onClick?.Invoke();
        });
    }

    private static void AddShortcutButton(
        VisualElement parent,
        string name,
        string label,
        Sprite icon,
        bool withBreath,
        out VisualElement breath)
    {
        breath = null;
        var button = new VisualElement { name = name };
        button.focusable = false;
        button.pickingMode = PickingMode.Position;
        ResetBox(button);
        button.style.width = ButtonSizePixels;
        button.style.height = ButtonSizePixels;
        button.style.minWidth = ButtonSizePixels;
        button.style.minHeight = ButtonSizePixels;
        button.style.maxWidth = ButtonSizePixels;
        button.style.maxHeight = ButtonSizePixels;
        button.style.marginRight = name == "Pause" ? 0 : ButtonSpacingPixels;
        button.style.overflow = Overflow.Hidden;
        button.style.backgroundColor = Color.clear;

        var disc = new Image
        {
            name = "Disc",
            sprite = GetCircleSprite(),
            scaleMode = ScaleMode.StretchToFill,
            pickingMode = PickingMode.Ignore,
            tintColor = ButtonFill
        };
        ResetBox(disc);
        disc.style.position = Position.Absolute;
        disc.style.left = 0;
        disc.style.top = 0;
        disc.style.right = 0;
        disc.style.bottom = 0;
        button.Add(disc);

        button.RegisterCallback<PointerEnterEvent>(_ => disc.tintColor = ButtonHover);
        button.RegisterCallback<PointerLeaveEvent>(_ => disc.tintColor = ButtonFill);

        if (icon != null)
        {
            var image = new Image
            {
                name = "Icon",
                sprite = icon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            ResetBox(image);
            image.style.position = Position.Absolute;
            image.style.left = 1;
            image.style.top = 1;
            image.style.right = 1;
            image.style.bottom = 1;
            button.Add(image);
        }
        else
        {
            var text = new Label(label) { name = "Label", pickingMode = PickingMode.Ignore };
            ResetBox(text);
            text.style.position = Position.Absolute;
            text.style.left = 0;
            text.style.top = 0;
            text.style.right = 0;
            text.style.bottom = 0;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.fontSize = 6;
            text.style.color = LabelColor;
            text.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.style.whiteSpace = WhiteSpace.NoWrap;
            button.Add(text);
        }

        if (withBreath)
        {
            breath = new Image
            {
                name = "Breath",
                sprite = GetCircleSprite(),
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore,
                tintColor = BreathColor
            };
            ResetBox(breath);
            breath.style.position = Position.Absolute;
            breath.style.left = 0;
            breath.style.top = 0;
            breath.style.right = 0;
            breath.style.bottom = 0;
            breath.style.opacity = 0f;
            breath.style.display = DisplayStyle.None;
            button.Add(breath);
        }

        parent.Add(button);
    }

    private static void AddDecorativeSprite(VisualElement parent, string name, Sprite sprite)
    {
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

    private static void SetSquareBorder(VisualElement element, float width, Color color)
    {
        element.style.borderTopWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width;
        element.style.borderTopColor = color;
        element.style.borderRightColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
        element.style.borderTopLeftRadius = 0;
        element.style.borderTopRightRadius = 0;
        element.style.borderBottomLeftRadius = 0;
        element.style.borderBottomRightRadius = 0;
    }

    private static void ResetBox(VisualElement element)
    {
        element.style.marginLeft = 0;
        element.style.marginRight = 0;
        element.style.marginTop = 0;
        element.style.marginBottom = 0;
        element.style.paddingLeft = 0;
        element.style.paddingRight = 0;
        element.style.paddingTop = 0;
        element.style.paddingBottom = 0;
        element.style.flexGrow = 0;
        element.style.flexShrink = 0;
    }

    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null)
            return _circleSprite;

        const int size = 12;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = "HudShortcutCircle"
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
            }
        }
        texture.Apply(false, false);

        _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _circleSprite.name = "HudShortcutCircle";
        _circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return _circleSprite;
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
