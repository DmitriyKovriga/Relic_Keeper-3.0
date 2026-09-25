using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Visuals;

namespace Scripts.Editor.Skills
{
    /// <summary>
    /// Global hold-stance pose editor. Edits WeaponStancePoseTableSO; WVC reads that SO at runtime.
    /// Preview coordinates match ApplyHandVisual: WeaponHolder local to HandPivot, finalEuler = poseEuler - InHandTilt.
    /// </summary>
    public sealed class StanceEditorTab
    {
        private const float PreviewSize = 320f;
        private const float Ppu = 24f;
        // HubScene Player: Visuals/HandPivot local pos (1px left at PPU 24).
        private static readonly Vector2 HandPivotLocal = new Vector2(-1f / 24f, 0f);
        private const string BodySpritePath = "Assets/Resources/Heroes/WarriorResources/WarriorWalk-Sheet.png";
        private const string BodyFallbackPath = "Assets/Resources/Heroes/WarriorResources/Warrioir_v0.1.png";
        private const string DebugBladePath = "Assets/Resources/Items/Debug/Debug Blade.asset";

        private WeaponStancePoseTableSO _table;
        private WeaponHoldStance _selectedStance = WeaponHoldStance.Default;
        private WeaponItemSO _previewWeapon;
        private List<WeaponItemSO> _weapons = new List<WeaponItemSO>();
        private Vector2 _listScroll;
        private Vector2 _draftPos;
        private float _draftEuler;
        private bool _draftFlipX;
        private bool _draftFlipY;
        private bool _draftBehind;
        private bool _dirty;
        private bool _draggingPos;
        private bool _draggingRot;
        private Vector2 _dragPrevDir;
        private Sprite _bodySprite;

        public void OnEnable()
        {
            EnsureTable();
            RefreshWeapons();
            LoadBodySprite();
            LoadDraftFromTable();
        }

