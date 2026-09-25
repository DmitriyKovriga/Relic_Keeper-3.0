using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Items;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Weapon InHand sprite tilt normalizer (tip-up = 0°).
    /// Aligns art so runtime cancel (pose.LocalEulerZ - InHandSpriteTiltZ) reads tip +Y.
    /// </summary>
    public sealed class WeaponEditorTab
    {
        private List<WeaponItemSO> _weapons = new List<WeaponItemSO>();
        private WeaponItemSO _selected;
        private string _search = "";
        private Vector2 _listScroll;
        private float _draftTilt;
        private bool _dragging;
        private Vector2 _dragPrevDir;
        private bool _dirty;

        private const float PreviewSize = 220f;

        public void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            string selectedPath = _selected != null ? AssetDatabase.GetAssetPath(_selected) : null;
            _weapons.Clear();
            foreach (string g in AssetDatabase.FindAssets("t:WeaponItemSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var w = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(path);
                if (w == null) continue;
                if (w.InHandSprite == null) continue;
                _weapons.Add(w);
            }

            _weapons = _weapons.OrderBy(w => w.name).ToList();

            if (!string.IsNullOrEmpty(selectedPath))
            {
                _selected = _weapons.FirstOrDefault(w => AssetDatabase.GetAssetPath(w) == selectedPath);
            }

            if (_selected == null && _weapons.Count > 0)
                Select(_weapons[0]);
            else if (_selected != null)
                _draftTilt = _selected.InHandSpriteTiltZ;

            _dirty = false;
        }

        public void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                Refresh();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                "Tip-up = 0°  |  finalEuler = poseEuler − InHandSpriteTiltZ",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            DrawList(280f);
            DrawInspector();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawList(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label($"Weapons with InHand ({_weapons.Count})", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField("Search", _search);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            string q = (_search ?? "").Trim().ToLowerInvariant();
            foreach (var w in _weapons)
            {
                if (w == null) continue;
                if (q.Length > 0
                    && !(w.name ?? "").ToLowerInvariant().Contains(q)
                    && !(w.ItemName ?? "").ToLowerInvariant().Contains(q)
                    && !(w.ID ?? "").ToLowerInvariant().Contains(q))
                    continue;

                bool sel = _selected == w;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                string label = $"{w.ItemName ?? w.name}  [{w.InHandSpriteTiltZ:0.##}°]";
                if (GUILayout.Button(new GUIContent(label, w.InHandSprite != null ? AssetPreview.GetAssetPreview(w.InHandSprite) : null), GUILayout.Height(36)))
                {
                    if (_dirty && _selected != null && !EditorUtility.DisplayDialog(
                            "Unsaved tilt",
                            $"Save InHandSpriteTiltZ on '{_selected.name}' before switching?",
                            "Save", "Discard"))
                    {
                        // Discard — fall through to select
                    }
                    else if (_dirty && _selected != null)
                    {
                        ApplyAndSave();
                    }

                    Select(w);
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (_selected == null)
            {
                EditorGUILayout.HelpBox(
                    "No weapons with InHandSprite found. Assign InHandSprite on WeaponItemSO (Items Editor), then Refresh.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Label(_selected.ItemName ?? _selected.name, EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Asset", _selected, typeof(WeaponItemSO), false);
            EditorGUILayout.ObjectField("InHand Sprite", _selected.InHandSprite, typeof(Sprite), false);

            if (_selected.InHandSprite == null)
            {
                EditorGUILayout.HelpBox("InHandSprite is null — this weapon should not appear in the list.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Rotate the preview until the blade tip aligns with the green +Y guide (tip-up). "
                + "Stored InHandSpriteTiltZ is the bake angle cancelled at runtime "
                + "(pose.LocalEulerZ − tilt). Do not bulk-rewrite — set per weapon by hand.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            float newTilt = EditorGUILayout.FloatField("InHandSpriteTiltZ", _draftTilt);
            if (EditorGUI.EndChangeCheck())
            {
                _draftTilt = newTilt;
                _dirty = true;
            }

            _draftTilt = EditorGUILayout.Slider("Tilt", _draftTilt, -180f, 180f);
            if (GUI.changed)
                _dirty = true;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset 0°", GUILayout.Width(80)))
            {
                _draftTilt = 0f;
                _dirty = true;
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = _dirty;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
                ApplyAndSave();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (_dirty)
                EditorGUILayout.HelpBox("Dirty — Save to write InHandSpriteTiltZ on the SO.", MessageType.Warning);

            EditorGUILayout.Space(8);
            DrawPreview();

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            Rect area = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(true), GUILayout.Height(PreviewSize + 24f));
            float side = Mathf.Min(area.width, PreviewSize);
            Rect preview = new Rect(
                area.x + (area.width - side) * 0.5f,
                area.y,
                side,
                side);

            EditorGUI.DrawRect(preview, new Color(0.15f, 0.15f, 0.15f, 1f));

            // Tip-up crosshair: screen-up = +Y guide
            Handles.BeginGUI();
            Vector2 c = preview.center;
            Handles.color = new Color(0.2f, 0.9f, 0.35f, 0.9f);
            Handles.DrawLine(new Vector3(c.x, preview.yMax - 8f), new Vector3(c.x, preview.yMin + 8f));
            Handles.DrawLine(new Vector3(preview.xMin + 8f, c.y), new Vector3(preview.xMax - 8f, c.y));
            // Arrow head toward top (+Y)
            Handles.DrawLine(new Vector3(c.x, preview.yMin + 8f), new Vector3(c.x - 8f, preview.yMin + 20f));
            Handles.DrawLine(new Vector3(c.x, preview.yMin + 8f), new Vector3(c.x + 8f, preview.yMin + 20f));
            Handles.color = Color.white;
            Handles.EndGUI();

            Texture2D tex = AssetPreview.GetAssetPreview(_selected.InHandSprite);
            if (tex == null)
                tex = _selected.InHandSprite.texture;

            if (tex != null)
            {
                // Show sprite after cancel: Unity euler = -tilt. IMGUI Y-down → negate once more.
                Matrix4x4 prev = GUI.matrix;
                GUIUtility.RotateAroundPivot(_draftTilt, c);

                float pad = side * 0.12f;
                Rect spriteRect = new Rect(preview.x + pad, preview.y + pad, side - pad * 2f, side - pad * 2f);
                // Flip Y so texture +Y reads toward tip-up guide (screen up)
                Rect uv = GetSpriteUv(_selected.InHandSprite);
                GUI.DrawTextureWithTexCoords(spriteRect, tex, uv, true);

                GUI.matrix = prev;
            }

            GUI.Label(
                new Rect(preview.x, preview.yMax + 2f, preview.width, 18f),
                "Drag on preview to rotate  |  green arrow = tip-up (+Y)",
                EditorStyles.centeredGreyMiniLabel);

            HandlePreviewDrag(preview, c);
        }

        private static Rect GetSpriteUv(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Rect(0, 0, 1, 1);

            Rect tr = sprite.textureRect;
            float tw = sprite.texture.width;
            float th = sprite.texture.height;
            return new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
        }

        private void HandlePreviewDrag(Rect preview, Vector2 center)
        {
            Event e = Event.current;
            if (!preview.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseUp)
                    _dragging = false;
                return;
            }

            Vector2 dir = e.mousePosition - center;
            if (dir.sqrMagnitude < 4f)
                return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _dragging = true;
                _dragPrevDir = dir;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragging && e.button == 0)
            {
                float prev = Mathf.Atan2(_dragPrevDir.y, _dragPrevDir.x) * Mathf.Rad2Deg;
                float next = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                // IMGUI Y-down: positive drag delta maps so clockwise drag increases bake tilt
                // in the same sense as Unity Z (Y-up CCW). Negate for Y-down.
                float delta = Mathf.DeltaAngle(prev, next);
                _draftTilt = Mathf.DeltaAngle(0f, _draftTilt - delta);
                _dragPrevDir = dir;
                _dirty = true;
                e.Use();
                GUI.changed = true;
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                _dragging = false;
                e.Use();
            }
        }

        private void Select(WeaponItemSO w)
        {
            _selected = w;
            _draftTilt = w != null ? w.InHandSpriteTiltZ : 0f;
            _dirty = false;
        }

        private void ApplyAndSave()
        {
            if (_selected == null) return;
            Undo.RecordObject(_selected, "Set InHandSpriteTiltZ");
            _selected.InHandSpriteTiltZ = _draftTilt;
            EditorUtility.SetDirty(_selected);
            AssetDatabase.SaveAssets();
            _dirty = false;
        }
    }
}
