using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Combat;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Stats;
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
    /// <summary>Actual shock threshold/state methods with disposable owners and native room rendering.</summary>
    public static class ShockVisualRuntimeCheck
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<Object> Owned = new List<Object>();
        private static readonly List<GameObject> Actors = new List<GameObject>();
        private static readonly List<string> Times = new List<string>();
        private static Scene _scene;
        private static Camera _camera;
        private static RenderTexture _target;
        private static PlayerStats _source;
        private static AilmentController _ailments;
        private static EnemyHealth _health;
        private static SpriteRenderer _body;
        private static ShockVisualController _shock;
        private static IgniteVisualController _ignite;
        private static EnemyFreezeController _freeze;
        private static Sprite[] _walk;
        private static int _stage, _frame, _events;
        private static float _activation;
        private static double _next, _deadline;
        private static bool _hadReduced, _reduced, _background;
        private static UnityEngine.Random.State _random;
        private static MaterialPropertyBlock _arcSample;
        private static int _litFrames, _quietFrames;
        private static string Output => Path.Combine(Path.GetTempPath(),"shock-presentation-check");
        private static T Own<T>(T value) where T : Object { Owned.Add(value); return value; }
        private static GameObject Host(string name)
        {
            var host = Own(new GameObject(name)); SceneManager.MoveGameObjectToScene(host,_scene); return host;
        }
        private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        private static object Get(object owner,string name) => owner.GetType().GetField(name,Private).GetValue(owner);
        private static object Call(object owner,string name,params object[] args) => owner.GetType().GetMethod(name,Private).Invoke(owner,args);
        private static bool Proc(AilmentController target,float lightning) => target.TryApplyShock(_source,_source,new DamageSnapshot(_source) { Lightning = lightning });
        public static void Run()
        {
            if(!EditorApplication.isPlaying || _camera != null) throw new InvalidOperationException("Run once from Play Mode.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output+"/Report.txt","RUNNING");
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _reduced = GameplayPresentationSettings.ReducedCombatEffects; _background = Application.runInBackground; _random = UnityEngine.Random.state;
            try
            {
                Application.runInBackground = true; GameplayPresentationSettings.SetReducedCombatEffects(false);
                _scene = EditorSceneManager.NewPreviewScene();
                var room = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab")));
                SceneManager.MoveGameObjectToScene(room,_scene);
                foreach(var behaviour in room.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach(var light in room.GetComponentsInChildren<Light2D>(true)) light.enabled = false;
                Vector3 spawn = room.GetComponent<RoomController>().PlayerSpawnPosition;
                _camera = Host("Shock native camera").AddComponent<Camera>(); _camera.enabled = false; _camera.scene = _scene;
                _camera.orthographic = true; _camera.orthographicSize = 270f/48; _camera.aspect = 480f/270;
                _camera.transform.position = spawn+new Vector3(4,1,-10); _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.09f,.12f,.13f);
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                _camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                var owner = _camera.gameObject.AddComponent<AtmosphereController>();
                owner.Profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/MortfallDepthMist.asset");
                var fill = Host("Fixture global fill").AddComponent<Light2D>(); fill.lightType = Light2D.LightType.Global;
                fill.intensity = 1; fill.blendStyleIndex = 0; fill.targetSortingLayers = SortingLayer.layers.Select(x=>x.id).ToArray();
                var sourceHost = Host("Owned shock source"); sourceHost.SetActive(false); _source = sourceHost.AddComponent<PlayerStats>();
                Call(_source,"Awake"); _source.Initialize(null);
                _source.GetStat(StatType.ShockChance).BaseValue = 100; _source.GetStat(StatType.ShockDuration).BaseValue = 8;
                _walk = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Enemy/ZombieResources/zombieVersion2/zombie walk-Sheet.png").OfType<Sprite>().ToArray();
                Require(_walk.Length>0,"Enemy animation frames missing.");
                for(int i=0;i<10;i++)
                {
                    var actor = Host("Owned shock enemy "+i); actor.SetActive(false); actor.transform.position = spawn+new Vector3(i*.7f,0,0);
                    var body = actor.AddComponent<SpriteRenderer>(); body.sprite = _walk[0]; body.sortingLayerName = "World"; body.sortingOrder = 200+i*8;
                    actor.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Characters);
                    var stats = actor.AddComponent<EnemyStats>(); stats.Initialize(null,1);
                    Require(stats.TryGetStat(StatType.MaxHealth,out var maxHealth),"Enemy max-health stat missing."); maxHealth.BaseValue = 1000;
                    var health = actor.AddComponent<EnemyHealth>(); health.DestroyOnDeath = false; health.Initialize();
                    var ailments = actor.AddComponent<AilmentController>();
                    Actors.Add(actor);
                    if(i==0)
                    {
                        actor.SetActive(true); health.Initialize(); _health = health; _ailments = ailments; _body = body;
                        _events = 0; _ailments.OnAilmentsChanged += Notice;
                    }
                }
                owner.RefreshColorGroups(); _target = Own(new RenderTexture(480,270,24)); _target.Create();
                _arcSample = new MaterialPropertyBlock(); _litFrames = _quietFrames = 0;
                Times.Clear(); Times.Add("frame,editor_time_seconds,game_time_seconds,arc_seed,arc_pulse");
                _stage = _frame = 0; _next = EditorApplication.timeSinceStartup+.3; _deadline = _next+40;
                EditorApplication.update += Tick;
            }
            catch(Exception ex) { Fail(ex); }
        }
        private static void Notice() => _events++;
        private static void Tick()
        {
            try
            {
                Require(EditorApplication.isPlaying && EditorApplication.timeSinceStartup<_deadline,"Interrupted or timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                foreach(var actor in Actors.Where(a=>a!=null && a.activeInHierarchy))
                {
                    var shock = actor.GetComponent<ShockVisualController>(); if(shock!=null) Call(shock,"LateUpdate");
                    var ignite = actor.GetComponent<IgniteVisualController>(); if(ignite!=null) Call(ignite,"LateUpdate");
                }
                if(EditorApplication.timeSinceStartup<_next) return;
                switch(_stage)
                {
                    case 0:
                        Capture("Quiet"); Require(!Proc(_ailments,100),"Partial proc incorrectly shocked."); _shock = Actors[0].GetComponent<ShockVisualController>();
                        Require(!_shock.IsActive && Mathf.Abs(_shock.BuildupNormalized-1f/3)<.001 && _ailments.DamageTakenMoreMultiplier==1 && _events==0,"Buildup changed gameplay/active status."); break;
                    case 1:
                        if(!BrightSample()) return;
                        Capture("Buildup-33"); Require(!Proc(_ailments,100),"Second partial proc incorrectly shocked."); break;
                    case 2:
                        if(!BrightSample()) return;
                        Capture("Buildup-67"); Require(Proc(_ailments,100) && _ailments.IsShocked && _shock.IsActive && _events==1,"Threshold activation lost.");
                        _activation = (float)Get(_shock,"_activationUntil"); Capture("Activation"); break;
                    case 3:
                        if(!BrightSample()) return;
                        Capture("Active"); _frame = 0; break;
                    case 4:
                        _body.sprite = _walk[(_frame/4)%_walk.Length]; _body.flipX = _frame>=48; Call(_shock,"LateUpdate");
                        SampleArc(); float pulse = _arcSample.GetFloat("_ArcPulse"), seed = _arcSample.GetFloat("_ArcSeed");
                        if(pulse>.05f) _litFrames++; else _quietFrames++;
                        Times.Add(_frame+","+EditorApplication.timeSinceStartup.ToString("F6",CultureInfo.InvariantCulture)+","+Time.time.ToString("F6",CultureInfo.InvariantCulture)+","+seed.ToString("F2",CultureInfo.InvariantCulture)+","+pulse.ToString("F6",CultureInfo.InvariantCulture));
                        Capture("Animation-"+_frame.ToString("D2"));
                        _frame++; if(_frame<96) { _next = EditorApplication.timeSinceStartup+.015; return; }
                        File.WriteAllLines(Output+"/AnimationTimes.csv",Times);
                        Require(_litFrames>5 && _quietFrames>5,"Lightning lacks visible snaps or quiet gaps.");
                        for(int i=0;i<20;i++) Require(Proc(_ailments,1),"Active refresh failed.");
                        Require((float)Get(_shock,"_activationUntil")==_activation && _events==1,"Refresh replayed activation or status events.");
                        _ignite = Actors[0].AddComponent<IgniteVisualController>(); _ignite.SetBurning(true); break;
                    case 5:
                        if(!BrightSample()) return;
                        Capture("Ignite-and-shock"); _ignite.SetBurning(false); _freeze = Actors[0].AddComponent<EnemyFreezeController>();
                        Require(_freeze.TryApplyFreeze(8),"Frozen compatibility fixture failed."); break;
                    case 6:
                        if(!BrightSample()) return;
                        var tint = _body.color; Call(_shock,"LateUpdate"); Require(_body.color==tint,"Shock overwrote freeze tint."); Capture("Frozen-and-shocked");
                        Call(_freeze,"OnDisable"); _freeze.enabled = false; GameplayPresentationSettings.SetReducedCombatEffects(true); break;
                    case 7:
                        if(!BrightSample()) return;
                        Capture("Reduced"); GameplayPresentationSettings.SetReducedCombatEffects(false);
                        for(int i=1;i<10;i++)
                        {
                            Actors[i].SetActive(true); Actors[i].GetComponent<EnemyHealth>().Initialize();
                            Require(Proc(Actors[i].GetComponent<AilmentController>(),300),"Crowd activation failed.");
                        }
                        break;
                    case 8:
                        Require(Actors.Sum(a=>a.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.name=="ShockArcs"))==10,"More than one arc layer per actor.");
                        Capture("Ten-shocked-enemies");
                        Call(_ailments,"UpdateShock",8.1f); Require(!_ailments.IsShocked && !_shock.IsActive && _events==2,"Authoritative expiry did not clear visual/status.");
                        for(int i=1;i<10;i++) Actors[i].SetActive(false); break;
                    case 9:
                        Capture("Expired"); Require(Mathf.Approximately(_health.CurrentHealth,_health.MaxHealth),"Presentation created damage.");
                        Require(!Proc(_ailments,100) && !_shock.IsActive,"Fresh threshold not required after expiry.");
                        _health.Initialize(); Require(_shock.BuildupNormalized==0,"Owner reset retained partial traces.");
                        File.WriteAllText(Output+"/Report.txt","PASS: actual successful-proc buildup (100/300 then 200/300), threshold activation, one discharge only, persistent arcs, refresh without replay/events, explicit authoritative expiry/reset, unchanged health and damage modifier, real zombie animation/flip, native 480x270 Mortfall geometry, Ignite coexistence, actual freeze tint preservation, reduced mode, ten enemies with one arc layer each, supported Resources shader/no magenta. Controlled owners/procs; no ordinary combat, performance benchmark or standalone build. Duration expiry advanced through the real ailment method; presentation advanced explicitly in preview scene. Original save untouched.");
                        End(); return;
                }
                _stage++; _next = EditorApplication.timeSinceStartup+.18;
            }
            catch(Exception ex) { Fail(ex); }
        }
        private static void SampleArc()
        {
            var arc = Actors[0].GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="ShockArcs");
            arc.GetPropertyBlock(_arcSample);
        }
        private static bool BrightSample()
        {
            SampleArc(); if(_arcSample.GetFloat("_ArcPulse")>.35f) return true;
            _next = EditorApplication.timeSinceStartup+.005; return false;
        }
        private static void Capture(string name)
        {
            if(_shock!=null && _shock.IsActive)
            {
                var arc = Actors[0].GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="ShockArcs");
                Require(arc.sprite==_body.sprite && arc.flipX==_body.flipX,"Arcs detached from animation.");
                Require(!ShaderUtil.ShaderHasError(arc.sharedMaterial.shader) && arc.sharedMaterial.shader.isSupported,"Shock shader failed.");
            }
            RenderPipeline.SubmitRenderRequest(_camera,new RenderPipeline.StandardRequest { destination = _target });
            var previous = RenderTexture.active; var image = new Texture2D(480,270,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active = _target; image.ReadPixels(new Rect(0,0,480,270),0,0); image.Apply();
                Require(!image.GetPixels32().Any(p=>p.r>240 && p.b>240 && p.g<20),"Magenta shader pixels.");
                File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Fail(Exception ex) { File.WriteAllText(Output+"/Report.txt","FAIL: "+ex); Debug.LogException(ex); End(); }
        private static void End()
        {
            EditorApplication.update -= Tick;
            if(_ailments!=null) _ailments.OnAilmentsChanged -= Notice;
            foreach(var actor in Actors) if(actor!=null) { actor.GetComponent<ShockVisualController>()?.Stop(); actor.GetComponent<IgniteVisualController>()?.SetBurning(false); }
            for(int i=Owned.Count-1;i>=0;i--) if(Owned[i]!=null) Object.DestroyImmediate(Owned[i]); Owned.Clear(); Actors.Clear(); _camera = null;
            if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            GameplayPresentationSettings.SetReducedCombatEffects(_reduced); if(!_hadReduced) PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            Application.runInBackground = _background; UnityEngine.Random.state = _random;
        }
    }
}
