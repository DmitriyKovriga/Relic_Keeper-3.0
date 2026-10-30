using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Combat;
using Scripts.Dungeon;
using Scripts.Enemies;
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
    /// <summary>Opt-in, disposable native-scale presentation audit. No player target or save writes.</summary>
    public static class EnemyIntentRuntimeCheck
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Actor
        {
            public EnemyEntity Entity;
            public EnemyAttackController Attack;
            public EnemyAnimationBridge Animation;
            public Transform Target;
        }
        private static readonly List<Object> Owned = new();
        private static readonly List<Actor> Actors = new();
        private static readonly List<string> Samples = new();
        private static Scene _scene;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static int _pass, _frame;
        private static float _start, _lastGameTime;
        private static double _deadline;
        private static bool _reduced, _hadReduced, _background;
        private static UnityEngine.Random.State _random;
        private static string _label;
        private static string Output => Path.Combine(Path.GetTempPath(), "enemy-intent-check", _label);
        private static T Own<T>(T value) where T : Object { Owned.Add(value); return value; }
        private static GameObject Host(string name)
        {
            var host = Own(new GameObject(name));
            SceneManager.MoveGameObjectToScene(host, _scene);
            return host;
        }
        private static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Call(object owner, string name) => owner.GetType().GetMethod(name, Private).Invoke(owner, null);
        public static void Before() => Run("Before");
        public static void After() => Run("After");
        public static void ProjectileAim() => Run("ProjectileAim");
        private static void Run(string label)
        {
            if (!EditorApplication.isPlaying || _camera != null)
                throw new InvalidOperationException("Run once from Play Mode.");
            _label = label;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Report.txt", "RUNNING");
            _reduced = GameplayPresentationSettings.ReducedCombatEffects;
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _background = Application.runInBackground;
            _random = UnityEngine.Random.state;
            try
            {
                Application.runInBackground = true;
                _scene = EditorSceneManager.NewPreviewScene();
                var room = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab")));
                SceneManager.MoveGameObjectToScene(room, _scene);
                room.transform.position += new Vector3(1000, 0, 0);
                foreach (var behaviour in room.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var light in room.GetComponentsInChildren<Light2D>(true)) light.enabled = false;
                Vector3 spawn = room.GetComponent<RoomController>().PlayerSpawnPosition;
                _camera = Host("Enemy intent native camera").AddComponent<Camera>();
                _camera.enabled = false;
                _camera.scene = _scene;
                _camera.orthographic = true;
                _camera.orthographicSize = 270f / 48;
                _camera.aspect = 480f / 270;
                _camera.transform.position = spawn + new Vector3(4, 1, -10);
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.09f, .12f, .13f);
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                _camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                var atmosphere = _camera.gameObject.AddComponent<AtmosphereController>();
                atmosphere.Profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/MortfallDepthMist.asset");
                var fill = Host("Intent fixture fill").AddComponent<Light2D>();
                fill.lightType = Light2D.LightType.Global;
                fill.intensity = 1;
                fill.targetSortingLayers = SortingLayer.layers.Select(x => x.id).ToArray();
                _texture = Own(new RenderTexture(480, 270, 24) { filterMode = FilterMode.Point });
                for (int i = 0; i < 10; i++)
                {
                    var data = Resources.Load<EnemyDataSO>(i % 2 == 0 ? "Enemy/SO_Knight" : "Enemy/SO_Mold");
                    var host = Own(Object.Instantiate(data.Prefab.gameObject));
                    SceneManager.MoveGameObjectToScene(host, _scene);
                    var parent = Host("Owned enemy and projectile parent");
                    host.transform.SetParent(parent.transform);
                    var entity = host.GetComponent<EnemyEntity>();
                    entity.Setup(data, 1);
                    host.transform.position = spawn + new Vector3(i < 2 ? 2 + i * 4 : (i - 2) * 1.8f - 3, 0, 0);
                    foreach (var behaviour in host.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                    foreach (var body in host.GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
                    var target = Host("Owned non-damageable target").transform;
                    target.position = host.transform.position + new Vector3(1, 0, 0);
                    Actors.Add(new Actor { Entity = entity, Attack = host.GetComponent<EnemyAttackController>(),
                        Animation = host.GetComponent<EnemyAnimationBridge>(), Target = target });
                }
                _pass = 0;
                Samples.Clear();
                Samples.Add("pass,frame,gameTime,actor,phase,phaseRemaining,sprite,animationTime,warning");
                StartPass();
                _deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload += End;
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void StartPass()
        {
            GameplayPresentationSettings.SetReducedCombatEffects((_pass & 1) != 0);
            _frame = 0;
            _start = Time.time;
            _lastGameTime = Time.time;
            foreach (var actor in Actors)
            {
                actor.Entity.gameObject.SetActive(_pass >= 2 || Actors.IndexOf(actor) < 2);
                // The real attack returns to idle before the next pass; do not reinitialize
                // the animation bridge and add its authored visual offset a second time.
                if (actor.Entity.gameObject.activeSelf && !actor.Attack.TryStartAttack(actor.Target))
                    throw new InvalidOperationException("Fixture attack rejected.");
            }
        }
        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > _deadline)
                    throw new InvalidOperationException("Audit interrupted or timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (Time.time <= _lastGameTime) return;
                _lastGameTime = Time.time;
                foreach (var actor in Actors.Where(x => x.Entity.gameObject.activeSelf))
                {
                    if (_label == "ProjectileAim" && actor.Attack.IsPreparingAttack)
                    {
                        int side = (_pass & 1) == 0 ? 1 : -1;
                        actor.Target.position = actor.Entity.transform.position + new Vector3(side * 4f, Mathf.Sin(Time.time - _start) * .7f, 0);
                    }
                    Call(actor.Animation, "Update");
                    Call(actor.Attack, "Update");
                    var warning = actor.Entity.GetComponent<EnemyAttackTelegraphVfx>();
                    if (warning.IsPlaying) Call(warning, "LateUpdate");
                    if (_label != "Before" && warning.IsPlaying != actor.Attack.IsPreparingAttack)
                        throw new InvalidOperationException("Warning does not match the real preparation phase.");
                    if (_label == "ProjectileAim")
                    {
                        var lines = actor.Entity.GetComponentsInChildren<SpriteRenderer>(true).Where(x => x.name == "ProjectileAimLine").ToArray();
                        bool expected = actor.Attack.TryGetPreparingProjectileAim(out Vector2 origin, out Vector2 direction);
                        if (lines.Count(x => x.gameObject.activeInHierarchy) != (expected ? 1 : 0))
                            throw new InvalidOperationException("Projectile-only line lifecycle mismatch.");
                        if (expected && (Vector2.Distance(lines[0].transform.position, origin) > .005f ||
                            Vector2.Distance(lines[0].transform.TransformPoint(Vector3.right * lines[0].size.x), origin + direction * 3f) > .005f))
                            throw new InvalidOperationException("Projectile line differs from the real launch ray.");
                    }
                    Samples.Add(string.Join(",", _pass, _frame, Time.time.ToString("F6", CultureInfo.InvariantCulture),
                        Actors.IndexOf(actor), Get(actor.Attack, "_phase"),
                        ((float)Get(actor.Attack, "_phaseTimer")).ToString("F6", CultureInfo.InvariantCulture),
                        actor.Entity.VisualRenderer.sprite.name,
                        ((float)Get(actor.Animation, "_stateTime")).ToString("F6", CultureInfo.InvariantCulture), warning.IsPlaying));
                }
                foreach (var projectile in _scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<EnemyProjectile>()))
                    Call(projectile, "Update");
                Capture();
                _frame++;
                if (Time.time - _start < 2.4f) return;
                if (Actors.Any(x => x.Entity.gameObject.activeSelf && x.Attack.IsBusy))
                    throw new InvalidOperationException("Attack did not finish within its existing windows.");
                _pass++;
                if (_pass < 4) { StartPass(); return; }
                File.WriteAllLines(Output + "/Samples.csv", Samples);
                File.WriteAllText(Output + "/Report.txt", "PASS: actual primary attacks and authored sprite release signals, Knight and Mold solo/crowded in normal/reduced FX, native 480x270 Mortfall geometry. " +
                    (_label == "ProjectileAim" ? "Original mark retained; projectile-only line matches the launch ray for moving targets in left/right aim and clears on release. " : "") +
                    "Preview callbacks advanced once per game frame with real Time.deltaTime. No player target, damage receiver, death/reward or save writes. This is a controlled presentation audit, not ordinary combat, charge movement, a performance benchmark or a standalone player check.");
                End();
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void Capture()
        {
            RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _texture });
            var previous = RenderTexture.active;
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = _texture;
                image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + _pass + "-" + _frame.ToString("D3") + ".png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex)
        {
            File.WriteAllText(Output + "/Report.txt", "FAIL: " + ex);
            Debug.LogException(ex);
            End();
        }
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            File.WriteAllText(Output + "/Report.txt", "INTERRUPTED: Play Mode stopped before audit completion.");
            End();
        }
        private static void End()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= End;
            for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) Object.DestroyImmediate(Owned[i]);
            Owned.Clear();
            Actors.Clear();
            _camera = null;
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            GameplayPresentationSettings.SetReducedCombatEffects(_reduced);
            if (!_hadReduced)
            {
                PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
                PlayerPrefs.Save();
            }
            Application.runInBackground = _background;
            UnityEngine.Random.state = _random;
        }
    }
}
