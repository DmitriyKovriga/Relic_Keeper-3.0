using System;
using System.Collections.Generic;
using Scripts.Dungeon;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DungeonFloorSelectUI : MonoBehaviour
{
    public const float SortingOrder = 20000f;
    public const int WindowWidth = 248;
    public const int WindowHeight = 196;
    public const int ButtonWidth = 220;
    public const int ButtonHeight = 16;
    public const int ListMaxHeight = 112;

    private static readonly Color WindowBackground = new Color(0.10f, 0.075f, 0.055f, 0.99f);
    private static readonly Color GoldBorder = new Color(0.48f, 0.37f, 0.21f, 1f);
    private static readonly Color GoldHighlight = new Color(0.78f, 0.61f, 0.32f, 1f);
    private static readonly Color PrimaryText = new Color(0.93f, 0.86f, 0.72f, 1f);
    private static readonly Color SecondaryText = new Color(0.80f, 0.76f, 0.68f, 1f);
    private static readonly Color ButtonFill = new Color(0.25f, 0.20f, 0.14f, 1f);

    private static DungeonFloorSelectUI _instance;

    private UIDocument _document;
    private VisualElement _overlay;
    private Label _title;
    private Label _emptyLabel;
    private VisualElement _list;
    private GamePauseService.PauseHandle _pauseHandle;
    private Action<int> _onSelected;
    private Action _onCancel;
    private bool _playerInputWasEnabled;
    private bool _resolved;

    public static bool IsVisible => _instance != null && _instance._overlay != null &&
                                    _instance._overlay.style.display == DisplayStyle.Flex;

    public static DungeonFloorSelectUI GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("DungeonFloorSelectUI");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<DungeonFloorSelectUI>();
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

    public void Show(string dungeonName, IReadOnlyList<int> floors, Action<int> onSelected, Action onCancel)
    {
        Build();
        if (_overlay == null)
        {
            onCancel?.Invoke();
            return;
        }

        _resolved = false;
        _onSelected = onSelected;
        _onCancel = onCancel;
        string name = string.IsNullOrWhiteSpace(dungeonName) ? "подземелье" : dungeonName;
        _title.text = $"Этажи {name}";
        _list.Clear();

        bool hasFloors = floors != null && floors.Count > 0;
        _emptyLabel.text = hasFloors
            ? string.Empty
            : $"Пока нет открытых этажей.\nДойдите до {DungeonRunProgress.FloorCheckpointSize} этажа {name}.";
        _emptyLabel.style.display = hasFloors ? DisplayStyle.None : DisplayStyle.Flex;
        _list.style.display = hasFloors ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasFloors)
        {
            for (int i = 0; i < floors.Count; i++)
                _list.Add(CreateFloorButton(floors[i]));
        }

        if (_document != null && _document.rootVisualElement != null)
            _document.rootVisualElement.pickingMode = PickingMode.Position;
        _overlay.style.display = DisplayStyle.Flex;
        _pauseHandle = GamePauseService.Acquire(GamePauseReason.DungeonChoice);
        _playerInputWasEnabled = InputManager.InputActions.Player.Get().enabled;
        InputManager.InputActions.Player.Disable();
    }

    public static VisualElement CreateWindow(
        out Label title,
        out Label emptyLabel,
        out VisualElement list,
        out Button closeButton)
    {
        var panel = new VisualElement { name = "DungeonFloorSelectPanel" };
        panel.style.width = WindowWidth;
        panel.style.height = WindowHeight;
        panel.style.flexShrink = 0;
        panel.style.paddingLeft = 6;
        panel.style.paddingRight = 6;
        panel.style.paddingTop = 6;
        panel.style.paddingBottom = 6;
        panel.style.backgroundColor = WindowBackground;
        SetSquareBorder(panel, 2f, GoldBorder);

        title = new Label("Этажи")
        {
            name = "FloorSelectTitle",
            pickingMode = PickingMode.Ignore
        };
        title.style.height = 18;
        title.style.flexShrink = 0;
        title.style.fontSize = 10;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.color = PrimaryText;
        panel.Add(title);

        var subtitle = new Label($"Стартовый бонус +{DungeonRunProgress.FloorSkipBonusPercent:0}%")
        {
            name = "FloorSelectSubtitle",
            pickingMode = PickingMode.Ignore
        };
        subtitle.style.height = 12;
        subtitle.style.flexShrink = 0;
        subtitle.style.marginBottom = 4;
        subtitle.style.fontSize = 7;
        subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        subtitle.style.color = SecondaryText;
        panel.Add(subtitle);

        emptyLabel = new Label("Пока нет открытых этажей.\nДойдите до 10 этажа Mortfall.")
        {
            name = "FloorSelectEmpty",
            pickingMode = PickingMode.Ignore
        };
        emptyLabel.style.flexGrow = 1;
        emptyLabel.style.minHeight = 0;
        emptyLabel.style.fontSize = 7;
        emptyLabel.style.whiteSpace = WhiteSpace.Normal;
        emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        emptyLabel.style.color = SecondaryText;
        panel.Add(emptyLabel);

        list = new ScrollView
        {
            name = "FloorSelectList",
            mode = ScrollViewMode.Vertical,
            horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            verticalScrollerVisibility = ScrollerVisibility.Auto
        };
        list.style.flexGrow = 1;
        list.style.minHeight = 0;
        list.style.maxHeight = ListMaxHeight;
        list.style.display = DisplayStyle.None;
        panel.Add(list);

        closeButton = new Button { name = "FloorSelectCloseButton", text = "Закрыть" };
        closeButton.style.height = ButtonHeight;
        closeButton.style.flexShrink = 0;
        closeButton.style.marginTop = 6;
        closeButton.style.paddingLeft = 2;
        closeButton.style.paddingRight = 2;
        closeButton.style.paddingTop = 0;
        closeButton.style.paddingBottom = 0;
        closeButton.style.fontSize = 7;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        closeButton.style.color = PrimaryText;
        closeButton.style.backgroundColor = ButtonFill;
        SetSquareBorder(closeButton, 1f, GoldHighlight);
        panel.Add(closeButton);
        return panel;
    }

    private VisualElement CreateFloorButton(int floor)
    {
        var button = new Button(() => Complete(floor))
        {
            name = $"FloorSelectButton_{floor}",
            text = $"Этаж {floor}  +{DungeonRunProgress.FloorSkipBonusPercent:0}%"
        };
        button.style.width = ButtonWidth;
        button.style.height = ButtonHeight;
        button.style.flexShrink = 0;
        button.style.marginBottom = 2;
        button.style.paddingLeft = 4;
        button.style.paddingRight = 4;
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

    private void Build()
    {
        if (_overlay != null || _document == null || _document.panelSettings == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;

        _overlay = new VisualElement { name = "DungeonFloorSelectOverlay" };
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

        VisualElement panel = CreateWindow(out _title, out _emptyLabel, out _list, out Button closeButton);
        closeButton.clicked += CompleteCancel;
        _overlay.Add(panel);
    }

    private void Complete(int floor)
    {
        if (_resolved)
            return;

        _resolved = true;
        Action<int> callback = _onSelected;
        _onSelected = null;
        _onCancel = null;
        HideInternal();
        callback?.Invoke(floor);
    }

    private void CompleteCancel()
    {
        if (_resolved)
            return;

        _resolved = true;
        Action callback = _onCancel;
        _onSelected = null;
        _onCancel = null;
        HideInternal();
        callback?.Invoke();
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
        _onSelected = null;
        _onCancel = null;
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
