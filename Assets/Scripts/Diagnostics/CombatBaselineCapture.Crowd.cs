#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Stats;
using Scripts.Visuals;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private bool _crowdReview, _crowdBefore, _crowdInteracted;
        private static bool s_crowdLifetime;
        private int _crowdPauseRelease;
        private GamePauseService.PauseHandle _crowdPause;
        private readonly List<EnemyEntity> _crowd = new List<EnemyEntity>();
        private EnemyHitFlash[] _crowdFlashes;
        private EnemyHitRecoil[] _crowdRecoils;
        private ProfilerRecorder _crowdCpu, _crowdGc;
        private static readonly string[] CrowdBaselineCases = { "fast-30", "fast-60", "fast-144" };
        private static readonly string[] CrowdReviewCases = { "fast-30", "fast-60", "fast-144", "dense-off", "dense-on", "fan", "return", "saw", "beam", "pause-stop", "cancel-stop", "slowmo-pause", "overlap-requests" };
        private static readonly string[] CrowdLifetimeCases = { "disable-stop", "destroy-stop" };
        private string[] CrowdCases => s_crowdLifetime ? CrowdLifetimeCases : _crowdBefore ? CrowdBaselineCases : CrowdReviewCases;
        private string CrowdOutput => s_crowdLifetime ? "Logs/CombatCrowdLifetime" : _crowdBefore ? "Logs/CombatCrowdBefore" : "Logs/CombatCrowdAfter";
        private int CrowdFps => _case.name == "fast-30" ? 30 : _case.name == "fast-144" ? 144 : 60;
        private string CrowdSkillPath => "Assets/Resources/Skills/" + (_case.name == "return" ? "2HWeapon/Chakram/lb/Pendulum/PendulumSkill"
            : _case.name == "saw" ? "2HWeapon/Chakram/rb/CircularSaw/CircularSawSkill"
            : _case.name == "fan" ? "2HWeapon/Bow/NearFan/NearFanSkill"
            : _case.name == "beam" ? "2HWeapon/Staff/Lightning/LightningRod/LightningRodSkill"
            : "2HWeapon/Axe/LeftButton/CleaveLB") + ".asset";

        public sealed partial class FrameRecord
        {
            public int activeFlashes, activeRecoils, recoilRenderers;
            public long feedbackManagedBytes, gcFrameBytes;
            public double feedbackCpuUs, renderCpuUs, mainThreadMs;
            public bool screenshot, menuPaused;
        }

        public sealed partial class CaseResult
        {
            public int reviewFps, targets;
            public string cpu, gpu, graphicsApi;
            public bool recoveredPause, recoveredCancel, recoveredLifetime;
            public float expectedTimeScale;
        }

        public static void BeginCrowdBaseline() { s_crowdLifetime = false; BeginCapture(true, true, false, false, true, true); }
        public static void BeginCrowdReview() { s_crowdLifetime = false; BeginCapture(true, true, false, false, true, false); }
        public static void BeginCrowdLifetimeReview() { s_crowdLifetime = true; BeginCapture(true, true, false, false, true, false); }

        private void ConfigureCrowdCase()
        {
            _crowd.Clear(); _crowdInteracted = false; _crowdPauseRelease = -1;
            Time.timeScale = _case.name == "slowmo-pause" ? .5f : 1;
            _case.expectedTimeScale = Time.timeScale; _case.reviewFps = CrowdFps;
            _case.cpu = SystemInfo.processorType; _case.gpu = SystemInfo.graphicsDeviceName; _case.graphicsApi = SystemInfo.graphicsDeviceVersion;
            Time.captureFramerate = CrowdFps;
            _frameLimit = CrowdFps * (_case.name == "beam" ? 9 : _case.name == "saw" || _case.name == "return" ? 6 : 4);
            bool fast = _case.name.StartsWith("fast", StringComparison.Ordinal) || _case.name.StartsWith("dense", StringComparison.Ordinal) || _case.name == "overlap-requests";
            _owner.GetStat(StatType.AttackSpeed).BaseValue = fast ? 12 : 1;
            _owner.GetStat(StatType.MaxMana).BaseValue = 100000; _owner.Mana.RestoreFull();
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = fast ? 200 : 0;
            _owner.NotifyChanged();
            bool enabled = _case.name != "dense-off";
            _comparisonProfile.EnableBodyFollow = _comparisonProfile.EnableEnemyRecoil = _comparisonProfile.EnableEnemyHitFlash = enabled;
            int count = _case.name.StartsWith("dense", StringComparison.Ordinal) ? 24 : _case.name == "saw" || _case.name.Contains("stop") || _case.name == "slowmo-pause" ? 1 : 6;
            for (int i = 0; i < count; i++)
            {
                EnemyEntity enemy = i == 0 ? _enemy : UnityEngine.Object.Instantiate(_enemyData.Prefab, _fixture.transform);
                if (i != 0) enemy.Setup(_enemyData, 1);
                enemy.GetComponent<EnemyBrain>().enabled = false;
                var movement = enemy.GetComponent<EnemyLocomotion2D>(); if (movement != null) movement.enabled = false;
                var rb = enemy.GetComponent<Rigidbody2D>(); if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeAll;
                enemy.transform.position = _owner.transform.position + new Vector3(1.1f + (i % 12) * .25f, i / 12 * 1.1f, 0);
                var stats = enemy.GetComponent<EnemyStats>();
                foreach (var stat in new[] { StatType.MaxHealth, StatType.StunThreshold }) Set(stats, stat, 1000000);
                foreach (var stat in new[] { StatType.Evasion, StatType.Armor, StatType.PhysicalResist }) Set(stats, stat, 0);
                enemy.GetComponent<EnemyHealth>().Initialize(); enemy.GetComponent<EnemyStunController>().RecalculateMaxMeter(true);
                foreach (var art in enemy.GetComponentsInChildren<SpriteRenderer>(true)) art.sharedMaterial = _material;
                var recoil = enemy.GetComponent<EnemyHitRecoil>(); var flash = enemy.GetComponent<EnemyHitFlash>();
                typeof(EnemyHitRecoil).GetField("_profile", Private).SetValue(recoil, _comparisonProfile);
                typeof(EnemyHitFlash).GetField("_profile", Private).SetValue(flash, _comparisonProfile);
                recoil.Initialize(enemy.VisualRenderer); flash.Initialize(enemy.VisualRenderer); _crowd.Add(enemy);
            }
            _case.targets = count;
            _beforeHealth = _health.CurrentHealth; _case.initialHealth = _beforeHealth;
            _crowdFlashes = _crowd.Select(e => e.GetComponent<EnemyHitFlash>()).ToArray();
            _crowdRecoils = _crowd.Select(e => e.GetComponent<EnemyHitRecoil>()).ToArray();
            _crowdCpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _crowdGc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            _camera.orthographicSize = 270f / 48; _camera.transform.position = new Vector3(3001.6f, 2000.4f, -10);
        }

        private void UpdateCrowdCase()
        {
            int start = Mathf.RoundToInt(CrowdFps * .3f), stop = CrowdFps * 3;
            bool repeated = _case.name.StartsWith("fast", StringComparison.Ordinal) || _case.name.StartsWith("dense", StringComparison.Ordinal) || _case.name == "overlap-requests";
            if (_frame == start || repeated && _frame > start && _frame < stop && !_runner.IsCasting) StartCaseCast();
            if (_case.name == "overlap-requests" && _frame > start && _frame < stop)
                for (int i = 0; i < 20; i++) new SkillHitStopGate(5).TryTrigger();
            bool freeze = Time.timeScale == 0 && !GamePauseService.IsPaused;
            if (!_crowdInteracted && freeze && (_case.name == "pause-stop" || _case.name == "slowmo-pause"))
            {
                _crowdInteracted = true; _crowdPause = GamePauseService.Acquire(GamePauseReason.PauseMenu);
                _crowdPauseRelease = _frame + Mathf.RoundToInt(CrowdFps * .2f);
            }
            if (_crowdPause != null)
            {
                HitStopService.Request(5); // Real pause must reject incoming requests.
                if (_frame >= _crowdPauseRelease)
                {
                    _crowdPause.Dispose(); _crowdPause = null;
                    _case.recoveredPause = Mathf.Abs(Time.timeScale - _case.expectedTimeScale) < .0001f;
                }
            }
            if (!_crowdInteracted && freeze && _case.name == "cancel-stop")
            {
                _crowdInteracted = true; _runner.Cancel();
            }
            if (!_crowdInteracted && freeze && s_crowdLifetime)
            {
                _crowdInteracted = true;
                var backend = (Behaviour)typeof(HitStopService).GetField("_runner", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                if (_case.name == "disable-stop") backend.enabled = false;
                else UnityEngine.Object.DestroyImmediate(backend.gameObject);
                _case.recoveredLifetime = Mathf.Abs(Time.timeScale - _case.expectedTimeScale) < .0001f && !HitStopService.IsActive;
                // A disabled backend must reject new requests rather than freezing forever without Update.
                HitStopService.Request(3);
                if (_case.name == "disable-stop" && Time.timeScale == 0) _case.recoveredLifetime = false;
            }
            if (_case.name == "cancel-stop" && _crowdInteracted && Time.timeScale > 0 && !_runner.IsCasting) _case.recoveredCancel = true;
        }

        private void RecordCrowdFrame(FrameRecord frame)
        {
            frame.menuPaused = GamePauseService.IsPaused;
            long memory = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            for (int i = 0; i < _crowdFlashes.Length; i++)
            {
                _crowdFlashes[i].Sample(Time.time); _crowdRecoils[i].Sample(Time.time);
                if (_crowdFlashes[i].Amount > 0) frame.activeFlashes++;
                var display = _crowdRecoils[i].DisplayRenderer;
                if (display != null) { frame.recoilRenderers++; if (display.enabled) frame.activeRecoils++; }
            }
            frame.feedbackCpuUs = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency;
            frame.feedbackManagedBytes = GC.GetAllocatedBytesForCurrentThread() - memory;
            frame.mainThreadMs = _crowdCpu.Valid ? _crowdCpu.LastValue / 1000000.0 : -1;
            frame.gcFrameBytes = _crowdGc.Valid ? _crowdGc.LastValue : -1;
        }

        private void RenderCrowdFrame()
        {
            var frame = _case.frames[_case.frames.Count - 1]; long start = Stopwatch.GetTimestamp();
            RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _target });
            frame.renderCpuUs = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency;
            frame.screenshot = _frame % Mathf.Max(1, CrowdFps / 6) == 0;
            if (!frame.screenshot) return;
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target; _image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); _image.Apply();
                File.WriteAllBytes(Output + "/" + _case.name + "/frame-" + _frame.ToString("D3") + ".png", _image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
        }

        private void ValidateCrowdCase()
        {
            if (_case.acceptedCasts < 1 || _case.confirmedHits < 1) _case.error = "Expected accepted casts and positive direct hits.";
            else if (_case.frames.Any(f => f.recoilRenderers > _case.targets || f.activeFlashes > _case.targets)) _case.error = "Feedback renderer count exceeded one per target.";
            else if (_case.frames.Last().activeFlashes > 0 || _case.frames.Last().activeRecoils > 0) _case.error = "Feedback did not settle to idle.";
            else if (Mathf.Abs(Time.timeScale - _case.expectedTimeScale) > .0001f) _case.error = "Time scale was not restored.";
            else if ((_case.name == "pause-stop" || _case.name == "slowmo-pause") && !_case.recoveredPause) _case.error = "Pause during hit-stop lost the original time scale.";
            else if (_case.name == "cancel-stop" && !_case.recoveredCancel) _case.error = "Cancellation failed to recover from hit-stop.";
            else if (s_crowdLifetime && !_case.recoveredLifetime) _case.error = "Backend lifetime failed to release time safely.";
            else if ((_case.name == "return" || _case.name == "saw" || _case.name == "beam") && _case.confirmedHits <= _case.targets) _case.error = "Repeated/return hits were not reproduced.";
        }

        private void DisposeCrowdCase()
        {
            _crowdPause?.Dispose(); _crowdPause = null; Time.timeScale = 1;
            if (s_crowdLifetime)
            {
                var backend = (Component)typeof(HitStopService).GetField("_runner", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                if (backend != null) UnityEngine.Object.DestroyImmediate(backend.gameObject);
            }
            if (_crowdCpu.Valid) _crowdCpu.Dispose(); if (_crowdGc.Valid) _crowdGc.Dispose();
            foreach (var projectile in UnityEngine.Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None))
            {
                var data = typeof(SkillProjectile).GetField("_data", Private).GetValue(projectile) as SkillProjectileLaunchData;
                if (data != null && data.OwnerStats == _owner) UnityEngine.Object.DestroyImmediate(projectile.gameObject);
            }
            _crowd.Clear(); _crowdFlashes = null; _crowdRecoils = null;
        }
    }
}
#endif
