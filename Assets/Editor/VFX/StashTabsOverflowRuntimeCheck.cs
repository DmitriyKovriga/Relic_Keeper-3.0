using System;
using System.IO;
using System.Linq;
using Scripts.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Exercises the shared stash/market presenter with real runtime layout and picking, without save changes.</summary>
    public static class StashTabsOverflowRuntimeCheck
    {
        private sealed class Grid : ITabbedItemGrid
        {
            public event Action OnChanged;
            public int TabCount { get; private set; }
            public int CurrentTabIndex { get; private set; }
            public bool CanAddTab { get; }
            public Grid(int count, bool stash) { TabCount = count; CanAddTab = stash; }
            public void SetCurrentTab(int index) { CurrentTabIndex = index; OnChanged?.Invoke(); }
            public void AddTab() { TabCount++; OnChanged?.Invoke(); }
            public bool CanRemoveTab(int index) => CanAddTab && TabCount > 1;
            public bool TryRemoveTab(int index) { TabCount--; OnChanged?.Invoke(); return true; }
            public string GetTabLabel(int index) => CanAddTab ? (index + 1).ToString() : "B" + (index + 1);
            public InventoryItem GetItem(int tab, int slot) => null;
            public InventoryItem GetItemAt(int tab, int slot, out int anchor) { anchor = -1; return null; }
            public InventoryItem TakeItem(int tab, int slot) => null;
            public bool TryAddItemPreferringTab(InventoryItem item, int tab) => false;
        }

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var previousWindow = EditorWindow.focusedWindow;
            var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show(); gameView.Focus();
            bool previousBackground = Application.runInBackground; Application.runInBackground = true;
            var temporaryObject = new GameObject("TemporaryTabOverflowCheck");
            var document = temporaryObject.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset");
            document.sortingOrder = 10000;
            var host = document.rootVisualElement;
            var previousDisplay = host.style.display; host.style.display = DisplayStyle.Flex;
            var root = new VisualElement { name = "TemporaryTabOverflowCheck" };
            root.style.position = Position.Absolute; root.style.left = 0; root.style.top = 0;
            host.Add(root); root.BringToFront();
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Inventory/InventoryStyles.uss"));
            root.style.width = 480; root.style.height = 270; root.style.backgroundColor = new Color(0.1f, 0.12f, 0.12f);
            var grids = new[] { new Grid(3, true), new Grid(12, true), new Grid(15, false) };
            var rows = new VisualElement[3];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = new VisualElement(); row.AddToClassList("stash-tabs-row"); row.style.top = 20 + i * 40;
                root.Add(row); rows[i] = row;
                Grid grid = grids[i];
                var type = typeof(InventoryUI).Assembly.GetType("StashTabsPresenter");
                var presenter = Activator.CreateInstance(type, new object[] { new Func<ITabbedItemGrid>(() => grid), null });
                void Render() => type.GetMethod("Render").Invoke(presenter, new object[] { row });
                grid.OnChanged += Render; Render();
            }
            string report = Path.Combine(Path.GetTempPath(), "stash-tabs-overflow-check.txt"); File.WriteAllText(report, "Running");
            double started = EditorApplication.timeSinceStartup; double nextStep = started + 0.5;
            int phase = 0; int clicks = 0;
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            void Activate(Button button)
            {
                var picked = root.panel.Pick(button.worldBound.center);
                Require(picked == button || button.Contains(picked), $"Another element blocks {button.text} at {button.worldBound}: picked {picked?.name}/{picked?.GetType().Name}, enabled {button.enabledSelf}, row {button.parent.worldBound}.");
                typeof(Clickable).GetMethod("SimulateSingleClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    .Invoke(button.clickable, new object[] { null, 0 });
            }
            void Tick()
            {
                EditorApplication.QueuePlayerLoopUpdate();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                if (EditorApplication.timeSinceStartup < nextStep) return;
                nextStep = EditorApplication.timeSinceStartup + 0.1;
                try
                {
                    Require(EditorApplication.isPlaying && EditorApplication.timeSinceStartup - started < 15,
                        $"Overflow check timed out/interrupted: phase {phase}, clicks {clicks}, panel {root.panel != null}, layout {root.layout}, document enabled {document.enabled}, host panel {host.panel != null}.");
                    if (root.panel == null || float.IsNaN(root.layout.width)) return;
                    // Overflow detection schedules the arrows after the first content layout.
                    if (rows.Skip(1).Any(row => row.Q<Button>("NextStashTabs").layout.width < 1f)) return;
                    foreach (var row in rows)
                    {
                        var scroll = row.Q<ScrollView>();
                        Require(scroll.horizontalScroller.resolvedStyle.display == DisplayStyle.None &&
                            scroll.verticalScroller.resolvedStyle.display == DisplayStyle.None, "Default scrollbar reappeared.");
                        Require(scroll.contentViewport.layout.height >= 13.5f, $"Viewport height lost: {scroll.contentViewport.layout}, scroll {scroll.layout}, row {row.layout}, phase {phase}, panel {root.layout}.");
                        foreach (var tab in row.Query<Button>(className: "stash-tab").ToList())
                            if (scroll.contentViewport.worldBound.Contains(tab.worldBound.center))
                            {
                                var picked = root.panel.Pick(tab.worldBound.center);
                                Require(picked == tab || tab.Contains(picked), $"Visible tab {tab.text} blocked at {tab.worldBound}; picked {picked?.name}/{picked?.GetType().Name}; phase {phase}, row {row.layout}.");
                            }
                    }
                    Require(rows[0].Q<Button>("NextStashTabs").resolvedStyle.display == DisplayStyle.None, "Short strip needs no navigation chrome.");
                    foreach (var row in rows.Skip(1))
                        Require(row.Q<Button>("NextStashTabs").layout.width <= 12.5f, "Navigation is too wide.");
                    if (phase == 0)
                    {
                        bool moved = false;
                        foreach (var row in rows.Skip(1))
                        {
                            var next = row.Q<Button>("NextStashTabs");
                            if (next.enabledSelf) { Activate(next); moved = true; clicks++; }
                        }
                        if (moved) return;
                        Require(clicks > 0, "Overflow navigation never activated."); phase = 1; return;
                    }
                    else if (phase == 1)
                    {
                        for (int i = 1; i < rows.Length; i++)
                        {
                            var last = rows[i].Query<Button>(className: "stash-tab").ToList().First(b => b.text == grids[i].GetTabLabel(grids[i].TabCount - 1));
                            Activate(last); Require(grids[i].CurrentTabIndex == grids[i].TabCount - 1, "Last tab did not select.");
                        }
                        phase = 2; return;
                    }
                    else if (phase == 2)
                    {
                        bool moved = false;
                        foreach (var row in rows.Skip(1))
                        {
                            var previous = row.Q<Button>("PreviousStashTabs");
                            if (previous.enabledSelf) { Activate(previous); moved = true; }
                        }
                        if (moved) return;
                        for (int i = 1; i < rows.Length; i++)
                        {
                            Activate(rows[i].Query<Button>(className: "stash-tab").ToList().First());
                            Require(grids[i].CurrentTabIndex == 0, "First tab did not select after scrolling back.");
                        }
                        phase = 3; return;
                    }
                    else
                    {
                        File.WriteAllText(report, "PASS: 3/12 stash tabs and 15 market tabs, both native scrollbars hidden, full-height clickable tab viewport, compact arrows, first/last tab selection and rerendered scroll restoration.");
                        Debug.Log("Tab overflow runtime check passed.");
                    }
                }
                catch (Exception ex)
                {
                    File.WriteAllText(report, "FAIL: " + ex.Message);
                    Debug.LogException(ex);
                }
                EditorApplication.update -= Tick;
                Application.runInBackground = previousBackground;
                root.RemoveFromHierarchy(); host.style.display = previousDisplay;
                Object.Destroy(temporaryObject);
                if (previousWindow != null) previousWindow.Focus();
            }
            EditorApplication.update += Tick;
        }
    }
}
