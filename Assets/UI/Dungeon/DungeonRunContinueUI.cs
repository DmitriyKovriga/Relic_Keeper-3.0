using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DungeonRunContinueUI : MonoBehaviour
{
    public const float SortingOrder = 20000f;
    public const int WindowWidth = 236;
    public const int WindowHeight = 78;
    public const int ButtonWidth = 96;
    public const int ButtonHeight = 16;

    private static readonly Color WindowBackground = new Color(0.10f, 0.075f, 0.055f, 0.99f);
    private static readonly Color GoldBorder = new Color(0.48f, 0.37f, 0.21f, 1f);
    private static readonly Color GoldHighlight = new Color(0.78f, 0.61f, 0.32f, 1f);
    private static readonly Color PrimaryText = new Color(0.93f, 0.86f, 0.72f, 1f);
    private static readonly Color ButtonFill = new Color(0.25f, 0.20f, 0.14f, 1f);

    private static DungeonRunContinueUI _instance;

    private UIDocument _document;
    private VisualElement _overlay;
    private Label _title;
    private GamePauseService.PauseHandle _pauseHandle;
    private Action _onContinue;
    private Action _onReturnToSettlement;
    private bool _playerInputWasEnabled;
    private bool _resolved;

    public static bool IsVisible => _instance != null && _instance._overlay != null &&
                                    _instance._overlay.style.display == DisplayStyle.Flex;

    public static DungeonRunContinueUI GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("DungeonRunContinueUI");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<DungeonRunContinueUI>();
        _instance._document = document;
        host.SetActive(true);
        _instance.Build();
        return _instance;
    }

    public static void HideIfVisible()
    {
        if (_instance != null)
            _instance.HideInternal();
    }

    public void Show(string title, Action onContinue, Action onReturnToSettlement)
    {
        Build();
        if (_overlay == null)
        {
            onReturnToSettlement?.Invoke();
            return;
        }

        _resolved = false;
        _onContinue = onContinue;
        _onReturnToSettlement = onReturnToSettlement;
        _title.text = string.IsNullOrWhiteSpace(title)
            ? "Идти дальше или вернуться в поселение?"
            : title;

        if (_document != null && _document.rootVisualElement != null)
            _document.rootVisualElement.pickingMode = PickingMode.Position;
        _overlay.style.display = DisplayStyle.Flex;
        _pauseHandle = GamePauseService.Acquire(GamePauseReason.DungeonChoice);
        _playerInputWasEnabled = InputManager.InputActions.Player.Get().enabled;
        InputManager.InputActions.Player.Disable();
    }

    public static VisualElement CreateWindow(out Button continueButton, out Button returnButton)
    {
        var panel = new VisualElement { name = "DungeonRunContinuePanel" };
        panel.style.width = WindowWidth;
        panel.style.height = WindowHeight;
        panel.style.flexShrink = 0;
        panel.style.paddingLeft = 6;
        panel.style.paddingRight = 6;
        panel.style.paddingTop = 6;
        panel.style.paddingBottom = 6;
        panel.style.backgroundColor = WindowBackground;
        SetSquareBorder(panel, 2f, GoldBorder);

        var title = new Label("Идти дальше или вернуться в поселение?")
        {
            name = "ContinueTitle",
            pickingMode = PickingMode.Ignore
        };
        title.style.height = 28;
        title.style.flexShrink = 0;
        title.style.fontSize = 8;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.whiteSpace = WhiteSpace.Normal;
        title.style.color = PrimaryText;
        panel.Add(title);

        var row = new VisualElement { name = "ContinueButtons" };
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        row.style.alignItems = Align.Center;
        row.style.flexGrow = 1;
        row.style.minHeight = 0;
        panel.Add(row);

        continueButton = CreateActionButton("ContinueRunButton", "Дальше");
        returnButton = CreateActionButton("ReturnToSettlementButton", "В поселение");
        continueButton.style.marginRight = 4;
        returnButton.style.marginLeft = 4;
        row.Add(continueButton);
        row.Add(returnButton);
        return panel;
    }

    private void Build()
    {
        if (_overlay != null || _document == null || _document.panelSettings == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;

        _overlay = new VisualElement { name = "DungeonRunContinueOverlay" };
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0;
        _overlay.style.right = 0;
        _overlay.style.top = 0;
        _overlay.style.bottom = 0;
        _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
        _overlay.style.justifyContent = Justify.Center;
        _overlay.style.alignItems = Align.Center;
        _overlay.style.display = DisplayStyle.None;
        root.Add(_overlay);

        VisualElement panel = CreateWindow(out Button continueButton, out Button returnButton);
        _title = panel.Q<Label>("ContinueTitle");
        continueButton.clicked += () => Complete(true);
        returnButton.clicked += () => Complete(false);
        _overlay.Add(panel);
    }

    private void Complete(bool continueRun)
    {
        if (_resolved)
            return;

        _resolved = true;
        Action onContinue = _onContinue;
        Action onReturn = _onReturnToSettlement;
        _onContinue = null;
        _onReturnToSettlement = null;
        HideInternal();
        if (continueRun)
            onContinue?.Invoke();
        else
            onReturn?.Invoke();
    }

    private void HideInternal()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;
        if (_document != null && _document.rootVisualElement != null)
            _document.rootVisualElement.pickingMode = PickingMode.Ignore;

        _pauseHandle?.Dispose();
        _pauseHandle = null;
        if (_playerInputWasEnabled)
            InputManager.InputActions.Player.Enable();
        _playerInputWasEnabled = false;
        _onContinue = null;
        _onReturnToSettlement = null;
    }

    private static Button CreateActionButton(string name, string text)
    {
        var button = new Button { name = name, text = text };
        button.style.width = ButtonWidth;
        button.style.height = ButtonHeight;
        button.style.flexShrink = 0;
        button.style.marginTop = 0;
        button.style.marginBottom = 0;
        button.style.paddingLeft = 2;
        button.style.paddingRight = 2;
        button.style.paddingTop = 0;
        button.style.paddingBottom = 0;
        button.style.fontSize = 7;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.color = PrimaryText;
        button.style.backgroundColor = ButtonFill;
        SetSquareBorder(button, 1f, GoldHighlight);
        return button;
    }

    private static void SetSquareBorder(VisualElement element, float width, Color color)
    {
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
        element.style.borderTopLeftRadius = 0;
        element.style.borderTopRightRadius = 0;
        element.style.borderBottomLeftRadius = 0;
        element.style.borderBottomRightRadius = 0;
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
        HideInternal();
        if (_instance == this)
            _instance = null;
    }
}
