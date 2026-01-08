using UnityEngine;
using System.Collections.Generic;

public class WindowManager : MonoBehaviour
{
    private readonly Stack<WindowView> windowStack = new();

    public void OpenWindow(WindowView window)
    {
        if (windowStack.Count > 0 && windowStack.Peek() == window)
            return;

        windowStack.Push(window);
        window.OpenInternal();
    }

    public void CloseTop()
    {
        if (windowStack.Count == 0)
            return;

        var top = windowStack.Pop();
        top.CloseInternal();
    }

    public void NotifyClosed(WindowView window)
    {
        // если окно закрылось само (overlay)
        if (windowStack.Contains(window))
        {
            var temp = new Stack<WindowView>();
            while (windowStack.Peek() != window)
                temp.Push(windowStack.Pop());

            windowStack.Pop(); // убрали нужное

            while (temp.Count > 0)
                windowStack.Push(temp.Pop());
        }
    }

    public bool HasOpenWindow => windowStack.Count > 0;
}
