using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Hub;
using Scripts.Items.World;
using Scripts.UI;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public static partial class RoomResolutionRuntimeCheck
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<Object> Owned = new List<Object>();
        private static readonly FieldInfo BannerInstance = typeof(RoomClearedBanner).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static GameObject _roomHost, _bannerHost;
        private static RoomController _room;
        private static DungeonPortal _portal, _reducedPortal;
        private static RoomClearedBanner _banner, _previousBanner;
        private static RenderTexture _uiTexture, _worldTexture;
        private static PanelSettings _panel;
        private static Camera _camera;
        private static Locale _locale;
        private static bool _hadLanguage, _hadReduced, _reduced, _background;
        private static string _language;
        private static int _stage;
        private static double _next, _deadline;
        private static EnemyHealth _lastEnemy;
        private static Vector3 _portalPosition, _portalScale;
        private static Vector2 _triggerSize;
        private static string Output => Path.Combine(Path.GetTempPath(), "room-resolution-check");
        private static RoomAnnouncementState State => (RoomAnnouncementState)Get(_banner, "_state");
        private static Label Heading => (Label)Get(_banner, "_label");
        private static Label Detail => (Label)Get(_banner, "_detail");
        private static VisualElement Box => (VisualElement)Get(_banner, "_box");
        private static T Own<T>(T obj) where T : Object { Owned.Add(obj); return obj; }
        private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        public static void Capture()
        {
            if (!EditorApplication.isPlaying || _roomHost != null) throw new InvalidOperationException("Run once from hub Play Mode.");
            Require(Object.FindFirstObjectByType<RoomController>() == null, "Run outside an active dungeon room.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Report.txt", "RUNNING");
            _locale = LocalizationSettings.SelectedLocale;
            _hadLanguage = PlayerPrefs.HasKey("selected_language"); _language = PlayerPrefs.GetString("selected_language");
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey); _reduced = GameplayPresentationSettings.ReducedCombatEffects;
            _background = Application.runInBackground; Application.runInBackground = true;
            _previousBanner = (RoomClearedBanner)BannerInstance.GetValue(null);
            try
            {
                GameplayPresentationSettings.SetReducedCombatEffects(false);
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                _uiTexture = Own(new RenderTexture(480, 270, 0)); _uiTexture.Create();
                _worldTexture = Own(new RenderTexture(480, 270, 24)); _worldTexture.Create();
                _panel = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset")));
                _panel.targetTexture = _uiTexture; _panel.clearColor = true; _panel.colorClearValue = new Color(.08f, .1f, .11f);
                _bannerHost = Own(new GameObject("Temporary Room Announcement")); _bannerHost.SetActive(false);
                var doc = _bannerHost.AddComponent<UIDocument>(); doc.panelSettings = _panel;
                _banner = _bannerHost.AddComponent<RoomClearedBanner>(); Set(_banner, "_document", doc);
                BannerInstance.SetValue(null, _banner); _bannerHost.SetActive(true); Call(_banner, "Build");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab");
                _roomHost = Own(Object.Instantiate(prefab, new Vector3(2000, 0), Quaternion.identity));
                _room = _roomHost.GetComponent<RoomController>(); ConfigureRoom(_room, 3);
                _portal = _room.GetComponentsInChildren<DungeonPortal>(true).First(p => p.Type == PortalType.NextRoom);
                _portalPosition = _portal.transform.position; _portalScale = _portal.transform.localScale; _triggerSize = _portal.GetComponent<BoxCollider2D>().size;
                var cameraHost = Own(new GameObject("Temporary Room Resolution Camera")); _camera = cameraHost.AddComponent<Camera>();
                if (Camera.main != null) _camera.CopyFrom(Camera.main);
                _camera.enabled = false; _camera.orthographic = true; _camera.orthographicSize = 270f / 48f; _camera.aspect = 480f / 270f;
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; _camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                if (Camera.main != null && Camera.main.TryGetComponent<AtmosphereController>(out var mainOwner))
                {
                    var owner = cameraHost.AddComponent<AtmosphereController>(); EditorUtility.CopySerialized(mainOwner, owner); owner.Profile = null;
                    owner.ApplyDungeonPresentation(null, AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/MortfallDepthMist.asset")); owner.RefreshColorGroups();
                }
                _stage = 0; _next = EditorApplication.timeSinceStartup + .4; _deadline = _next + 40;
                EditorApplication.update += Tick;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void ConfigureRoom(RoomController room, int enemies)
        {
            room.enabled = false;
            foreach (var spawner in room.GetComponentsInChildren<EnemySpawner>(true)) spawner.enabled = false;
            Set(room, "_activeModifiers", new DungeonModifierContext()); Set(room, "_roomClearRewardsSpawned", false);
            Set(room, "_initialEnemyCount", enemies); Set(room, "_nextRoomPortalUnlocked", false);
            var living = (List<EnemyHealth>)Get(room, "_livingEnemies"); living.Clear();
            for (int i = 0; i < enemies; i++)
            {
                var actor = new GameObject("Owned enemy count fixture " + i); actor.SetActive(false); actor.transform.SetParent(room.transform, false);
                living.Add(actor.AddComponent<EnemyHealth>());
            }
            Call(room, "SetNextRoomPortalsActive", false);
        }
        private static EnemyHealth Defeat(RoomController room)
        {
            var living = (List<EnemyHealth>)Get(room, "_livingEnemies"); var enemy = living[0]; Call(room, "OnSpawnedEnemyDeath", enemy); return enemy;
        }
        private static RoomController SimpleRoom(int count, float x)
        {
            var host = Own(new GameObject("Owned simple resolution room")); host.SetActive(false); host.transform.position = new Vector3(x, 0);
            var room = host.AddComponent<RoomController>(); Set(room, "_autoCreateCameraBounds", false);
            var portal = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spawner/NextRoomPortal.prefab"), host.transform);
            portal.transform.localPosition = Vector3.up * 2;
            host.SetActive(true); ConfigureRoom(room, count); return room;
        }
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
                        Require(!_portal.CanInteract() && State.Current == RoomNotice.None, "Initial lock must be quiet."); CaptureWorld("Locked", _portal);
                        Defeat(_room); Require(!_portal.CanInteract() && State.Current == RoomNotice.None, "Exit opened before threshold.");
                        Defeat(_room); Require(_portal.CanInteract() && _portal.GetComponent<BoxCollider2D>().enabled, "Cosmetic feedback delayed interaction.");
                        Require(!_room.IsCleared && State.Current == RoomNotice.PortalUnlocked && !(bool)Get(_room, "_roomClearRewardsSpawned"), "Unlock was confused with full clear."); break;
                    case 1:
                        Require(_portal.transform.Find("PortalUnlockEcho") != null, "Local unlock echo missing.");
                        Require(_portal.transform.position == _portalPosition && _portal.transform.localScale == _portalScale && _portal.GetComponent<BoxCollider2D>().size == _triggerSize, "Portal interaction geometry changed.");
                        CaptureWorld("Unlock-pulse", _portal); CaptureUI("Unlock-en"); RoomClearedBanner.ShowCharacterLevelUp(); break;
                    case 2:
                        Require(State.Current == (RoomNotice.PortalUnlocked | RoomNotice.LevelUp), "Level-up overwrote the unlock.");
                        CaptureUI("Unlock-level-en"); _lastEnemy = Defeat(_room); break;
                    case 3:
                        Require(_room.IsCleared && State.Current == (RoomNotice.PortalUnlocked | RoomNotice.Cleared | RoomNotice.LevelUp), "Full clear lost coincident information.");
                        Require(_room.GetComponentsInChildren<WorldLootCache>(true).Length == 0 && _room.GetComponentsInChildren<HubServiceNpc>(true).Length == 0, "No-reward room created loot or services.");
                        CaptureUI("Clear-level-en"); LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ru"); break;
                    case 4:
                        Require(Heading.text.Contains("Портал") || Heading.text.Contains("портал"), "Russian room message missing.");
                        Require(Detail.text.IndexOf("уров", StringComparison.OrdinalIgnoreCase) >= 0, "Russian level-up detail missing: " + Detail.text); CaptureUI("Clear-level-ru");
                        float expires = State.ExpiresAt; Call(_room, "OnSpawnedEnemyDeath", _lastEnemy);
                        Require(!(bool)Call(_room, "TryUnlockNextRoomPortal", true) && State.ExpiresAt == expires, "Duplicate death/reentry replayed an announcement.");
                        var main = Camera.main; Vector3 position = _portal.transform.position;
                        if (main != null)
                        {
                            try
                            {
                                _portal.transform.position = main.ViewportToWorldPoint(new Vector3(.5f, .5f, 10)); Call(_banner, "RefreshMessage", true);
                                Require(!Detail.text.Contains("Выход"), "Onscreen exit has an unnecessary direction hint.");
                            }
                            finally { _portal.transform.position = position; }
                        }
                        _roomHost.SetActive(false); break;
                    case 5:
                        Require(State.Current == RoomNotice.None && Box.style.display == DisplayStyle.None, "Leaving room did not cancel its message.");
                        _roomHost.SetActive(true); _portal.PresentUnlock();
                        Require(_portal.transform.Find("PortalUnlockEcho") == null && State.Current == RoomNotice.None, "Re-enabling room replayed the unlock.");
                        Call(_room, "SpawnRoomClearRewards"); Require((bool)Get(_room, "_roomClearRewardsSpawned"), "Reward guard lost.");
                        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                        var single = SimpleRoom(1, 2200); Defeat(single);
                        Require(State.Current == (RoomNotice.PortalUnlocked | RoomNotice.Cleared) && single.GetComponentInChildren<DungeonPortal>().CanInteract(), "One-enemy room did not combine clear/unlock immediately."); break;
                    case 6:
                        CaptureUI("Single-clear-en");
                        var empty = SimpleRoom(0, 2400); empty.OnRoomEntered(null, new DungeonModifierContext());
                        Require(empty.GetComponentInChildren<DungeonPortal>().CanInteract() && State.Current == RoomNotice.None, "Empty room must start open without a victory claim.");
                        Require(empty.GetComponentsInChildren<WorldLootCache>(true).Length == 0 && empty.GetComponentsInChildren<HubServiceNpc>(true).Length == 0, "Empty room invented rewards.");
                        GameplayPresentationSettings.SetReducedCombatEffects(true);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab");
                        var reducedRoom = Own(Object.Instantiate(prefab, new Vector3(3000, 0), Quaternion.identity)).GetComponent<RoomController>(); ConfigureRoom(reducedRoom, 1);
                        _reducedPortal = reducedRoom.GetComponentsInChildren<DungeonPortal>(true).First(p => p.Type == PortalType.NextRoom);
                        Defeat(reducedRoom); break;
                    case 7:
                        var echo = _reducedPortal.transform.Find("PortalUnlockEcho"); Require(echo != null && echo.localScale.x <= 1.22f && echo.GetComponent<SpriteRenderer>().color.a < .3f, "Reduced portal cue is not quieter.");
                        CaptureWorld("Unlock-reduced", _reducedPortal); CaptureUI("Reduced-clear-en");
                        Object.DestroyImmediate(_reducedPortal.GetComponentInParent<RoomController>().gameObject);
                        Require(State.Current == RoomNotice.None, "Room destruction left its announcement alive.");
                        RoomClearedBanner.ShowCharacterLevelUp(); RoomClearedBanner.HideForRoom(_room);
                        Require(State.Current == RoomNotice.LevelUp, "Unrelated room cancellation hid a global level-up."); break;
                    case 8:
                        CaptureUI("Level-only-en"); _next = EditorApplication.timeSinceStartup + 2.6; break;
                    case 9:
                        Require(State.Current == RoomNotice.None && Box.style.display == DisplayStyle.None && _portal.transform.Find("PortalUnlockEcho") == null, "Feedback did not settle and clean up.");
                        File.WriteAllText(Output + "/Report.txt", "PASS: original half-kill threshold; immediate portal interaction; local echo with fixed collider/transform; unlock with one enemy alive; full clear and level-up merged; EN/RU layout; offscreen-only direction; duplicate death quiet; no replay on room re-enable; scoped cancellation on leave/destruction; single-enemy combined result; empty/no-reward rooms truthful; reward guard retained; reduced local echo; unrelated level-up retained; timed cleanup. Real Mortfall room and UI, disposable enemy-count fixtures; no user inventory or XP changes.");
                        End(); return;
                }
                _stage++; if (_stage != 9) _next = EditorApplication.timeSinceStartup + .18;
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void CaptureUI(string name)
        {
            Require(Box.worldBound.xMin >= 0 && Box.worldBound.xMax <= 480 && Box.worldBound.yMin >= 0 && Box.worldBound.yMax <= 270, "Announcement exceeds native canvas: " + Box.worldBound);
            Require(Box.pickingMode == PickingMode.Ignore && Heading.pickingMode == PickingMode.Ignore && Detail.pickingMode == PickingMode.Ignore, "Announcement intercepts input.");
            Save(_uiTexture, name);
        }
        private static void CaptureWorld(string name, DungeonPortal portal)
        {
            _camera.transform.position = portal.transform.position + new Vector3(-3, 1, -10);
            _camera.ResetWorldToCameraMatrix(); _camera.ResetProjectionMatrix();
            RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _worldTexture }); Save(_worldTexture, name);
        }
        private static void Save(RenderTexture texture, string name)
        {
            var previous = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try { RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex) { File.WriteAllText(Output + "/Report.txt", "FAIL: " + ex); Debug.LogException(ex); End(); }
        private static void End()
        {
            EditorApplication.update -= Tick;
            foreach (var room in Owned.OfType<GameObject>().Where(go => go != null && go.GetComponent<RoomController>() != null).ToArray()) Object.DestroyImmediate(room);
            if (_bannerHost != null) Object.DestroyImmediate(_bannerHost);
            BannerInstance.SetValue(null, _previousBanner);
            for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) Object.DestroyImmediate(Owned[i]); Owned.Clear(); _roomHost = null;
            LocalizationSettings.SelectedLocale = _locale;
            if (_hadLanguage) PlayerPrefs.SetString("selected_language", _language); else PlayerPrefs.DeleteKey("selected_language");
            GameplayPresentationSettings.SetReducedCombatEffects(_reduced);
            if (!_hadReduced) PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            Application.runInBackground = _background;
        }
    }
}
