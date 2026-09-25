using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Items;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Weapon InHand sprite tilt + handle-origin authoring (tip-up = 0 deg).
    /// Preview rotates/positions around sprite.pivot (same as StanceEditorTab.DrawSpriteAt).
    /// Persistence: prefer TextureImporter pivot when InHand texture is unique (1 sprite);
    /// otherwise WeaponItemSO.InHandSpriteLocalOffset (shared sheet). See CanEditSpritePivot.
    /// Runtime cancel unchanged: finalEuler = pose.LocalEulerZ - InHandSpriteTiltZ.
    /// </summary>
    public sealed class WeaponEditorTab
    {
        private List<WeaponItemSO> _weapons = new List<WeaponItemSO>();
        private WeaponItemSO _selected;
        private string _search = "";
        private Vector2 _listScroll;
        private float _draftTilt;
        private Vector2 _draftPivotPx;
        private Vector2 _draftLocalOffset;
        private bool _draggingRot;
        private bool _draggingPos;
        private Vector2 _dragPrevDir;
        private bool _dirty;
        private bool _pivotMode;

        private const float PreviewSize = 220f;
        private const float RotateGrabMinPx = 42f;

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
                LoadDraftFromSelected();

            _dirty = false;
        }

        public void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                Refresh();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                "Tip-up = 0  |  finalEuler = poseEuler - InHandSpriteTiltZ  |  crosshair = handle origin",
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
                string label = $"{w.ItemName ?? w.name}  [{w.InHandSpriteTiltZ:0.##}]";
                if (GUILayout.Button(new GUIContent(label, w.InHandSprite != null ? AssetPreview.GetAssetPreview(w.InHandSprite) : null), GUILayout.Height(36)))
                {
                    if (_dirty && _selected != null && !EditorUtility.DisplayDialog(
                            "Unsaved changes",
                            $"Save InHand alignment on '{_selected.name}' before switching?",
                            "Save", "Discard"))
                    {
                        // Discard - fall through to select
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
                EditorGUILayout.HelpBox("InHandSprite is null - this weapon should not appear in the list.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            Sprite sprite = _selected.InHandSprite;
            float ppu = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : 24f;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Green crosshair = tip-up + handle origin (0,0). "
                + "Drag near center to move handle onto the crosshair; drag farther out to set InHandSpriteTiltZ (blade tip to +Y). "
                + "Runtime: finalEuler = pose.LocalEulerZ - tilt (unchanged).",
                MessageType.None);

            if (_pivotMode)
            {
                EditorGUILayout.LabelField(
                    "Persistence: sprite.pivot via TextureImporter (unique InHand texture)",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Persistence: InHandSpriteLocalOffset on SO (shared sheet — pivot edit unsafe)",
                    EditorStyles.miniLabel);
            }

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

            EditorGUI.BeginChangeCheck();
            if (_pivotMode)
            {
                Vector2 pivotPx = EditorGUILayout.Vector2Field(
                    new GUIContent("Pivot (px)", "Pixels from sprite rect bottom-left. SpriteRenderer origin."),
                    _draftPivotPx);
                if (EditorGUI.EndChangeCheck())
                {
                    _draftPivotPx = ClampPivotPx(pivotPx, sprite);
                    _dirty = true;
                }

                Vector2 pivotN = new Vector2(
                    sprite.rect.width > 0.01f ? _draftPivotPx.x / sprite.rect.width : 0.5f,
                    sprite.rect.height > 0.01f ? _draftPivotPx.y / sprite.rect.height : 0.5f);
                EditorGUILayout.LabelField(
                    $"Pivot normalized: ({pivotN.x:0.###}, {pivotN.y:0.###})  |  rect {sprite.rect.width:0.#}x{sprite.rect.height:0.#}  PPU {ppu:0.#}",
                    EditorStyles.miniLabel);
            }
            else
            {
                Vector2 off = EditorGUILayout.Vector2Field(
                    new GUIContent("InHandSpriteLocalOffset", "HandPivot-local nudge added to stance LocalPosition."),
                    _draftLocalOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    _draftLocalOffset = off;
                    _dirty = true;
                }

                EditorGUILayout.LabelField(
                    $"Current sprite.pivot (px): ({sprite.pivot.x:0.##}, {sprite.pivot.y:0.##}) — read-only (shared)",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset tilt 0", GUILayout.Width(100)))
            {
                _draftTilt = 0f;
                _dirty = true;
            }

            if (_pivotMode)
            {
                if (GUILayout.Button("Pivot center", GUILayout.Width(100)))
                {
                    _draftPivotPx = new Vector2(sprite.rect.width * 0.5f, sprite.rect.height * 0.5f);
                    _dirty = true;
                }
            }
            else
            {
                if (GUILayout.Button("Offset zero", GUILayout.Width(100)))
                {
                    _draftLocalOffset = Vector2.zero;
                    _dirty = true;
                }
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = _dirty;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
                ApplyAndSave();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (_dirty)
                EditorGUILayout.HelpBox("Dirty — Save writes tilt + handle alignment.", MessageType.Warning);

            EditorGUILayout.Space(8);
            DrawPreview();

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            Rect area = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(true), GUILayout.Height(PreviewSize + 28f));
            float side = Mathf.Min(area.width, PreviewSize);
            Rect preview = new Rect(
                area.x + (area.width - side) * 0.5f,
                area.y,
                side,
                side);

            EditorGUI.DrawRect(preview, new Color(0.15f, 0.15f, 0.15f, 1f));

            Vector2 c = preview.center;

            // Tip-up crosshair = handle origin
            Handles.BeginGUI();
            Handles.color = new Color(0.2f, 0.9f, 0.35f, 0.9f);
            Handles.DrawLine(new Vector3(c.x, preview.yMax - 8f), new Vector3(c.x, preview.yMin + 8f));
            Handles.DrawLine(new Vector3(preview.xMin + 8f, c.y), new Vector3(preview.xMax - 8f, c.y));
            Handles.DrawLine(new Vector3(c.x, preview.yMin + 8f), new Vector3(c.x - 8f, preview.yMin + 20f));
            Handles.DrawLine(new Vector3(c.x, preview.yMin + 8f), new Vector3(c.x + 8f, preview.yMin + 20f));
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            Handles.DrawWireDisc(new Vector3(c.x, c.y, 0f), Vector3.forward, RotateGrabMinPx);
            Handles.EndGUI();

            Sprite sprite = _selected.InHandSprite;
            if (sprite != null)
            {
                float ppu = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : 24f;
                Vector2 worldSize = sprite.rect.size / ppu;
                float maxDim = Mathf.Max(worldSize.x, worldSize.y, 0.01f);
                float worldToGui = (side * 0.72f) / maxDim;

                // Pivot mode: draft pivot sits on crosshair. Offset mode: sprite.pivot on crosshair + SO offset.
                Vector2 drawCenter = c;
                if (!_pivotMode)
                    drawCenter = c + WorldToGui(_draftLocalOffset, worldToGui);

                // Tip-up preview (poseEuler 0): unityEuler = 0 - tilt = -tilt.
                // IMGUI Y-down => RotateAroundPivot(-unityEuler) = +tilt (matches prior Weapon tab / Stance).
                float unityEulerZ = -_draftTilt;
                DrawSpriteAtPivot(preview, sprite, drawCenter, unityEulerZ, worldToGui, _pivotMode ? _draftPivotPx : (Vector2?)null);
            }

            GUI.Label(
                new Rect(preview.x, preview.yMax + 2f, preview.width, 22f),
                "LMB near center = move handle  |  LMB outside ring = rotate tilt  |  green = tip-up",
                EditorStyles.centeredGreyMiniLabel);

            HandlePreviewInput(preview, c);
        }

        /// <summary>
        /// Draw sprite with transform origin at centerGui using pivot pixels (SpriteRenderer match).
        /// Optional pivotOverridePx replaces sprite.pivot for live draft before reimport.
        /// </summary>
        private static void DrawSpriteAtPivot(
            Rect preview,
            Sprite sprite,
            Vector2 centerGui,
            float unityEulerZ,
            float worldToGui,
            Vector2? pivotOverridePx)
        {
            if (sprite == null) return;
            Texture2D tex = sprite.texture;
            if (tex == null) return;

            float ppu = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : 24f;
            Vector2 worldSize = sprite.rect.size / ppu;
            Vector2 guiSize = worldSize * worldToGui;

            Vector2 pivotPx = pivotOverridePx ?? sprite.pivot;
            float pivotGuiX = pivotPx.x / ppu * worldToGui;
            float pivotGuiY = pivotPx.y / ppu * worldToGui;
            // Unity pivot from rect bottom-left (Y-up); IMGUI rect origin is top-left (Y-down).
            Rect spriteRect = new Rect(
                centerGui.x - pivotGuiX,
                centerGui.y - (guiSize.y - pivotGuiY),
                guiSize.x,
                guiSize.y);

            if (!spriteRect.Overlaps(preview))
                return;

            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(-unityEulerZ, centerGui);

            Rect uv = GetSpriteUv(sprite);
            GUI.DrawTextureWithTexCoords(spriteRect, tex, uv, true);
            GUI.matrix = prev;
        }

        private static Vector2 WorldToGui(Vector2 world, float worldToGui)
        {
            return new Vector2(world.x * worldToGui, -world.y * worldToGui);
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

        private void HandlePreviewInput(Rect preview, Vector2 center)
        {
            Event e = Event.current;
            if (!preview.Contains(e.mousePosition)
                && e.type != EventType.MouseUp
                && e.type != EventType.MouseDrag)
                return;

            Sprite sprite = _selected != null ? _selected.InHandSprite : null;
            float ppu = sprite != null && sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : 24f;
            float worldToGui = 1f;
            if (sprite != null)
            {
                Vector2 worldSize = sprite.rect.size / ppu;
                float maxDim = Mathf.Max(worldSize.x, worldSize.y, 0.01f);
                worldToGui = (preview.width * 0.72f) / maxDim;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && preview.Contains(e.mousePosition))
            {
                float dist = Vector2.Distance(e.mousePosition, center);
                if (dist < RotateGrabMinPx)
                {
                    _draggingPos = true;
                    _draggingRot = false;
                }
                else
                {
                    _draggingRot = true;
                    _draggingPos = false;
                    _dragPrevDir = e.mousePosition - center;
                }

                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_draggingPos && sprite != null)
                {
                    Vector2 deltaGui = e.delta;
                    if (_pivotMode)
                    {
                        // Slide art under fixed crosshair: pivot (px) moves opposite to visual drag.
                        // GUI +x right / +y down; pivot Y is Unity bottom-up.
                        _draftPivotPx.x -= deltaGui.x * ppu / worldToGui;
                        _draftPivotPx.y += deltaGui.y * ppu / worldToGui;
                        _draftPivotPx = ClampPivotPx(_draftPivotPx, sprite);
                    }
                    else
                    {
                        Vector2 deltaWorld = new Vector2(deltaGui.x / worldToGui, -deltaGui.y / worldToGui);
                        _draftLocalOffset += deltaWorld;
                    }

                    _dirty = true;
                    e.Use();
                    GUI.changed = true;
                }
                else if (_draggingRot)
                {
                    Vector2 dir = e.mousePosition - center;
                    if (dir.sqrMagnitude > 4f && _dragPrevDir.sqrMagnitude > 4f)
                    {
                        float prev = Mathf.Atan2(_dragPrevDir.y, _dragPrevDir.x) * Mathf.Rad2Deg;
                        float next = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        float delta = Mathf.DeltaAngle(prev, next);
                        // IMGUI Y-down: negate so clockwise drag matches Unity Z bake sense.
                        _draftTilt = Mathf.DeltaAngle(0f, _draftTilt - delta);
                        _dragPrevDir = dir;
                        _dirty = true;
                        e.Use();
                        GUI.changed = true;
                    }
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                _draggingPos = false;
                _draggingRot = false;
            }
        }

        private static Vector2 ClampPivotPx(Vector2 pivotPx, Sprite sprite)
        {
            if (sprite == null) return pivotPx;
            // Allow slight overhang so handle can sit just outside tight rects.
            float pad = 4f;
            return new Vector2(
                Mathf.Clamp(pivotPx.x, -pad, sprite.rect.width + pad),
                Mathf.Clamp(pivotPx.y, -pad, sprite.rect.height + pad));
        }

        private void Select(WeaponItemSO w)
        {
            _selected = w;
            LoadDraftFromSelected();
            _dirty = false;
        }

        private void LoadDraftFromSelected()
        {
            if (_selected == null)
            {
                _draftTilt = 0f;
                _draftPivotPx = Vector2.zero;
                _draftLocalOffset = Vector2.zero;
                _pivotMode = true;
                return;
            }

            _draftTilt = _selected.InHandSpriteTiltZ;
            _draftLocalOffset = _selected.InHandSpriteLocalOffset;
            _pivotMode = CanEditSpritePivot(_selected.InHandSprite);

            if (_selected.InHandSprite != null)
                _draftPivotPx = _selected.InHandSprite.pivot;
            else
                _draftPivotPx = Vector2.zero;
        }

        /// <summary>
        /// True when InHand texture has a single Sprite — safe to write pivot via TextureImporter.
        /// Multi-sprite sheets stay on InHandSpriteLocalOffset so other cuts are not corrupted.
        /// </summary>
        private static bool CanEditSpritePivot(Sprite sprite)
        {
            if (sprite == null) return false;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path)) return false;

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            int spriteCount = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Sprite)
                    spriteCount++;
            }

            return spriteCount <= 1;
        }

        private void ApplyAndSave()
        {
            if (_selected == null) return;

            Undo.RecordObject(_selected, "Set InHand alignment");
            _selected.InHandSpriteTiltZ = _draftTilt;

            if (_pivotMode)
            {
                // Chosen path: sprite.pivot — SpriteRenderer origin; unique InHand texture.
                _selected.InHandSpriteLocalOffset = Vector2.zero;
                if (!TryWriteSpritePivot(_selected.InHandSprite, _draftPivotPx, out string err))
                {
                    EditorUtility.DisplayDialog("Pivot save failed", err, "OK");
                    return;
                }
            }
            else
            {
                // Chosen path: SO offset — shared sheet; applied in WVC with stance LocalPosition.
                _selected.InHandSpriteLocalOffset = _draftLocalOffset;
            }

            EditorUtility.SetDirty(_selected);
            AssetDatabase.SaveAssets();

            // Re-bind sprite after possible reimport.
            string soPath = AssetDatabase.GetAssetPath(_selected);
            _selected = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(soPath);
            LoadDraftFromSelected();
            _dirty = false;
        }

        private static bool TryWriteSpritePivot(Sprite sprite, Vector2 pivotPx, out string error)
        {
            error = null;
            if (sprite == null)
            {
                error = "InHandSprite is null.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(sprite);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                error = $"Not a TextureImporter: {path}";
                return false;
            }

            float w = Mathf.Max(sprite.rect.width, 0.01f);
            float h = Mathf.Max(sprite.rect.height, 0.01f);
            Vector2 normalized = new Vector2(pivotPx.x / w, pivotPx.y / h);

            Undo.RecordObject(importer, "Set InHand sprite pivot");

            if (importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                SpriteMetaData[] metas = importer.spritesheet;
                bool found = false;
                for (int i = 0; i < metas.Length; i++)
                {
                    if (metas[i].name != sprite.name) continue;
                    SpriteMetaData m = metas[i];
                    m.alignment = (int)SpriteAlignment.Custom;
                    m.pivot = normalized;
                    metas[i] = m;
                    found = true;
                    break;
                }

                if (!found)
                {
                    error = $"Sprite '{sprite.name}' not found in spritesheet.";
                    return false;
                }

                importer.spritesheet = metas;
            }
            else
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                importer.SetTextureSettings(settings);
                importer.spritePivot = normalized;
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return true;
        }
    }
}
