using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Scripts.Dungeon;
using Scripts.StatusEffects;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Controlled native-scale fire, animation, light and crowd check. No combat or user-save mutations.</summary>
    public static class IgniteVisualRuntimeCheck
    {
        private static readonly List<Object> Owned = new List<Object>();
        private static readonly List<IgniteVisualController> Burns = new List<IgniteVisualController>();
        private static readonly List<SpriteRenderer> Bodies = new List<SpriteRenderer>();
        private static readonly List<string> AnimationTimes = new List<string>();
        private static float _minimumPulse, _maximumPulse;
        private static Scene _scene;
        private static Camera _camera;
        private static RenderTexture _target;
        private static Sprite[] _walk;
        private static Sprite _mold, _knight;
        private static bool _hadReduced, _reduced, _background;
        private static int _stage, _frame;
        private static double _next, _deadline;
        private static string Folder => Path.Combine(Path.GetTempPath(), "ignite-burn-check");
        private static T Own<T>(T value) where T : Object { Owned.Add(value); return value; }
        private static GameObject Host(string name)
        {
            var host = Own(new GameObject(name)); SceneManager.MoveGameObjectToScene(host, _scene); return host;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        public static void Run()
        {
            if (!EditorApplication.isPlaying || _camera != null) throw new InvalidOperationException("Run once from Play Mode.");
            Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/Report.txt", "RUNNING");
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _reduced = GameplayPresentationSettings.ReducedCombatEffects; _background = Application.runInBackground;
            try
            {
                Application.runInBackground = true; GameplayPresentationSettings.SetReducedCombatEffects(false);
                _scene = EditorSceneManager.NewPreviewScene();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab");
                var room = Own(Object.Instantiate(prefab)); SceneManager.MoveGameObjectToScene(room, _scene);
                foreach (var behaviour in room.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var light in room.GetComponentsInChildren<Light2D>(true)) light.enabled = false;
                var spawn = room.GetComponent<RoomController>().PlayerSpawnPosition;
                _camera = Host("Ignite native camera").AddComponent<Camera>(); _camera.enabled = false; _camera.scene = _scene;
                _camera.orthographic = true; _camera.orthographicSize = 270f / 48; _camera.aspect = 480f / 270;
                _camera.transform.position = spawn + new Vector3(4,1,-10); _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.09f,.12f,.13f);
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                _camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                var owner = _camera.gameObject.AddComponent<AtmosphereController>();
                owner.Profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/MortfallDepthMist.asset");
                var fill = Host("Fixture global fill").AddComponent<Light2D>(); fill.lightType = Light2D.LightType.Global;
                fill.intensity = 1; fill.blendStyleIndex = 0; fill.targetSortingLayers = SortingLayer.layers.Select(x => x.id).ToArray();
                _walk = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Enemy/ZombieResources/zombieVersion2/zombie walk-Sheet.png").OfType<Sprite>().ToArray();
                Require(_walk.Length > 0, "Actual animation frames missing.");
                _mold = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/MoldEnemyPF.prefab").GetComponent<SpriteRenderer>().sprite;
                _knight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/KnightEnemyPF.prefab").GetComponent<SpriteRenderer>().sprite;
                for (int i=0;i<10;i++)
                {
                    var actor = Host("Owned burning enemy " + i); actor.transform.position = spawn + new Vector3(i * .65f,0,0);
                    var body = actor.AddComponent<SpriteRenderer>(); body.sprite = i == 1 || i == 2 ? _mold : _walk[0]; body.sortingLayerName = "World"; body.sortingOrder = 200 + i * 8;
                    actor.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Characters);
                    Bodies.Add(body); Burns.Add(actor.AddComponent<IgniteVisualController>());
                    if (i > 0) actor.SetActive(false);
                }
                owner.RefreshColorGroups(); _target = Own(new RenderTexture(480,270,24)); _target.Create();
                AnimationTimes.Clear(); AnimationTimes.Add("frame,editor_time_seconds,game_time_seconds,light_glow_motion,light_radius");
                _minimumPulse = float.MaxValue; _maximumPulse = float.MinValue;
                _stage = _frame = 0; _next = EditorApplication.timeSinceStartup + .5; _deadline = _next + 35;
                EditorApplication.update += Tick;
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void Tick()
        {
            try
            {
                Require(EditorApplication.isPlaying && EditorApplication.timeSinceStartup < _deadline, "Interrupted or timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                // Preview scenes do not dispatch ordinary actor updates. Advance the
                // real presentation explicitly before inspecting a changed sprite/setting.
                foreach (var burn in Burns)
                    typeof(IgniteVisualController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(burn, null);
                if (EditorApplication.timeSinceStartup < _next) return;
                switch (_stage)
                {
                    case 0: Capture("Not-burning"); Burns[0].SetBurning(true); break;
                    case 1:
                        CheckBurn(Burns[0]); Capture("Burning"); _frame = 0; break;
                    case 2:
                        Bodies[0].sprite = _walk[(_frame / 4) % _walk.Length]; Bodies[0].flipX = _frame >= 48;
                        typeof(IgniteVisualController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Burns[0], null);
                        var animatedLight = Burns[0].GetComponentInChildren<LocalLight2DSource>();
                        var profile = _camera.GetComponent<AtmosphereController>().ActiveDepthMistProfile;
                        float pulse = animatedLight.GlowMultiplier(profile);
                        _minimumPulse = Mathf.Min(_minimumPulse,pulse); _maximumPulse = Mathf.Max(_maximumPulse,pulse);
                        AnimationTimes.Add(_frame + "," + EditorApplication.timeSinceStartup.ToString("F6",CultureInfo.InvariantCulture) + "," + Time.time.ToString("F6",CultureInfo.InvariantCulture) + "," + pulse.ToString("F6",CultureInfo.InvariantCulture) + "," + animatedLight.ShadowRadius(profile).ToString("F6",CultureInfo.InvariantCulture));
                        CheckBurn(Burns[0]); Capture("Animation-" + _frame.ToString("D2"));
                        _frame++; if (_frame < 96) { _next = EditorApplication.timeSinceStartup + .03; return; }
                        File.WriteAllLines(Folder + "/AnimationTimes.csv",AnimationTimes);
                        Require(_maximumPulse-_minimumPulse > .2f, "Ignite light/glow did not visibly breathe: " + _minimumPulse + " to " + _maximumPulse);
                        break;
                    case 3:
                        var source = Burns[0].GetComponentInChildren<LocalLight2DSource>();
                        var glow = source.Glow; glow.enabled = false; source.ConfigureGlow(glow);
                        var lit = Capture("Light-on"); source.enabled = false; var dark = Capture("Light-off");
                        int changed = 0;
                        var bounds = Bodies[0].bounds; bounds.Expand(1.1f);
                        for (int y=0;y<270;y++) for(int x=0;x<480;x++)
                        {
                            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(x,y,10));
                            if (!bounds.Contains(new Vector3(world.x,world.y,bounds.center.z)) && lit[y*480+x].r > dark[y*480+x].r + 2) changed++;
                        }
                        Require(changed > 4, "Burning light did not illuminate nearby terrain outside the fire: " + changed);
                        source.enabled = true; GameplayPresentationSettings.SetReducedCombatEffects(true); break;
                    case 4:
                        Capture("Reduced"); GameplayPresentationSettings.SetReducedCombatEffects(false);
                        Bodies[0].sprite = _mold; break;
                    case 5:
                        CheckBurn(Burns[0]); Capture("Mold-burning"); Bodies[0].sprite = _knight; break;
                    case 6:
                        CheckBurn(Burns[0]); Capture("Knight-burning"); Bodies[0].sprite = _walk[0];
                        for(int i=1;i<10;i++) { Bodies[i].gameObject.SetActive(true); Burns[i].SetBurning(true); }
                        break;
                    case 7:
                        foreach(var burn in Burns) { CheckBurn(burn); burn.SetBurning(true); }
                        Require(Burns.Sum(b => b.GetComponentsInChildren<LocalLight2DSource>(true).Length) == 10, "Stack refresh duplicated lights.");
                        Capture("Ten-burning-enemies"); foreach(var burn in Burns) burn.SetBurning(false); break;
                    case 8:
                        Capture("Extinguished");
                        Require(Burns.All(b => !b.IsBurning && b.GetComponentsInChildren<LocalLight2DSource>().Length == 0), "Extinguished fire left an active light.");
                        File.WriteAllText(Folder + "/Report.txt", "PASS: real Mortfall room geometry at 480x270; zombie animation/flip tracking plus Mold and Knight silhouettes; large fire stays behind the body; supported Resources shader with no magenta; nearby terrain receives burn light (paired same-frame captures with glow disabled); ten burning enemies with one light each; repeated refresh bounded; reduced effects; extinguish cleanup. Preview-scene presentation is explicitly advanced before each capture. Controlled disposable actors, not an ordinary combat/performance clip; no health, XP, inventory or save changes.");
                        End(); return;
                }
                _stage++; _next = EditorApplication.timeSinceStartup + .2;
            }
            catch(Exception ex) { Fail(ex); }
        }
        private static void CheckBurn(IgniteVisualController burn)
        {
            var heat = burn.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Ignite body heat");
            Require(heat.sprite == burn.GetComponent<SpriteRenderer>().sprite && heat.flipX == burn.GetComponent<SpriteRenderer>().flipX, "Fire detached from animated body.");
            Require(heat.sharedMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(heat.sharedMaterial.shader), "Burn shader is unsupported or has errors.");
            var fire = burn.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Ignite rising fire");
            Require(fire.sortingOrder < burn.GetComponent<SpriteRenderer>().sortingOrder, "Large fire covers the enemy instead of staying behind its silhouette.");
        }
        private static Color32[] Capture(string name)
        {
            RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _target });
            var previous = RenderTexture.active; var image = new Texture2D(480,270,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active = _target; image.ReadPixels(new Rect(0,0,480,270),0,0); image.Apply(); var pixels = image.GetPixels32();
                Require(!pixels.Any(p => p.r > 240 && p.b > 240 && p.g < 20), "Magenta shader pixels.");
                File.WriteAllBytes(Folder + "/" + name + ".png",image.EncodeToPNG()); return pixels;
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex) { File.WriteAllText(Folder + "/Report.txt", "FAIL: " + ex); Debug.LogException(ex); End(); }
        private static void End()
        {
            EditorApplication.update -= Tick;
            foreach(var burn in Burns) if(burn != null) burn.SetBurning(false);
            for(int i=Owned.Count-1;i>=0;i--) if(Owned[i] != null) Object.DestroyImmediate(Owned[i]);
            Owned.Clear(); Burns.Clear(); Bodies.Clear(); _camera = null;
            if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            GameplayPresentationSettings.SetReducedCombatEffects(_reduced);
            if(!_hadReduced) PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            Application.runInBackground = _background;
        }
    }
}
