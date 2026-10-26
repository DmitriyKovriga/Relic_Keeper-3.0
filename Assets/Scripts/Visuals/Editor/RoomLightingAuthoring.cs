using System.Linq;
using System.IO;
using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class RoomLightingAuthoring
    {
        public const string CoolFillPath = "Assets/Prefabs/Visuals/CoolRoomFill.prefab";
        public const string WarmFillPath = "Assets/Prefabs/Visuals/WarmRoomFill.prefab";
        public const string SoftShaftPath = "Assets/Prefabs/Visuals/SoftCeilingShaft.prefab";
        public static readonly Color CoolTint = new Color(0.63f, 0.78f, 1);
        public static readonly Color WarmTint = new Color(1, 0.79f, 0.55f);
        private static WorldColorPreviewStage _capture;

        // Explicit setup, never an import-time rewrite of authored room content.
        public static void CreateLibrary()
        {
            foreach (bool warm in new[] { false, true })
            {
                string path = warm ? WarmFillPath : CoolFillPath;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject(warm ? "Warm room fill" : "Cool room fill");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    root.AddComponent<LocalLight2DSource>().ConfigureFill(0.18f, 4.5f, warm ? WarmTint : CoolTint);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SoftShaftPath) == null)
            {
                var root = PrefabUtility.LoadPrefabContents(LocalLightShaftSetup.PrefabPath);
                try
                {
                    root.name = "Soft ceiling shaft";
                    var shaft = root.GetComponent<LocalLightShaftSource>(); shaft.UsePreset(true);
                    LocalLightShaftSetup.Reclip(shaft); shaft.PrepareForSave();
                    PrefabUtility.SaveAsPrefabAsset(root, SoftShaftPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // One low-strength example at the entrance, without replacing the user's beam.
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                if (room.GetComponentsInChildren<LocalLight2DSource>(true).Any(x => x.IsRoomFill)) return;
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CoolFillPath), room.scene);
                root.transform.SetParent(room.transform, false);
                var spawn = room.GetComponent<RoomController>().PlayerSpawnPosition;
                root.transform.position = spawn + new Vector3(0.75f, 1.5f);
                PrefabUtility.SaveAsPrefabAsset(room, LocalLightingSetup.RoomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        [MenuItem("GameObject/Light/Relic Keeper/Cool room fill", false, 20)]
        private static void PlaceCool() => Place(CoolFillPath);
        [MenuItem("GameObject/Light/Relic Keeper/Warm room fill", false, 21)]
        private static void PlaceWarm() => Place(WarmFillPath);
        [MenuItem("GameObject/Light/Relic Keeper/Focused ceiling beam", false, 22)]
        private static void PlaceBeam() => Place(LocalLightShaftSetup.PrefabPath);
        [MenuItem("GameObject/Light/Relic Keeper/Soft ceiling shaft", false, 23)]
        private static void PlaceShaft() => Place(SoftShaftPath);
        private static void Place(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("The room-lighting prefab is missing: " + path); return; }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var scene = stage != null ? stage.scene : SceneManager.GetActiveScene();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(root, "Place room light");
            if (stage != null) root.transform.SetParent(stage.prefabContentsRoot.transform, false);
            Vector3 point = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            point.z = 0;
            var beam = root.GetComponent<LocalLightShaftSource>();
            root.transform.position = point - (beam != null ? beam.SourcePosition - root.transform.position : Vector3.zero);
            if (beam != null) LocalLightShaftSetup.Reclip(beam);
            Selection.activeGameObject = root; EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void OpenRoom(string path, bool beam)
        {
            var stage = PrefabStageUtility.OpenPrefab(path);
            if (stage == null) return;
            Component target = beam ? stage.prefabContentsRoot.GetComponentInChildren<LocalLightShaftSource>(true) :
                stage.prefabContentsRoot.GetComponentsInChildren<LocalLight2DSource>(true).FirstOrDefault(x => x.IsRoomFill);
            Selection.activeGameObject = target != null ? target.gameObject : stage.prefabContentsRoot;
            if (target != null) SceneView.lastActiveSceneView?.FrameSelected();
        }

        public static void PrepareCapture()
        { _capture?.Dispose(); _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>()); _capture.FocusShaft(); }
        public static void Capture()
        {
            if (_capture == null) throw new System.InvalidOperationException("Prepare the room-light preview first.");
            var profile = Object.Instantiate(_capture.Controller.ActiveDepthMistProfile); _capture.Controller.PreviewDepthMistProfile = profile;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var fill = _capture.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)).Single(x => x.IsRoomFill);
                var shaft = _capture.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLightShaftSource>(true)).Single();
                Directory.CreateDirectory("Logs/RoomLighting");
                for (int i = 0; i < 4; i++)
                {
                    profile.roomFillLights = i != 0; profile.localShafts = i == 0 || i == 3;
                    fill.ConfigureFill(0.18f, 4.5f, i == 2 ? WarmTint : CoolTint);
                    if (i == 3)
                    {
                        var original = room.GetComponentInChildren<LocalLightShaftSource>(true); original.UsePreset(true);
                        var maps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
                        shaft.UsePreset(true); shaft.Configure(shaft.Artwork, shaft.SourcePixel, LocalLightShaftSetup.ClipColumns(maps, original));
                    }
                    RenderPipeline.SubmitRenderRequest(_capture.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/RoomLighting/Variant-{i}.png", image.EncodeToPNG());
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(room); RenderTexture.active = previous;
                Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image); _capture.Dispose(); _capture = null;
            }
        }
        public static void Show()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(WorldColorProfilesWindow).GetField("_appearanceTab", flags).SetValue(window, 3);
            typeof(WorldColorProfilesWindow).GetField("_roomIndex", flags).SetValue(window, 0);
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Mortfall); window.Show(); window.Focus();
        }
    }

    [CustomEditor(typeof(LocalLightShaftSource))]
    public sealed class LocalLightShaftEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var source = (LocalLightShaftSource)target;
            EditorGUILayout.HelpBox("Settings belong to this beam only. Turn off Follow environment to give it independent brightness and dust. Save the room prefab to keep changes.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Focused beam")) Preset(source, false);
            if (GUILayout.Button("Soft shaft")) Preset(source, true);
            EditorGUILayout.EndHorizontal();
            var value = source.ReadAppearance();
            Vector3 point = source.transform.localPosition + CeilingBeamControls.AnchorOffset(source);
            if (CeilingBeamControls.Draw(ref value, ref point, source.transform)) CeilingBeamControls.Commit(source, value, point);
            if (source.transform.hasChanged)
            { LocalLightShaftSetup.Reclip(source); source.transform.hasChanged = false; SceneView.RepaintAll(); }
            EditorGUILayout.LabelField("Move the source or drag Radius / rotation at the visible tip. Save the room prefab to keep placement. World Appearance > Beams has the same controls.", EditorStyles.wordWrappedMiniLabel);
        }
        private static void Preset(LocalLightShaftSource source, bool soft)
        {
            Undo.RecordObject(source, "Change light style"); source.UsePreset(soft);
            CeilingBeamControls.Commit(source, source.ReadAppearance(), source.transform.localPosition + CeilingBeamControls.AnchorOffset(source));
        }
        private void OnSceneGUI()
        {
            var source = (LocalLightShaftSource)target;
            if (source.Artwork == null) return;
            Handles.color = new Color(0.65f, 0.85f, 1);
            Vector3 origin = source.SourcePosition;
            Vector3 end = source.EndPosition;
            Handles.DrawDottedLine(origin, end, 4); Handles.Label(origin, "Light source"); Handles.Label(end, "Radius / rotation");
            foreach (float x in new[] { 32f, 56f })
                Handles.DrawLine(origin, source.transform.TransformPoint(source.PixelToLocal(new Vector2(x, 145))));
            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(origin, Quaternion.identity);
            var newEnd = Handles.FreeMoveHandle(end, HandleUtility.GetHandleSize(end) * 0.06f, Vector3.zero, Handles.DotHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObjects(new Object[] { source, source.transform }, "Shape room light");
            if (moved != origin) { moved.z = origin.z; source.transform.position += moved - origin; }
            if (newEnd != end)
            {
                var direction = newEnd - origin; direction.z = 0;
                source.ApplyAppearance(CeilingBeamControls.AimAt(source.transform, source.ReadAppearance(), direction));
            }
            LocalLightShaftSetup.Reclip(source);
            EditorUtility.SetDirty(source.transform); PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source.transform);
        }
    }

    [CustomEditor(typeof(LocalLight2DSource))]
    public sealed class LocalRoomLightEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var source = (LocalLight2DSource)target;
            serializedObject.Update();
            EditorGUILayout.LabelField(source.IsRoomFill ? "Soft room fill" : "Flame light", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(source.IsRoomFill
                ? "Soft illumination on characters and nearby scenery. Keep the center in open air. Walls block the light; use several gentle fills across a large room."
                : "Torch and forge lighting follows the environment's flame color and gentle flicker.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_strength"), new GUIContent("Local brightness"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_radius"), new GUIContent("Radius (world units)"));
            if (source.IsRoomFill)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_tint"), new GUIContent("Color"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_innerRadius"), new GUIContent("Bright center size", "Low values give a gradual falloff. Higher values make a broader bright center."));
            }
            else EditorGUILayout.PropertyField(serializedObject.FindProperty("_glow"), new GUIContent("Source glow"), true);
            if (serializedObject.ApplyModifiedProperties()) SceneView.RepaintAll();
            EditorGUILayout.LabelField("Save the room prefab to keep placement. World Appearance > Lighting controls the environment's overall strength and reach.", EditorStyles.wordWrappedMiniLabel);
        }
        private void OnSceneGUI()
        {
            var source = (LocalLight2DSource)target;
            Handles.color = new Color(0.65f, 0.85f, 1); var position = source.transform.position;
            Handles.Label(position, source.IsRoomFill ? "Room fill — keep center in open air" : "Flame light");
            EditorGUI.BeginChangeCheck();
            float radius = Handles.RadiusHandle(Quaternion.identity, position, source.Radius, true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(source, "Resize room light");
                var settings = new SerializedObject(source); settings.FindProperty("_radius").floatValue = Mathf.Max(0.1f, radius); settings.ApplyModifiedProperties();
            }
        }
    }
}
