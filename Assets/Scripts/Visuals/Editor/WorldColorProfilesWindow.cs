using System.IO;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        [SerializeField] private int _appearanceTab = 1;
        [SerializeField] private int _detailArea;
        [SerializeField] private int _dripSpot;
        private WorldColorPreviewStage _stage;
        private string[] _rooms, _roomLabels;
        private bool _overview, _showActors = true;
        private readonly Dictionary<WorldColorProfileSO, WorldColorProfileSO> _drafts = new Dictionary<WorldColorProfileSO, WorldColorProfileSO>();
        private readonly Dictionary<AtmosphereProfileSO, AtmosphereProfileSO> _mistDrafts = new Dictionary<AtmosphereProfileSO, AtmosphereProfileSO>();
        private AtmosphereController _controller;
        private WorldColorProfileSO _source, _working;
        private SerializedObject _settings;
        private AtmosphereProfileSO _mistSource, _mistWorking;
        private SerializedObject _mistSettings;
        private RenderTexture _previewTexture;
        private double _lastRepaint;
        private bool _showSaved;
        private Vector2 _scroll;
        private bool _animatePreview;
        private float _previewTravel;
        private readonly CeilingBeamEditing _beamEdits = new CeilingBeamEditing();
        private int _beamIndex;
        private string _beamSaveError;
        internal AtmosphereProfileSO MortfallBeamProfile => _environment == PreviewEnvironment.Mortfall ? (_showSaved ? _mistSource : _mistWorking) : null;
        private bool HasRoomPreview => _environment == PreviewEnvironment.Mortfall && _stage != null && _rooms != null && _rooms.Length > 0;

        [MenuItem("Tools/Relic Keeper/Visuals/World Appearance")]
        public static void Open() => GetWindow<WorldColorProfilesWindow>("World Appearance");
        public static void ShowMortfall()
        {
            var window = GetWindow<WorldColorProfilesWindow>("World Appearance");
            window.SetPreviewEnvironment(PreviewEnvironment.Mortfall);
            window._appearanceTab = 1;
            var editor = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(editor.x + 20, editor.y + 40, Mathf.Max(860, editor.width - 40), Mathf.Max(520, editor.height - 80));
            window.Show(); window.Focus();
        }
        private AtmosphereController PreviewController => _environment == PreviewEnvironment.CurrentGame ? _controller : _stage?.Controller;
        internal Camera ActivePreviewCamera => PreviewController != null ? PreviewController.GetComponent<Camera>() : null;

        private void OnEnable()
        {
            minSize = new Vector2(420, 500);
            EditorApplication.update += Tick;
            EditorApplication.delayCall += ConnectMainCamera;
            PrefabStage.prefabSaved += RoomSaved;
        }

        private void RoomSaved(GameObject room)
        {
            if (_environment != PreviewEnvironment.Mortfall || _rooms == null || _rooms.Length == 0) return;
            if (PrefabStageUtility.GetPrefabStage(room)?.assetPath != _rooms[_roomIndex]) return;
            EditorApplication.delayCall -= RefreshPlacedLights;
            EditorApplication.delayCall += RefreshPlacedLights;
        }
        private void RefreshPlacedLights()
        {
            if (this == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (HasRoomPreview) _beamEdits.ForgetIfClean(_rooms[_roomIndex]);
            SetPreviewEnvironment(_environment);
        }

        private void ConnectMainCamera()
        {
            // Restoring a draft is safe even while scene/material rebinding is suspended.
            if (_working == null && _source != null) Load(_source);
            if (_mistWorking == null && _mistSource != null) LoadMist(_mistSource);
            if (WorldColorGrading.BindingsSuspended) return;
            if (_controller == null && Camera.main != null)
                _controller = Camera.main.GetComponent<AtmosphereController>();
            if (_environment != PreviewEnvironment.CurrentGame)
            {
                EnsureStage();
                if (_working == null) Load(_source != null ? _source : _stage?.AssignedProfile);
                LoadMist(_stage?.Controller.AssignedDepthMistProfile);
                ApplyPreview();
                return;
            }
            if (_working == null && _source != null) { Load(_source); return; }
            if (_controller == null) return;
            WorldColorGrading.Register(_controller);
            if (_working == null) Load(_controller.AssignedColorProfile);
            else ApplyPreview(true);
            LoadMist(_controller.AssignedDepthMistProfile);
        }

        private void Tick()
        {
            if (WorldColorGrading.BindingsSuspended) return;
            if (EditorApplication.timeSinceStartup - _lastRepaint < 1.0 / 12.0) return;
            _lastRepaint = EditorApplication.timeSinceStartup;
            if (_controller == null || _working == null || (_environment != PreviewEnvironment.CurrentGame && _stage == null)) ConnectMainCamera();
            if (_environment == PreviewEnvironment.CurrentGame && _controller != null)
            {
                if (_source != _controller.AssignedColorProfile) Load(_controller.AssignedColorProfile);
                LoadMist(_controller.AssignedDepthMistProfile);
            }
            if (PreviewController != null) Repaint();
            if (_stage != null) _stage.SetCameraOffset(_animatePreview ? Mathf.Sin((float)EditorApplication.timeSinceStartup * 0.5f) * 8f : _previewTravel);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Choose a location, adjust its appearance, then press Save. Changes appear in the preview.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var environment = (PreviewEnvironment)EditorGUILayout.Popup("Location", (int)_environment,
                new[] { "Current game", "Hub", "Mortfall" });
            if (EditorGUI.EndChangeCheck()) SetPreviewEnvironment(environment);
            if (_environment == PreviewEnvironment.Mortfall)
            {
                ReadRooms();
                EditorGUI.BeginChangeCheck();
                int previousRoom = _roomIndex;
                using (new EditorGUILayout.HorizontalScope())
                {
                    _roomIndex = EditorGUILayout.Popup("Room", Mathf.Clamp(_roomIndex, 0, Mathf.Max(0, _rooms.Length - 1)), _roomLabels);
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
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _overview = EditorGUILayout.ToggleLeft("Whole room", _overview, GUILayout.Width(120));
                    _showActors = EditorGUILayout.ToggleLeft("Characters", _showActors, GUILayout.Width(120));
                    if (EditorGUI.EndChangeCheck()) _stage?.SetView(_overview, _showActors);
                }
            }
            _showSaved = GUILayout.Toggle(_showSaved, "Compare with saved", EditorStyles.miniButton);
            bool wide = position.width >= 860;
            if (wide) { EditorGUILayout.BeginHorizontal(); EditorGUILayout.BeginVertical(GUILayout.Width(390)); }
            else DrawPreview(false);
            int previousTab = _appearanceTab;
            _appearanceTab = GUILayout.Toolbar(_appearanceTab, new[] { "Colors", "Mist", "Movement", "Lighting", "Details", "Beams" }, GUILayout.Height(27));
            if (_stage != null && previousTab != _appearanceTab)
            {
                _stage.SetView(_overview, _showActors);
                if (_appearanceTab == 3) _stage.FocusLighting();
                if (_appearanceTab == 4) FocusDetails();
                if (_appearanceTab == 5) _stage.FocusShaft(_beamIndex);
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_showSaved) EditorGUILayout.HelpBox("Showing the saved version. Turn off Compare with saved to see your edits.", MessageType.Info);
            if (_appearanceTab == 0)
            {
                if (_working != null)
                {
                    _settings.Update();
                    DrawGroup("characters", "Characters", "Player, enemies and weapons.");
                    DrawGroup("environment", "Environment", "Ground, platforms, buildings and nearby decoration.");
                    DrawGroup("background", "Background", "Sky, mountains and distant scenery.");
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Combat effects", EditorStyles.boldLabel);
                        DrawPercent(_settings.FindProperty("effectsInfluence"), "Color influence (%)", 0, 100);
                    }
                    if (_settings.ApplyModifiedProperties()) ApplyPreview(true);
                    EditorGUILayout.LabelField("100% keeps the original brightness, contrast and saturation.", EditorStyles.wordWrappedMiniLabel);
                }
                else EditorGUILayout.HelpBox("Choose Hub or Mortfall to edit its appearance.", MessageType.Info);
            }
            else if (_appearanceTab == 1) DrawMist();
            else if (_appearanceTab == 2) DrawMovement();
            else if (_appearanceTab == 3) DrawLighting();
            else if (_appearanceTab == 4) DrawAmbient();
            else DrawCeilingBeams();
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_working == null && _mistWorking == null))
                {
                    if (GUILayout.Button("Save", GUILayout.Height(30))) SaveAppearance();
                    if (GUILayout.Button("Discard changes", GUILayout.Height(30))) DiscardAppearance();
                }
            }
            if (wide) { EditorGUILayout.EndVertical(); EditorGUILayout.BeginVertical(); DrawPreview(true); EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal(); }
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
            LoadMist(PreviewController != null ? PreviewController.AssignedDepthMistProfile : _mistSource);
            if (PreviewController != null) WorldColorGrading.Register(PreviewController);
            ApplyPreview();
        }

        private void LoadMist(AtmosphereProfileSO source)
        {
            if (_mistSource == source && (_mistWorking != null || source == null)) return;
            _mistSettings?.Dispose(); _mistSettings = null;
            _mistSource = source;
            if (source == null) _mistWorking = null;
            else if (!_mistDrafts.TryGetValue(source, out _mistWorking) || _mistWorking == null)
            {
                _mistWorking = Instantiate(source); _mistWorking.name = source.name;
                _mistWorking.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
                _mistDrafts[source] = _mistWorking;
            }
            if (_mistWorking != null) _mistSettings = new SerializedObject(_mistWorking);
            ApplyPreview(true);
        }

        internal void SaveAppearance()
        {
            if (HasRoomPreview)
            {
                try { _beamEdits.Save(_rooms[_roomIndex]); _beamSaveError = null; }
                catch (System.InvalidOperationException exception) { _beamSaveError = exception.Message; Repaint(); return; }
            }
            if (_source != null && _working != null)
            {
                Undo.RecordObject(_source, "Save world appearance");
                EditorUtility.CopySerialized(_working, _source); _source.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(_source); AssetDatabase.SaveAssetIfDirty(_source);
            }
            if (_mistSource != null && _mistWorking != null)
            {
                Undo.RecordObject(_mistSource, "Save world appearance");
                EditorUtility.CopySerialized(_mistWorking, _mistSource); _mistSource.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(_mistSource); AssetDatabase.SaveAssetIfDirty(_mistSource);
            }
            _showSaved = false; ApplyPreview(true);
            if (hasFocus) ShowNotification(new GUIContent("Appearance saved"));
        }

        internal void DiscardAppearance()
        {
            _beamSaveError = null;
            if (HasRoomPreview)
            {
                _beamEdits.Discard(_rooms[_roomIndex]); ClearPreview(); _stage.Dispose(); _stage = null; EnsureStage();
            }
            ClearPreview(); _settings?.Dispose(); _settings = null;
            _mistSettings?.Dispose(); _mistSettings = null;
            if (_source != null && _drafts.TryGetValue(_source, out var draft))
            { _drafts.Remove(_source); if (draft != null) DestroyImmediate(draft); }
            if (_mistSource != null && _mistDrafts.TryGetValue(_mistSource, out var mistDraft))
            { _mistDrafts.Remove(_mistSource); if (mistDraft != null) DestroyImmediate(mistDraft); }
            _working = null; _mistWorking = null; _showSaved = false; Load(_source);
        }

        internal void SetPreviewEnvironment(PreviewEnvironment environment)
        {
            ClearPreview(); _stage?.Dispose(); _stage = null;
            _environment = environment;
            if (_controller == null && Camera.main != null) _controller = Camera.main.GetComponent<AtmosphereController>();
            if (_environment == PreviewEnvironment.CurrentGame)
            {
                ConnectMainCamera(); ClearPreview();
                if (_controller != null) Load(_controller.AssignedColorProfile);
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
            if (_appearanceTab == 3) _stage.FocusLighting();
            if (_appearanceTab == 4) FocusDetails();
            if (_appearanceTab == 5) _stage.FocusShaft(_beamIndex);
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

        private void DrawMist()
        {
            if (_mistWorking == null)
            {
                EditorGUILayout.HelpBox("Choose Hub or Mortfall to edit its mist.", MessageType.Info);
                return;
            }
            _mistSettings.Update();
            EditorGUILayout.LabelField("Background mist", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mist softens the distant scenery. Set Amount to 0% to turn it off.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);
            DrawPercent(_mistSettings.FindProperty("backgroundMist"), "Amount (%)", 0, 100);
            var tint = _mistSettings.FindProperty("backgroundTint");
            EditorGUI.BeginChangeCheck();
            Color color = EditorGUILayout.ColorField(new GUIContent("Mist color"), tint.colorValue, true, false, false);
            if (EditorGUI.EndChangeCheck()) tint.colorValue = color;
            DrawPercent(_mistSettings.FindProperty("wispStrength"), "Wisps / clear gaps (%)", 0, 100);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("limitDepthMistHeight"), new GUIContent("Limit height"));
            if (_mistSettings.FindProperty("limitDepthMistHeight").boolValue)
            {
                EditorGUILayout.LabelField("Only near a fixed height above the room entrance.", EditorStyles.wordWrappedMiniLabel);
                DrawMistSlider("depthMistCenterY", "Vertical position", -10f, 25f);
                DrawMistSlider("depthMistHeight", "Thickness", 0.5f, 30f);
                DrawMistSlider("depthMistFeather", "Soft edges", 0.1f, 15f);
            }
            else EditorGUILayout.LabelField("Mist covers the whole view, including lower floors.", EditorStyles.wordWrappedMiniLabel);
            var scale = _mistSettings.FindProperty("noiseScale");
            EditorGUI.BeginChangeCheck();
            float size = EditorGUILayout.Slider("Size of wisps", 1f / Mathf.Max(0.001f, scale.floatValue), 0.5f, 20f);
            if (EditorGUI.EndChangeCheck()) scale.floatValue = 1f / size;
            var drift = _mistSettings.FindProperty("driftSpeed");
            EditorGUI.BeginChangeCheck();
            float speed = EditorGUILayout.Slider("Drift speed (%)", drift.vector2Value.magnitude * 1000f, 0f, 100f);
            if (EditorGUI.EndChangeCheck())
            {
                Vector2 direction = drift.vector2Value.sqrMagnitude > 0.000001f ? drift.vector2Value.normalized :
                    _mistSource.driftSpeed.sqrMagnitude > 0.000001f ? _mistSource.driftSpeed.normalized : Vector2.right;
                drift.vector2Value = direction * (speed / 1000f);
            }
            if (_mistSettings.ApplyModifiedProperties()) ApplyPreview(true);
        }

        private void DrawMistSlider(string field, string label, float min, float max)
        {
            var property = _mistSettings.FindProperty(field);
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(label, property.floatValue, min, max);
            if (EditorGUI.EndChangeCheck()) property.floatValue = value;
        }

        private void DrawMovement()
        {
            if (_mistWorking == null) { EditorGUILayout.HelpBox("Choose Hub or Mortfall to edit background movement.", MessageType.Info); return; }
            _mistSettings.Update();
            EditorGUILayout.LabelField("Background depth", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Higher values make scenery look farther away. Ground and buildings stay fixed.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("layeredParallax"), new GUIContent("Background movement"));
            DrawPercent(_mistSettings.FindProperty("parallaxFar"), "Distant scenery (%)", 0, 100);
            DrawPercent(_mistSettings.FindProperty("parallaxMiddle"), "Middle scenery (%)", 0, 100);
            DrawPercent(_mistSettings.FindProperty("parallaxNear"), "Nearby scenery (%)", 0, 100);
            DrawPercent(_mistSettings.FindProperty("parallaxVertical"), "Vertical movement (%)", 0, 100);
            if (_environment == PreviewEnvironment.Mortfall)
            {
                var columns = _mistSettings.FindProperty("parallaxSilhouettes");
                EditorGUI.BeginChangeCheck();
                bool visible = EditorGUILayout.Toggle("Distant columns", columns.floatValue > 0f);
                if (EditorGUI.EndChangeCheck()) columns.floatValue = visible ? 1f : 0f;
            }
            if (_mistSettings.ApplyModifiedProperties()) ApplyPreview(true);
            if (_stage != null)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("Try it in the preview", EditorStyles.boldLabel);
                _animatePreview = EditorGUILayout.Toggle("Move preview", _animatePreview);
                using (new EditorGUI.DisabledScope(_animatePreview))
                    _previewTravel = EditorGUILayout.Slider("Look left / right", _previewTravel, -15f, 15f);
                EditorGUILayout.LabelField("Preview movement does not move your character.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawAmbient()
        {
            if (_mistWorking == null) { EditorGUILayout.HelpBox("Choose Hub or Mortfall to edit small animations.", MessageType.Info); return; }
            _mistSettings.Update();
            if (_stage != null)
            {
                var areas = _environment == PreviewEnvironment.Mortfall ? new[] { "Ceiling drops", "Torch dust", "Ground movement", "Ceiling light beam" } : new[] { "Chimney smoke", "Forge embers", "Tree leaves", "Ground movement" };
                EditorGUI.BeginChangeCheck();
                _detailArea = EditorGUILayout.Popup("Preview area", Mathf.Clamp(_detailArea, 0, areas.Length - 1), areas);
                if (EditorGUI.EndChangeCheck()) { _previewTravel = 0; FocusDetails(); }
                if (_environment == PreviewEnvironment.Mortfall && _detailArea == 0 && _stage.AmbientSourceCount > 0)
                {
                    var spots = new string[_stage.AmbientSourceCount];
                    for (int i = 0; i < spots.Length; i++) spots[i] = "Spot " + (i + 1);
                    EditorGUI.BeginChangeCheck();
                    _dripSpot = EditorGUILayout.Popup("Drop location", Mathf.Clamp(_dripSpot, 0, spots.Length - 1), spots);
                    if (EditorGUI.EndChangeCheck()) { _previewTravel = 0; FocusDetails(); }
                }
            }
            if (_environment == PreviewEnvironment.Mortfall && _detailArea == 3)
            {
                DrawCeilingBeams();
                return;
            }
            EditorGUILayout.LabelField("Small environmental animations", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("ambientAnimations"), new GUIContent("Small animations"));
            DrawPercent(_mistSettings.FindProperty("chimneySmokeOpacity"), "Chimney smoke (%)", 0, 100);
            DrawPercent(_mistSettings.FindProperty("dripFrequency"), "Water drops (%)", 0, 200);
            EditorGUILayout.LabelField(_environment == PreviewEnvironment.Mortfall
                ? "Choose a room and drop location above. Drops can enter the view from higher ceilings. 0% turns drops off."
                : "The chimney smoke stays attached to the tavern. 0% hides it.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Air particles", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("ambientParticles"), new GUIContent("Enable particles"));
            DrawPercent(_mistSettings.FindProperty("ambientParticleDensity"), "Density (%)", 0, 100);
            DrawPercent(_mistSettings.FindProperty("ambientParticleOpacity"), "Visibility (%)", 0, 100);
            EditorGUILayout.LabelField(_environment == PreviewEnvironment.Mortfall
                ? "Faint dust near the first torch in Room 001. Turning its light off also hides the dust."
                : "A few embers rise from the forge. Leaves fall occasionally beneath one tree.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Movement on ground", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("surfaceMovementEffects"), new GUIContent("Enable ground effects"));
            DrawPercent(_mistSettings.FindProperty("surfaceRunDust"), "Running dust (%)", 0, 100);
            DrawPercent(_mistSettings.FindProperty("surfaceEffectOpacity"), "Visibility (%)", 0, 100);
            DrawPercent(_mistSettings.FindProperty("surfaceLandingStrength"), "Landing strength (%)", 0, 200);
            EditorGUILayout.LabelField("Choose Ground movement above to preview a footstep and landing loop. 0% running dust keeps landings enabled.", EditorStyles.wordWrappedMiniLabel);
            if (_mistSettings.ApplyModifiedProperties()) ApplyPreview(true);
        }

        private void FocusDetails()
        {
            if (_stage == null) return;
            _detailArea = Mathf.Clamp(_detailArea, 0, 3);
            if (_environment == PreviewEnvironment.Mortfall && _detailArea == 3)
            { _overview = false; if (!_stage.FocusShaft()) _stage.FocusLighting(); return; }
            if (_detailArea == (_environment == PreviewEnvironment.Mortfall ? 2 : 3)) { _overview = false; _stage.FocusGroundMovement(); return; }
            if (_detailArea == 0) _stage.FocusAmbient(_environment == PreviewEnvironment.Mortfall ? _dripSpot : 0);
            else _stage.FocusParticles(_environment == PreviewEnvironment.Mortfall ? AmbientParticleKind.TorchDust :
                _detailArea == 1 ? AmbientParticleKind.ForgeEmbers : AmbientParticleKind.DriftingLeaves);
        }

        private void DrawLighting()
        {
            if (_mistWorking == null) { EditorGUILayout.HelpBox("Choose Hub or Mortfall to edit lighting.", MessageType.Info); return; }
            _mistSettings.Update();
            EditorGUILayout.LabelField("Local lights", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Warm light from the forge and torches. Nearby stone and characters share the same light.", EditorStyles.wordWrappedMiniLabel);
            var enabled = _mistSettings.FindProperty("localLights");
            enabled.boolValue = EditorGUILayout.Toggle("Enable lights", enabled.boolValue);
            DrawPercent(_mistSettings.FindProperty("localLightStrength"), "Strength (%)", 0, 300);
            DrawPercent(_mistSettings.FindProperty("localLightRadius"), "Reach (%)", 50, 200);
            DrawPercent(_mistSettings.FindProperty("stoneReliefStrength"), "Stone relief (%)", 0, 100);
            EditorGUILayout.HelpBox("Stone relief affects the marked wall and platform near the entrance torch in Mortfall Room 001. Zero restores flat lighting.", MessageType.None);
            var tint = _mistSettings.FindProperty("localLightTint");
            tint.colorValue = EditorGUILayout.ColorField(new GUIContent("Light color"), tint.colorValue, true, false, false);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Player light shadow", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("A faint moving shadow from nearby torches. The contact shadow under the feet stays unchanged.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("playerLightShadow"), new GUIContent("Enable player shadow"));
            DrawPercent(_mistSettings.FindProperty("playerLightShadowStrength"), "Light blocked (%)", 0, 100);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("playerLightShadowReach"), new GUIContent("Shadow reach", "World units from the player's body; the shadow fades out before this distance."));
            DrawPercent(_mistSettings.FindProperty("playerLightShadowWidth"), "Shadow width (%)", 10, 400);
            DrawPercent(_mistSettings.FindProperty("playerLightShadowHeight"), "Shadow height (%)", 10, 300);
            DrawPercent(_mistSettings.FindProperty("playerLightShadowSoftness"), "Edge softness (%)", 0, 200);
            EditorGUILayout.LabelField("Try 70–100% blocking and 100–150% width for a clearer shadow. Reach only affects areas inside the source light. Ambient brightness is preserved, even at 100% blocking.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Ground projection", EditorStyles.boldLabel);
            DrawPercent(_mistSettings.FindProperty("playerGroundShadowOpacity"), "Ground light blocked (%)", 0, 100);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("playerGroundShadowLength"), new GUIContent("Maximum ground length", "The shadow extends away from the source, based on its height. This caps the length."));
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("playerGroundShadowThickness"), new GUIContent("Ground shadow thickness"));
            DrawPercent(_mistSettings.FindProperty("playerGroundShadowDistanceResponse"), "Light height response (%)", 0, 200);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("playerLightCrossingFade"), new GUIContent("Crossing fade distance", "Distance from the flame where shadows fade out before changing side."));
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("playerGroundShadowGrowthDistance"), new GUIContent("Length growth softness", "Larger values make the shadow extend more gradually. It always starts at the feet and collapses when crossing the flame."));
            DrawPercent(_mistSettings.FindProperty("playerLightOverlapDarkness"), "Overlap darkness (%)", 0, 60);
            EditorGUILayout.LabelField("This soft floor shadow extends away from a nearby light and fades in the air. Set its opacity to zero to use light blocking alone.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Soft room fills", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("A gentle pool of light on stone and characters, without a visible cone. Each placed fill has its own color, size and softness.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("roomFillLights"), new GUIContent("Enable room fills"));
            DrawPercent(_mistSettings.FindProperty("roomFillStrength"), "Fill strength (%)", 0, 300);
            DrawPercent(_mistSettings.FindProperty("roomFillRadius"), "Fill reach (%)", 50, 200);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Source glow", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Small halos around flames and portals. Select a source in the room to adjust its own glow; these controls affect the whole location.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("sourceGlow"), new GUIContent("Enable glow"));
            DrawPercent(_mistSettings.FindProperty("sourceGlowStrength"), "Glow strength (%)", 0, 200);
            DrawPercent(_mistSettings.FindProperty("sourceGlowRadius"), "Glow radius (%)", 25, 300);
            DrawPercent(_mistSettings.FindProperty("sourceGlowSoftness"), "Glow softness (%)", 25, 300);
            if (_mistSettings.ApplyModifiedProperties()) ApplyPreview(true);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(_environment == PreviewEnvironment.Mortfall
                ? "Placed torches light automatically. Every flame breathes with its own gentle flicker."
                : "The sample view focuses on the forge when this tab opens.", EditorStyles.wordWrappedMiniLabel);
            DrawRoomPlacement(false);
        }

        private void DrawCeilingBeams()
        {
            if (!HasRoomPreview || _mistWorking == null)
            { EditorGUILayout.HelpBox("Choose Mortfall and a room to edit its roof beams.", MessageType.Info); return; }
            _mistSettings.Update();
            EditorGUILayout.LabelField("Ceiling beams — environment", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mistSettings.FindProperty("localShafts"), new GUIContent("Show beams"));
            DrawPercent(_mistSettings.FindProperty("localShaftStrength"), "Overall brightness (%)", 0, 200);
            DrawPercent(_mistSettings.FindProperty("localShaftDust"), "Overall dust (%)", 0, 100);
            if (_mistSettings.ApplyModifiedProperties()) ApplyPreview(true);
            EditorGUILayout.Space(8);
            var drafts = _beamEdits.Get(_rooms[_roomIndex], _stage);
            if (drafts.Count != _stage.Shafts.Count)
            {
                EditorGUILayout.HelpBox("Beam placements changed in the room while preview edits were pending. Discard changes to reload this room's beams.", MessageType.Warning);
                return;
            }
            if (drafts.Count == 0)
            {
                EditorGUILayout.HelpBox("No roof beam is placed in this room. Room 001 has the example. Add a beam in the room from GameObject > Light > Relic Keeper.", MessageType.Info);
                DrawRoomPlacement(true); return;
            }
            var labels = new string[drafts.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = (i + 1) + ": " + _stage.Shafts[i].name + (drafts[i].Dirty ? " *" : "");
            EditorGUI.BeginChangeCheck();
            _beamIndex = EditorGUILayout.Popup("Placed beam", Mathf.Clamp(_beamIndex, 0, labels.Length - 1), labels);
            if (EditorGUI.EndChangeCheck()) { _previewTravel = 0; _stage.FocusShaft(_beamIndex); }
            var draft = drafts[_beamIndex]; var source = _stage.Shafts[_beamIndex];
            var value = draft.appearance;
            Vector3 anchorOffset = CeilingBeamControls.AnchorOffset(source);
            Vector3 point = draft.position + anchorOffset;
            using (new EditorGUI.DisabledScope(_showSaved))
            {
                if (CeilingBeamControls.Draw(ref value, ref point, source.transform))
                {
                    draft.appearance = value; draft.position = point - anchorOffset;
                    _beamEdits.Apply(_rooms[_roomIndex], _stage, false); _beamSaveError = null; Repaint();
                }
            }
            EditorGUILayout.HelpBox("Changes appear immediately. Save keeps this beam in the selected room and saves the environment sliders. Compare and Discard also include beam shape and placement.", MessageType.Info);
            if (!string.IsNullOrEmpty(_beamSaveError)) EditorGUILayout.HelpBox(_beamSaveError, MessageType.Warning);
            DrawRoomPlacement(true);
        }

        private void DrawRoomPlacement(bool beam)
        {
            if (_environment != PreviewEnvironment.Mortfall || _rooms == null || _rooms.Length == 0) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Level design", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(beam
                ? "You can also shape the beam in the room's Inspector and Scene view. Open the room below; add more beams from GameObject > Light > Relic Keeper. Save the room to update this preview."
                : "These sliders affect the whole environment. Edit individual light positions and shapes inside the room prefab. Add more from GameObject > Light > Relic Keeper, then save the room.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Open this room to place lights")) RoomLightingAuthoring.OpenRoom(_rooms[_roomIndex], beam);
        }

        private void DrawPreview(bool expand)
        {
            EditorGUILayout.LabelField(_showSaved ? "Saved appearance" : "Live preview", EditorStyles.boldLabel);
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
                    _stage?.SampleGroundMovement(_appearanceTab == 4 && _detailArea == (_environment == PreviewEnvironment.Mortfall ? 2 : 3),
                        (float)EditorApplication.timeSinceStartup);
                    RenderPipeline.SubmitRenderRequest(PreviewController.GetComponent<Camera>(), new RenderPipeline.StandardRequest { destination = _previewTexture });
                    float zoom = Mathf.Min(area.width / 480f, area.height / 270f);
                    if (zoom >= 1f) zoom = Mathf.Floor(zoom);
                    var image = new Rect(area.center.x - 240f * zoom, area.center.y - 135f * zoom, 480f * zoom, 270f * zoom);
                    GUI.DrawTexture(image, _previewTexture, ScaleMode.ScaleToFit, false);
                }
                else GUI.Label(area, "Choose Hub or Mortfall above, or open HubScene for the game camera.", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.LabelField(_environment == PreviewEnvironment.CurrentGame
                ? "Changes are previewed here. Press Save to keep them."
                : "Adjust the appearance on the left. Press Save to keep all changes.", EditorStyles.wordWrappedMiniLabel);
        }

        private void ApplyPreview(bool force = false)
        {
            var controller = PreviewController;
            if (controller == null || WorldColorGrading.BindingsSuspended) return;
            var desired = _showSaved ? _source : _working;
            var mist = _showSaved ? _mistSource : _mistWorking;
            bool changed = controller.PreviewColorProfile != desired || controller.PreviewNeutralColors || controller.PreviewDepthMistProfile != mist || force;
            controller.PreviewColorProfile = desired;
            controller.PreviewNeutralColors = false;
            controller.PreviewDepthMistProfile = mist;
            if (HasRoomPreview) _beamEdits.Apply(_rooms[_roomIndex], _stage, _showSaved);
            if (changed) { SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate(); }
        }

        private void ClearPreview()
        {
            if (_controller != null) { _controller.PreviewColorProfile = null; _controller.PreviewNeutralColors = false; _controller.PreviewDepthMistProfile = null; }
            if (_stage?.Controller != null) { _stage.Controller.PreviewColorProfile = null; _stage.Controller.PreviewNeutralColors = false; _stage.Controller.PreviewDepthMistProfile = null; }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.delayCall -= ConnectMainCamera;
            PrefabStage.prefabSaved -= RoomSaved;
            EditorApplication.delayCall -= RefreshPlacedLights;
            ClearPreview();
            _settings?.Dispose(); _settings = null;
            _mistSettings?.Dispose(); _mistSettings = null;
            _stage?.Dispose(); _stage = null;
            foreach (var draft in _drafts.Values) if (draft != null) DestroyImmediate(draft);
            _drafts.Clear(); _working = null;
            foreach (var draft in _mistDrafts.Values) if (draft != null) DestroyImmediate(draft);
            _mistDrafts.Clear(); _mistWorking = null;
            if (_previewTexture != null) { _previewTexture.Release(); DestroyImmediate(_previewTexture); }
        }
    }
}
