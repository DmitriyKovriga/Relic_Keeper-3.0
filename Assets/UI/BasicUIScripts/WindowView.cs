using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class WindowView : MonoBehaviour
{
    public UIDocument ui;

    private VisualElement root;
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

        // Ищем WindowRoot, который создал PassiveTreeUI
        root = ui.rootVisualElement.Q<VisualElement>("WindowRoot");

        if (root == null)
        {
            // Если все равно не нашли - значит что-то сломалось в генерации
            Debug.LogError($"[WindowView] Не найден 'WindowRoot' в '{gameObject.name}'. Проверь PassiveTreeUI.", gameObject);
            return;
        }

        overlay = root.Q<VisualElement>("Overlay"); // Оверлея может и не быть в коде генерации, это не критично
        if (overlay != null) overlay.RegisterCallback<ClickEvent>(_ => Close());

        // ГЛАВНОЕ: Скрываем окно сразу после того, как нашли корень
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
        root.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        if (!isInitialized) return;
        manager.NotifyClosed(this);
    }

    internal void CloseInternal()
    {
        if (!isInitialized) return;
        root.style.display = DisplayStyle.None;
    }

    private void CloseInstant()
    {
        if (root != null)
        {
            root.style.display = DisplayStyle.None;
        }
    }
}