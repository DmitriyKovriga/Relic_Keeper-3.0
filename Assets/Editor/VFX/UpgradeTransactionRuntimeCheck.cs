using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.PassiveTree.UI;
using Scripts.Stats;
using Scripts.UI;
using Scripts.Economy;
using Scripts.Saving;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Offscreen native UI, real operation callbacks, disposable data. No player inventory edits.</summary>
    public static class UpgradeTransactionRuntimeCheck
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<Object> Owned = new List<Object>();
        private static readonly List<OrbCountEntry> FixtureCurrency = new List<OrbCountEntry>();
        private static readonly FieldInfo InventorySingleton = typeof(InventoryManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo TooltipSingleton = typeof(ItemTooltipController).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static GameObject _host;
        private static InventoryManager _inventory;
        private static InventoryUI _ui;
        private static ItemTooltipController _tooltip;
        private static VisualElement _root;
        private static RenderTexture _texture;
        private static PanelSettings _panel;
        private static Locale _locale;
        private static bool _hadLanguage, _background;
        private static string _language;
        private static GamePauseService.PauseHandle _pause;
        private static InventoryItem _craftItem;
        private static CraftingOrbSO _orb;
        private static PassiveTreeManager _treeManager;
        private static PassiveTreeUI _treeUi;
        private static PassiveTreeRenderer _renderer;
        private static double _next, _deadline;
        private static int _stage;
        private static Rect _craftBounds;
        private static string Output => Path.Combine(Path.GetTempPath(), "upgrade-transaction-check");

        public static void Capture()
        {
            if (!EditorApplication.isPlaying || _host != null) throw new InvalidOperationException("Run once in Play Mode.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Report.txt", "RUNNING");
            FixtureCurrency.Clear();
            _locale = LocalizationSettings.SelectedLocale;
            _hadLanguage = PlayerPrefs.HasKey("selected_language"); _language = PlayerPrefs.GetString("selected_language");
            _background = Application.runInBackground; Application.runInBackground = true;
            _pause = GamePauseService.Acquire(GamePauseReason.Inventory);
            try
            {
                _texture = Own(new RenderTexture(480, 270, 0)); _texture.Create();
                _panel = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset")));
                _panel.targetTexture = _texture; _panel.clearColor = true; _panel.colorClearValue = new Color(.08f, .1f, .1f);
                _host = Own(new GameObject("Temporary Upgrade Transaction Check")); _host.SetActive(false);
                var document = _host.AddComponent<UIDocument>(); document.panelSettings = _panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Inventory/InventoryLayout.uxml");
                _ui = _host.AddComponent<InventoryUI>(); _ui.enabled = false;
                _tooltip = _host.AddComponent<ItemTooltipController>(); _tooltip.enabled = false;
                object oldTooltip = TooltipSingleton.GetValue(null); TooltipSingleton.SetValue(null, null);
                try { _host.SetActive(true); } finally { TooltipSingleton.SetValue(null, oldTooltip); }
                _root = document.rootVisualElement; UIFontApplier.ApplyToRoot(_root);
                var managerHost = Own(new GameObject("Temporary Upgrade Inventory")); managerHost.SetActive(false);
                _inventory = managerHost.AddComponent<InventoryManager>();
                object previous = InventorySingleton.GetValue(null); InventorySingleton.SetValue(null, null);
                try { Invoke(_inventory, "Awake"); } finally { InventorySingleton.SetValue(null, previous); }
                Set(_ui, "_uiDoc", document); Set(_ui, "_root", _root);
                Set(_ui, "_windowRoot", _root.Q("WindowRoot"));
                Set(_ui, "_inventoryContainer", _root.Q("InventoryGrid"));
                Set(_ui, "_equipmentView", _root.Q("EquipmentView"));
                Set(_tooltip, "_root", _root);
                Invoke(_tooltip, "BuildTooltipStructureAtRoot");
                WithInventory(() =>
                {
                    Invoke(_ui, "CreateGhostIcon"); Invoke(_ui, "GenerateBackpackGrid"); Invoke(_ui, "SetupEquipmentSlots");
                    Invoke(_ui, "LoadOrbSlotsConfig"); Invoke(_ui, "SetupTabs"); Invoke(_ui, "SetupCraftView");
                    Invoke(_ui, "ApplyInventoryArtTheme"); Invoke(_ui, "RefreshInventorySlotLabels");
                    _inventory.OnInventoryChanged += Refresh;
                    var data = Helmet();
                    _inventory.PlaceItemAt(new InventoryItem(data), InventoryManager.EQUIP_OFFSET, -1);
                    _inventory.PlaceItemAt(new InventoryItem(data), 0, -1);
                    Invoke(_ui, "RefreshInventory");
                });
                _root.Q("WindowRoot").style.display = DisplayStyle.Flex;
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                _stage = 0; _next = EditorApplication.timeSinceStartup + .4; _deadline = _next + 25;
                EditorApplication.update += Tick;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Refresh() => Invoke(_ui, "RefreshInventory");
        private static T Own<T>(T obj) where T : Object { Owned.Add(obj); return obj; }
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private | BindingFlags.Public).SetValue(obj, value);
        private static object Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void WithInventory(Action action)
        {
            var currency = new List<OrbCountEntry>(); CraftingCurrencyWallet.WriteToSave(currency);
            object oldInventory = InventorySingleton.GetValue(null), oldTooltip = TooltipSingleton.GetValue(null);
            InventorySingleton.SetValue(null, _inventory); TooltipSingleton.SetValue(null, _tooltip);
            CraftingCurrencyWallet.LoadFromSave(FixtureCurrency);
            try { action(); }
            finally
            {
                CraftingCurrencyWallet.WriteToSave(FixtureCurrency); CraftingCurrencyWallet.LoadFromSave(currency);
                InventorySingleton.SetValue(null, oldInventory); TooltipSingleton.SetValue(null, oldTooltip);
            }
        }
        private static ArmorItemSO Helmet()
        {
            var source = AssetDatabase.FindAssets("t:ArmorItemSO").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ArmorItemSO>).First(a => a.Slot == EquipmentSlot.Helmet && !a.IsUnique);
            return Own(Object.Instantiate(source));
        }
        private static void PrepareCraft()
        {
            var data = Helmet(); data.DropLevel = 100;
            var pool = Own(ScriptableObject.CreateInstance<AffixPoolSO>()); pool.Affixes = new List<ItemAffixSO>(); data.AffixPool = pool;
            _craftItem = new InventoryItem(data);
            foreach (var stat in new[] { StatType.Armor, StatType.Evasion, StatType.CritChance, StatType.AttackSpeed })
            {
                var affix = Own(ScriptableObject.CreateInstance<ItemAffixSO>());
                affix.UniqueID = "upgrade_check_" + stat; affix.GroupID = affix.UniqueID; affix.Tier = 5;
                affix.Stats = new[] { new ItemAffixSO.AffixStatData { Stat = stat, Scope = StatScope.Global, Type = stat == StatType.AttackSpeed ? StatModType.PercentAdd : StatModType.Flat, MinValue = 1, MaxValue = 8 } };
                pool.Affixes.Add(affix);
                var instance = new AffixInstance(affix, _craftItem);
                instance.Modifiers[0].PrimaryMod = new StatModifier(30, instance.Modifiers[0].PrimaryMod.Type);
                _craftItem.Affixes.Add(instance);
            }
            _inventory.SetCraftingSlotItem(_craftItem); _ui.SetTab(1);
            _orb = Own(ScriptableObject.CreateInstance<CraftingOrbSO>()); _orb.ID = "upgrade_runtime_fixture"; _orb.EffectId = CraftingOrbEffectId.RerollRare;
            _inventory.AddOrb(_orb.ID, 1);
        }
        private static bool Craft()
        {
            Set(_ui, "_applyOrbOrb", _orb); Set(_ui, "_applyOrbMode", true);
            return (bool)Invoke(_ui, "TryApplyOrbOnPointerUp", _root.Q("CraftSlot").worldBound.center, true);
        }
        private static void SetupTree()
        {
            _tooltip.HideTooltipImmediate();
            _root.Q("InventoryScreenRoot").style.display = DisplayStyle.None;
            var container = new VisualElement(); container.style.position = Position.Absolute;
            container.style.width = 480; container.style.height = 270; _root.Add(container);
            var tree = Own(ScriptableObject.CreateInstance<PassiveSkillTreeSO>());
            var start = new PassiveNodeDefinition { ID = "start", NodeType = PassiveNodeType.Start, Position = new Vector2(80, 130) };
            var next = new PassiveNodeDefinition { ID = "next", Position = new Vector2(200, 130) };
            var blocked = new PassiveNodeDefinition { ID = "blocked", Position = new Vector2(320, 130) };
            start.ConnectionIDs.Add("next"); next.ConnectionIDs.Add("start"); next.ConnectionIDs.Add("blocked"); blocked.ConnectionIDs.Add("next");
            tree.Nodes.Add(start); tree.Nodes.Add(next); tree.Nodes.Add(blocked); tree.InitLookup();
            var managerHost = Own(new GameObject("Temporary Upgrade Tree")); managerHost.SetActive(false);
            var stats = managerHost.AddComponent<PlayerStats>();
            Set(stats, "<Leveling>k__BackingField", new LevelingSystem(1, 0, 100, 1));
            _treeManager = managerHost.AddComponent<PassiveTreeManager>(); Set(_treeManager, "_playerStats", stats); Set(_treeManager, "_treeData", tree);
            ((HashSet<string>)typeof(PassiveTreeManager).GetField("_allocatedNodeIDs", Private).GetValue(_treeManager)).Add("start");
            _treeUi = managerHost.AddComponent<PassiveTreeUI>(); _treeUi.enabled = false;
            var theme = AssetDatabase.LoadAssetAtPath<PassiveTreeThemeSO>("Assets/Scripts/Skills/PassiveTree/UI/Themes/DefaultPassiveTreeTheme.asset");
            _renderer = new PassiveTreeRenderer(container, theme, new PassiveTreeTooltip(_root),
                id => Invoke(_treeUi, "OnNodeClick", id), _ => { });
            Set(_treeUi, "_treeManager", _treeManager); Set(_treeUi, "_renderer", _renderer);
            _renderer.BuildGraph(tree); _renderer.UpdateVisuals(_treeManager);
            _treeManager.OnTreeUpdated += () => _renderer.UpdateVisuals(_treeManager);
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.isPlaying && EditorApplication.timeSinceStartup < _deadline, "Interrupted or timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < _next) return;
                WithInventory(() =>
                {
                    switch (_stage)
                    {
                        case 0:
                            Require((bool)Invoke(_ui, "TryCtrlEquipWithFeedback", 0), "Equipment swap rejected."); break;
                        case 1:
                            Require(_root.Q("TransactionFeedback") != null, "Equipment confirmation missing while paused."); CaptureFrame("Equipment-en");
                            PrepareCraft(); break;
                        case 2:
                            _craftBounds = _root.Q("CraftSlot").worldBound;
                            var rng = UnityEngine.Random.state;
                            try { UnityEngine.Random.InitState(90210); Require(Craft(), "Reroll failed."); } finally { UnityEngine.Random.state = rng; }
                            Require(_craftItem.Affixes.All(a => a.Modifiers[0].PrimaryMod.Value <= 8), "Committed lower rolls were not used."); break;
                        case 3:
                            ValidateCraftLayout(false); CaptureFrame("Craft-lower-en");
                            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ru");
                            Invoke(_tooltip, "OnLocaleChanged", LocalizationSettings.SelectedLocale); Invoke(_ui, "OnInventoryLocaleChanged", LocalizationSettings.SelectedLocale); break;
                        case 4:
                            ValidateCraftLayout(true); CaptureFrame("Craft-lower-ru");
                            Require(!Craft(), "An empty currency stack must reject.");
                            Require(_inventory.GetOrbCount(_orb.ID) == 0, "A rejection changed currency."); break;
                        case 5:
                            Require(_root.Q("TransactionFeedback").Q<Label>().text == "!", "Rejection cue missing."); CaptureFrame("Craft-rejected-ru");
                            _orb.EffectId = CraftingOrbEffectId.PurgeAll; _inventory.AddOrb(_orb.ID, 1); Require(Craft(), "Purge failed."); break;
                        case 6:
                            Require(_craftItem.Affixes.Count == 0 && _root.Query<Label>("CraftChangeRow").ToList().All(l => l.text.StartsWith("- ")), "Purge must show removed rolls.");
                            CaptureFrame("Craft-purged-ru"); SetupTree(); break;
                        case 7:
                            Invoke(_treeUi, "OnNodeClick", "next"); Require(_treeManager.IsAllocated("next") && _treeManager.SkillPoints == 0, "Allocation was not committed exactly once."); break;
                        case 8:
                            Require(_root.Q("TransactionFeedback") != null, "Node confirmation missing."); CaptureFrame("Passive-allocated");
                            Invoke(_treeUi, "OnNodeClick", "blocked"); Require(!_treeManager.IsAllocated("blocked") && _treeManager.SkillPoints == 0, "Rejected allocation changed points."); break;
                        case 9: CaptureFrame("Passive-rejected"); _next = EditorApplication.timeSinceStartup + .8; break;
                        case 10:
                            Require(_root.Q("TransactionFeedback") == null, "Feedback did not expire on the paused UI clock.");
                            File.WriteAllText(Output + "/Report.txt", "PASS: equipment swap; paused confirmation; committed lower rerolls; EN/RU live tooltip; fixed craft target; currency rejection; removed affixes after purge; one passive point spent; no-point rejection; nonblocking bounded overlays; automatic cleanup. Disposable inventory/tree, original singleton restored between callbacks.");
                            End(); return;
                    }
                });
                _stage++; if (_stage != 10) _next = EditorApplication.timeSinceStartup + .16;
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void ValidateCraftLayout(bool russian)
        {
            Require(_root.Q("CraftSlot").worldBound == _craftBounds, "Crafting moved its pointer target.");
            var heading = _root.Q<Label>("CraftResultHeading");
            Require(heading != null && heading.text.StartsWith(russian ? "Крафт" : "Craft"), "Craft result did not follow locale.");
            var box = _root.Q("GlobalItemTooltip");
            Require(box.worldBound.xMin >= 0 && box.worldBound.yMin >= 0 && box.worldBound.xMax <= 480 && box.worldBound.yMax <= 270, "Craft tooltip exceeds native canvas: " + box.worldBound);
            Require(_root.Query<Label>("CraftChangeRow").ToList().Count == 4, "Wrong number of actual affix changes.");
        }
        private static void CaptureFrame(string name)
        {
            var previous = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try { RenderTexture.active = _texture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex) { File.WriteAllText(Output + "/Report.txt", "FAIL: " + ex); Debug.LogException(ex); End(); }
        private static void End()
        {
            EditorApplication.update -= Tick;
            if (_ui != null && typeof(InventoryUI).GetField("_transactionFeedback", Private).GetValue(_ui) is IDisposable disposable) disposable.Dispose();
            _renderer?.DisposeTransaction(); _renderer = null;
            for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) Object.DestroyImmediate(Owned[i]); Owned.Clear(); _host = null;
            LocalizationSettings.SelectedLocale = _locale;
            if (_hadLanguage) PlayerPrefs.SetString("selected_language", _language); else PlayerPrefs.DeleteKey("selected_language");
            _pause?.Dispose(); _pause = null; Application.runInBackground = _background;
        }
    }
}
