using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEngine;
using UnityEngine.UIElements;

public partial class TavernUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private CharacterDatabaseSO _characterDB;
    [SerializeField] private ItemDatabaseSO _itemDatabase;
    [Tooltip("Для кнопки Tree: окно дерева пассивок (WindowView на PassiveTree_Canvas)")]
    [SerializeField] private WindowView _passiveTreeWindow;
    [Tooltip("Для кнопки Tree: PassiveTreeManager на игроке")]
    [SerializeField] private PassiveTreeManager _passiveTreeManager;
    [Tooltip("Для интеграции с WindowManager (Escape, порядок окон). Если задан — открытие/закрытие через него.")]
    [SerializeField] private WindowView _windowView;

    public WindowView WindowView => _windowView;

    private const string MenuLabelsTable = "MenuLabels";
    private const int HireChoiceCount = 3;

    // Fixed 480x270 layout contract.
    private const int ScreenWidth = 480;
    private const int ScreenHeight = 270;
    private const int PanelMargin = 8;
    private const int CardWidth = 142;
    private const int CardGap = 4;

    private VisualElement _windowRoot;
    private VisualElement _overlay;
    private VisualElement _hireChoicesContainer;
    private VisualElement _hostelListContainer;
    private VisualElement _hostelContent;
    private VisualElement _recruitContent;
    private Button _tabHostel;
    private Button _tabRecruit;
    private Button _rerollButton;
    private Button _closeButton;
    private readonly List<CharacterDataSO> _currentHireChoices = new List<CharacterDataSO>();
    private bool _isNewGameMode;
    private int _activeTabIndex; // 0 = Hostel, 1 = Recruitment

    public event System.Action OnClosed;
    public bool IsOpen => _windowRoot != null && _windowRoot.style.display == DisplayStyle.Flex;

    private void OnEnable()
    {
        if (_characterDB != null) _characterDB.Init();
        if (_windowView == null) _windowView = GetComponent<WindowView>();
        BuildUI();
        if (_windowView != null)
            _windowView.OnClosed += OnTavernWindowClosed;
    }

    private void OnDisable()
    {
        if (_windowView != null)
            _windowView.OnClosed -= OnTavernWindowClosed;
    }

    private void OnTavernWindowClosed() => OnClosed?.Invoke();

    public void Open(bool forNewGame = false)
    {
        _isNewGameMode = forNewGame;
        if (_closeButton != null)
            _closeButton.style.display = forNewGame ? DisplayStyle.None : DisplayStyle.Flex;
        RefreshHireChoices();
        RefreshHostelList();
        ShowTab(_activeTabIndex);
        if (_windowView != null)
            _windowView.Open();
        else
        {
            if (_windowRoot != null) _windowRoot.style.display = DisplayStyle.Flex;
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Disable();
        }
    }

    public void Close()
    {
        if (_windowView != null)
            _windowView.Close();
        else
        {
            if (_windowRoot != null) _windowRoot.style.display = DisplayStyle.None;
            if (InputManager.InputActions != null)
                InputManager.InputActions.Player.Enable();
            OnClosed?.Invoke();
        }
    }
}
