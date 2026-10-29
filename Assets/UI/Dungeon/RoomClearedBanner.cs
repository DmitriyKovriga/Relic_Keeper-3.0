using System.Collections;
using Scripts.Dungeon;
using Scripts.UI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

/// <summary>
/// Краткое объявление по центру экрана, чуть выше середины, после зачистки комнаты.
/// </summary>
public sealed class RoomClearedBanner : MonoBehaviour
{
    public const string LocalizationKey = "dungeon.ui.roomCleared";
    public const string FallbackText = "Room Cleared";
    public const string PortalUnlockedLocalizationKey = "dungeon.ui.portalUnlocked";
    public const string PortalUnlockedFallbackText = "Portal Open";
    public const string ClearedAndPortalUnlockedLocalizationKey = "dungeon.ui.roomClearedPortalUnlocked";
    public const string ClearedAndPortalUnlockedFallbackText = "Room Cleared — Portal Open";
    public const string CharacterLevelUpKey = "player.levelUp";
    public const string CharacterLevelUpFallback = "Character Level Up";

    private const float SortingOrder = 800f;

    private static RoomClearedBanner _instance;

    private UIDocument _document;
    private Label _label;
    private Label _detail;
    private VisualElement _box;
    private Coroutine _playRoutine;
    private readonly RoomAnnouncementState _state = new RoomAnnouncementState();
    private DungeonPortal _exitTarget;
    private float _hintUntil;
    private ExitHintDirection _lastDirection;
    private RoomNotice _lastNotice;
    private RoomController _roomOwner;
    private bool _hasRoomOwner;

    public static void Show()
    {
        GetOrCreate().Play(RoomNotice.Cleared);
    }
    public static void Show(RoomController room) => GetOrCreate().Play(RoomNotice.Cleared, owner: room);

    public static void ShowPortalUnlocked()
    {
        ShowPortalUnlocked(null);
    }

    public static void ShowPortalUnlocked(DungeonPortal portal) => GetOrCreate().Play(RoomNotice.PortalUnlocked, portal);

    public static void ShowClearedAndPortalUnlocked()
    {
        ShowClearedAndPortalUnlocked(null);
    }

    public static void ShowClearedAndPortalUnlocked(DungeonPortal portal) => GetOrCreate().Play(RoomNotice.Cleared | RoomNotice.PortalUnlocked, portal);

    public static void ShowCharacterLevelUp()
    {
        GetOrCreate().Play(RoomNotice.LevelUp);
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.StopAndHide();
    }
    public static void HideForRoom(RoomController room)
    {
        if (_instance != null && _instance._hasRoomOwner && ReferenceEquals(_instance._roomOwner, room)) _instance.StopAndHide();
    }

