#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Scripts.Enemies;
using Scripts.Stats;
using Scripts.Visuals;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private bool _cameraReview, _cameraInteracted;
        private CinemachineCamera _reviewVcam;
        private CinemachineBrain _reviewBrain;
        private CombatCameraImpulse _reviewImpulse;
        private BoxCollider2D _reviewBounds;
        private GamePauseService.PauseHandle _cameraPause;
        private float _savedCameraStrength;
        private bool _savedCameraStrengthExists;
        private Scripts.Skills.SkillDataSO _cameraSkill;
        private static readonly string[] CameraCases = { "heavy-off", "heavy-on", "normal-on", "bow-on", "heavy-edge-left", "heavy-edge-right", "heavy-pause", "heavy-disable", "heavy-room-change", "heavy-crowd", "heavy-owner-zero-health" };

        public sealed partial class FrameRecord
        {
            public Vector3 cameraPosition, cameraImpulse;
            public int cameraPulses;
            public bool cameraInsideBounds;
        }

        public static void BeginCameraReview() => BeginCapture(true, true, cameraReview: true);

        private void ConfigureCameraCase()
        {
            _frameLimit = 180; _cameraInteracted = false;
            _savedCameraStrengthExists = PlayerPrefs.HasKey(GameplayPresentationSettings.CameraImpulseStrengthKey);
            _savedCameraStrength = GameplayPresentationSettings.CameraImpulseStrength;
            // Temporary preference overrides are restored after every case; no editor asset is changed.
            GameplayPresentationSettings.SetCameraImpulseStrength(_case.name == "heavy-off" ? 0 : .5f);
            var host = new GameObject("Review Cinemachine"); host.transform.SetParent(_fixture.transform);
            host.transform.position = _camera.transform.position;
            _reviewVcam = host.AddComponent<CinemachineCamera>();
            _reviewVcam.OutputChannel = (OutputChannels)(1 << 20);
            _reviewVcam.Follow = _owner.transform; _reviewVcam.Lens.OrthographicSize = _camera.orthographicSize;
            var composer = host.AddComponent<CinemachinePositionComposer>(); composer.Damping = Vector3.zero;
            composer.TargetOffset = new Vector3(.8f, .5f, 0);
            var boundsHost = new GameObject("Review room camera bounds"); boundsHost.transform.SetParent(_fixture.transform);
            boundsHost.transform.position = new Vector3(3000.8f, 2000.5f, 0);
            _reviewBounds = boundsHost.AddComponent<BoxCollider2D>(); _reviewBounds.isTrigger = true;
            _reviewBounds.size = new Vector2(24, 15);
            if (_case.name == "heavy-edge-left") _reviewBounds.transform.position += Vector3.right * 2;
            if (_case.name == "heavy-edge-right") _reviewBounds.transform.position += Vector3.left * 2;
            var confiner = host.AddComponent<CinemachineConfiner2D>(); confiner.BoundingShape2D = _reviewBounds;
            confiner.Damping = 0; confiner.SlowingDistance = 0;
            _reviewImpulse = host.AddComponent<CombatCameraImpulse>();
            _reviewBrain = _camera.gameObject.AddComponent<CinemachineBrain>();
            _reviewBrain.ChannelMask = _reviewVcam.OutputChannel; _reviewBrain.IgnoreTimeScale = false;
            _reviewBrain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            _reviewBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            if (_followReview) { ConfigureFollowCase(composer); return; }
            if (_case.name == "heavy-crowd")
            {
                _cameraSkill = Object.Instantiate(_runner.Data); _cameraSkill.Cooldown = 0;
                _runner.Initialize(_owner, _cameraSkill);
                _owner.GetStat(StatType.AttackSpeed).BaseValue = 12;
                _owner.GetStat(StatType.AreaOfEffect).BaseValue = 200; _owner.NotifyChanged();
                var firstStats = _enemy.GetComponent<EnemyStats>(); Set(firstStats, StatType.MaxHealth, 1000000); _health.Initialize();
                for (int i = 1; i < 12; i++)
                {
                    var enemy = Object.Instantiate(_enemyData.Prefab, _fixture.transform); enemy.Setup(_enemyData, 1);
                    enemy.GetComponent<EnemyBrain>().enabled = false; enemy.GetComponent<EnemyLocomotion2D>().enabled = false;
                    enemy.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
                    enemy.transform.position = _enemy.transform.position + Vector3.right * i * .1f;
                    var stats = enemy.GetComponent<EnemyStats>(); Set(stats, StatType.MaxHealth, 1000000); Set(stats, StatType.StunThreshold, 1000000);
                    Set(stats, StatType.Evasion, 0); Set(stats, StatType.Armor, 0); Set(stats, StatType.PhysicalResist, 0);
                    enemy.GetComponent<EnemyHealth>().Initialize(); enemy.GetComponent<EnemyStunController>().RecalculateMaxMeter(true);
                }
            }
        }

        private void UpdateCameraCase()
        {
            if (_followReview) { UpdateFollowCase(); return; }
            if (_frame == CastFrame || _case.name == "heavy-crowd" && _frame > CastFrame && _frame < 110 && !_runner.IsCasting) StartCaseCast();
            if (!_cameraInteracted && _reviewImpulse.AcceptedImpulses > 0 && _reviewImpulse.LastOffset != Vector3.zero)
            {
                if (_case.name == "heavy-pause") _cameraPause = GamePauseService.Acquire(GamePauseReason.PauseMenu);
                if (_case.name == "heavy-disable") GameplayPresentationSettings.SetCameraImpulseStrength(0);
                if (_case.name == "heavy-owner-zero-health") _owner.Health.SetCurrent(0); // No OnDepleted/save/death flow.
                if (_case.name == "heavy-room-change")
                {
                    var nextBounds = new GameObject("Review next room bounds").AddComponent<BoxCollider2D>();
                    nextBounds.transform.SetParent(_fixture.transform); nextBounds.transform.position = _reviewBounds.transform.position;
                    nextBounds.size = _reviewBounds.size; nextBounds.isTrigger = true;
                    _reviewBounds = nextBounds; _reviewVcam.GetComponent<CinemachineConfiner2D>().BoundingShape2D = nextBounds;
                }
                _cameraInteracted = true;
            }
            if (_cameraPause != null && _frame > 120) { _cameraPause.Dispose(); _cameraPause = null; }
        }

        private void RenderCameraFrame()
        {
            var record = _case.frames.Last();
            record.cameraPosition = _camera.transform.position;
            record.cameraImpulse = _reviewImpulse.LastOffset; record.cameraPulses = _reviewImpulse.AcceptedImpulses;
            Bounds bounds = _reviewBounds.bounds; float height = _camera.orthographicSize, width = height * _camera.aspect;
            record.cameraInsideBounds = record.cameraPosition.x - width >= bounds.min.x - .01f && record.cameraPosition.x + width <= bounds.max.x + .01f &&
                record.cameraPosition.y - height >= bounds.min.y - .01f && record.cameraPosition.y + height <= bounds.max.y + .01f;
            if (_frame % 2 != 0) return;
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(_camera, new UniversalRenderPipeline.SingleCameraRequest { destination = _target });
                RenderTexture.active = _target; _image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); _image.Apply(false, false);
                File.WriteAllBytes(Output + "/" + _case.name + "/frame-" + _frame.ToString("D3") + ".png", _image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
        }

        private void ValidateCameraCase()
        {
            if (_followReview) { ValidateFollowCase(); return; }
            bool muted = _case.name == "heavy-off" || _case.name == "normal-on" || _case.name == "bow-on";
            int pulses = _reviewImpulse.AcceptedImpulses;
            if (_case.confirmedHits == 0) _case.error = "No real damage was delivered.";
            else if (muted && pulses != 0 || !muted && pulses < 1) _case.error = "Unexpected impulse eligibility.";
            else if (_case.frames.Any(f => !f.cameraInsideBounds)) _case.error = "Camera viewport escaped room bounds.";
            else if (_reviewImpulse.LastOffset != Vector3.zero) _case.error = "Impulse did not settle to zero.";
            else if (_case.name != "heavy-crowd" && !muted && pulses != 1) _case.error = "One cast produced multiple impulses.";
            else if (!muted && !_case.frames.Any(f => f.cameraImpulse != Vector3.zero)) _case.error = "Impulse was never visible.";
            else if ((_case.name == "heavy-pause" || _case.name == "heavy-disable" || _case.name == "heavy-room-change" || _case.name == "heavy-owner-zero-health") && !_cameraInteracted) _case.error = "Lifecycle scenario did not run.";
            else if (_case.name == "heavy-crowd" && (_case.acceptedCasts < 3 || pulses >= _case.confirmedHits || pulses > _case.acceptedCasts)) _case.error = "Rapid casts/crowd did not exercise the shared limit.";
        }

        private void DisposeCameraCase()
        {
            _cameraPause?.Dispose(); _cameraPause = null;
            if (_reviewBrain != null) Object.DestroyImmediate(_reviewBrain);
            if (_cameraSkill != null) Object.DestroyImmediate(_cameraSkill); _cameraSkill = null;
            GameplayPresentationSettings.SetCameraImpulseStrength(_savedCameraStrength);
            if (!_savedCameraStrengthExists) PlayerPrefs.DeleteKey(GameplayPresentationSettings.CameraImpulseStrengthKey);
            PlayerPrefs.Save();
        }
    }
}
#endif
