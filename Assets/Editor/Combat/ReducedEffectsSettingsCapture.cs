using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Combat
{
    /// <summary>Explicit temporary live UI capture; never saves scenes or source assets.</summary>
    public static class ReducedEffectsSettingsCapture
    {
        private static GameObject _host;
        private static UIDocument _document;
        private static PanelSettings _panel;
        private static RenderTexture _texture;
        private static Locale _locale;
        private static string _language;
        private static float _strength;
        private static bool _hadLanguage, _hadStrength;
        private static bool _hadReduced, _reduced;
        private static int _stage, _frame, _lastFrame;
        private const string Output = "Logs/ReducedEffectsSettings";

        public static void Begin()
        {
            if (!Application.isPlaying || _host != null) throw new InvalidOperationException("Requires Play Mode and no active settings capture.");
            Directory.CreateDirectory(Output);
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey); _reduced = GameplayPresentationSettings.ReducedCombatEffects;
            _locale = LocalizationSettings.SelectedLocale;
            _hadLanguage = PlayerPrefs.HasKey("selected_language"); _language = PlayerPrefs.GetString("selected_language");
            _hadStrength = PlayerPrefs.HasKey(GameplayPresentationSettings.CameraImpulseStrengthKey);
            _strength = PlayerPrefs.GetFloat(GameplayPresentationSettings.CameraImpulseStrengthKey);
            _texture = new RenderTexture(480, 270, 0); _texture.Create();
            _panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset"));
            _panel.targetTexture = _texture; _panel.clearColor = true; _panel.colorClearValue = new Color(.08f, .09f, .1f);
            _host = new GameObject("Temporary Camera Settings Review"); _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>(); _document.panelSettings = _panel;
            _document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SettingsUI/SettingUXML.uxml");
            _host.AddComponent<LanguageSelector>().ui = _document;
            _host.SetActive(true); _document.rootVisualElement.Q("WindowRoot").style.display = DisplayStyle.Flex;
            _stage = _frame = 0; _lastFrame = -1;
            GameplayPresentationSettings.SetReducedCombatEffects(false);
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!Application.isPlaying) { End(); return; }
            EditorApplication.QueuePlayerLoopUpdate();
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;
            if (++_frame < 12) return;
            _frame = 0;
            try
            {
                var label = _document.rootVisualElement.Q<Label>("CombatEffectsLabel");
                var slider = _document.rootVisualElement.Q<Button>("CombatEffectsButton");
                if (label.worldBound.x < 0 || slider.worldBound.xMax > 480 || slider.worldBound.yMax > 270)
                    throw new InvalidOperationException("Camera setting is outside native 480x270 layout.");
                string expected = _stage == 0 ? "Combat effects" : "Эффекты боя";
                if (label.text != expected) throw new InvalidOperationException("Live locale did not update camera setting.");
                Capture(_stage == 0 ? "settings-en" : _stage == 1 ? "settings-ru" : "settings-reduced");
                if (_stage == 0) LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ru");
                else if (_stage == 1)
                {
                    using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = slider; slider.SendEvent(submit); }
                    if (!GameplayPresentationSettings.ReducedCombatEffects) throw new InvalidOperationException("Settings button did not persist Reduced mode.");
                }
                else
                {
                    File.WriteAllText(Output + "/audit.json", "{\"status\":\"complete\",\"locales\":[\"en\",\"ru\"],\"nativeSize\":[480,270],\"reducedSaved\":true}");
                    End(); return;
                }
                _stage++;
            }
            catch (Exception exception)
            {
                File.WriteAllText(Output + "/audit.json", JsonUtility.ToJson(new Failure { error = exception.ToString() }, true));
                Debug.LogException(exception); End();
            }
        }

        [Serializable] private sealed class Failure { public string status = "failed", error; }
        private static void Capture(string name)
        {
            var before = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = _texture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(false, false);
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = before; Object.DestroyImmediate(image); }
        }

        private static void End()
        {
            GameplayPresentationSettings.SetReducedCombatEffects(_reduced);
            if (!_hadReduced) PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            EditorApplication.update -= Tick;
            if (_host != null) Object.DestroyImmediate(_host); _host = null;
            if (_panel != null) Object.DestroyImmediate(_panel);
            if (_texture != null) { _texture.Release(); Object.DestroyImmediate(_texture); }
            if (_hadLanguage) PlayerPrefs.SetString("selected_language", _language); else PlayerPrefs.DeleteKey("selected_language");
            if (_hadStrength) GameplayPresentationSettings.SetCameraImpulseStrength(_strength);
            else { GameplayPresentationSettings.SetCameraImpulseStrength(.5f); PlayerPrefs.DeleteKey(GameplayPresentationSettings.CameraImpulseStrengthKey); }
            PlayerPrefs.Save(); LocalizationSettings.SelectedLocale = _locale;
        }
    }
}
