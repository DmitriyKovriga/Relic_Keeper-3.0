using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Hub;
using Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class WorldInformationVisibilityTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _created = new List<Object>();
        private readonly Dictionary<string, int?> _preferences = new Dictionary<string, int?>();
        private readonly string[] _keys = { GameplayPresentationSettings.NearbyServiceNamesKey, GameplayPresentationSettings.CombatEnemyBarsKey, GameplayPresentationSettings.RecentDummyDpsKey };

        [SetUp]
        public void SetUp()
        {
            foreach (string key in _keys)
            {
                _preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            foreach (var pair in _preferences)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            _preferences.Clear(); PlayerPrefs.Save();
        }

        private GameObject Keep(string name)
        {
            var go = new GameObject(name); _created.Add(go); return go;
        }
        private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, Private).Invoke(owner, null);
        private static void Field(object owner, string name, object value) => owner.GetType().GetField(name, Private | BindingFlags.Public).SetValue(owner, value);

        [Test]
        public void DefaultsAreContextual_ChoicesPersist_AndHudPreferencesStayUnchanged()
        {
            float scale = GameplayPresentationSettings.HudScale, opacity = GameplayPresentationSettings.HudOpacity;
            Assert.That(GameplayPresentationSettings.NearbyServiceNames && GameplayPresentationSettings.CombatEnemyBars && GameplayPresentationSettings.RecentDummyDps, Is.True);
            int changes = 0; Action changed = () => changes++;
            GameplayPresentationSettings.Changed += changed;
            try
            {
                GameplayPresentationSettings.SetNearbyServiceNames(false); GameplayPresentationSettings.SetNearbyServiceNames(false);
                GameplayPresentationSettings.SetCombatEnemyBars(false); GameplayPresentationSettings.SetRecentDummyDps(false);
                Assert.That(changes, Is.EqualTo(3));
                foreach (string key in _keys) Assert.That(PlayerPrefs.GetInt(key, -1), Is.Zero);
                Assert.That(GameplayPresentationSettings.NearbyServiceNames || GameplayPresentationSettings.CombatEnemyBars || GameplayPresentationSettings.RecentDummyDps, Is.False);
                Assert.That(GameplayPresentationSettings.HudScale, Is.EqualTo(scale)); Assert.That(GameplayPresentationSettings.HudOpacity, Is.EqualTo(opacity));
            }
            finally { GameplayPresentationSettings.Changed -= changed; }
        }

        [Test]
        public void DistanceAndRecentDamageHaveContinuousFades_AndIdleStartsHidden()
        {
            Assert.That(WorldInformationVisibility.ProximityOpacity(2), Is.EqualTo(1));
            Assert.That(WorldInformationVisibility.ProximityOpacity(3.25f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(WorldInformationVisibility.ProximityOpacity(4), Is.Zero);
            float Read(float now) => WorldInformationVisibility.RecentActivityOpacity(now, 10, 5);
            Assert.That(Read(10), Is.EqualTo(1)); Assert.That(Read(15), Is.EqualTo(1));
            Assert.That(Read(15.175f), Is.EqualTo(0.5f).Within(0.001f)); Assert.That(Read(15.35f), Is.Zero.Within(0.001f));
            Assert.That(WorldInformationVisibility.RecentActivityOpacity(0, float.NegativeInfinity, 5), Is.Zero);
        }

        [Test]
        public void ServiceLabelsFadeByPlayerDistance_WithoutChangingTheirTextOrPlacement()
        {
            var service = Keep("Service").AddComponent<HubServiceNpc>(); Invoke(service, "Awake");
            var label = service.GetComponentInChildren<WorldLocalizedLabel>(); Vector3 placement = label.transform.localPosition;
            var player = Keep("Nearby player"); Field(service, "_labelPlayer", player.transform);
            player.transform.position = new Vector3(1, 0); Invoke(service, "UpdateLabelVisibility");
            Assert.That(label.PresentationOpacity, Is.EqualTo(1)); string prompt = service.GetPrompt();
            player.transform.position = new Vector3(5, 0); Invoke(service, "UpdateLabelVisibility");
            Assert.That(label.PresentationOpacity, Is.Zero); Assert.That(service.GetPrompt(), Is.EqualTo(prompt));
            Assert.That(label.transform.localPosition, Is.EqualTo(placement));
            GameplayPresentationSettings.SetNearbyServiceNames(false); Invoke(service, "UpdateLabelVisibility");
            Assert.That(label.PresentationOpacity, Is.EqualTo(1));
        }

        private EnemyHealth Enemy(string name)
        {
            var go = Keep(name); go.AddComponent<EnemyStats>().Initialize(null, 1);
            var health = go.AddComponent<EnemyHealth>(); health.DestroyOnDeath = false; health.Initialize(); return health;
        }

        [Test]
        public void DummyShowsRealDamage_FadesAfterInactivity_AndKeepsItsTenSecondMath()
        {
            var health = Enemy("DPS dummy"); var meter = health.gameObject.AddComponent<DummyDpsMeter>();
            Invoke(meter, "Awake"); Invoke(meter, "OnEnable");
            var label = meter.GetComponentInChildren<WorldLocalizedLabel>();
            Assert.That(label.PresentationOpacity, Is.Zero);
            health.ApplyPureDamage(20, null); Assert.That(label.PresentationOpacity, Is.EqualTo(1));
            StringAssert.StartsWith("2.0/", label.CurrentText);
            Field(meter, "_lastDamageAt", Time.unscaledTime - 5.175f); Invoke(meter, "UpdateVisibility");
            Assert.That(label.PresentationOpacity, Is.EqualTo(0.5f).Within(0.02f));
            Field(meter, "_lastDamageAt", Time.unscaledTime - 6); Invoke(meter, "UpdateVisibility"); Assert.That(label.PresentationOpacity, Is.Zero);
            var window = (DummyDpsWindow)typeof(DummyDpsMeter).GetField("_window", Private).GetValue(meter);
            Assert.That(window.Evaluate(Time.unscaledTime), Is.EqualTo(2), "Visibility must not clear the damage window.");
            GameplayPresentationSettings.SetRecentDummyDps(false); Invoke(meter, "UpdateVisibility"); Assert.That(label.PresentationOpacity, Is.EqualTo(1));
            Invoke(meter, "OnDisable");
        }

        [Test]
        public void EnemyBarsRevealForAggroOrDamage_Expire_ResetOnDeath_AndUnsubscribe()
        {
            var health = Enemy("Information target"); var sensor = health.gameObject.AddComponent<EnemySensor2D>();
            var canvasGo = Keep("Information test canvas"); canvasGo.AddComponent<RectTransform>();
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var bars = Keep("Information bars").AddComponent<RectTransform>(); bars.SetParent(canvasGo.transform, false);
            var camera = Keep("Information camera").AddComponent<Camera>(); camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            camera.pixelRect = new Rect(0, 0, Screen.width, Screen.height);
            var manager = Keep("Information manager").AddComponent<WorldStatusBarsManager>();
            Field(manager, "_canvas", canvas); Field(manager, "_canvasRect", canvas.transform as RectTransform);
            Field(manager, "_barsRoot", bars); Field(manager, "_mainCamera", camera);
            Invoke(manager, "RescanTargets"); Invoke(manager, "UpdateEnemies");
            var trackedMap = (IDictionary)typeof(WorldStatusBarsManager).GetField("_enemies", Private).GetValue(manager);
            object tracked = trackedMap[health]; Assert.That(tracked, Is.Not.Null);
            var view = tracked.GetType().GetField("View").GetValue(tracked);
            var root = (RectTransform)view.GetType().GetProperty("Root").GetValue(view);
            Assert.That(root.gameObject.activeSelf, Is.False);
            typeof(EnemySensor2D).GetField("<HasTarget>k__BackingField", Private).SetValue(sensor, true);
            Invoke(manager, "UpdateEnemies"); Assert.That(root.gameObject.activeSelf, Is.True, $"An undamaged enemy in combat must be readable. Alpha={root.GetComponent<CanvasGroup>().alpha}, target={sensor.HasTarget}, canvas={canvas.transform.GetType()}, screen={camera.WorldToScreenPoint(health.transform.position + Vector3.up)}, resolution={Screen.width}x{Screen.height}");
            typeof(EnemySensor2D).GetField("<HasTarget>k__BackingField", Private).SetValue(sensor, false);
            Field(tracked, "LastActivityAt", Time.unscaledTime - 5); Invoke(manager, "UpdateEnemies"); Assert.That(root.gameObject.activeSelf, Is.False);
            health.ApplyPureDamage(10, null); Invoke(manager, "UpdateEnemies"); Assert.That(root.gameObject.activeSelf, Is.True);
            Field(tracked, "LastActivityAt", Time.unscaledTime - 4.175f); Invoke(manager, "UpdateEnemies");
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.5f).Within(0.02f));
            Field(tracked, "LastActivityAt", Time.unscaledTime - 5); Invoke(manager, "UpdateEnemies"); Assert.That(root.gameObject.activeSelf, Is.False, "Injury alone must not keep an idle bar visible forever.");
            var died = (Action<EnemyHealth>)typeof(EnemyHealth).GetField("OnDeath", Private).GetValue(health); died(health);
            health.Initialize(); Invoke(manager, "UpdateEnemies"); Assert.That(root.gameObject.activeSelf, Is.False);
            GameplayPresentationSettings.SetCombatEnemyBars(false); Invoke(manager, "UpdateEnemies"); Assert.That(root.gameObject.activeSelf, Is.True);
            Invoke(manager, "OnDisable");
            Assert.That(typeof(EnemyHealth).GetField("OnDamageReceived", Private).GetValue(health), Is.Null);
            Assert.That(typeof(EnemyHealth).GetField("OnHealthChanged", Private).GetValue(health), Is.Null);
        }

        [UnityTest]
        public IEnumerator EnglishAndRussianChoicesExist_AndHiddenLabelsStayHiddenAcrossLocaleChanges()
        {
            yield return LocalizationSettings.InitializationOperation;
            var previous = LocalizationSettings.SelectedLocale;
            var label = WorldLocalizedLabel.Create(Keep("Localized label").transform, "hub.label.stash", "Stash", Vector3.up);
            label.SetPresentationOpacity(0);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SettingsUI/SettingUXML.uxml"); var root = tree.Instantiate();
            foreach (string name in new[] { "ServiceNames", "EnemyBars", "DummyDps" })
            {
                Assert.That(root.Q<Button>(name + "Button"), Is.Not.Null);
                Assert.That(root.Q<Label>(name + "Label"), Is.Not.Null);
            }
            try
            {
                foreach (string code in new[] { "en", "ru" })
                {
                    var table = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Localization/LocalizationTables/MenuLabels_" + code + ".asset");
                    LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    yield return null;
                    foreach (var text in WorldInformationVisibility.Translations)
                    {
                        string expected = code == "ru" ? text.Russian : text.English;
                        Assert.That(table.GetEntry(text.Key)?.LocalizedValue, Is.EqualTo(expected));
                        Assert.That(WorldInformationVisibility.Text(text.Key), Is.EqualTo(expected));
                    }
                    Assert.That(label.PresentationOpacity, Is.Zero); Assert.That(label.GetComponentInChildren<TextMeshPro>().alpha, Is.Zero);
                    Assert.That(label.CurrentText, Is.EqualTo(code == "ru" ? "Склад" : "Stash"));
                }
            }
            finally { LocalizationSettings.SelectedLocale = previous; }
        }
    }
}
