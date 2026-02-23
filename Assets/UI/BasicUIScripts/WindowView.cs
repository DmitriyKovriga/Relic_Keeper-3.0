using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;

public class WindowView : MonoBehaviour
{
    public event Action OnClosed;
    public event Action OnOpened;
    public UIDocument ui;

    /// <summary> Внутренний контейнер окна (WindowRoot из UXML). Показ/скрытие окна — через root. </summary>
    private VisualElement root;
    /// <summary> Корень UIDocument. При закрытии ставим pickingMode = Ignore, чтобы не перехватывать ввод; видимость меняем у root. </summary>
    private VisualElement documentRoot;
    private VisualElement overlay;
    private WindowManager manager;
    
    private bool isInitialized = false;

    private void Awake()
    {
        // В Awake мы только ищем зависимости, но НЕ ищем WindowRoot, 
        // так как PassiveTreeUI может его еще не создать.
        if (ui == null) ui = GetComponent<UIDocument>();
    }

    private void Start()
    {
        manager = FindFirstObjectByType<WindowManager>();
        if (manager == null) Debug.LogError("WindowManager NOT FOUND");

        // Запускаем инициализацию с небольшой задержкой (1 кадр), 
        // чтобы гарантировать, что PassiveTreeUI.OnEnable успел отработать.
        StartCoroutine(LateInitialize());
    }

    private IEnumerator LateInitialize()
    {
        // Ждем конца кадра. К этому моменту PassiveTreeUI точно построит интерфейс.
        yield return new WaitForEndOfFrame();
        
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        if (ui == null || ui.rootVisualElement == null) return;

        documentRoot = ui.rootVisualElement;
        UIFontApplier.ApplyToRoot(documentRoot);
        root = documentRoot.Q<VisualElement>("WindowRoot");
        if (root == null)
            root = documentRoot;

        overlay = root.Q<VisualElement>("Overlay");
        if (overlay != null)
            overlay.RegisterCallback<ClickEvent>(evt => { if (evt.target == overlay) Close(); });

        // Скрываем содержимое окна и отключаем приём ввода у корня документа (чтобы не блокировать другие окна)
        CloseInstant();
        isInitialized = true;
    }

    public void Open()
    {
        if (!isInitialized) return;
        manager.OpenWindow(this);
    }

    internal void OpenInternal()
    {
        if (!isInitialized) return;
        if (documentRoot != null) documentRoot.pickingMode = PickingMode.Position;
        if (root != null) root.style.display = DisplayStyle.Flex;
        if (documentRoot != null) documentRoot.Focus();
        OnOpened?.Invoke();
    }

    /// <summary> Вызывается WindowManager: больший order = окно поверх и первым получает ввод. </summary>
    internal void SetPanelSortOrder(int order)
    {
        if (ui != null)
            ui.sortingOrder = order;
    }

    public void Close()
    {
        if (!isInitialized) return;
        manager.NotifyClosed(this);
    }

    internal void CloseInternal()
    {
        if (!isInitialized) return;
        if (root != null) root.style.display = DisplayStyle.None;
        if (documentRoot != null) documentRoot.pickingMode = PickingMode.Ignore;
        SetPanelSortOrder(0);
        OnClosed?.Invoke();
    }

    private void CloseInstant()
    {
        if (root != null) root.style.display = DisplayStyle.None;
        if (documentRoot != null) documentRoot.pickingMode = PickingMode.Ignore;
        SetPanelSortOrder(0);
    }
}