        public void OnGUI()
        {
            EnsureTable();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                EnsureTable();
                RefreshWeapons();
                LoadBodySprite();
                LoadDraftFromTable();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                "WeaponHolder local to HandPivot  |  finalEuler = poseEuler − InHandTilt  |  tip-up=0, −90 tip-fwd, +90 tip-back",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (_table == null)
            {
                EditorGUILayout.HelpBox("Could not create/load WeaponStancePoseTableSO.", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawStanceList(200f);
            DrawInspector();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStanceList(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label("Hold Stance", EditorStyles.boldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            foreach (WeaponHoldStance stance in Enum.GetValues(typeof(WeaponHoldStance)))
            {
                bool sel = _selectedStance == stance;
                GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                WeaponVisualController.StancePose pose = _table.GetPose(stance);
                string label = $"{stance}\n  ({pose.LocalPosition.x:0.##}, {pose.LocalPosition.y:0.##})  {pose.LocalEulerZ:0.#}°";
                if (GUILayout.Button(label, GUILayout.Height(40)))
                {
                    if (!ConfirmDiscardOrSave())
                    {
                        GUI.backgroundColor = Color.white;
                        continue;
                    }

                    _selectedStance = stance;
                    LoadDraftFromTable();
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.ObjectField("Pose Table", _table, typeof(WeaponStancePoseTableSO), false);
            EditorGUILayout.LabelField($"Version {_table.Version}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            GUILayout.Label($"Stance: {_selectedStance}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _previewWeapon = (WeaponItemSO)EditorGUILayout.ObjectField(
                "Preview Weapon",
                _previewWeapon,
                typeof(WeaponItemSO),
                false);
            if (EditorGUI.EndChangeCheck())
                GUI.changed = true;

            if (_bodySprite == null)
            {
                EditorGUILayout.HelpBox(
                    "Body sprite missing. Tried:\n" + BodySpritePath + "\n" + BodyFallbackPath,
                    MessageType.Error);
            }

            if (_previewWeapon == null || _previewWeapon.InHandSprite == null)
            {
                EditorGUILayout.HelpBox(
                    "Pick a WeaponItemSO with InHandSprite (default: Debug Blade).",
                    MessageType.Info);
            }
            else
            {
                string bodyLabel = _bodySprite != null
                    ? $"{_bodySprite.name} (PPU {Ppu})"
                    : "MISSING";
                EditorGUILayout.LabelField(
                    $"InHand tilt: {_previewWeapon.InHandSpriteTiltZ:0.##}°  |  body: {bodyLabel}",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            _draftPos = EditorGUILayout.Vector2Field("Local Position", _draftPos);
            _draftEuler = EditorGUILayout.FloatField("Local Euler Z", _draftEuler);
            _draftEuler = EditorGUILayout.Slider("Rotate", _draftEuler, -180f, 180f);
            _draftFlipX = EditorGUILayout.Toggle("Flip X", _draftFlipX);
            _draftFlipY = EditorGUILayout.Toggle("Flip Y", _draftFlipY);
            _draftBehind = EditorGUILayout.Toggle(
                new GUIContent("Sort Behind Character", "за моделькой — Player band instead of PlayerOverlay"),
                _draftBehind);
            if (EditorGUI.EndChangeCheck())
                _dirty = true;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Stance Defaults", GUILayout.Width(160)))
            {
                WeaponVisualController.StancePose def = FindDefaultPose(_selectedStance);
                ApplyDraft(def);
                _dirty = true;
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = _dirty;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
                Save();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (_dirty)
                EditorGUILayout.HelpBox("Dirty — Save writes WeaponStancePoseTableSO (global).", MessageType.Warning);

            EditorGUILayout.Space(6);
            DrawPreview();

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            Rect area = GUILayoutUtility.GetRect(
                PreviewSize,
                PreviewSize,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(PreviewSize + 28f));
            float side = Mathf.Min(area.width, PreviewSize);
            Rect preview = new Rect(
                area.x + (area.width - side) * 0.5f,
                area.y,
                side,
                side);

            EditorGUI.DrawRect(preview, new Color(0.14f, 0.14f, 0.16f, 1f));

            float worldToGui = side * 0.45f; // 1 world unit → GUI px (character ~1u tall)
            Vector2 center = preview.center;

            // Tip-up guide through hand pivot
            Vector2 handGui = center + WorldToGui(HandPivotLocal, worldToGui);
            Handles.BeginGUI();
            Handles.color = new Color(0.2f, 0.9f, 0.35f, 0.55f);
            Handles.DrawLine(
                new Vector3(handGui.x, preview.yMax - 6f),
                new Vector3(handGui.x, preview.yMin + 6f));
            Handles.DrawLine(
                new Vector3(preview.xMin + 6f, handGui.y),
                new Vector3(preview.xMax - 6f, handGui.y));
            Handles.color = new Color(1f, 1f, 1f, 0.25f);
            Handles.DrawWireDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, worldToGui * 0.08f);
            Handles.EndGUI();

            Sprite weaponSprite = _previewWeapon != null ? _previewWeapon.InHandSprite : null;
            float tilt = _previewWeapon != null ? _previewWeapon.InHandSpriteTiltZ : 0f;
            float finalEuler = _draftEuler - tilt;

            Action drawBody = () => DrawSpriteAt(preview, _bodySprite, center, 0f, false, false, worldToGui);
            Action drawWeapon = () =>
            {
                if (weaponSprite == null) return;
                Vector2 weaponGui = handGui + WorldToGui(_draftPos, worldToGui);
                DrawSpriteAt(preview, weaponSprite, weaponGui, finalEuler, _draftFlipX, _draftFlipY, worldToGui);
            };

            if (_draftBehind)
            {
                drawWeapon();
                drawBody();
            }
            else
            {
                drawBody();
                drawWeapon();
            }

            // Hand pivot marker
            EditorGUI.DrawRect(new Rect(handGui.x - 2f, handGui.y - 2f, 4f, 4f), new Color(1f, 0.85f, 0.2f, 0.95f));

            GUI.Label(
                new Rect(preview.x, preview.yMax + 2f, preview.width, 22f),
                "LMB drag empty = move  |  LMB drag near weapon = rotate  |  yellow = HandPivot  |  green = tip-up",
                EditorStyles.centeredGreyMiniLabel);

            HandlePreviewInput(preview, center, handGui, worldToGui, weaponSprite != null);
        }

        private void HandlePreviewInput(
            Rect preview,
            Vector2 bodyCenter,
            Vector2 handGui,
            float worldToGui,
            bool hasWeapon)
        {
            Event e = Event.current;
            if (!preview.Contains(e.mousePosition)
                && e.type != EventType.MouseUp
                && e.type != EventType.MouseDrag)
                return;

            Vector2 weaponGui = handGui + WorldToGui(_draftPos, worldToGui);
            float nearWeapon = Vector2.Distance(e.mousePosition, weaponGui);

            if (e.type == EventType.MouseDown && e.button == 0 && preview.Contains(e.mousePosition))
            {
                if (hasWeapon && nearWeapon < 36f)
                {
                    _draggingRot = true;
                    _draggingPos = false;
                    _dragPrevDir = e.mousePosition - weaponGui;
                }
                else
                {
                    _draggingPos = true;
                    _draggingRot = false;
                }

                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_draggingPos)
                {
                    Vector2 deltaGui = e.delta;
                    // IMGUI Y-down → world +Y up
                    Vector2 deltaWorld = new Vector2(deltaGui.x / worldToGui, -deltaGui.y / worldToGui);
                    _draftPos += deltaWorld;
                    _dirty = true;
                    e.Use();
                    GUI.changed = true;
                }
                else if (_draggingRot && hasWeapon)
                {
                    Vector2 dir = e.mousePosition - weaponGui;
                    if (dir.sqrMagnitude > 4f && _dragPrevDir.sqrMagnitude > 4f)
                    {
                        float prev = Mathf.Atan2(_dragPrevDir.y, _dragPrevDir.x) * Mathf.Rad2Deg;
                        float next = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        float delta = Mathf.DeltaAngle(prev, next);
                        // IMGUI Y-down: negate so clockwise drag matches Unity Z (Y-up CCW sense for bake).
                        _draftEuler = Mathf.DeltaAngle(0f, _draftEuler - delta);
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

        private static Vector2 WorldToGui(Vector2 world, float worldToGui)
        {
            return new Vector2(world.x * worldToGui, -world.y * worldToGui);
        }

        private static void DrawSpriteAt(
            Rect preview,
            Sprite sprite,
            Vector2 centerGui,
            float unityEulerZ,
            bool flipX,
            bool flipY,
            float worldToGui)
        {
            if (sprite == null) return;

            // Always use sprite.texture with textureRect UVs. AssetPreview thumbnails are already
            // cropped; applying atlas UVs to them makes multi-sprite sheets (WarriorWalk) invisible.
            Texture2D tex = sprite.texture;
            if (tex == null) return;

            float ppu = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : Ppu;
            Vector2 worldSize = sprite.rect.size / ppu;
            Vector2 guiSize = worldSize * worldToGui;

            // Match SpriteRenderer: transform = sprite.pivot. Flip mirrors verts around pivot.
            float pivotGuiX = sprite.pivot.x / ppu * worldToGui;
            float pivotGuiY = sprite.pivot.y / ppu * worldToGui;
            if (flipX) pivotGuiX = guiSize.x - pivotGuiX;
            if (flipY) pivotGuiY = guiSize.y - pivotGuiY;
            // Unity pivot from rect bottom-left (Y-up); IMGUI rect origin is top-left (Y-down).
            Rect spriteRect = new Rect(
                centerGui.x - pivotGuiX,
                centerGui.y - (guiSize.y - pivotGuiY),
                guiSize.x,
                guiSize.y);

            // Clip lightly to preview
            if (!spriteRect.Overlaps(preview))
                return;

            Matrix4x4 prev = GUI.matrix;
            // Unity Z CCW (Y-up). IMGUI Y-down => negate for visual match with runtime.
            GUIUtility.RotateAroundPivot(-unityEulerZ, centerGui);

            Rect uv = GetSpriteUv(sprite);
            if (flipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            if (flipY) uv = new Rect(uv.x, uv.yMax, uv.width, -uv.height);

            GUI.DrawTextureWithTexCoords(spriteRect, tex, uv, true);
            GUI.matrix = prev;
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

        private void EnsureTable()
        {
            if (_table != null) return;
            _table = WeaponStancePoseTableAssetUtility.EnsureAsset();
            if (_table != null)
                _table.EnsurePoseArray();
        }

        private void RefreshWeapons()
        {
            string selectedPath = _previewWeapon != null ? AssetDatabase.GetAssetPath(_previewWeapon) : null;
            _weapons.Clear();
            foreach (string g in AssetDatabase.FindAssets("t:WeaponItemSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var w = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(path);
                if (w == null || w.InHandSprite == null) continue;
                _weapons.Add(w);
            }

            _weapons = _weapons.OrderBy(w => w.name).ToList();

            if (!string.IsNullOrEmpty(selectedPath))
                _previewWeapon = _weapons.FirstOrDefault(w => AssetDatabase.GetAssetPath(w) == selectedPath);

            if (_previewWeapon == null)
            {
                _previewWeapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(DebugBladePath);
                if (_previewWeapon == null || _previewWeapon.InHandSprite == null)
                    _previewWeapon = _weapons.FirstOrDefault();
            }
        }

        private void LoadBodySprite()
        {
            _bodySprite = null;
            // Prefer first frame of WarriorWalk sheet (idle = run[0] at runtime).
            UnityEngine.Object[] walk = AssetDatabase.LoadAllAssetsAtPath(BodySpritePath);
            if (walk != null)
            {
                foreach (UnityEngine.Object o in walk)
                {
                    if (o is Sprite s && s.name.EndsWith("_0", StringComparison.Ordinal))
                    {
                        _bodySprite = s;
                        break;
                    }
                }

                if (_bodySprite == null)
                {
                    foreach (UnityEngine.Object o in walk)
                    {
                        if (o is Sprite s)
                        {
                            _bodySprite = s;
                            break;
                        }
                    }
                }
            }

            if (_bodySprite == null)
                _bodySprite = AssetDatabase.LoadAssetAtPath<Sprite>(BodyFallbackPath);
        }

        private void LoadDraftFromTable()
        {
            if (_table == null) return;
            WeaponVisualController.StancePose pose = _table.GetPose(_selectedStance);
            ApplyDraft(pose);
            _dirty = false;
        }

        private void ApplyDraft(WeaponVisualController.StancePose pose)
        {
            _draftPos = pose.LocalPosition;
            _draftEuler = pose.LocalEulerZ;
            _draftFlipX = pose.FlipX;
            _draftFlipY = pose.FlipY;
            _draftBehind = pose.SortBehindCharacter;
        }

        private static WeaponVisualController.StancePose FindDefaultPose(WeaponHoldStance stance)
        {
            foreach (var p in WeaponVisualController.CreateDefaultPoseTable())
            {
                if (p.Stance == stance)
                    return p;
            }

            return WeaponVisualController.CreateDefaultPoseTable()[0];
        }

        private bool ConfirmDiscardOrSave()
        {
            if (!_dirty) return true;
            int choice = EditorUtility.DisplayDialogComplex(
                "Unsaved stance",
                $"Save pose for '{_selectedStance}' before switching?",
                "Save",
                "Cancel",
                "Discard");
            if (choice == 0)
            {
                Save();
                return true;
            }

            if (choice == 2)
            {
                _dirty = false;
                return true;
            }

            return false;
        }

        private void Save()
        {
            if (_table == null) return;

            var pose = new WeaponVisualController.StancePose
            {
                Stance = _selectedStance,
                LocalPosition = _draftPos,
                LocalEulerZ = _draftEuler,
                FlipX = _draftFlipX,
                FlipY = _draftFlipY,
                SortBehindCharacter = _draftBehind
            };

            Undo.RecordObject(_table, "Edit Stance Pose");
            _table.SetPose(pose);
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssets();
            _dirty = false;

            // Refresh any open WVC in loaded scenes / prefabs (play or edit).
            WeaponVisualController[] all = UnityEngine.Object.FindObjectsByType<WeaponVisualController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    all[i].EditorReloadPoseTable(_table);
            }
        }
    }

    /// <summary>Creates Resources/Visuals/WeaponStancePoseTable.asset once if missing.</summary>
    public static class WeaponStancePoseTableAssetUtility
    {
        [InitializeOnLoadMethod]
        private static void AutoEnsure()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                EnsureAsset();
            };
        }

        public static WeaponStancePoseTableSO EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponStancePoseTableSO>(
                WeaponStancePoseTableSO.DefaultAssetPath);
            if (existing != null)
            {
                existing.EnsurePoseArray();
                return existing;
            }

            existing = WeaponStancePoseTableSO.LoadDefault();
            if (existing != null)
            {
                existing.EnsurePoseArray();
                return existing;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Visuals");

            var so = ScriptableObject.CreateInstance<WeaponStancePoseTableSO>();
            so.ResetToDefaults();
            AssetDatabase.CreateAsset(so, WeaponStancePoseTableSO.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StanceEditor] Created " + WeaponStancePoseTableSO.DefaultAssetPath + " (v"
                      + WeaponStancePoseTableSO.CurrentVersion + " defaults).");
            return so;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
