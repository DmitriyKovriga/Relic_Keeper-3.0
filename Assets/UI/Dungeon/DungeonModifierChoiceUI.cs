using System;
using System.Collections.Generic;
using Scripts.Dungeon;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DungeonModifierChoiceUI : MonoBehaviour
{
    private const float SortingOrder = 20000f;
    private const float WindowWidth = 460f;
    private const float WindowHeight = 244f;
    private const float CardWidth = 138f;
    private const float CardHeight = 196f;
    public const int ReturnButtonWidth = 78;
    public const int ReturnButtonHeight = 14;
    public const int ReturnButtonInset = 4;

    private static readonly Color WindowBackground = new Color(0.10f, 0.075f, 0.055f, 0.99f);
    private static readonly Color CardBackground = new Color(0.15f, 0.12f, 0.09f, 1f);
    private static readonly Color ImageBackground = new Color(0.055f, 0.045f, 0.038f, 1f);
    private static readonly Color GoldBorder = new Color(0.48f, 0.37f, 0.21f, 1f);
    private static readonly Color GoldHighlight = new Color(0.78f, 0.61f, 0.32f, 1f);
    private static readonly Color PrimaryText = new Color(0.93f, 0.86f, 0.72f, 1f);
    private static readonly Color SecondaryText = new Color(0.80f, 0.76f, 0.68f, 1f);
    private static DungeonModifierChoiceUI _instance;

    private UIDocument _document;
    private VisualElement _overlay;
    private Label _title;
    private VisualElement _choices;
    private GamePauseService.PauseHandle _pauseHandle;
    private Action<DungeonModifierSO> _onSelected;
    private Action _onReturnToSettlement;
    private Button _returnButton;
    private bool _playerInputWasEnabled;
    private bool _resolved;

    public static bool IsVisible => _instance != null && _instance._overlay != null &&
                                    _instance._overlay.style.display == DisplayStyle.Flex;

    public static DungeonModifierChoiceUI GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("DungeonModifierChoiceUI");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;

        _instance = host.AddComponent<DungeonModifierChoiceUI>();
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

    public void Show(
        string title,
        IReadOnlyList<DungeonModifierSO> choices,
        Action<DungeonModifierSO> onSelected,
        Action onReturnToSettlement = null)
    {
        if (choices == null || choices.Count == 0)
        {
            onSelected?.Invoke(null);
            return;
        }

        Build();
        if (_overlay == null)
        {
            onSelected?.Invoke(choices[0]);
            return;
        }

        _resolved = false;
        _onSelected = onSelected;
        _onReturnToSettlement = onReturnToSettlement;
        if (_returnButton != null)
        {
            _returnButton.style.display = onReturnToSettlement != null ? DisplayStyle.Flex : DisplayStyle.None;
            _returnButton.BringToFront();
        }
        _title.text = string.IsNullOrWhiteSpace(title) ? "Выберите модификатор" : title;
        _choices.Clear();

        for (int i = 0; i < choices.Count; i++)
        {
            DungeonModifierSO modifier = choices[i];
            if (modifier == null)
                continue;

            _choices.Add(CreateModifierCard(modifier));
        }

        if (_document != null && _document.rootVisualElement != null)
            _document.rootVisualElement.pickingMode = PickingMode.Position;
        _overlay.style.display = DisplayStyle.Flex;
        _pauseHandle = GamePauseService.Acquire(GamePauseReason.DungeonChoice);
        _playerInputWasEnabled = InputManager.InputActions.Player.Get().enabled;
        InputManager.InputActions.Player.Disable();
    }

    private VisualElement CreateModifierCard(DungeonModifierSO modifier)
    {
        DungeonUIPresentationSO presentation = DungeonUIPresentationSO.Load();
        var card = new VisualElement { name = $"ModifierCard_{modifier.ID}" };
        card.style.width = CardWidth;
        card.style.height = CardHeight;
        card.style.flexShrink = 0;
        card.style.marginLeft = 4;
        card.style.marginRight = 4;
        card.style.paddingLeft = 5;
        card.style.paddingRight = 5;
        card.style.paddingTop = 5;
        card.style.paddingBottom = 5;
        card.style.overflow = Overflow.Hidden;
        card.style.backgroundColor = CardBackground;
        SetSquareBorder(card, 1f, GoldBorder);
        AddDecorativeSprite(card, "CardFrameArt", presentation != null ? presentation.ChoiceCardFrame : null);

        var imageFrame = new VisualElement { name = "ModifierImageFrame", pickingMode = PickingMode.Ignore };
        imageFrame.style.height = 72;
        imageFrame.style.flexShrink = 0;
        imageFrame.style.alignItems = Align.Center;
        imageFrame.style.justifyContent = Justify.Center;
        imageFrame.style.backgroundColor = ImageBackground;
        SetSquareBorder(imageFrame, 1f, new Color(0.30f, 0.25f, 0.18f, 1f));
        card.Add(imageFrame);

        Sprite imageSprite = modifier.Icon != null
            ? modifier.Icon
            : presentation != null ? presentation.ChoiceImagePlaceholder : null;
        if (imageSprite != null)
        {
            var icon = new Image
            {
                name = "ModifierIcon",
                sprite = imageSprite,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.style.width = 68;
            icon.style.height = 68;
            imageFrame.Add(icon);
        }
        else
        {
            var placeholder = new Label("МЕСТО ПОД АРТ") { pickingMode = PickingMode.Ignore };
            placeholder.style.fontSize = 6;
            placeholder.style.color = new Color(0.45f, 0.40f, 0.32f, 1f);
            placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            imageFrame.Add(placeholder);
        }

        var title = new Label(string.IsNullOrWhiteSpace(modifier.DisplayName) ? modifier.name : modifier.DisplayName)
        {
            name = "ModifierTitle",
            pickingMode = PickingMode.Ignore
        };
        title.style.height = 28;
        title.style.flexShrink = 0;
        title.style.marginTop = 4;
        title.style.fontSize = 8;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.whiteSpace = WhiteSpace.Normal;
        title.style.color = PrimaryText;
        card.Add(title);

        var description = new Label(modifier.Description ?? string.Empty)
        {
            name = "ModifierDescription",
            pickingMode = PickingMode.Ignore
        };
        description.style.flexGrow = 1;
        description.style.minHeight = 0;
        description.style.paddingLeft = 2;
        description.style.paddingRight = 2;
        description.style.fontSize = 7;
        description.style.unityTextAlign = TextAnchor.UpperCenter;
        description.style.whiteSpace = WhiteSpace.Normal;
        description.style.overflow = Overflow.Hidden;
        description.style.color = SecondaryText;
        card.Add(description);

        var selectButton = new Button(() => CompleteChoice(modifier))
        {
            name = "SelectModifierButton",
            text = "ВЫБРАТЬ"
        };
        selectButton.style.height = 20;
        selectButton.style.flexShrink = 0;
        selectButton.style.marginTop = 4;
        selectButton.style.marginLeft = 0;
        selectButton.style.marginRight = 0;
        selectButton.style.marginBottom = 0;
        selectButton.style.paddingLeft = 2;
        selectButton.style.paddingRight = 2;
        selectButton.style.paddingTop = 0;
        selectButton.style.paddingBottom = 0;
        selectButton.style.fontSize = 8;
        selectButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        selectButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        selectButton.style.color = PrimaryText;
        selectButton.style.backgroundColor = new Color(0.25f, 0.20f, 0.14f, 1f);
        SetSquareBorder(selectButton, 1f, GoldHighlight);
        card.Add(selectButton);

        return card;
    }

    private void CompleteChoice(DungeonModifierSO selected)
    {
        if (_resolved)
            return;

        _resolved = true;
        Action<DungeonModifierSO> callback = _onSelected;
        _onSelected = null;
        _onReturnToSettlement = null;
        HideInternal();
        callback?.Invoke(selected);
    }

    private void CompleteReturnToSettlement()
    {
        if (_resolved)
            return;

        _resolved = true;
        Action callback = _onReturnToSettlement;
        _onSelected = null;
        _onReturnToSettlement = null;
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
        _onReturnToSettlement = null;
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

        _overlay = new VisualElement { name = "DungeonModifierChoiceOverlay" };
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

        var panel = new VisualElement { name = "DungeonModifierChoicePanel" };
        panel.style.width = WindowWidth;
        panel.style.height = WindowHeight;
        panel.style.flexShrink = 0;
        panel.style.paddingLeft = 7;
        panel.style.paddingRight = 7;
        panel.style.paddingTop = 6;
        panel.style.paddingBottom = 7;
        panel.style.backgroundColor = WindowBackground;
        SetSquareBorder(panel, 2f, GoldBorder);
        DungeonUIPresentationSO presentation = DungeonUIPresentationSO.Load();
        AddDecorativeSprite(panel, "WindowFrameArt", presentation != null ? presentation.ChoiceWindowFrame : null);
        _overlay.Add(panel);

        _title = new Label();
        _title.style.height = 24;
        _title.style.flexShrink = 0;
        _title.style.fontSize = 11;
        _title.style.unityFontStyleAndWeight = FontStyle.Bold;
        _title.style.unityTextAlign = TextAnchor.MiddleCenter;
        _title.style.whiteSpace = WhiteSpace.Normal;
        _title.style.color = PrimaryText;
        _title.style.marginBottom = 4;
        panel.Add(_title);

        _choices = new VisualElement();
        _choices.style.flexDirection = FlexDirection.Row;
        _choices.style.justifyContent = Justify.Center;
        _choices.style.alignItems = Align.Center;
        _choices.style.flexGrow = 1;
        _choices.style.minHeight = 0;
        panel.Add(_choices);

        _returnButton = CreateReturnToSettlementButton();
        _returnButton.clicked += CompleteReturnToSettlement;
        _overlay.Add(_returnButton);
    }

    public static Button CreateReturnToSettlementButton()
    {
        var button = new Button { name = "ReturnToSettlementButton", text = "В поселение" };
        button.style.position = Position.Absolute;
        button.style.right = ReturnButtonInset;
        button.style.bottom = ReturnButtonInset;
        button.style.width = ReturnButtonWidth;
        button.style.height = ReturnButtonHeight;
        button.style.flexShrink = 0;
        button.style.marginLeft = 0;
        button.style.marginRight = 0;
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
        button.style.backgroundColor = new Color(0.25f, 0.20f, 0.14f, 1f);
        SetSquareBorder(button, 1f, GoldHighlight);
        return button;
    }

    private static void SetSquareBorder(VisualElement element, float width, Color color)
    {
        if (element == null)
            return;

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
        HideInternal();
        if (_instance == this)
            _instance = null;
    }
}
