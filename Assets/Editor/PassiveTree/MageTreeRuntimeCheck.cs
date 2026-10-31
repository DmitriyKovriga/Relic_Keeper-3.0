using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Scripts.Combat;
using Scripts.Configuration;
using Scripts.Enemies;
using Scripts.Saving;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.PassiveTree.UI;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Scripts.Editor.PassiveTree
{
    public static class MageTreeRuntimeCheck
    {
        private static readonly List<Object> Owned = new List<Object>();
        private static GameObject _host;
        private static VisualElement _root;
        private static RenderTexture _texture;
        private static PassiveTreeRenderer _renderer;
        private static PassiveTreeTooltip _tooltip;
        private static VisualElement _tooltipBox;
        private static PassiveSkillTreeSO _tree;
        private static Locale _locale;
        private static bool _background, _immortal, _hadLanguage;
        private static string _language;
        private static GamePauseService.PauseHandle _pause;
        private static double _next, _deadline;
        private static int _stage;
        private const string Output = "Logs/MageTreeAudit/Runtime";
        private static T Own<T>(T value) where T : Object { Owned.Add(value); return value; }

        public static void Capture()
        {
            if (!EditorApplication.isPlaying || _host != null) throw new InvalidOperationException("Run once in hub Play Mode.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Report.txt", "RUNNING");
            _locale = LocalizationSettings.SelectedLocale; _background = Application.runInBackground;
            _hadLanguage = PlayerPrefs.HasKey("selected_language"); _language = PlayerPrefs.GetString("selected_language");
            _immortal = PlaytestConfiguration.PlayerImmortal;
            _pause = GamePauseService.Acquire(GamePauseReason.Inventory); Application.runInBackground = true;
            try
            {
                PlaytestConfiguration.SetEditorPlayerImmortal(false);
                _texture = Own(new RenderTexture(480, 270, 0)); _texture.Create();
                var panel = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset")));
                panel.targetTexture = _texture; panel.clearColor = true; panel.colorClearValue = new Color(.005f, .006f, .008f);
                _host = Own(new GameObject("Temporary mage passive check")); _host.SetActive(false);
                var doc = _host.AddComponent<UIDocument>(); doc.panelSettings = panel; _host.SetActive(true);
                _root = doc.rootVisualElement;
                _tree = Own(ScriptableObject.CreateInstance<PassiveSkillTreeSO>());
                string[] paths = { MageTreeNodeCompletion.ManaBarrierPath, MageTreeNodeCompletion.ManaPowerPath, MageTreeNodeCompletion.CritPath, MageTreeNodeCompletion.ChainPath };
                for (int i = 0; i < paths.Length; i++)
                    _tree.Nodes.Add(new PassiveNodeDefinition { ID = i.ToString(), NodeType = i < 2 ? PassiveNodeType.Keystone : PassiveNodeType.Notable,
                        Position = new Vector2(65 + i * 115, 85), Template = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(paths[i]) });
                var content = new VisualElement(); content.style.position = Position.Absolute; content.style.width = 480; content.style.height = 270; _root.Add(content);
                _tooltip = new PassiveTreeTooltip(_root);
                _tooltipBox = (VisualElement)typeof(PassiveTreeTooltip).GetField("_tooltipBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_tooltip);
                _renderer = new PassiveTreeRenderer(content, AssetDatabase.LoadAssetAtPath<PassiveTreeThemeSO>("Assets/Scripts/Skills/PassiveTree/UI/Themes/DefaultPassiveTreeTheme.asset"), _tooltip, _ => { }, _ => { });
                _renderer.BuildGraph(_tree);
                var manager = _host.AddComponent<PassiveTreeManager>();
                var allocated = (HashSet<string>)typeof(PassiveTreeManager).GetField("_allocatedNodeIDs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                foreach (var node in _tree.Nodes) allocated.Add(node.ID);
                _renderer.UpdateVisuals(manager);

                var statsHost = Own(new GameObject("Temporary mana absorption check"));
                var stats = statsHost.AddComponent<PlayerStats>(); stats.GetStat(StatType.MaxHealth).BaseValue = 1000; stats.Health.RestoreFull();
                stats.GetStat(StatType.MaxMana).BaseValue = 1000; stats.Mana.RestoreFull(); stats.GetStat(StatType.ManaBarrierAbsorption).BaseValue = 50;
                var receiver = statsHost.AddComponent<PlayerDamageReceiver>();
                var hit = receiver.TakeDamageDetailed(new DamageSnapshot(new object()) { Physical = 100 });
                if (Mathf.Abs(hit.FinalHealthDelta - 50) > .001f || Mathf.Abs(hit.ManaAbsorbedDamage - 50) > .001f) throw new InvalidOperationException("Runtime mana split was not 50 health / 50 mana.");
                File.WriteAllText(Output + "/Damage.txt", $"100 incoming damage -> {hit.FinalHealthDelta} Health, {hit.ManaAbsorbedDamage} Mana. Disposable player, no save edits.");
                SetLocale(false); UIFontApplier.ApplyToRoot(_root);
                _stage = 0; _next = EditorApplication.timeSinceStartup + .7; _deadline = _next + 20;
                EditorApplication.update += Tick;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void SetLocale(bool ru)
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(ru ? "ru" : "en");
            foreach (var old in _root.Query<Label>("MageNodeName").ToList()) old.RemoveFromHierarchy();
            string[] names = ru ? new[] { "Манабарьер", "Сила маны", "Звездочёт", "Растущее напряжение" } : new[] { "Mana Barrier", "Mana Power", "Stargazer", "Growing Tension" };
            for (int i = 0; i < names.Length; i++)
            {
                var label = new Label(names[i]) { name = "MageNodeName", pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute; label.style.left = 9 + i * 115; label.style.top = 120;
                label.style.width = 112; label.style.fontSize = 8; label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.color = new Color(.85f, .82f, .7f);
                _root.Add(label);
            }
            UIFontApplier.ApplyToRoot(_root);
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > _deadline) throw new InvalidOperationException("Interrupted or timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < _next) return;
                switch (_stage++)
                {
                    case 0: Frame("IconsEN"); Show(0); break;
                    case 1: Frame("ManaBarrierEN"); Show(2); break;
                    case 2: Frame("StargazerEN"); Show(3); break;
                    case 3: Frame("ChainEN"); _tooltip.Hide(); SetLocale(true); break;
                    case 4: Frame("IconsRU"); Show(0); break;
                    case 5: Frame("ManaBarrierRU"); Show(2); break;
                    case 6: Frame("StargazerRU"); Show(3); break;
                    case 7: Frame("ChainRU"); File.WriteAllText(Output + "/Report.txt", "PASS: native 480x270 real tree renderer/icons and tooltips in EN/RU; live mana absorption 50/50 on disposable player. Layout is a presentation fixture, not a changed tree graph. Inspect captures separately."); End(); return;
                }
                _next = EditorApplication.timeSinceStartup + .35;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Show(int index)
        {
            if (_tooltipBox.panel == null) throw new InvalidOperationException("Tooltip detached from native panel.");
            _tooltip.Show(_tree.Nodes[index], new Rect(41 + index * 115, 61, 48, 48));
            _tooltipBox.BringToFront();
        }
        private static void Frame(string name)
        {
            if (!name.StartsWith("Icons"))
            {
                Rect bounds = _tooltipBox.worldBound;
                if (_tooltipBox.panel == null || _tooltipBox.resolvedStyle.display == DisplayStyle.None || bounds.width <= 0 || bounds.height <= 0 ||
                    bounds.xMin < 0 || bounds.yMin < 0 || bounds.xMax > 480.1f || bounds.yMax > 270.1f)
                    throw new InvalidOperationException("Tooltip exceeds or is absent from the native canvas: " + name + " " + bounds);
            }
            var previous = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try { RenderTexture.active = _texture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex) { File.WriteAllText(Output + "/Report.txt", "FAIL: " + ex); Debug.LogException(ex); End(); }
        private static void End()
        {
            EditorApplication.update -= Tick; _renderer?.DisposeTransaction(); _renderer = null;
            for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) Object.DestroyImmediate(Owned[i]); Owned.Clear(); _host = null;
            LocalizationSettings.SelectedLocale = _locale;
            if (_hadLanguage) PlayerPrefs.SetString("selected_language", _language); else PlayerPrefs.DeleteKey("selected_language");
            PlaytestConfiguration.SetEditorPlayerImmortal(_immortal); _pause?.Dispose(); _pause = null; Application.runInBackground = _background;
        }
    }
}
