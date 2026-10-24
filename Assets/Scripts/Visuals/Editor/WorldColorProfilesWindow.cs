using System.IO;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace RelicKeeper.Editor.Visuals
{
    public sealed class WorldColorProfilesWindow : EditorWindow
    {
        internal enum PreviewEnvironment { CurrentGame, Hub, Mortfall }
        [SerializeField] private PreviewEnvironment _environment;
        [SerializeField] private int _roomIndex;
        private WorldColorPreviewStage _stage;
        private string[] _rooms, _roomLabels;
        private bool _overview, _showActors = true;
        private readonly Dictionary<WorldColorProfileSO, WorldColorProfileSO> _drafts = new Dictionary<WorldColorProfileSO, WorldColorProfileSO>();
        private AtmosphereController _controller;
        private WorldColorProfileSO _source, _working;
        private SerializedObject _settings;
        private RenderTexture _previewTexture;
        private double _lastRepaint;
        private bool _preview = true, _neutral;
        private Vector2 _scroll;

        [MenuItem("Tools/Relic Keeper/Visuals/Color Profiles")]
        public static void Open() => GetWindow<WorldColorProfilesWindow>("World Color Profiles");
        public static void ShowMortfall() => GetWindow<WorldColorProfilesWindow>("World Color Profiles").SetPreviewEnvironment(PreviewEnvironment.Mortfall);
        private AtmosphereController PreviewController => _environment == PreviewEnvironment.CurrentGame ? _controller : _stage?.Controller;
        internal Camera ActivePreviewCamera => PreviewController != null ? PreviewController.GetComponent<Camera>() : null;

        private void OnEnable()
        {
            minSize = new Vector2(420, 500);
            EditorApplication.update += Tick;
            EditorApplication.delayCall += ConnectMainCamera;
        }

        private void ConnectMainCamera()
        {
            // Restoring a draft is safe even while scene/material rebinding is suspended.
            if (_working == null && _source != null) Load(_source);
            if (WorldColorGrading.BindingsSuspended) return;
            if (_controller == null && Camera.main != null)
                _controller = Camera.main.GetComponent<AtmosphereController>();
            if (_environment != PreviewEnvironment.CurrentGame)
            {
                EnsureStage();
                if (_working == null) Load(_source != null ? _source : _stage?.AssignedProfile);
                return;
            }
            if (_working == null && _source != null) { Load(_source); return; }
            if (_controller == null) return;
            WorldColorGrading.Register(_controller);
            if (_working == null) Load(_controller.ActiveColorProfile);
            else ApplyPreview(true);
        }

        private void Tick()
        {
            if (WorldColorGrading.BindingsSuspended) return;
            if (EditorApplication.timeSinceStartup - _lastRepaint < 1.0 / 12.0) return;
            _lastRepaint = EditorApplication.timeSinceStartup;
            if (_controller == null || _working == null || (_environment != PreviewEnvironment.CurrentGame && _stage == null)) ConnectMainCamera();
            if (PreviewController != null) Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Move a slider and watch the world preview. 100% means unchanged. Click Save Profile to keep your changes; closing this window discards unsaved preview edits.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var environment = (PreviewEnvironment)EditorGUILayout.Popup("Preview environment", (int)_environment,
                new[] { "Current game camera", "Hub (sample view)", "Mortfall (sample view)" });
            if (EditorGUI.EndChangeCheck()) SetPreviewEnvironment(environment);
            if (_environment == PreviewEnvironment.Mortfall)
            {
                ReadRooms();
                EditorGUI.BeginChangeCheck();
                int previousRoom = _roomIndex;
                using (new EditorGUILayout.HorizontalScope())
                {
                    _roomIndex = EditorGUILayout.Popup("Mortfall room", Mathf.Clamp(_roomIndex, 0, Mathf.Max(0, _rooms.Length - 1)), _roomLabels);
                    using (new EditorGUI.DisabledScope(_roomIndex <= 0))
                        if (GUILayout.Button("Previous", GUILayout.Width(75))) _roomIndex--;
                    using (new EditorGUI.DisabledScope(_roomIndex >= _rooms.Length - 1))
                        if (GUILayout.Button("Next", GUILayout.Width(60))) _roomIndex++;
                }
                if (EditorGUI.EndChangeCheck() || previousRoom != _roomIndex)
                { ClearPreview(); _stage?.Dispose(); _stage = null; EnsureStage(); ApplyPreview(true); }
            }
            if (_environment != PreviewEnvironment.CurrentGame)
            {
                EditorGUI.BeginChangeCheck();
                _overview = EditorGUILayout.Toggle("Show whole room", _overview);
                _showActors = EditorGUILayout.Toggle("Show character samples", _showActors);
                if (EditorGUI.EndChangeCheck()) _stage?.SetView(_overview, _showActors);
            }
            else
            {
            EditorGUI.BeginChangeCheck();
            var controller = (AtmosphereController)EditorGUILayout.ObjectField("Camera controller", _controller, typeof(AtmosphereController), true);
            if (EditorGUI.EndChangeCheck())
            {
                ClearPreview(); _controller = controller;
                if (_controller != null) WorldColorGrading.Register(_controller);
            }
            if (_controller == null && GUILayout.Button("Use main camera"))
            {
                _controller = Camera.main != null ? Camera.main.GetComponent<AtmosphereController>() : null;
                if (_controller != null) Load(_controller.ActiveColorProfile);
            }
            }
            EditorGUI.BeginChangeCheck();
            var source = (WorldColorProfileSO)EditorGUILayout.ObjectField("Profile to edit", _source, typeof(WorldColorProfileSO), false);
            if (EditorGUI.EndChangeCheck()) Load(source);
            if (_controller != null && _source == null && GUILayout.Button("Load current environment profile")) Load(_controller.ActiveColorProfile);
            _preview = EditorGUILayout.Toggle("Live preview", _preview);
            _neutral = EditorGUILayout.Toggle("Show original (before)", _neutral);
            if (_neutral) EditorGUILayout.HelpBox("Showing original colors. Turn off 'Show original' to see your adjustments.", MessageType.Info);
            bool wide = position.width >= 860;
            if (wide) { EditorGUILayout.BeginHorizontal(); EditorGUILayout.BeginVertical(GUILayout.Width(390)); }
            else DrawPreview(false);
            if (_working != null)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                _settings.Update();
                DrawGroup("characters", "Characters", "Player, enemies and weapons.");
                DrawGroup("environment", "Environment", "Ground, platforms, walls, buildings and nearby decoration.");
                DrawGroup("background", "Background", "Sky, mountains, distant forest and dungeon backdrops.");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Combat effects", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("0% keeps their original colors. Higher values apply more of the profile.", EditorStyles.wordWrappedMiniLabel);
                    DrawPercent(_settings.FindProperty("effectsInfluence"), "Color influence (%)", 0, 100);
                }
                if (_settings.ApplyModifiedProperties()) ApplyPreview(true);
                EditorGUILayout.LabelField("100% = unchanged. White tint = original colors.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset all to neutral")) { Undo.RecordObject(_working, "Reset world colors"); _working.ResetToNeutral(); ApplyPreview(true); }
                    if (GUILayout.Button("Discard edits / Reload")) ReloadSaved();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save Profile") && _source != null)
                    {
                        Undo.RecordObject(_source, "Save world color profile");
                        EditorUtility.CopySerialized(_working, _source);
                        _source.hideFlags = HideFlags.None;
                        EditorUtility.SetDirty(_source); AssetDatabase.SaveAssetIfDirty(_source);
                        ShowNotification(new GUIContent("Profile saved"));
                    }
                    if (GUILayout.Button("Save As…")) SaveAs();
                }
            }
            else EditorGUILayout.HelpBox("Choose HubDay, Mortfall or Neutral in 'Profile to edit' above.", MessageType.Info);
            if (wide) { EditorGUILayout.EndVertical(); EditorGUILayout.BeginVertical(); DrawPreview(true); EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal(); }
            if (PreviewController != null && GUILayout.Button("Refresh scene groups")) PreviewController.RefreshColorGroups();
            EditorGUILayout.LabelField("Shared renderer bindings", WorldColorGrading.BoundRendererCount.ToString());
            if (WorldColorGrading.UnsupportedRendererCount > 0)
                EditorGUILayout.HelpBox("Some custom materials are left unchanged. See the color-profile guide for supported shaders and explicit group overrides.", MessageType.Warning);
            ApplyPreview();
        }

        private void Load(WorldColorProfileSO source)
        {
            ClearPreview();
            _settings?.Dispose(); _settings = null;
            _source = source;
            if (source == null) _working = null;
            else if (!_drafts.TryGetValue(source, out _working) || _working == null)
            { _working = CreateEditablePreview(source); _drafts[source] = _working; }
            if (_working != null) _settings = new SerializedObject(_working);
            if (PreviewController != null) WorldColorGrading.Register(PreviewController);
            ApplyPreview();
        }

        private void ReloadSaved()
        {
            ClearPreview(); _settings?.Dispose(); _settings = null;
            if (_source != null && _drafts.TryGetValue(_source, out var draft))
            { _drafts.Remove(_source); if (draft != null) DestroyImmediate(draft); }
            _working = null; Load(_source);
        }

        internal void SetPreviewEnvironment(PreviewEnvironment environment)
        {
            ClearPreview(); _stage?.Dispose(); _stage = null;
            _environment = environment;
            if (_controller == null && Camera.main != null) _controller = Camera.main.GetComponent<AtmosphereController>();
            if (_environment == PreviewEnvironment.CurrentGame)
            {
                ConnectMainCamera(); ClearPreview();
                if (_controller != null) Load(_controller.ActiveColorProfile);
            }
            else { EnsureStage(); if (_stage != null) Load(_stage.AssignedProfile); }
            ApplyPreview(true); Repaint();
        }

        private void ReadRooms()
        {
            if (_rooms != null) return;
            _rooms = WorldColorPreviewStage.GetMortfallRooms();
            _roomLabels = System.Array.ConvertAll(_rooms, x => Path.GetFileNameWithoutExtension(x).Replace("MortfallRoom_", "Room "));
        }

        private void EnsureStage()
        {
            if (_stage != null || WorldColorGrading.BindingsSuspended) return;
            ReadRooms();
            if (_environment == PreviewEnvironment.Mortfall && _rooms.Length == 0) return;
            _roomIndex = Mathf.Clamp(_roomIndex, 0, Mathf.Max(0, _rooms.Length - 1));
            _stage = WorldColorPreviewStage.Create(_environment == PreviewEnvironment.Mortfall,
                _rooms.Length > 0 ? _rooms[_roomIndex] : null, _controller);
            _stage.SetView(_overview, _showActors);
        }

        internal static WorldColorProfileSO CreateEditablePreview(WorldColorProfileSO source)
        {
            if (source == null) return null;
            var working = Instantiate(source);
            working.name = source.name;
            // HideAndDontSave also includes NotEditable, which greys out inspector controls.
            working.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            return working;
        }

        private void DrawGroup(string field, string title, string description)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                SerializedProperty group = _settings.FindProperty(field);
                SerializedProperty exposure = group.FindPropertyRelative("exposure");
                EditorGUI.BeginChangeCheck();
                float brightness = EditorGUILayout.Slider(new GUIContent("Brightness (%)", "100 = original, 50 = half as bright, 200 = twice as bright."), Mathf.Pow(2f, exposure.floatValue) * 100f, 25f, 400f);
                if (EditorGUI.EndChangeCheck()) exposure.floatValue = Mathf.Log(brightness / 100f, 2f);
                DrawPercent(group.FindPropertyRelative("contrast"), "Contrast (%)", 0, 200);
                DrawPercent(group.FindPropertyRelative("saturation"), "Saturation (%)", 0, 200);
                EditorGUILayout.PropertyField(group.FindPropertyRelative("tint"), new GUIContent("Color tint"));
            }
        }

        private static void DrawPercent(SerializedProperty property, string label, float min, float max)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(label, property.floatValue * 100f, min, max);
            if (EditorGUI.EndChangeCheck()) property.floatValue = value / 100f;
        }

        private void DrawPreview(bool expand)
        {
            EditorGUILayout.LabelField(_neutral ? "Original colors — before" : _environment + " preview — your adjustments", EditorStyles.boldLabel);
            Rect area = expand
                ? GUILayoutUtility.GetRect(480, 270, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))
                : GUILayoutUtility.GetAspectRect(480f / 270f);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, new Color(0.075f, 0.085f, 0.095f));
                if (PreviewController != null && !WorldColorGrading.BindingsSuspended && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    if (_previewTexture == null)
                    {
                        _previewTexture = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32)
                            { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                        _previewTexture.Create();
                    }
                    RenderPipeline.SubmitRenderRequest(PreviewController.GetComponent<Camera>(), new RenderPipeline.StandardRequest { destination = _previewTexture });
                    float zoom = Mathf.Min(area.width / 480f, area.height / 270f);
                    if (zoom >= 1f) zoom = Mathf.Floor(zoom);
                    var image = new Rect(area.center.x - 240f * zoom, area.center.y - 135f * zoom, 480f * zoom, 270f * zoom);
                    GUI.DrawTexture(image, _previewTexture, ScaleMode.ScaleToFit, false);
                }
                else GUI.Label(area, "Choose Hub or Mortfall above, or open HubScene for the game camera.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.LabelField(_environment == PreviewEnvironment.CurrentGame
                ? "480 × 270 world view. HUD is hidden here; existing atmosphere and fog remain visible."
                : "Static color samples with flat lighting. Use Current game camera to check live lighting and effects.", EditorStyles.wordWrappedMiniLabel);
        }

        private void SaveAs()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save world color profile", _source.name + " Copy", "asset", "Choose the profile's location.", "Assets/Visuals/ColorProfiles");
            if (string.IsNullOrEmpty(path)) return;
            var saved = Instantiate(_working); saved.name = Path.GetFileNameWithoutExtension(path); saved.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(saved, path); AssetDatabase.SaveAssetIfDirty(saved);
            Load(saved); EditorGUIUtility.PingObject(saved);
        }

        private void ApplyPreview(bool force = false)
        {
            var controller = PreviewController;
            if (controller == null || WorldColorGrading.BindingsSuspended) return;
            var desired = _preview ? _working : null;
            bool neutral = _preview && _neutral;
            bool changed = controller.PreviewColorProfile != desired || controller.PreviewNeutralColors != neutral || force;
            controller.PreviewColorProfile = desired;
            controller.PreviewNeutralColors = neutral;
            if (changed) { SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate(); }
        }

        private void ClearPreview()
        {
            if (_controller != null) { _controller.PreviewColorProfile = null; _controller.PreviewNeutralColors = false; }
            if (_stage?.Controller != null) { _stage.Controller.PreviewColorProfile = null; _stage.Controller.PreviewNeutralColors = false; }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.delayCall -= ConnectMainCamera;
            ClearPreview();
            _settings?.Dispose(); _settings = null;
            _stage?.Dispose(); _stage = null;
            foreach (var draft in _drafts.Values) if (draft != null) DestroyImmediate(draft);
            _drafts.Clear(); _working = null;
            if (_previewTexture != null) { _previewTexture.Release(); DestroyImmediate(_previewTexture); }
        }
    }
}
