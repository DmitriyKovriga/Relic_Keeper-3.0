using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Skills;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Native uGUI/HUD check using copied slot art and disposable skill runtimes.</summary>
    public static class SkillStateFeedbackRuntimeCheck
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<Object> Owned = new List<Object>();
        private static GameObject _host;
        private static HUDController _hud;
        private static PlayerStats _stats;
        private static PlayerSkillManager _manager;
        private static SkillStateCaptureProbe[] _skills;
        private static UISkillSlot[] _slots;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static Locale _locale;
        private static bool _hadReduced, _reduced, _hadLanguage, _background;
        private static string _language;
        private static double _next, _deadline;
        private static int _stage;
        private static Vector3[] _slotPositions;
        private static string Output => Path.Combine(Path.GetTempPath(), "skill-state-no-popup-check");

        public static void Capture()
        {
            if (!EditorApplication.isPlaying || _host != null || GamePauseService.IsPaused) throw new InvalidOperationException("Run once from unpaused hub Play Mode.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Report.txt", "RUNNING");
            _locale = LocalizationSettings.SelectedLocale;
            _hadLanguage = PlayerPrefs.HasKey("selected_language"); _language = PlayerPrefs.GetString("selected_language");
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey); _reduced = GameplayPresentationSettings.ReducedCombatEffects;
            _background = Application.runInBackground; Application.runInBackground = true;
            try
            {
                var slots = Object.FindObjectsByType<UISkillSlot>(FindObjectsSortMode.None);
                var template = slots.FirstOrDefault(s => s.CurrentSkill?.Icon != null) ?? slots.FirstOrDefault();
                Require(template != null, "The hub HUD must be loaded before capture.");
                // A fresh hub may still be loading its equipped skills. Reuse its slot
                // art and a saved skill icon without waiting on the user's character.
                var art = template.CurrentSkill ?? AssetDatabase.FindAssets("t:SkillDataSO")
                    .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<SkillDataSO>).First(s => s.Icon != null);
                _texture = Own(new RenderTexture(480, 270, 24)); _texture.Create();
                var cameraHost = Own(new GameObject("Temporary Skill State Camera")); cameraHost.transform.position = new Vector3(2000, 0, -10);
                _camera = cameraHost.AddComponent<Camera>(); _camera.orthographic = true; _camera.orthographicSize = 5.625f;
                _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = new Color(.1f, .13f, .14f); _camera.cullingMask = 1 << 5; _camera.targetTexture = _texture;
                _host = Own(new GameObject("Temporary Skill State HUD", typeof(RectTransform))); _host.SetActive(false); _host.layer = 5;
                var canvas = _host.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _camera; canvas.planeDistance = 5; canvas.pixelPerfect = true;
                var scaler = _host.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1;
                var playerHost = Own(new GameObject("Temporary Skill State Owner")); playerHost.SetActive(false);
                _stats = playerHost.AddComponent<PlayerStats>(); Call(_stats, "Awake"); _stats.Initialize(null);
                _stats.GetStat(StatType.MaxMana).BaseValue = 100; _stats.NotifyChanged(); _stats.Mana.RestoreFull();
                _manager = playerHost.AddComponent<PlayerSkillManager>(); Call(_manager, "Awake");
                var panelHost = new GameObject("Skills", typeof(RectTransform), typeof(HorizontalLayoutGroup)); panelHost.layer = 5; panelHost.transform.SetParent(_host.transform, false);
                var panel = panelHost.GetComponent<RectTransform>(); panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1); panel.pivot = new Vector2(.5f, 1);
                panel.anchoredPosition = new Vector2(0, -34); panel.sizeDelta = new Vector2(234, 35);
                var layout = panelHost.GetComponent<HorizontalLayoutGroup>(); layout.childControlWidth = layout.childControlHeight = false; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                _slots = new UISkillSlot[6]; _skills = new SkillStateCaptureProbe[6];
                string[] names = { "SkillSlot_MainAttack", "SkillSlot_SpecialAttack", "SkillSlot_HelmetSkill", "SkillSlot_BodyArmorSkill", "SkillSlot_GlovesSkill", "SkillSlot_BootsSkill" };
                var active = (Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", Private).GetValue(_manager);
                for (int i = 0; i < 6; i++)
                {
                    _slots[i] = Object.Instantiate(template, panel, false); _slots[i].name = names[i];
                    _slots[i].GetComponent<RectTransform>().sizeDelta = new Vector2(34, 35);
                    var stale = _slots[i].transform.Find("SkillStateFrame"); if (stale != null) Object.DestroyImmediate(stale.gameObject);
                    var data = Own(Object.Instantiate(art)); data.Cooldown = i == 0 || i == 2 ? 0 : i == 4 ? 1 : 5; data.ManaCost = i == 0 || i == 4 ? 0 : 25;
                    var skill = playerHost.AddComponent<SkillStateCaptureProbe>(); skill.Initialize(_stats, data); skill.SetRuntimeSlot(_manager, i); _skills[i] = skill; active[i] = skill;
                }
                _hud = _host.AddComponent<HUDController>(); Set(_hud, "_skillSlots", _slots); Set(_hud, "_skillManager", _manager); Set(_hud, "_playerStats", _stats);
                // HUD normally uses a shared shortcut bar. Supply an owned inactive sentinel
                // so this isolated copy neither reuses nor destroys the user's bar.
                var shortcutHost = Own(new GameObject("Temporary Shortcut Sentinel")); shortcutHost.SetActive(false);
                Set(_hud, "_shortcutBar", shortcutHost.AddComponent<HudShortcutBar>());
                GameplayPresentationSettings.SetReducedCombatEffects(false);
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                _host.SetActive(true); Call(_hud, "RefreshAllSkillSlotBindings");
                _stage = 0; _next = EditorApplication.timeSinceStartup + .5; _deadline = _next + 25;
                EditorApplication.update += Tick;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static T Own<T>(T obj) where T : Object { Owned.Add(obj); return obj; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static bool MessageVisible => _host.GetComponentsInChildren<Transform>(true).Any(t => t.name == "SkillStateMessage");
        private static Image Frame(int index) => _slots[index].transform.Find("SkillStateFrame")?.GetComponent<Image>();
        private static bool FrameVisible(int index) => Frame(index) != null && Frame(index).isActiveAndEnabled;
        private static void Observe() => Call(_hud, "UpdateCooldownOverlays");
        private static void Tick()
        {
            try
            {
                Require(EditorApplication.isPlaying && EditorApplication.timeSinceStartup < _deadline, "Interrupted or timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < _next) return;
                switch (_stage)
                {
                    case 0:
                        Require(!MessageVisible, "Initialization must stay quiet.");
                        _slotPositions = _slots.Select(s => s.transform.position).ToArray(); CaptureFrame("Initial-quiet");
                        Require(_manager.UseSkillWithResult(1) == SkillCastResult.Accepted, "Long cast rejected."); Observe();
                        Require(!MessageVisible, "An accepted cast must not show a rejection or ready message.");
                        Require(_manager.UseSkillWithResult(1) == SkillCastResult.Cooldown, "Cooldown rejection lost.");
                        for (int i = 0; i < 60; i++) _manager.UseSkillWithResult(1, false);
                        break;
                    case 1:
                        Require(!MessageVisible && FrameVisible(1), "Cooldown should highlight only its slot."); CaptureFrame("Cooldown-en");
                        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ru"); break;
                    case 2:
                        Require(!MessageVisible, "Language switch created a popup."); CaptureFrame("Cooldown-ru");
                        _skills[1].Clock = 6;
                        var storm = _stats.gameObject.AddComponent<PlayerWinterStormController>();
                        Set(storm, "<IsActive>k__BackingField", true); Set(storm, "<Skill>k__BackingField", _skills[1].Data);
                        Observe(); Require(_slots[1].WaitingForReady, "A persistent skill lock falsely appeared ready.");
                        Set(storm, "<IsActive>k__BackingField", false); Observe(); break;
                    case 3:
                        Require(!MessageVisible && FrameVisible(1) && !_slots[1].WaitingForReady, "Readiness should highlight only its slot."); CaptureFrame("Ready-ru");
                        float expires = (float)Get(_slots[1], "_feedbackUntil"); Observe(); Observe();
                        Require((float)Get(_slots[1], "_feedbackUntil") == expires, "Ready highlight repeated every frame.");
                        _stats.Mana.SetCurrent(0); Require(_manager.UseSkillWithResult(2) == SkillCastResult.InsufficientMana, "Mana rejection lost."); break;
                    case 4:
                        Require(!MessageVisible && FrameVisible(2) && _skills[2].Executions == 0 && _skills[2].CooldownRemaining == 0, "Failed cast was presented as success or a popup.");
                        CaptureFrame("Mana-ru"); LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en"); break;
                    case 5:
                        Require(!MessageVisible, "Mana rejection created a popup."); CaptureFrame("Mana-en");
                        _stats.Mana.RestoreFull(); Require(_manager.UseSkillWithResult(1) == SkillCastResult.Accepted, "Second cast rejected."); Observe();
                        _skills[1].Clock = 12; Call(_hud, "RefreshAllSkillSlotBindings"); Observe();
                        Require(!MessageVisible, "A binding rebuild falsely replayed readiness.");
                        var old = _skills[1]; var replacement = _stats.gameObject.AddComponent<SkillStateCaptureProbe>(); replacement.Initialize(_stats, old.Data); replacement.SetRuntimeSlot(_manager, 1);
                        ((Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", Private).GetValue(_manager))[1] = replacement;
                        _skills[1] = replacement; Call(_hud, "UpdateSkillSlotUI", 1, replacement.Data); Observe();
                        Call(_hud, "OnSkillCastAttempted", 1, old, SkillCastResult.Cooldown);
                        Require(!MessageVisible, "A previous weapon runtime affected the replacement slot.");
                        for (int i = 0; i < 20; i++) _manager.UseSkillWithResult(0, false);
                        Observe(); Require(!MessageVisible, "Zero-cooldown primary spammed readiness.");
                        Require(_manager.UseSkillWithResult(1) == SkillCastResult.Accepted, "Replacement cast failed."); Observe();
                        GameplayPresentationSettings.SetReducedCombatEffects(true); replacement.Clock = 6; Observe(); break;
                    case 6:
                        Require(!MessageVisible && FrameVisible(1) && !_slots[1].WaitingForReady, "Reduced readiness should highlight only its slot."); CaptureFrame("Ready-reduced-en");
                        using (GamePauseService.Acquire(GamePauseReason.Inventory))
                        {
                            Require(_manager.UseSkillWithResult(2) == SkillCastResult.Paused, "Menu cast was accepted.");
                            Observe(); Require(!MessageVisible, "Menu opening replayed a cue.");
                        }
                        _next = EditorApplication.timeSinceStartup + .85; break;
                    case 7:
                        Require(!MessageVisible, "Message was queued after unpause.");
                        Require(_slots.All(s => s.transform.Find("SkillStateFrame") == null || !s.transform.Find("SkillStateFrame").GetComponent<Image>().enabled), "A slot frame failed to expire.");
                        Call(_hud, "OnSkillCastAttempted", 1, _skills[1], SkillCastResult.Busy);
                        Require(!MessageVisible && FrameVisible(1), "Already-casting feedback should stay in its slot."); CaptureFrame("Already-casting-no-popup");
                        File.WriteAllText(Output + "/Report.txt", "PASS: no floating skill messages during initialization, accepted casts, cooldown/mana/busy rejections or readiness; actual acceptance/cooldown/mana results retained; held retry silence; EN/RU switch; one long-cooldown slot highlight; reduced mode; no cue on HUD rebuild or same-data runtime replacement; zero-cooldown primary quiet; pause rejection; fixed layout; nonblocking slot highlights; timed cleanup. Disposable owner, mana and skill data only. Busy presentation injected through the HUD callback; other cast outcomes use the real shared gate.");
                        End(); return;
                }
                _stage++; if (_stage != 7) _next = EditorApplication.timeSinceStartup + .14;
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void CaptureFrame(string name)
        {
            Canvas.ForceUpdateCanvases();
            Require(!MessageVisible, "A floating skill message was created.");
            for (int i = 0; i < _slots.Length; i++)
            {
                Require(Vector3.Distance(_slots[i].transform.position, _slotPositions[i]) < .01f, "Feedback moved a pointer target.");
                var frame = Frame(i); if (frame == null) continue;
                foreach (var edge in frame.GetComponentsInChildren<Image>(true)) Require(!edge.raycastTarget, "Slot highlight intercepts input.");
                var corners = new Vector3[4]; frame.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var screen = _camera.WorldToScreenPoint(corner);
                    Require(screen.x >= 0 && screen.y >= 0 && screen.x <= 480 && screen.y <= 270, "Slot highlight outside native canvas.");
                }
            }
            _camera.Render();
            var previous = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try { RenderTexture.active = _texture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex) { File.WriteAllText(Output + "/Report.txt", "FAIL: " + ex); Debug.LogException(ex); End(); }
        private static void End()
        {
            EditorApplication.update -= Tick;
            // Destroy the HUD before its shortcut sentinel; both are owned here.
            if (_host != null) Object.DestroyImmediate(_host); _host = null;
            for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) Object.DestroyImmediate(Owned[i]); Owned.Clear();
            LocalizationSettings.SelectedLocale = _locale;
            if (_hadLanguage) PlayerPrefs.SetString("selected_language", _language); else PlayerPrefs.DeleteKey("selected_language");
            GameplayPresentationSettings.SetReducedCombatEffects(_reduced);
            if (!_hadReduced) PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            Application.runInBackground = _background;
        }
    }
    public sealed class SkillStateCaptureProbe : SkillBehaviour
    {
        public float Clock;
        public int Executions;
        protected override float CurrentTime => Clock;
        protected override void Execute() { Executions++; }
    }
}
