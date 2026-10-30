using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Opt-in death UI checks with an owned return callback; never kills/removes a real character.</summary>
    public static class DeathScreenRuntimeCheck
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject _host;
        private static PanelSettings _panel;
        private static RenderTexture _texture;
        private static DeathScreenOverlay _overlay;
        private static UIDocument _document;
        private static Locale _locale;
        private static bool _background, _hadLanguage, _held, _pressed;
        private static bool _warming;
        private static string _language;
        private static float _scale, _start, _callbackAt;
        private static double _deadline;
        private static int _pass, _stage, _callbacks;
        private static readonly List<string> Events = new();
        private static string Output => Path.Combine(Path.GetTempPath(), "death-screen-check");
        private static object Get(string name) => typeof(DeathScreenOverlay).GetField(name, Private).GetValue(_overlay);
        private static Button Continue => _document.rootVisualElement.Q<Button>("DeathScreenContinue");
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

        public static void EnsureLocalization()
        {
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Localization/LocalizationTables/MenuLabels_" + code + ".asset");
                table.AddEntry(DeathScreenOverlay.ReturnToHubKey, code == "en" ? "Return to hub" : "Вернуться в хаб");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
                AssetDatabase.SaveAssetIfDirty(table); AssetDatabase.SaveAssetIfDirty(table.SharedData);
            }
        }

        public static void Run()
        {
            if (!EditorApplication.isPlaying || _host != null) throw new InvalidOperationException("Run once from Play Mode.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Report.txt", "RUNNING");
            _locale = LocalizationSettings.SelectedLocale;
            _hadLanguage = PlayerPrefs.HasKey("selected_language"); _language = PlayerPrefs.GetString("selected_language");
            _background = Application.runInBackground; _scale = Time.timeScale;
            Application.runInBackground = true; Time.timeScale = 0;
            try
            {
                _texture = new RenderTexture(480, 270, 0); _texture.Create();
                _panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset"));
                _panel.targetTexture = _texture; _panel.clearColor = true; _panel.colorClearValue = new Color(.08f, .1f, .11f);
                _host = new GameObject("Owned death-screen check"); _host.SetActive(false);
                _document = _host.AddComponent<UIDocument>(); _document.panelSettings = _panel;
                _overlay = _host.AddComponent<DeathScreenOverlay>();
                typeof(DeathScreenOverlay).GetField("_document", Private).SetValue(_overlay, _document);
                _host.SetActive(true);
                _pass = 0; Events.Clear(); Events.Add("pass,event,seconds,callbacks");
                StartPass();
                _deadline = EditorApplication.timeSinceStartup + 35;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload += End;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void StartPass()
        {
            _warming = true;
            _overlay.StartCoroutine(Warmup());
        }

        private static IEnumerator Warmup()
        {
            // The bridge runs from Editor.update; do not reuse the preceding Play-entry delta.
            yield return null;
            yield return null;
            try { BeginSequence(); }
            catch (Exception ex) { Fail(ex); }
        }

        private static void BeginSequence()
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(_pass == 1 ? "ru" : "en");
            _stage = 0; _callbacks = 0; _held = _pass == 1; _pressed = _held; _start = Time.unscaledTime;
            var sequence = (IEnumerator)typeof(DeathScreenOverlay).GetMethod("PlayDeathSequenceCore", Private).Invoke(_overlay,
                new object[] { (Action)Covered, _pass == 1 ? 99999 : 7, _pass == 1 ? 1234567 : (_pass == 2 ? 67 : 23),
                    (Func<float>)(() => Time.unscaledDeltaTime), (Func<bool>)(() => _held), (Func<bool>)(() => _pressed) });
            _overlay.StartCoroutine(sequence);
            Require(!_overlay.TryContinue(), "Early input skipped the summary.");
            Events.Add(_pass + ",start-delta," + Time.unscaledDeltaTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ",0");
            _warming = false;
        }

        private static void Covered()
        {
            _callbacks++;
            Require(_callbacks == 1, "Return callback duplicated.");
            Require(_document.rootVisualElement.Q("DeathScreenBackground").style.backgroundColor.value.a == 1 &&
                Continue.style.opacity.value == 0, "Return is not fully covered.");
            var image = Read();
            try
            {
                foreach (var pixel in image.GetPixels32())
                    Require(pixel.r <= 2 && pixel.g <= 2 && pixel.b <= 2, "Previous rendered frame was not black before return.");
                File.WriteAllBytes(Output + "/" + _pass + "-Covered.png", image.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(image); }
            _callbackAt = Time.unscaledTime - _start;
            Events.Add(_pass + ",covered," + _callbackAt.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + _callbacks);
            for (int i = 0; i < 10; i++) Require(!_overlay.TryContinue(), "Return accepted duplicate continuation.");
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.isPlaying && EditorApplication.timeSinceStartup < _deadline, "Interrupted or timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (_warming) return;
                float elapsed = Time.unscaledTime - _start;
                if (_pass == 0)
                {
                    if (_stage == 0 && elapsed >= 1.25f)
                    {
                        ValidateLayout(); Capture("EN-summary");
                        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ru"); _stage++;
                    }
                    else if (_stage == 1 && elapsed >= 1.55f)
                    {
                        Require(Continue.text == "Вернуться в хаб", "Live Russian switch failed.");
                        ValidateLayout(); Capture("RU-live-switch");
                        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en"); _stage++;
                    }
                    else if (_stage == 2 && elapsed >= 1.85f)
                    {
                        using (var submit = NavigationSubmitEvent.GetPooled()) Continue.SendEvent(submit);
                        _stage++;
                    }
                    else if (_stage == 3 && elapsed >= 2.05f)
                    {
                        Require((bool)Get("_continueRequested"), "Actual UI submit did not activate Continue (enabled=" + Continue.enabledSelf + ", ready=" + Get("_canContinue") + ", callbacks=" + _callbacks + ").");
                        for (int i = 0; i < 10; i++) Require(!_overlay.TryContinue(), "Repeated input accepted.");
                        _stage++;
                    }
                }
                else if (_pass == 1)
                {
                    if (_stage == 0 && elapsed >= 1.5f)
                    {
                        Require(!Continue.enabledSelf && !_overlay.TryContinue(), "Held confirm skipped the summary.");
                        Capture("RU-held-confirm"); _held = _pressed = false; _stage++;
                    }
                    else if (_stage == 1 && elapsed >= 1.85f)
                    {
                        Require(Continue.enabledSelf, "Release did not arm continuation.");
                        ValidateLayout(); Capture("RU-large-statistics"); _held = _pressed = true; _stage++;
                    }
                }
                else if (_stage == 0 && elapsed >= 1.25f)
                {
                    ValidateLayout(); Capture("Repeated-automatic-summary"); _stage++;
                }
                if ((bool)Get("_sequencePlaying")) return;
                Require(_callbacks == 1 && _document.rootVisualElement.style.display.value == DisplayStyle.None, "Sequence did not clean up once.");
                Require(_pass == 2 ? _callbackAt >= 6f : _callbackAt < 4f, "Early/automatic timing changed incorrectly.");
                _pass++;
                if (_pass < 3) { StartPass(); return; }
                File.WriteAllLines(Output + "/Events.csv", Events);
                File.WriteAllText(Output + "/Report.txt", "PASS: real death overlay at 480x270; EN/RU and live locale switch; large existing-stat values; original summary placement retained; actual UI navigation submit; held-confirm rejection then release/fresh confirmation; repeated input/three sequences with exactly one owned callback each; rendered black frame before callback; unchanged six-second automatic return; unscaled operation with timeScale=0; control cleanup. Controlled clock/input providers and owned callback, no real character death, save write, recruitment or standalone build check.");
                End();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void ValidateLayout()
        {
            Require(Continue.worldBound.width > 100 && Continue.worldBound.xMin >= 0 && Continue.worldBound.xMax <= 480 &&
                Continue.worldBound.yMin >= 0 && Continue.worldBound.yMax <= 270, "Continue does not fit the logical canvas.");
            Require(Continue.MeasureTextSize(Continue.text, 0, VisualElement.MeasureMode.Undefined, 0,
                VisualElement.MeasureMode.Undefined).x < Continue.worldBound.width - 8, "Continue label clips.");
            foreach (string name in new[] { "DeathScreenTitle", "DeathScreenHighestStage", "DeathScreenMonstersKilled" })
            {
                Rect bounds = _document.rootVisualElement.Q<Label>(name).worldBound;
                Require(bounds.xMin >= 0 && bounds.xMax <= 480 && bounds.yMin >= 0 && bounds.yMax <= 270, "Summary label outside canvas: " + name);
            }
        }

        private static Texture2D Read()
        {
            var previous = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try { RenderTexture.active = _texture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); return image; }
            finally { RenderTexture.active = previous; }
        }
        private static void Capture(string name)
        {
            var image = Read();
            try { File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
            finally { Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex) { File.WriteAllLines(Output + "/Events.csv", Events); File.WriteAllText(Output + "/Report.txt", "FAIL: " + ex); Debug.LogException(ex); End(); }
        private static void OnPlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) End(); }
        private static void End()
        {
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= End;
            if (_overlay != null) _overlay.StopAllCoroutines();
            if (_host != null) Object.DestroyImmediate(_host); _host = null;
            if (_panel != null) Object.DestroyImmediate(_panel);
            if (_texture != null) { _texture.Release(); Object.DestroyImmediate(_texture); }
            LocalizationSettings.SelectedLocale = _locale;
            if (_hadLanguage) PlayerPrefs.SetString("selected_language", _language); else PlayerPrefs.DeleteKey("selected_language");
            PlayerPrefs.Save(); Time.timeScale = _scale; Application.runInBackground = _background;
        }
    }
}
