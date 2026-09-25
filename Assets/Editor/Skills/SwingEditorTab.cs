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
    /// 2-point (Windup / Impact) HandPivot swing editor.
    /// Edits WeaponSwingStyleTableSO; SkillHandAnimation reads that SO at runtime.
    /// Preview: stance hold on WeaponHolder + swing deltas on HandPivot (same as play mode).
    /// </summary>
    public sealed class SwingEditorTab
    {
        private enum PreviewKey
        {
            Idle = 0,
            Windup = 1,
            Impact = 2
        }

        private const float PreviewSize = 320f;
        private const float Ppu = 24f;
        private static readonly Vector2 HandPivotLocal = new Vector2(-1f / 24f, 0f);
        private const string BodySpritePath = "Assets/Resources/Heroes/WarriorResources/WarriorWalk-Sheet.png";
        private const string BodyFallbackPath = "Assets/Resources/Heroes/WarriorResources/Warrioir_v0.1.png";
        private const string DebugBladePath = "Assets/Resources/Items/Debug/Debug Blade.asset";

        private static readonly WeaponSwingStyle[] EditableStyles =
        {
            WeaponSwingStyle.Slash,
            WeaponSwingStyle.LowArc,
            WeaponSwingStyle.OverheadStab
        };

        private WeaponSwingStyleTableSO _table;
        private WeaponStancePoseTableSO _poseTable;
        private WeaponHoldStance _previewStance = WeaponHoldStance.LowGuard;
        private WeaponSwingStyle _selectedStyle = WeaponSwingStyle.LowArc;
        private int _selectedIndex;
        private string _createName = "";
        private string _draftId = "";
        private string _draftDisplayName = "";
        private System.Collections.Generic.List<string> _draftAllowedStanceIds = new System.Collections.Generic.List<string>();
        private PreviewKey _previewKey = PreviewKey.Windup;
        private WeaponItemSO _previewWeapon;
        private List<WeaponItemSO> _weapons = new List<WeaponItemSO>();
        private Vector2 _listScroll;

        private float _draftWindupZ;
        private Vector2 _draftWindupPos;
        private float _draftImpactZ;
        private Vector2 _draftImpactPos;
        private bool _dirty;

        private bool _draggingPos;
        private bool _draggingRot;
        private Vector2 _dragPrevDir;
        private Sprite _bodySprite;

        private bool _playing;
        private double _playStartTime;
        private const float PlayDuration = 0.45f;

        public void OnEnable()
        {
            EnsureTables();
            RefreshWeapons();
            LoadBodySprite();
            LoadDraftFromTable();
            StopPlay();
        }

        public void OnGUI()
        {
            EnsureTables();
            TickPlay();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                EnsureTables();
                RefreshWeapons();
                LoadBodySprite();
                LoadDraftFromTable();
                StopPlay();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                "HandPivot deltas  |  hold = Stance pose on WeaponHolder  |  tip-up=0, -90 tip-fwd, +90 tip-back",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (_table == null)
            {
                EditorGUILayout.HelpBox("Could not create/load WeaponSwingStyleTableSO.", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                "Tip-up space. ImpactZ: negative ≈ tip ABOVE path (current LowArc seed −150 with LowGuard hold); positive ≈ tip BELOW (samurai arc). Windup/Impact are HandPivot-only; hold pose stays on WeaponHolder.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawStyleList(200f);
            DrawInspector();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStyleList(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label("Swing Style", EditorStyles.boldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            var styles = _table.Styles;
            if (styles != null)
            {
                for (int i = 0; i < styles.Length; i++)
                {
                    var kf = styles[i];
                    bool sel = _selectedIndex == i;
                    GUI.backgroundColor = sel ? new Color(0.5f, 0.7f, 1f) : Color.white;
                    string title = string.IsNullOrEmpty(kf.DisplayName) ? (kf.Id ?? kf.Style.ToString()) : kf.DisplayName;
                    string seed = WeaponSwingStyleTableSO.IsSeededRow(kf) ? " [seed]" : "";
                    string label = string.Format("{0}{1}\n  W {2:0.#}  ({3:0.##},{4:0.##})\n  I {5:0.#}  ({6:0.##},{7:0.##})",
                        title, seed, kf.WindupZ, kf.WindupLocalPos.x, kf.WindupLocalPos.y, kf.ImpactZ, kf.ImpactLocalPos.x, kf.ImpactLocalPos.y);
                    if (GUILayout.Button(label, GUILayout.Height(56)))
                    {
                        if (!ConfirmDiscardOrSave())
                        {
                            GUI.backgroundColor = Color.white;
                            continue;
                        }
                        _selectedIndex = i;
                        _selectedStyle = kf.Style;
                        LoadDraftFromTable();
                        StopPlay();
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.Space(6);
            _createName = EditorGUILayout.TextField("New name", _createName);
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_createName));
            if (GUILayout.Button("Create Swing"))
                CreateNamedSwing(_createName.Trim());
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.ObjectField("Swing Table", _table, typeof(WeaponSwingStyleTableSO), false);
            EditorGUILayout.LabelField($"Version {_table.Version}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "FromStance = resolver only (not stored here).",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            GUILayout.Label($"Style: {_draftDisplayName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Id", _draftId);
            EditorGUI.BeginChangeCheck();
            _draftDisplayName = EditorGUILayout.TextField("Display Name", _draftDisplayName);
            DrawAllowedStances();
            if (EditorGUI.EndChangeCheck())
                _dirty = true;

            EditorGUI.BeginChangeCheck();
            _previewStance = (WeaponHoldStance)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Preview Stance",
                    "Hold pose from WeaponStancePoseTable (WeaponHolder). Does not change swing SO."),
                _previewStance);
            if (EditorGUI.EndChangeCheck())
                StopPlay();

            WeaponSwingStyle resolved = ResolveFromStance(_previewStance);
            EditorGUILayout.LabelField(
                $"FromStance for this hold → {StyleLabel(resolved)}",
                EditorStyles.miniLabel);

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
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Preview key", GUILayout.Width(80));
            PreviewKey newKey = (PreviewKey)GUILayout.Toolbar(
                (int)_previewKey,
                new[] { "Idle", "Windup", "Impact" });
            if (newKey != _previewKey)
            {
                _previewKey = newKey;
                StopPlay();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Windup (HandPivot delta)", EditorStyles.boldLabel);
            _draftWindupPos = EditorGUILayout.Vector2Field("Windup Local Pos", _draftWindupPos);
            _draftWindupZ = EditorGUILayout.FloatField("Windup Euler Z", _draftWindupZ);
            _draftWindupZ = EditorGUILayout.Slider("Windup Rotate", _draftWindupZ, -180f, 180f);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Impact (HandPivot delta)", EditorStyles.boldLabel);
            _draftImpactPos = EditorGUILayout.Vector2Field("Impact Local Pos", _draftImpactPos);
            _draftImpactZ = EditorGUILayout.FloatField("Impact Euler Z", _draftImpactZ);
            _draftImpactZ = EditorGUILayout.Slider("Impact Rotate", _draftImpactZ, -180f, 180f);
            if (EditorGUI.EndChangeCheck())
            {
                _dirty = true;
                StopPlay();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Style Defaults", GUILayout.Width(160)))
            {
                WeaponSwingStyleTableSO.SwingKeyframes def = FindDefault(_selectedStyle);
                ApplyDraft(def);
                _dirty = true;
                StopPlay();
            }

            if (GUILayout.Button(_playing ? "Stop" : "Play Windup→Impact", GUILayout.Width(150)))
            {
                if (_playing)
                    StopPlay();
                else
                    StartPlay();
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = _dirty;
            if (GUILayout.Button("Save", GUILayout.Width(80)))
                Save();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (_dirty)
                EditorGUILayout.HelpBox("Dirty — Save writes WeaponSwingStyleTableSO (global).", MessageType.Warning);

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

            float worldToGui = side * 0.45f;
            Vector2 center = preview.center;

            GetActiveHandDelta(out Vector2 handDeltaPos, out float handDeltaZ);
            Vector2 handLocal = HandPivotLocal + handDeltaPos;
            Vector2 handGui = center + WorldToGui(handLocal, worldToGui);

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

            WeaponVisualController.StancePose hold = GetHoldPose(_previewStance);
            Sprite weaponSprite = _previewWeapon != null ? _previewWeapon.InHandSprite : null;
            float tilt = _previewWeapon != null ? _previewWeapon.InHandSpriteTiltZ : 0f;

            // WeaponHolder local → rotate by HandPivot delta (child of HandPivot).
            Vector2 holdRotated = Rotate(hold.LocalPosition, handDeltaZ);
            Vector2 weaponGui = handGui + WorldToGui(holdRotated, worldToGui);
            float finalEuler = hold.LocalEulerZ + handDeltaZ - tilt;

            Action drawBody = () => DrawSpriteAt(preview, _bodySprite, center, 0f, false, false, worldToGui);
            Action drawWeapon = () =>
            {
                if (weaponSprite == null) return;
                DrawSpriteAt(
                    preview,
                    weaponSprite,
                    weaponGui,
                    finalEuler,
                    hold.FlipX,
                    hold.FlipY,
                    worldToGui);
            };

            if (hold.SortBehindCharacter)
            {
                drawWeapon();
                drawBody();
            }
            else
            {
                drawBody();
                drawWeapon();
            }

            // Idle HandPivot ghost (cyan) when editing windup/impact
            if (_previewKey != PreviewKey.Idle)
            {
                Vector2 idleGui = center + WorldToGui(HandPivotLocal, worldToGui);
                EditorGUI.DrawRect(
                    new Rect(idleGui.x - 2f, idleGui.y - 2f, 4f, 4f),
                    new Color(0.3f, 0.85f, 1f, 0.55f));
            }

            // Active HandPivot (yellow)
            EditorGUI.DrawRect(new Rect(handGui.x - 3f, handGui.y - 3f, 6f, 6f), new Color(1f, 0.85f, 0.2f, 0.95f));

            // Other key ghost (magenta)
            if (_previewKey == PreviewKey.Windup || _previewKey == PreviewKey.Impact)
            {
                Vector2 otherPos = _previewKey == PreviewKey.Windup ? _draftImpactPos : _draftWindupPos;
                float otherZ = _previewKey == PreviewKey.Windup ? _draftImpactZ : _draftWindupZ;
                Vector2 otherHand = HandPivotLocal + otherPos;
                Vector2 otherGui = center + WorldToGui(otherHand, worldToGui);
                EditorGUI.DrawRect(
                    new Rect(otherGui.x - 2f, otherGui.y - 2f, 4f, 4f),
                    new Color(1f, 0.35f, 0.85f, 0.7f));
                Handles.BeginGUI();
                Handles.color = new Color(1f, 0.35f, 0.85f, 0.35f);
                Handles.DrawLine(
                    new Vector3(handGui.x, handGui.y),
                    new Vector3(otherGui.x, otherGui.y));
                Handles.EndGUI();
                _ = otherZ; // rotation ghost omitted (position path is enough)
            }

            string keyLabel = _playing ? $"Play t={GetPlayT():0.00}" : _previewKey.ToString();
            GUI.Label(
                new Rect(preview.x, preview.yMax + 2f, preview.width, 22f),
                $"LMB empty = move HandPivot  |  LMB near weapon = rotate HandPivot  |  yellow=HandPivot  |  {keyLabel}",
                EditorStyles.centeredGreyMiniLabel);

            if (!_playing && _previewKey != PreviewKey.Idle)
                HandlePreviewInput(preview, handGui, weaponGui, worldToGui, weaponSprite != null);
        }

        private void HandlePreviewInput(
            Rect preview,
            Vector2 handGui,
            Vector2 weaponGui,
            float worldToGui,
            bool hasWeapon)
        {
            Event e = Event.current;
            if (!preview.Contains(e.mousePosition)
                && e.type != EventType.MouseUp
                && e.type != EventType.MouseDrag)
                return;

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
                    Vector2 deltaWorld = new Vector2(deltaGui.x / worldToGui, -deltaGui.y / worldToGui);
                    if (_previewKey == PreviewKey.Windup)
                        _draftWindupPos += deltaWorld;
                    else if (_previewKey == PreviewKey.Impact)
                        _draftImpactPos += deltaWorld;
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
                        // IMGUI Y-down: negate so clockwise drag matches Unity Z.
                        float apply = -delta;
                        if (_previewKey == PreviewKey.Windup)
                            _draftWindupZ = Mathf.DeltaAngle(0f, _draftWindupZ + apply);
                        else if (_previewKey == PreviewKey.Impact)
                            _draftImpactZ = Mathf.DeltaAngle(0f, _draftImpactZ + apply);
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

        private void GetActiveHandDelta(out Vector2 pos, out float z)
        {
            if (_playing)
            {
                float t = GetPlayT();
                pos = Vector2.Lerp(_draftWindupPos, _draftImpactPos, t);
                z = Mathf.LerpAngle(_draftWindupZ, _draftImpactZ, t);
                return;
            }

            switch (_previewKey)
            {
                case PreviewKey.Windup:
                    pos = _draftWindupPos;
                    z = _draftWindupZ;
                    break;
                case PreviewKey.Impact:
                    pos = _draftImpactPos;
                    z = _draftImpactZ;
                    break;
                default:
                    pos = Vector2.zero;
                    z = 0f;
                    break;
            }
        }

        private void StartPlay()
        {
            _playing = true;
            _playStartTime = EditorApplication.timeSinceStartup;
            _previewKey = PreviewKey.Windup;
        }

        private void StopPlay()
        {
            _playing = false;
        }

        private float GetPlayT()
        {
            if (!_playing) return 0f;
            return Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - _playStartTime) / PlayDuration));
        }

        private void TickPlay()
        {
            if (!_playing) return;
            if (GetPlayT() >= 1f)
            {
                _previewKey = PreviewKey.Impact;
                _playing = false;
            }

            // Keep repainting while lerping.
            var window = EditorWindow.focusedWindow;
            if (window != null)
                window.Repaint();
            else
                EditorWindow.GetWindow<SkillEditorWindow>()?.Repaint();
        }

        private WeaponVisualController.StancePose GetHoldPose(WeaponHoldStance stance)
        {
            if (_poseTable != null)
                return _poseTable.GetPose(stance);
            return FindDefaultHold(stance);
        }

        private static WeaponVisualController.StancePose FindDefaultHold(WeaponHoldStance stance)
        {
            foreach (var p in WeaponVisualController.CreateDefaultPoseTable())
            {
                if (p.Stance == stance)
                    return p;
            }

            return WeaponVisualController.CreateDefaultPoseTable()[0];
        }

        private static Vector2 Rotate(Vector2 v, float unityEulerZ)
        {
            float rad = unityEulerZ * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
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

            Texture2D tex = sprite.texture;
            if (tex == null) return;

            float ppu = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : Ppu;
            Vector2 worldSize = sprite.rect.size / ppu;
            Vector2 guiSize = worldSize * worldToGui;

            float pivotGuiX = sprite.pivot.x / ppu * worldToGui;
            float pivotGuiY = sprite.pivot.y / ppu * worldToGui;
            if (flipX) pivotGuiX = guiSize.x - pivotGuiX;
            if (flipY) pivotGuiY = guiSize.y - pivotGuiY;

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


        private void DrawAllowedStances()
        {
            EnsureTables();
            EditorGUILayout.LabelField("Allowed Stances", EditorStyles.boldLabel);
            var poses = _poseTable != null ? _poseTable.Poses : null;
            if (poses == null || poses.Length == 0)
            {
                EditorGUILayout.HelpBox("No stances in pose table.", MessageType.Info);
                return;
            }

            for (int i = 0; i < poses.Length; i++)
            {
                string sid = poses[i].Id;
                if (string.IsNullOrEmpty(sid)) continue;
                string label = string.IsNullOrEmpty(poses[i].DisplayName) ? sid : poses[i].DisplayName;
                bool on = _draftAllowedStanceIds.Contains(sid);
                bool next = EditorGUILayout.ToggleLeft(label + " (" + sid + ")", on);
                if (next && !on) _draftAllowedStanceIds.Add(sid);
                if (!next && on) _draftAllowedStanceIds.Remove(sid);
            }
        }

        private void CreateNamedSwing(string displayName)
        {
            EnsureTables();
            string baseId = SlugifySwing(displayName);
            if (string.IsNullOrEmpty(baseId)) baseId = "swing";
            string id = baseId;
            int n = 2;
            while (_table.IndexOfId(id) >= 0)
            {
                id = baseId + "_" + n;
                n++;
            }

            var seed = FindDefault(WeaponSwingStyle.Slash);
            var kf = new WeaponSwingStyleTableSO.SwingKeyframes
            {
                Style = WeaponSwingStyle.Slash,
                Id = id,
                DisplayName = displayName,
                AllowedStanceIds = new string[0],
                WindupZ = seed.WindupZ,
                WindupLocalPos = seed.WindupLocalPos,
                ImpactZ = seed.ImpactZ,
                ImpactLocalPos = seed.ImpactLocalPos
            };

            Undo.RecordObject(_table, "Create Swing");
            if (!_table.Add(kf))
            {
                EditorUtility.DisplayDialog("Create Swing", "Failed to add swing.", "OK");
                return;
            }
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssets();
            _selectedIndex = _table.IndexOfId(id);
            _selectedStyle = kf.Style;
            _createName = "";
            LoadDraftFromTable();
            StopPlay();
            _dirty = false;
        }

        private static string SlugifySwing(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in name.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if ((c == ' ' || c == '-' || c == '_') && sb.Length > 0 && sb[sb.Length - 1] != '_')
                    sb.Append('_');
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == '_') sb.Length--;
            return sb.ToString();
        }

        private void EnsureTables()
        {
            if (_table == null)
            {
                _table = WeaponSwingStyleTableAssetUtility.EnsureAsset();
                if (_table != null)
                    _table.EnsureStyleArray();
            }

            if (_poseTable == null)
            {
                _poseTable = WeaponStancePoseTableAssetUtility.EnsureAsset();
                if (_poseTable != null)
                    _poseTable.EnsurePoseArray();
            }
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
            if (_table.Styles == null || _table.Styles.Length == 0)
                _table.EnsureStyleArray();
            if (_selectedIndex < 0 || _selectedIndex >= _table.Styles.Length)
                _selectedIndex = 0;
            var kf = _table.Styles[_selectedIndex];
            _selectedStyle = kf.Style;
            ApplyDraft(kf);
            _dirty = false;
        }

        private void ApplyDraft(WeaponSwingStyleTableSO.SwingKeyframes kf)
        {
            _draftId = kf.Id ?? "";
            _draftDisplayName = string.IsNullOrEmpty(kf.DisplayName) ? (kf.Id ?? kf.Style.ToString()) : kf.DisplayName;
            _draftAllowedStanceIds = new System.Collections.Generic.List<string>();
            if (kf.AllowedStanceIds != null)
                _draftAllowedStanceIds.AddRange(kf.AllowedStanceIds);
            _draftWindupZ = kf.WindupZ;
            _draftWindupPos = kf.WindupLocalPos;
            _draftImpactZ = kf.ImpactZ;
            _draftImpactPos = kf.ImpactLocalPos;
        }

        private static WeaponSwingStyleTableSO.SwingKeyframes FindDefault(WeaponSwingStyle style)
        {
            foreach (var k in WeaponSwingStyleTableSO.CreateDefaultStyleTable())
            {
                if (k.Style == style)
                    return k;
            }

            return WeaponSwingStyleTableSO.CreateDefaultStyleTable()[0];
        }

        private static WeaponSwingStyle ResolveFromStance(WeaponHoldStance stance)
        {
            // Mirrors WeaponSwingStyleResolver.Resolve HoldStance defaults (no SkillDataSO needed).
            switch (stance)
            {
                case WeaponHoldStance.LowGuard:
                    return WeaponSwingStyle.LowArc;
                case WeaponHoldStance.Dagger:
                    return WeaponSwingStyle.OverheadStab;
                default:
                    return WeaponSwingStyle.Slash;
            }
        }

        private static string StyleLabel(WeaponSwingStyle style)
        {
            switch (style)
            {
                case WeaponSwingStyle.Slash: return "Slash";
                case WeaponSwingStyle.LowArc: return "Low Arc";
                case WeaponSwingStyle.OverheadStab: return "Overhead Stab";
                case WeaponSwingStyle.FromStance: return "From Stance";
                default: return style.ToString();
            }
        }

        private bool ConfirmDiscardOrSave()
        {
            if (!_dirty) return true;
            int choice = EditorUtility.DisplayDialogComplex(
                "Unsaved swing",
                $"Save keyframes for '{StyleLabel(_selectedStyle)}' before switching?",
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

            var existing = _table.Styles[_selectedIndex];
            var kf = new WeaponSwingStyleTableSO.SwingKeyframes
            {
                Style = existing.Style,
                Id = string.IsNullOrEmpty(_draftId) ? existing.Id : _draftId,
                DisplayName = string.IsNullOrEmpty(_draftDisplayName) ? existing.DisplayName : _draftDisplayName,
                AllowedStanceIds = _draftAllowedStanceIds.ToArray(),
                WindupZ = _draftWindupZ,
                WindupLocalPos = _draftWindupPos,
                ImpactZ = _draftImpactZ,
                ImpactLocalPos = _draftImpactPos
            };

            Undo.RecordObject(_table, "Edit Swing Style");
            _table.SetAt(_selectedIndex, kf);
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssets();
            _dirty = false;
        }
    }

    /// <summary>Creates Resources/Visuals/WeaponSwingStyleTable.asset once if missing.</summary>
    public static class WeaponSwingStyleTableAssetUtility
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

        public static WeaponSwingStyleTableSO EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponSwingStyleTableSO>(
                WeaponSwingStyleTableSO.DefaultAssetPath);
            if (existing != null)
            {
                existing.EnsureStyleArray();
                return existing;
            }

            existing = WeaponSwingStyleTableSO.LoadDefault();
            if (existing != null)
            {
                existing.EnsureStyleArray();
                return existing;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Visuals");

            var so = ScriptableObject.CreateInstance<WeaponSwingStyleTableSO>();
            so.ResetToDefaults();
            AssetDatabase.CreateAsset(so, WeaponSwingStyleTableSO.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SwingEditor] Created " + WeaponSwingStyleTableSO.DefaultAssetPath + " (v"
                      + WeaponSwingStyleTableSO.CurrentVersion + " defaults).");
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