    public static RoomClearedBanner GetOrCreate()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("RoomClearedBanner");
        host.SetActive(false);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = ResolvePanelSettings();
        document.sortingOrder = SortingOrder;
        _instance = host.AddComponent<RoomClearedBanner>();
        _instance._document = document;
        host.SetActive(true);
        _instance.Build();
        return _instance;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        StopAndHide();
    }
    private void OnLocaleChanged(Locale _) => RefreshMessage(true);

    private void Play(RoomNotice notice, DungeonPortal portal = null, RoomController owner = null)
    {
        if (!_state.Add(notice, Time.unscaledTime)) return;
        Build();
        if (_label == null)
            return;

        if (owner == null && portal != null) owner = portal.GetComponentInParent<RoomController>();
        if (owner != null) { _roomOwner = owner; _hasRoomOwner = true; }

        if (portal != null && (notice & RoomNotice.PortalUnlocked) != 0)
        {
            _exitTarget = portal; _hintUntil = Time.unscaledTime + RoomAnnouncementState.FadeIn + RoomAnnouncementState.Hold;
        }
        RefreshMessage(true);
        if (_playRoutine == null && Application.isPlaying) _playRoutine = StartCoroutine(PlayRoutine());
    }

    private void StopAndHide()
    {
        _state.Clear(); _exitTarget = null; _roomOwner = null; _hasRoomOwner = false;
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_box != null)
        {
            _box.style.opacity = 0f;
            _box.style.display = DisplayStyle.None;
        }
    }

    private IEnumerator PlayRoutine()
    {
        _box.style.display = DisplayStyle.Flex;
        while (_state.IsVisible(Time.unscaledTime))
        {
            if (_hasRoomOwner && (_roomOwner == null || !_roomOwner.gameObject.activeInHierarchy)) break;
            RefreshMessage(false);
            _box.style.opacity = _state.Opacity(Time.unscaledTime);
            yield return null;
        }

        _state.Clear(); _exitTarget = null; _roomOwner = null; _hasRoomOwner = false;
        _box.style.opacity = 0f;
        _box.style.display = DisplayStyle.None;
        _playRoutine = null;
    }

    private void Build()
    {
        if (_label != null || _document == null)
            return;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;
        root.style.flexGrow = 1;

        _box = new VisualElement { name = "RoomAnnouncement", pickingMode = PickingMode.Ignore };
        _box.style.position = Position.Absolute; _box.style.left = 8; _box.style.right = 8;
        _box.style.top = Length.Percent(34f); _box.style.display = DisplayStyle.None;
        root.Add(_box);

        _label = new Label(FallbackText)
        {
            name = "RoomClearedLabel",
            pickingMode = PickingMode.Ignore
        };
        _label.style.unityTextAlign = TextAnchor.MiddleCenter;
        _label.style.unityFontStyleAndWeight = FontStyle.Bold;
        _label.style.fontSize = 16;
        _label.style.color = new Color(0.93f, 0.82f, 0.48f, 1f);
        _label.style.whiteSpace = WhiteSpace.Normal;
        _box.Add(_label);
        _detail = new Label { name = "RoomAnnouncementDetail", pickingMode = PickingMode.Ignore };
        _detail.style.fontSize = 10; _detail.style.unityTextAlign = TextAnchor.MiddleCenter;
        _detail.style.color = new Color(.88f, .89f, .86f); _detail.style.whiteSpace = WhiteSpace.Normal;
        _box.Add(_detail);
    }

    private void RefreshMessage(bool force)
    {
        if (_label == null || _state.Current == RoomNotice.None) return;
        ExitHintDirection direction = ExitHintDirection.None;
        if (_exitTarget != null && _exitTarget.isActiveAndEnabled && _exitTarget.CanInteract() && Time.unscaledTime < _hintUntil && Camera.main != null)
        {
            direction = RoomAnnouncementState.ExitDirection(Camera.main.WorldToViewportPoint(_exitTarget.transform.position));
            if (direction == ExitHintDirection.None) _exitTarget = null;
        }
        if (!force && _lastNotice == _state.Current && _lastDirection == direction) return;
        _lastNotice = _state.Current; _lastDirection = direction;
        bool cleared = (_state.Current & RoomNotice.Cleared) != 0;
        bool portal = (_state.Current & RoomNotice.PortalUnlocked) != 0;
        bool level = (_state.Current & RoomNotice.LevelUp) != 0;
        string levelText = RuntimeLocalization.Resolve(CharacterLevelUpKey, CharacterLevelUpFallback, "Новый уровень персонажа");
        _label.text = cleared && portal ? RuntimeLocalization.Resolve(ClearedAndPortalUnlockedLocalizationKey, ClearedAndPortalUnlockedFallbackText, "Комната зачищена — портал открыт") :
            cleared ? RuntimeLocalization.Resolve(LocalizationKey, FallbackText, "Комната зачищена") :
            portal ? RuntimeLocalization.Resolve(PortalUnlockedLocalizationKey, PortalUnlockedFallbackText, "Портал открыт") : levelText;
        string detail = (cleared || portal) && level ? levelText : "";
        if (direction != ExitHintDirection.None)
        {
            string arrow = direction == ExitHintDirection.Left ? "<" : direction == ExitHintDirection.Right ? ">" : direction == ExitHintDirection.Above ? "^" : "v";
            string hint = RuntimeLocalization.Resolve("dungeon.ui.exitDirection", "Exit", "Выход") + " " + arrow;
            detail = string.IsNullOrEmpty(detail) ? hint : detail + "  |  " + hint;
        }
        _detail.text = detail; _detail.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
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
}
