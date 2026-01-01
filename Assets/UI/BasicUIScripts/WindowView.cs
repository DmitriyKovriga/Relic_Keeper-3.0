using UnityEngine;
using UnityEngine.UIElements;

public class WindowView : MonoBehaviour
{
    public UIDocument ui;

    private VisualElement root;
    private VisualElement overlay;
    private WindowManager manager;
    private bool isOpen;

    private void Awake()
    {
        root = ui.rootVisualElement.Q<VisualElement>("WindowRoot");
        overlay = root.Q<VisualElement>("Overlay");

        overlay?.RegisterCallback<ClickEvent>(_ => Close());
    }

    private void Start()
    {
        manager = FindFirstObjectByType<WindowManager>();

        if (manager == null)
            Debug.LogError("WindowManager NOT FOUND");

        CloseInstant();
    }

    private void Update()
    {
        
    }

    public void Open()
    {
        manager.OpenWindow(this);
    }

    internal void OpenInternal()
    {
        isOpen = true;
        root.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        manager.NotifyClosed(this);
        CloseInternal();
    }

    internal void CloseInternal()
    {
        isOpen = false;
        root.style.display = DisplayStyle.None;
    }

    private void CloseInstant()
    {
        isOpen = false;
        root.style.display = DisplayStyle.None;
    }
}
