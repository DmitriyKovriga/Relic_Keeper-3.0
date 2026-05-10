using System.Collections.Generic;
using System.Linq;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.StatusEffects
{
    public sealed class StatusEffectsEditorWindow : EditorWindow
    {
        private readonly List<StatusEffectSO> _effects = new List<StatusEffectSO>();
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private string _search = string.Empty;
        private int _kindFilterIndex;
        private int _selectedIndex = -1;
        private StatusEffectsHudSettingsSO _hudSettings;

        [MenuItem("Tools/Status Effects Editor")]
        public static void Open()
        {
            var window = GetWindow<StatusEffectsEditorWindow>();
            window.titleContent = new GUIContent("Status Effects");
            window.minSize = new Vector2(980f, 620f);
            window.Refresh();
        }

        private void OnEnable()
        {
            EnsureHudSettingsAsset();
            Refresh();
        }

        private void Refresh()
        {
            _effects.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:StatusEffectSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StatusEffectSO asset = AssetDatabase.LoadAssetAtPath<StatusEffectSO>(path);
                if (asset != null)
                    _effects.Add(asset);
            }

            _effects.Sort((a, b) =>
            {
                int kindCompare = a.Kind.CompareTo(b.Kind);
                if (kindCompare != 0)
                    return kindCompare;

                return string.Compare(a.GetDisplayName(preferRu: false), b.GetDisplayName(preferRu: false), System.StringComparison.OrdinalIgnoreCase);
            });

            if (_selectedIndex >= _effects.Count)
                _selectedIndex = _effects.Count - 1;
            if (_selectedIndex < 0 && _effects.Count > 0)
                _selectedIndex = 0;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", GUILayout.Width(42f));
            string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.Width(220f));
            if (newSearch != _search)
                _search = newSearch;

            GUILayout.Space(8f);
            GUILayout.Label("Kind", GUILayout.Width(26f));
            _kindFilterIndex = EditorGUILayout.Popup(_kindFilterIndex, new[] { "All", "Buff", "Debuff" }, EditorStyles.toolbarPopup, GUILayout.Width(110f));

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Buff", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                CreateEffectAsset(StatusEffectKind.Buff);
            if (GUILayout.Button("Create Debuff", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                CreateEffectAsset(StatusEffectKind.Debuff);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                Refresh();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(320f));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            List<StatusEffectSO> filtered = GetFilteredEffects();
            for (int i = 0; i < filtered.Count; i++)
            {
                StatusEffectSO effect = filtered[i];
                bool selected = _selectedIndex >= 0 && _selectedIndex < _effects.Count && _effects[_selectedIndex] == effect;
                GUIStyle style = selected ? EditorStyles.helpBox : EditorStyles.miniButton;
                string label = $"{effect.GetDisplayName()}  [{effect.Kind}]";
                if (GUILayout.Button(label, style, GUILayout.Height(28f)))
                    _selectedIndex = _effects.IndexOf(effect);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            DrawHudSettingsPanel();

            if (_selectedIndex < 0 || _selectedIndex >= _effects.Count)
            {
                EditorGUILayout.HelpBox("Select an effect on the left or create a new one.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            StatusEffectSO effect = _effects[_selectedIndex];
            if (effect == null)
            {
                EditorGUILayout.HelpBox("The selected asset no longer exists. Press Refresh.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            SerializedObject so = new SerializedObject(effect);
            so.Update();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Id"));
            EditorGUILayout.PropertyField(so.FindProperty("Kind"));
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("NameEn"));
            EditorGUILayout.PropertyField(so.FindProperty("NameRu"));
            EditorGUILayout.PropertyField(so.FindProperty("DescriptionEn"));
            EditorGUILayout.PropertyField(so.FindProperty("DescriptionRu"));
            EditorGUILayout.PropertyField(so.FindProperty("Icon"));
            EditorGUILayout.PropertyField(so.FindProperty("ShowInHud"));
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("BaseDurationSeconds"));
            EditorGUILayout.PropertyField(so.FindProperty("Modifiers"), new GUIContent("Direct stat modifiers"), true);
            EditorGUILayout.PropertyField(so.FindProperty("DerivedModifiers"), new GUIContent("Derived stat modifiers"), true);
            DrawEventReactions(so);
            EditorGUILayout.Space(8f);

            DrawValidation(effect);
            EditorGUILayout.Space(10f);
            DrawActions(effect, so);

            EditorGUILayout.EndScrollView();

            so.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        private void DrawHudSettingsPanel()
        {
            StatusEffectsHudSettingsSO settings = EnsureHudSettingsAsset();
            if (settings == null)
                return;

            SerializedObject settingsSo = new SerializedObject(settings);
            settingsSo.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("HUD Display", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Global buff/debuff HUD icon layout. Change size and spacing here, then test it in play mode.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(settingsSo.FindProperty("IconSizePixels"), new GUIContent("Icon Size (px)"));
            EditorGUILayout.PropertyField(settingsSo.FindProperty("IconSpacingPixels"), new GUIContent("Icon Spacing (px)"));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset to 5x5"))
            {
                settingsSo.FindProperty("IconSizePixels").floatValue = 5f;
                settingsSo.FindProperty("IconSpacingPixels").floatValue = 0f;
            }

            if (GUILayout.Button("Ping HUD Settings"))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                settingsSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            else
            {
                settingsSo.ApplyModifiedProperties();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6f);
        }

        private void DrawValidation(StatusEffectSO effect)
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (string.IsNullOrWhiteSpace(effect.Id))
                EditorGUILayout.HelpBox("It is better to fill Id so the effect is easier to find and maintain.", MessageType.Info);

            if (effect.BaseDurationSeconds <= 0f)
                EditorGUILayout.HelpBox("Duration must be greater than 0 seconds.", MessageType.Warning);

            if (effect.Modifiers == null || effect.Modifiers.Count == 0)
            {
                bool hasDerived = effect.DerivedModifiers != null && effect.DerivedModifiers.Count > 0;
                bool hasReactions = effect.EventReactions != null && effect.EventReactions.Count > 0;
                if (!hasDerived && !hasReactions)
                    EditorGUILayout.HelpBox("This effect currently does not modify stats and has no event reactions. That is okay only for purely visual/system statuses.", MessageType.Info);
            }

            if (effect.ShowInHud && effect.Icon == null)
                EditorGUILayout.HelpBox("Show In HUD is enabled, but no icon is assigned.", MessageType.Warning);
        }

        private void DrawActions(StatusEffectSO effect, SerializedObject so)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open in Inspector"))
            {
                Selection.activeObject = effect;
                EditorGUIUtility.PingObject(effect);
            }

            if (GUILayout.Button("Save"))
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(effect);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEventReactions(SerializedObject so)
        {
            SerializedProperty reactions = so.FindProperty("EventReactions");
            if (reactions == null)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Event reactions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Reactions are evaluated while this status is active. Example: DamageTaken + CarrierAsTarget + EndCurrentEffect = buff works until the carrier takes damage.", MessageType.None);

            for (int i = 0; i < reactions.arraySize; i++)
            {
                SerializedProperty reaction = reactions.GetArrayElementAtIndex(i);
                SerializedProperty action = reaction.FindPropertyRelative("Action");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Reaction #{i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                {
                    reactions.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(reaction.FindPropertyRelative("EventType"), new GUIContent("Event"));
                EditorGUILayout.PropertyField(reaction.FindPropertyRelative("Subject"), new GUIContent("Who must be involved"));
                EditorGUILayout.PropertyField(action, new GUIContent("Action"));

                var actionValue = (StatusEventReactionAction)action.enumValueIndex;
                switch (actionValue)
                {
                    case StatusEventReactionAction.ApplyStatusEffect:
                        EditorGUILayout.PropertyField(reaction.FindPropertyRelative("StatusEffectToApply"), new GUIContent("Status to apply"));
                        break;
                    case StatusEventReactionAction.ApplyQuickEffect:
                        EditorGUILayout.PropertyField(reaction.FindPropertyRelative("QuickEffectKind"), new GUIContent("Quick effect kind"));
                        EditorGUILayout.PropertyField(reaction.FindPropertyRelative("QuickEffectDurationSeconds"), new GUIContent("Duration seconds"));
                        EditorGUILayout.PropertyField(reaction.FindPropertyRelative("QuickModifiers"), new GUIContent("Direct quick modifiers"), true);
                        EditorGUILayout.PropertyField(reaction.FindPropertyRelative("QuickDerivedModifiers"), new GUIContent("Derived quick modifiers"), true);
                        break;
                    case StatusEventReactionAction.ExtendCurrentEffect:
                        EditorGUILayout.PropertyField(reaction.FindPropertyRelative("ExtendSeconds"), new GUIContent("Extend by seconds"));
                        break;
                    case StatusEventReactionAction.EndCurrentEffect:
                        EditorGUILayout.HelpBox("Ends this status immediately when the selected event happens.", MessageType.None);
                        break;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add event reaction"))
            {
                int index = reactions.arraySize;
                reactions.InsertArrayElementAtIndex(index);
                SerializedProperty reaction = reactions.GetArrayElementAtIndex(index);
                reaction.FindPropertyRelative("EventType").enumValueIndex = 0;
                reaction.FindPropertyRelative("Subject").enumValueIndex = (int)StatusEventSubject.CarrierAsTarget;
                reaction.FindPropertyRelative("Action").enumValueIndex = (int)StatusEventReactionAction.EndCurrentEffect;
                reaction.FindPropertyRelative("QuickEffectDurationSeconds").floatValue = 3f;
                reaction.FindPropertyRelative("ExtendSeconds").floatValue = 1f;
            }
        }

        private List<StatusEffectSO> GetFilteredEffects()
        {
            IEnumerable<StatusEffectSO> query = _effects;
            if (!string.IsNullOrWhiteSpace(_search))
            {
                string search = _search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.Id ?? string.Empty).ToLowerInvariant().Contains(search) ||
                    (x.NameEn ?? string.Empty).ToLowerInvariant().Contains(search) ||
                    (x.NameRu ?? string.Empty).ToLowerInvariant().Contains(search) ||
                    x.name.ToLowerInvariant().Contains(search));
            }

            if (_kindFilterIndex == 1)
                query = query.Where(x => x.Kind == StatusEffectKind.Buff);
            else if (_kindFilterIndex == 2)
                query = query.Where(x => x.Kind == StatusEffectKind.Debuff);

            return query.ToList();
        }

        private void CreateEffectAsset(StatusEffectKind kind)
        {
            string defaultName = kind == StatusEffectKind.Buff ? "NewBuff" : "NewDebuff";
            string path = EditorUtility.SaveFilePanelInProject("Create Status Effect", defaultName, "asset", "Choose a path for the new StatusEffectSO", EditorPaths.StatusEffectsFolder);
            if (string.IsNullOrEmpty(path))
                return;

            EnsureFolder(EditorPaths.StatusEffectsFolder);

            var asset = CreateInstance<StatusEffectSO>();
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.Id = fileName;
            asset.Kind = kind;
            asset.NameEn = ObjectNames.NicifyVariableName(fileName);
            asset.NameRu = asset.NameEn;
            asset.BaseDurationSeconds = 5f;

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Refresh();
            _selectedIndex = _effects.IndexOf(asset);
            Selection.activeObject = asset;
        }

        private StatusEffectsHudSettingsSO EnsureHudSettingsAsset()
        {
            if (_hudSettings != null)
                return _hudSettings;

            _hudSettings = AssetDatabase.LoadAssetAtPath<StatusEffectsHudSettingsSO>(EditorPaths.StatusEffectsHudSettingsAsset);
            if (_hudSettings != null)
                return _hudSettings;

            EnsureFolder(EditorPaths.StatusEffectsFolder);

            _hudSettings = CreateInstance<StatusEffectsHudSettingsSO>();
            _hudSettings.IconSizePixels = 5f;
            _hudSettings.IconSpacingPixels = 0f;
            AssetDatabase.CreateAsset(_hudSettings, EditorPaths.StatusEffectsHudSettingsAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return _hudSettings;
        }

        private static void EnsureFolder(string folderPath)
        {
            string normalized = folderPath.Replace("\\", "/");
            string[] parts = normalized.Split('/');
            if (parts.Length < 2)
                return;

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
