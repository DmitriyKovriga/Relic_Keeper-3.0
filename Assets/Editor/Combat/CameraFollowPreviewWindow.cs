using System;
using Unity.Cinemachine;
using Unity.Cinemachine.Editor;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    // Temporary edits to the existing pipeline. No second follow target or camera mover.
    public sealed class CameraFollowPreviewSession : IDisposable
    {
        private readonly CinemachinePositionComposer _camera;
        private readonly Vector3 _damping;
        private readonly LookaheadSettings _lookahead;
        public bool IsPreviewing { get; private set; }
        public CameraFollowPreviewSession(CinemachinePositionComposer camera)
        {
            _camera = camera; _damping = camera.Damping; _lookahead = camera.Lookahead;
            SaveDuringPlay.OnHotSave += Restore;
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
        }
        public static void ApplyCandidate(CinemachinePositionComposer camera, float lead = .12f, float damping = .05f)
        {
            CombatBaselineCapture.ApplyFollowCandidate(camera, lead, damping);
            ResetHistory(camera);
        }
        public void Preview(float lead, float damping) { if (_camera == null) return; ApplyCandidate(_camera, lead, damping); IsPreviewing = true; }
        public void Restore()
        {
            if (_camera != null && IsPreviewing)
            {
                _camera.Damping = _damping; _camera.Lookahead = _lookahead; ResetHistory(_camera);
            }
            IsPreviewing = false;
        }
        private static void ResetHistory(CinemachinePositionComposer camera)
        {
            var vcam = camera.GetComponent<CinemachineCamera>();
            if (vcam != null) vcam.PreviousStateIsValid = false;
        }
        public void Dispose()
        {
            Restore(); SaveDuringPlay.OnHotSave -= Restore;
            AssemblyReloadEvents.beforeAssemblyReload -= Restore;
        }
    }

    public sealed class CameraFollowPreviewWindow : EditorWindow
    {
        private CameraFollowPreviewSession _session;
        private CinemachinePositionComposer _camera;
        private float _lead = .12f, _damping = .05f;
        [MenuItem("Relic Keeper/Camera Follow Preview")]
        public static void Open() => GetWindow<CameraFollowPreviewWindow>("Camera Follow").Show();
        private void OnEnable() { EditorApplication.playModeStateChanged += ModeChanged; }
        private void OnDisable() { EditorApplication.playModeStateChanged -= ModeChanged; Release(); }
        private void ModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode) Release(); Repaint(); }
        private void Release() { _session?.Dispose(); _session = null; _camera = null; }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Press Play, then compare Current and Look ahead while moving. Closing this window or stopping Play restores the original camera. Nothing is saved.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (_session != null && (_camera == null || !CinemachineCore.IsLive(_camera.GetComponent<CinemachineCamera>())))
                    Release();
                if (_camera == null && EditorApplication.isPlaying)
                    foreach (var c in UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                        if (c.Follow != null && CinemachineCore.IsLive(c)) { _camera = c.GetComponent<CinemachinePositionComposer>(); if (_camera != null) break; }
                EditorGUILayout.LabelField("Camera", _camera != null ? _camera.gameObject.name : "Available during Play");
                EditorGUI.BeginChangeCheck();
                _lead = EditorGUILayout.Slider("Look ahead (seconds)", _lead, 0, .2f);
                _damping = EditorGUILayout.Slider("Horizontal softness", _damping, 0, .1f);
                if (EditorGUI.EndChangeCheck() && _session != null && _session.IsPreviewing) _session.Preview(_lead, _damping);
                using (new EditorGUI.DisabledScope(_camera == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Current")) _session?.Restore();
                    if (GUILayout.Button("Look ahead")) { if (_session == null) _session = new CameraFollowPreviewSession(_camera); _session.Preview(_lead, _damping); }
                }
                EditorGUILayout.LabelField("Preview", _session != null && _session.IsPreviewing ? "Look ahead" : "Current");
            }
        }
    }
}
