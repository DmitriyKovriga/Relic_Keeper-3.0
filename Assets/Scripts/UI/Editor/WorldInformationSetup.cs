using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Scripts.UI;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.UI
{
    // Editor-only setup and QA entry points; no maintenance button is added.
    public static class WorldInformationSetup
    {
        private static GameObject _uiHost;
        private static UIDocument _document;
        private static PanelSettings _panel;
        private static RenderTexture _target;
        private static Locale _previousLocale;
        private static readonly Dictionary<string, int?> _preferences = new Dictionary<string, int?>();

        public static void PrepareSettingsCapture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Use Play Mode for runtime UI capture.");
            DisposeCapture(); _previousLocale = LocalizationSettings.SelectedLocale;
            foreach (string key in new[] { GameplayPresentationSettings.NearbyServiceNamesKey, GameplayPresentationSettings.CombatEnemyBarsKey, GameplayPresentationSettings.RecentDummyDpsKey })
                _preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            _target = new RenderTexture(480, 270, 24); _target.Create();
            _panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset"));
            _panel.targetTexture = _target;
            _uiHost = new GameObject("World information UI preview") { hideFlags = HideFlags.HideAndDontSave }; _uiHost.SetActive(false);
            _document = _uiHost.AddComponent<UIDocument>(); _document.panelSettings = _panel;
            _document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SettingsUI/SettingUXML.uxml");
            var controller = _uiHost.AddComponent<LanguageSelector>(); controller.ui = _document;
            _uiHost.SetActive(true); _document.rootVisualElement.Q("WindowRoot").style.display = DisplayStyle.Flex;
            SetEnglish();
        }
        public static void SetEnglish() => SetLocale("en");
        public static void SetRussian() => SetLocale("ru");
        private static void SetLocale(string code)
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
            _document?.rootVisualElement.MarkDirtyRepaint();
        }
        public static void CaptureSettings()
        {
            if (_document == null) throw new InvalidOperationException("Prepare the settings capture first.");
            string code = LocalizationSettings.SelectedLocale.Identifier.Code;
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                Directory.CreateDirectory("Logs/WorldInformation"); File.WriteAllBytes($"Logs/WorldInformation/Settings-{code}.png", image.EncodeToPNG());
                var report = new System.Text.StringBuilder();
                foreach (string prefix in new[] { "ServiceNames", "EnemyBars", "DummyDps" })
                {
                    var label = _document.rootVisualElement.Q<Label>(prefix + "Label"); var button = _document.rootVisualElement.Q<Button>(prefix + "Button");
                    var textSize = button.MeasureTextSize(button.text, float.MaxValue, VisualElement.MeasureMode.Undefined, float.MaxValue, VisualElement.MeasureMode.Undefined);
                    report.AppendLine($"{label.text}: {button.text}; row {button.worldBound}; text width {textSize.x}; content width {button.contentRect.width}");
                    if (button.worldBound.width <= 0 || textSize.x > button.contentRect.width) throw new InvalidOperationException("A presentation choice does not fit its settings control.");
                }
                File.WriteAllText($"Logs/WorldInformation/Layout-{code}.txt", report.ToString());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        public static void CheckChoiceClicks()
        {
            if (_document == null) throw new InvalidOperationException("Prepare the settings capture first.");
            var simulate = typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (simulate == null) throw new InvalidOperationException("Unity's Clickable simulation method is unavailable.");
            foreach (var choice in new (string Button, Func<bool> Read)[]
            {
                ("ServiceNamesButton", () => GameplayPresentationSettings.NearbyServiceNames),
                ("EnemyBarsButton", () => GameplayPresentationSettings.CombatEnemyBars),
                ("DummyDpsButton", () => GameplayPresentationSettings.RecentDummyDps)
            })
            {
                bool before = choice.Read(); var button = _document.rootVisualElement.Q<Button>(choice.Button);
                var args = new object[simulate.GetParameters().Length];
                for (int i = 0; i < args.Length; i++) if (simulate.GetParameters()[i].ParameterType == typeof(int)) args[i] = 0;
                simulate.Invoke(button.clickable, args);
                if (choice.Read() == before) throw new InvalidOperationException(choice.Button + " did not change its preference.");
                simulate.Invoke(button.clickable, args);
                if (choice.Read() != before) throw new InvalidOperationException(choice.Button + " did not restore its original preference.");
            }
            Directory.CreateDirectory("Logs/WorldInformation"); File.WriteAllText("Logs/WorldInformation/Clicks.txt", "All three actual settings buttons changed and restored their preferences in Play Mode.");
        }
        public static void CaptureLiveHub()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Use Play Mode for the live hub capture.");
            var camera = Camera.main;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                Directory.CreateDirectory("Logs/WorldInformation"); File.WriteAllBytes("Logs/WorldInformation/LiveHub.png", image.EncodeToPNG());
                var report = new System.Text.StringBuilder();
                foreach (var service in Object.FindObjectsByType<Scripts.Hub.HubServiceNpc>(FindObjectsSortMode.None))
                {
                    var label = service.GetComponentInChildren<WorldLocalizedLabel>();
                    report.AppendLine($"{service.name}: {label?.CurrentText}, opacity={label?.PresentationOpacity}");
                }
                foreach (var meter in Object.FindObjectsByType<Scripts.Enemies.DummyDpsMeter>(FindObjectsSortMode.None))
                {
                    var label = meter.GetComponentInChildren<WorldLocalizedLabel>();
                    report.AppendLine($"Training DPS: {label?.CurrentText}, opacity={label?.PresentationOpacity}");
                }
                File.WriteAllText("Logs/WorldInformation/LiveHub.txt", report.ToString());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
        }
        public static void DisposeCapture()
        {
            if (_uiHost != null) Object.DestroyImmediate(_uiHost);
            if (_panel != null) Object.DestroyImmediate(_panel);
            if (_target != null) Object.DestroyImmediate(_target);
            _uiHost = null; _panel = null; _target = null; _document = null;
            if (_previousLocale != null) LocalizationSettings.SelectedLocale = _previousLocale;
            _previousLocale = null;
            foreach (var pair in _preferences)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            if (_preferences.Count > 0) PlayerPrefs.Save(); _preferences.Clear();
        }

        public static void SyncLocales()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("MenuLabels");
            if (collection == null) throw new InvalidOperationException("MenuLabels was not found.");
            var en = collection.GetTable("en") as StringTable; var ru = collection.GetTable("ru") as StringTable;
            if (en == null || ru == null) throw new InvalidOperationException("MenuLabels needs both English and Russian tables.");
            foreach (var text in WorldInformationVisibility.Translations)
            {
                en.AddEntry(text.Key, text.English); ru.AddEntry(text.Key, text.Russian);
            }
            foreach (var asset in new UnityEngine.Object[] { en, ru, collection.SharedData })
            {
                EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }
}
