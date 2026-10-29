#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace RelicKeeper.Diagnostics
{
    // Opt-in only, never attached to gameplay scenes. Also runs in a development
    // player with -combat-decoration-audit; no shipping UI or background monitor.
    public sealed class CombatDecorationStressProbe : MonoBehaviour
    {
        [Serializable] public sealed class ModeResult
        {
            public string mode; public int requests, admitted, peakImpacts, particlesRequested, particlesAdmitted, hits;
            public float damage, medianFrameMs, p95FrameMs; public bool passed;
        }
        [Serializable] private sealed class Report
        {
            public string platform, gpu, cpu; public bool player; public ModeResult[] modes;
        }
        private readonly List<GameObject> _owned = new List<GameObject>();
        private readonly List<GameObject> _effects = new List<GameObject>();
        private bool _previousReduced, _quit;
        private int _previousVsync, _previousFps;
        private bool _previousBackground;
        private RenderTexture _target;
        private Material _particleMaterial;
        private string Output => Path.Combine(Directory.GetCurrentDirectory(), "Logs/CombatDecorationStress" + (Application.isEditor ? "Editor" : "Player"));
        private static readonly Vector3 Origin = new Vector3(15000, 15000);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Environment.GetCommandLineArgs().Contains("-combat-decoration-audit")) Begin();
        }
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            new GameObject("Temporary decoration stress audit").AddComponent<CombatDecorationStressProbe>();
        }
        private GameObject Keep(GameObject go) { _owned.Add(go); return go; }
        private IEnumerator Start()
        {
            _quit = !Application.isEditor;
            _previousReduced = GameplayPresentationSettings.ReducedCombatEffects;
            _previousVsync = QualitySettings.vSyncCount; _previousFps = Application.targetFrameRate;
            _previousBackground = Application.runInBackground; Application.runInBackground = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Directory.CreateDirectory(Output);
            var camera = Keep(new GameObject("Audit camera")).AddComponent<Camera>();
            camera.transform.position = Origin + Vector3.back * 10; camera.orthographic = true;
            camera.orthographicSize = 3; camera.depth = 100; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.10f, .14f, .15f);
            camera.enabled = false;
            _target = new RenderTexture(480, 270, 24); _target.Create();
            var owner = Keep(new GameObject("Audit projectile owner")).AddComponent<PlayerStats>();
            owner.transform.position = Origin + Vector3.left * 5;
            owner.GetStat(StatType.DamagePhysical).BaseValue = 10; owner.GetStat(StatType.CritChance).BaseValue = 0;
            var target = Keep(new GameObject("Audit damage recorder")).AddComponent<DecorationAuditTarget>();
            target.transform.position = Origin + Vector3.right * 2; target.gameObject.layer = 7;
            target.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 5);
            var skill = Resources.Load<SkillDataSO>("Skills/2HWeapon/Bow/LuckyShot/LuckyShotSkill");
            var shot = skill.Recipe.Steps.First(x => x.StepDefinition.Id == "SpawnProjectile");
            var impact = Resources.Load<GameObject>("Skills/2HWeapon/Chakram/rb/CircularSaw/Sparks");
            var particles = Keep(new GameObject("Audit decorative particles")).AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.transform.position = Origin;
            var main = particles.main; main.maxParticles = 2000; main.startLifetime = .5f;
            main.startSize = .04f; main.startSpeed = .3f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.enabled = false;
            _particleMaterial = new Material(Shader.Find("Sprites/Default"));
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = _particleMaterial;
            var results = new List<ModeResult>();
            foreach (string mode in new[] { "uncapped-reference", "normal", "reduced" })
            {
                GameplayPresentationSettings.SetReducedCombatEffects(mode == "reduced");
                var result = new ModeResult { mode = mode }; var samples = new List<float>();
                target.Hits = 0; target.Damage = 0;
                for (int i = 0; i < 32; i++)
                    SkillProjectile.Spawn(new SkillProjectileLaunchData {
                        OwnerStats = owner, OwnerTransform = owner.transform, Skill = skill, Step = shot,
                        ProjectilePrefab = shot.GetObject<GameObject>("ProjectilePrefab"),
                        DamageContext = new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Projectile),
                        TargetLayer = 1 << 7, WorldLayer = 0, Speed = 6, Lifetime = 2, HitRadius = .1f,
                        IgnoreFork = true, IgnoreChain = true, RotationDegreesPerSecond = 0
                    }, Origin + new Vector3(-3, (i % 8 - 3.5f) * .4f), Vector2.right);
                float start = Time.time, next = start; int batch = 0; bool captured = false;
                while (Time.time - start < 3)
                {
                    if (Time.time >= next && batch < 20)
                    {
                        next = start + ++batch * .1f;
                        for (int i = 0; i < 40; i++)
                        {
                            var position = Origin + new Vector3((i % 10 - 4.5f) * .6f, (i / 10 - 1.5f) * .7f);
                            result.requests++;
                            var effect = mode == "uncapped-reference" ? Instantiate(impact, position, Quaternion.identity)
                                : CombatDecorationBudget.SpawnImpact(impact, position, Quaternion.identity);
                            if (effect != null) { result.admitted++; _effects.Add(effect); }
                        }
                        result.particlesRequested += 200;
                        int admitted = mode == "uncapped-reference" ? 200 : CombatDecorationBudget.AdmitParticles(200, .5f);
                        result.particlesAdmitted += admitted; particles.Emit(admitted);
                    }
                    _effects.RemoveAll(x => x == null);
                    result.peakImpacts = Mathf.Max(result.peakImpacts, _effects.Count);
                    // Hidden Windows players can skip backbuffer rendering even with simulation
                    // enabled. Force the same offscreen 480x270 camera workload in every mode.
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = _target });
                    if (Time.time - start > .5f) samples.Add(Time.unscaledDeltaTime * 1000);
                    if (!captured && Time.time - start > 1)
                    {
                        var previous = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = _target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                            File.WriteAllBytes(Path.Combine(Output, mode + ".png"), image.EncodeToPNG());
                        }
                        finally { RenderTexture.active = previous; Destroy(image); }
                        captured = true;
                    }
                    yield return null;
                }
                samples.Sort(); result.medianFrameMs = samples[samples.Count / 2];
                result.p95FrameMs = samples[Mathf.Min(samples.Count - 1, (int)(samples.Count * .95f))];
                result.hits = target.Hits; result.damage = target.Damage;
                result.passed = target.Hits == 32 && (mode == "uncapped-reference" ||
                    result.peakImpacts <= CombatEffectsPolicy.ImpactDecorationLimit(mode == "reduced"));
                results.Add(result);
                yield return new WaitForSeconds(.6f);
            }
            // The empty player scene has no equipped-hand/global character modifiers.
            // A live Hub Editor session can supply randomized weapon ranges; do not
            // mistake distinct valid damage rolls there for lost gameplay hits.
            bool sameDamage = Application.isEditor || results.All(x => Mathf.Approximately(x.damage, results[0].damage));
            foreach (var result in results) result.passed &= sameDamage;
            File.WriteAllText(Path.Combine(Output, "audit.json"), JsonUtility.ToJson(new Report {
                platform = Application.unityVersion + " " + SystemInfo.graphicsDeviceType, gpu = SystemInfo.graphicsDeviceName,
                cpu = SystemInfo.processorType, player = !Application.isEditor, modes = results.ToArray()
            }, true));
            if (results.Any(x => !x.passed)) Debug.LogError("Decoration stress audit failed; see " + Output);
            Destroy(gameObject);
        }
        private void OnDestroy()
        {
            GameplayPresentationSettings.SetReducedCombatEffects(_previousReduced);
            QualitySettings.vSyncCount = _previousVsync; Application.targetFrameRate = _previousFps;
            Application.runInBackground = _previousBackground;
            foreach (var effect in _effects) if (effect != null) Destroy(effect);
            foreach (var go in _owned) if (go != null) Destroy(go);
            if (_particleMaterial != null) Destroy(_particleMaterial);
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_quit) Application.Quit();
        }
    }
    public sealed class DecorationAuditTarget : MonoBehaviour, IDamageable
    {
        public int Hits; public float Damage;
        public bool TakeDamage(DamageSnapshot damage) { Hits++; Damage += damage.TotalDamage; return true; }
    }
}
#endif
