using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Dungeon;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Dungeons
{
    public sealed class RoomPngExporterWindow : EditorWindow
    {
        [SerializeField] private DungeonDataSO _dungeon;
        [SerializeField] private string _outputFolder;
        [SerializeField] private int _scaleIndex;
        [SerializeField] private int _padding = 24;
        [SerializeField] private bool _includeOuterTerrain;
        private List<RoomExportEntry> _rooms = new List<RoomExportEntry>();
        private readonly HashSet<string> _selected = new HashSet<string>();
        private Vector2 _scroll;
        private Vector2 _windowScroll;
        private Sprite _background;
        private string _lastFile, _status;
        private Texture2D _preview;

        [MenuItem("Tools/Dungeons/Room PNG Exporter")]
        public static void Open() => GetWindow<RoomPngExporterWindow>("Room PNG Exporter");

        private void OnEnable()
        {
            minSize = new Vector2(480, 520);
            if (_dungeon == null) _dungeon = Resources.LoadAll<DungeonDataSO>("Dungeons").FirstOrDefault(x => x.ID == "Mortfall");
            if (string.IsNullOrWhiteSpace(_outputFolder))
                _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RelicKeeper", "RoomExports");
            Reload();
        }

        private void OnDisable() { if (_preview != null) DestroyImmediate(_preview); }

        private void Reload()
        {
            _rooms = RoomPngExportUtility.GetRooms(_dungeon);
            _selected.Clear();
            if (_rooms.Count > 0) _selected.Add(_rooms[0].Path);
            _background = _dungeon != null ? _dungeon.LoadBackgroundSprite() : null;
        }

        private void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_windowScroll))
            {
                _windowScroll = scroll.scrollPosition;
                DrawContents();
            }
        }

        private void DrawContents()
        {
            EditorGUILayout.LabelField("Full room PNG for environment painting", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Exports saved room prefabs; save your room edits first. Includes the dungeon's tiled background, with no enemies, player or UI. Native 24 PPU; 2×/4× uses nearest-neighbour enlargement. Standard room art uses neutral lighting.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _dungeon = (DungeonDataSO)EditorGUILayout.ObjectField("Dungeon", _dungeon, typeof(DungeonDataSO), false);
            if (EditorGUI.EndChangeCheck()) { Reload(); GUIUtility.ExitGUI(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh rooms")) { Reload(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Select all"))
                    foreach (var room in _rooms)
                        if (AssetDatabase.LoadAssetAtPath<GameObject>(room.Path) != null) _selected.Add(room.Path);
                if (GUILayout.Button("Select none")) _selected.Clear();
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Min(220f, Mathf.Max(70f, _rooms.Count * 27f))));
            foreach (var room in _rooms)
            {
                bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(room.Path) != null;
                using (new EditorGUI.DisabledScope(!exists))
                {
                    bool picked = EditorGUILayout.ToggleLeft(new GUIContent(room.Name + "  [" + room.Role + "]" + (exists ? "" : " — missing"), room.Path), _selected.Contains(room.Path));
                    if (picked) _selected.Add(room.Path); else _selected.Remove(room.Path);
                }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Background", _background, typeof(Sprite), false);
            if (_dungeon != null && _background == null) EditorGUILayout.HelpBox("Set this dungeon's Background Sprite Resource Path to enable export.", MessageType.Warning);
            _scaleIndex = EditorGUILayout.Popup("Image scale", _scaleIndex, new[] { "1× — native pixels", "2× — nearest neighbour", "4× — nearest neighbour" });
            _padding = Mathf.Clamp(EditorGUILayout.IntField("Border padding (native px)", _padding), 0, 240);
            _includeOuterTerrain = EditorGUILayout.Toggle(new GUIContent("Include outer prefab terrain", "Default framing uses the room's camera bounds. Enable to include off-screen terrain and boundary walls as well."), _includeOuterTerrain);
            EditorGUILayout.LabelField("Files: RoomName.png — re-export replaces the existing PNG.", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("Output folder", _outputFolder);
                if (GUILayout.Button("Browse…", GUILayout.Width(75)))
                {
                    string folder = EditorUtility.OpenFolderPanel("Room PNG output", _outputFolder, "");
                    if (folder.Length > 0) _outputFolder = folder;
                }
            }
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing) EditorGUILayout.HelpBox("Exit Play Mode to export safely from room prefabs.", MessageType.Info);
            using (new EditorGUI.DisabledScope(playing || _dungeon == null || _background == null || _selected.Count == 0 || string.IsNullOrWhiteSpace(_outputFolder)))
                if (GUILayout.Button($"Export {_selected.Count} selected room(s) and open folder", GUILayout.Height(30)))
                {
                    ExportSelected();
                    GUIUtility.ExitGUI();
                }
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
            if (!string.IsNullOrEmpty(_lastFile) && File.Exists(_lastFile) && GUILayout.Button("Show last PNG in folder")) EditorUtility.RevealInFinder(_lastFile);
            if (_preview != null)
            {
                Rect rect = GUILayoutUtility.GetAspectRect(_preview.width / (float)_preview.height, GUILayout.MaxHeight(260));
                GUI.DrawTexture(rect, _preview, ScaleMode.ScaleToFit, false);
            }
        }

        private void ExportSelected()
        {
            var selected = _rooms.Where(x => _selected.Contains(x.Path)).ToList();
            var failures = new List<string>();
            int exported = 0;
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Room PNG Exporter", selected[i].Name, i / (float)selected.Count)) break;
                    try
                    {
                        _lastFile = RoomPngExportUtility.Export(_dungeon, selected[i], _outputFolder, new[] { 1, 2, 4 }[_scaleIndex], _padding, _includeOuterTerrain);
                        exported++;
                    }
                    catch (Exception exception) { failures.Add(selected[i].Name + ": " + exception.Message); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            _status = $"Exported {exported} PNG(s)." + (failures.Count > 0 ? "\n" + string.Join("\n", failures) : "");
            if (exported > 0)
            {
                if (_preview != null) DestroyImmediate(_preview);
                _preview = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                _preview.LoadImage(File.ReadAllBytes(_lastFile));
                EditorUtility.RevealInFinder(_lastFile);
            }
        }
    }
}
