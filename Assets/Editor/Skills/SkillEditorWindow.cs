using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Modules;
using Scripts.StatusEffects;
using Scripts.Stats;

namespace Scripts.Editor.Skills
{
    public class SkillEditorWindow : EditorWindow
    {
        private List<SkillRecipeSO> _recipes = new List<SkillRecipeSO>();
        private List<SkillDataSO> _skills = new List<SkillDataSO>();
        private List<SkillPoolSO> _skillPools = new List<SkillPoolSO>();
        private List<StepDefinitionSO> _stepDefs = new List<StepDefinitionSO>();
        private int _selectedSkillIndex;
        private int _selectedSkillPoolIndex;
        private bool _displayRu = true;
        private Vector2 _stepsInSkillScroll;
        private Vector2 _typesScroll;
        private Vector2 _inspectorScroll;
        private Vector2 _skillPoolsListScroll;
        private Vector2 _skillPoolInspectorScroll;
        private int _selectedStepIndex = -1;
        private int _selectedSubStepIndex = -1;
        private EditorTab _editorTab = EditorTab.Skills;
        private InspectorMode _inspectorMode = InspectorMode.Step;
        private SkillPoolSO _skillPoolUsageCacheTarget;
        private List<string> _skillPoolUsageCache = new List<string>();
        private StringTableCollection _skillsLabelsCollection;
        private string _skillLocNameEn = string.Empty;
        private string _skillLocNameRu = string.Empty;
        private string _skillLocDescEn = string.Empty;
        private string _skillLocDescRu = string.Empty;
        private string _lastLoadedSkillLocalizationState = string.Empty;
        private const float LeftColFraction = 0.30f;
        private const float CenterColFraction = 0.40f;
        private const float RightColFraction = 0.30f;

        private enum InspectorMode
        {
            Step,
            Base
        }

        private enum EditorTab
        {
            Skills,
            SkillPools
        }

        [MenuItem("Tools/Skill Editor")]
        public static void Open()
        {
            var w = GetWindow<SkillEditorWindow>();
            w.titleContent = new GUIContent("Skill Editor");
        }

        private void OnEnable()
        {
            if (_skillsLabelsCollection == null)
                _skillsLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.SkillsLabelsTable);

            EnsureBuiltInStepDefinitions();
            Refresh();
        }

        private void Refresh()
        {
            _recipes.Clear();
            _skills.Clear();
            _skillPools.Clear();
            _stepDefs.Clear();
            _skillPoolUsageCacheTarget = null;
            _skillPoolUsageCache.Clear();
            foreach (var g in AssetDatabase.FindAssets("t:SkillRecipeSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var r = AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(path);
                if (r != null) _recipes.Add(r);
            }
            foreach (var g in AssetDatabase.FindAssets("t:SkillDataSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var s = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);
                if (s != null) _skills.Add(s);
            }
            foreach (var g in AssetDatabase.FindAssets("t:SkillPoolSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var p = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(path);
                if (p != null) _skillPools.Add(p);
            }
            foreach (var g in AssetDatabase.FindAssets("t:StepDefinitionSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var d = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(path);
                if (d != null) _stepDefs.Add(d);
            }
            _recipes = _recipes.OrderBy(x => x.name).ToList();
            _skills = _skills.OrderBy(x => x.name).ToList();
            _skillPools = _skillPools.OrderBy(x => x.name).ToList();
            _stepDefs = _stepDefs.OrderBy(x => x.GetDisplayName(false)).ToList();
            _selectedSkillIndex = Mathf.Clamp(_selectedSkillIndex, 0, Mathf.Max(0, _skills.Count - 1));
            _selectedSkillPoolIndex = Mathf.Clamp(_selectedSkillPoolIndex, 0, Mathf.Max(0, _skillPools.Count - 1));
            _selectedStepIndex = Mathf.Clamp(_selectedStepIndex, -1, Mathf.Max(-1, _skills.Count > 0 && _selectedSkillIndex >= 0 && _selectedSkillIndex < _skills.Count && _skills[_selectedSkillIndex] != null && _skills[_selectedSkillIndex].Recipe != null && _skills[_selectedSkillIndex].Recipe.Steps != null ? _skills[_selectedSkillIndex].Recipe.Steps.Count - 1 : -1));
            _selectedSubStepIndex = Mathf.Max(-1, _selectedSubStepIndex);
        }

        private void ResetInspectorInputState(bool resetSubStepSelection = false)
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            if (resetSubStepSelection)
                _selectedSubStepIndex = -1;
        }

        private void SelectSkill(int newSkillIndex)
        {
            if (newSkillIndex == _selectedSkillIndex)
                return;

            ResetInspectorInputState(resetSubStepSelection: true);
            _selectedSkillIndex = newSkillIndex;
            _selectedStepIndex = -1;
        }

        private void SelectStep(int stepIndex)
        {
            if (stepIndex == _selectedStepIndex)
                return;

            ResetInspectorInputState(resetSubStepSelection: true);
            _selectedStepIndex = stepIndex;
            _inspectorMode = InspectorMode.Step;
        }

        private void SelectSubStep(int subStepIndex)
        {
            if (subStepIndex == _selectedSubStepIndex)
                return;

            ResetInspectorInputState();
            _selectedSubStepIndex = subStepIndex;
        }

        private void OnGUI()
        {
            int tab = GUILayout.Toolbar(_editorTab == EditorTab.Skills ? 0 : 1, new[] { "Skills", "Skill Pools" });
            _editorTab = tab == 0 ? EditorTab.Skills : EditorTab.SkillPools;
            EditorGUILayout.Space(2);

            if (_editorTab == EditorTab.SkillPools)
            {
                DrawSkillPoolsTab();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Skill", GUILayout.Width(36));
            var skillNames = _skills.Select(s => s.SkillName ?? s.name).ToArray();
            if (skillNames.Length > 0)
            {
                int newSelectedSkillIndex = EditorGUILayout.Popup(_selectedSkillIndex, skillNames);
                if (newSelectedSkillIndex != _selectedSkillIndex)
                    SelectSkill(newSelectedSkillIndex);
            }
            else
            {
                EditorGUILayout.LabelField("No skills found");
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60))) { ResetInspectorInputState(resetSubStepSelection: true); Refresh(); }
            if (GUILayout.Button("Create Skill", GUILayout.Width(96))) CreateNewSkill();
            EditorGUILayout.EndHorizontal();

            SkillDataSO skill = _selectedSkillIndex >= 0 && _selectedSkillIndex < _skills.Count ? _skills[_selectedSkillIndex] : null;
            SkillRecipeSO recipe = skill != null ? skill.Recipe : null;

            if (skill == null)
            {
                EditorGUILayout.HelpBox("Select a skill or create Skill Data + Recipe.", MessageType.Info);
                return;
            }

            if (recipe == null)
            {
                EditorGUILayout.HelpBox($"Skill '{skill.SkillName}' has no Recipe. Assign Recipe in the asset or create one.", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create Recipe", GUILayout.Width(160), GUILayout.Height(28)))
                {
                    recipe = CreateRecipeForSkill(skill);
                    if (recipe != null)
                    {
                        skill.Recipe = recipe;
                        EditorUtility.SetDirty(skill);
                        AssetDatabase.SaveAssets();
                        Refresh();
                        _selectedSkillIndex = Mathf.Clamp(_skills.IndexOf(skill), 0, Mathf.Max(0, _skills.Count - 1));
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (recipe.Steps == null) recipe.Steps = new List<StepEntry>();

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Names", GUILayout.Width(40));
            int lang = GUILayout.Toolbar(_displayRu ? 0 : 1, new[] { "RU", "EN" });
            _displayRu = (lang == 0);
            EditorGUILayout.EndHorizontal();

            float w = position.width;
            float leftW = Mathf.Max(120f, w * LeftColFraction);
            float centerW = Mathf.Max(160f, w * CenterColFraction);
            float rightW = Mathf.Max(160f, w * RightColFraction);

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            DrawLeftColumn(recipe, leftW);
            DrawCenterColumn(recipe, centerW);
            DrawRightColumn(skill, recipe, rightW);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSkillPoolsTab()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                Refresh();
            if (GUILayout.Button("Create Skill Pool", EditorStyles.toolbarButton, GUILayout.Width(120)))
                CreateNewSkillPool();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_skillPools.Count == 0)
            {
                EditorGUILayout.HelpBox("No Skill Pool assets found. Create a new pool to start binding skills to equipment pools.", MessageType.Info);
                return;
            }

            float listW = Mathf.Clamp(position.width * 0.32f, 260f, 420f);
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawSkillPoolsList(listW);
            DrawSkillPoolInspector();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSkillPoolsList(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            GUILayout.Label($"Skill Pools ({_skillPools.Count})", EditorStyles.boldLabel);
            _skillPoolsListScroll = EditorGUILayout.BeginScrollView(_skillPoolsListScroll, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _skillPools.Count; i++)
            {
                var pool = _skillPools[i];
                if (pool == null) continue;

                bool selected = i == _selectedSkillPoolIndex;
                var oldColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.45f, 0.55f, 0.75f);

                int count = pool.PossibleSkills != null ? pool.PossibleSkills.Count : 0;
                string label = $"{pool.name}  ({count})";
                if (GUILayout.Button(label, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Height(24)))
                {
                    _selectedSkillPoolIndex = i;
                    _skillPoolUsageCacheTarget = null;
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = oldColor;
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(pool), EditorStyles.miniLabel);
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSkillPoolInspector()
        {
            SkillPoolSO pool = _selectedSkillPoolIndex >= 0 && _selectedSkillPoolIndex < _skillPools.Count
                ? _skillPools[_selectedSkillPoolIndex]
                : null;

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _skillPoolInspectorScroll = EditorGUILayout.BeginScrollView(_skillPoolInspectorScroll, GUILayout.ExpandHeight(true));

            if (pool == null)
            {
                EditorGUILayout.HelpBox("Select a Skill Pool from the left list.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            DrawSkillPoolHeader(pool);
            EditorGUILayout.Space(6);
            DrawSkillPoolEntries(pool);
            EditorGUILayout.Space(8);
            DrawSkillPoolUsages(pool);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSkillPoolHeader(SkillPoolSO pool)
        {
            GUILayout.Label("Pool", EditorStyles.boldLabel);
            string path = AssetDatabase.GetAssetPath(pool);

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.DelayedTextField("Asset Name", pool.name);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(newName) && newName != pool.name)
            {
                string renameError = AssetDatabase.RenameAsset(path, SanitizeFileName(newName));
                if (!string.IsNullOrEmpty(renameError))
                    EditorUtility.DisplayDialog("Rename Skill Pool", renameError, "OK");
                AssetDatabase.SaveAssets();
                Refresh();
                _selectedSkillPoolIndex = Mathf.Clamp(_skillPools.IndexOf(pool), 0, Mathf.Max(0, _skillPools.Count - 1));
                return;
            }

            EditorGUILayout.LabelField("Path", path);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping Pool", GUILayout.Width(120)))
            {
                Selection.activeObject = pool;
                EditorGUIUtility.PingObject(pool);
            }

            if (GUILayout.Button("Save", GUILayout.Width(120)))
            {
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssets();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSkillPoolEntries(SkillPoolSO pool)
        {
            if (pool.PossibleSkills == null)
            {
                Undo.RecordObject(pool, "Initialize Skill Pool");
                pool.PossibleSkills = new List<SkillPoolSO.SkillWeight>();
                EditorUtility.SetDirty(pool);
            }

            int totalWeight = pool.PossibleSkills.Sum(x => Mathf.Max(0, x.Weight));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Skills in pool ({pool.PossibleSkills.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Skill", GUILayout.Width(100)))
            {
                Undo.RecordObject(pool, "Add Skill To Pool");
                pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = null, Weight = 1 });
                EditorUtility.SetDirty(pool);
            }
            EditorGUILayout.EndHorizontal();

            if (pool.PossibleSkills.Count == 0)
            {
                EditorGUILayout.HelpBox("Pool is empty. Add Skill entries and set positive weights.", MessageType.Info);
                return;
            }

            if (totalWeight <= 0)
                EditorGUILayout.HelpBox("All weights are zero. Runtime random selection needs at least one positive weight.", MessageType.Warning);

            int removeIndex = -1;
            int moveFrom = -1;
            int moveTo = -1;

            for (int i = 0; i < pool.PossibleSkills.Count; i++)
            {
                var entry = pool.PossibleSkills[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(28));

                DrawSkillPoolSkillPicker(pool, i, entry.Skill);

                EditorGUI.BeginChangeCheck();
                int newWeight = Mathf.Max(0, EditorGUILayout.IntField(entry.Weight, GUILayout.Width(64)));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(pool, "Edit Skill Pool Entry");
                    entry.Weight = newWeight;
                    pool.PossibleSkills[i] = entry;
                    EditorUtility.SetDirty(pool);
                }

                float chance = totalWeight > 0 ? Mathf.Max(0, entry.Weight) * 100f / totalWeight : 0f;
                EditorGUILayout.LabelField($"{chance:0.#}%", GUILayout.Width(54));

                if (GUILayout.Button("Ping", GUILayout.Width(42)) && entry.Skill != null)
                {
                    Selection.activeObject = entry.Skill;
                    EditorGUIUtility.PingObject(entry.Skill);
                }

                if (GUILayout.Button("^", GUILayout.Width(24)) && i > 0)
                {
                    moveFrom = i;
                    moveTo = i - 1;
                }

                if (GUILayout.Button("v", GUILayout.Width(24)) && i < pool.PossibleSkills.Count - 1)
                {
                    moveFrom = i;
                    moveTo = i + 1;
                }

                if (GUILayout.Button("?", GUILayout.Width(24)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();

                if (entry.Skill != null)
                {
                    string skillTitle = string.IsNullOrWhiteSpace(entry.Skill.SkillName) ? entry.Skill.name : entry.Skill.SkillName;
                    EditorGUILayout.LabelField(skillTitle, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"Mana: {entry.Skill.ManaCost:0.#}  Cooldown: {entry.Skill.Cooldown:0.#}s  Active: {entry.Skill.IsActive}",
                        EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(entry.Skill), EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("No skill assigned", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            if (moveFrom >= 0 && moveTo >= 0)
            {
                Undo.RecordObject(pool, "Reorder Skill Pool Entry");
                var tmp = pool.PossibleSkills[moveFrom];
                pool.PossibleSkills[moveFrom] = pool.PossibleSkills[moveTo];
                pool.PossibleSkills[moveTo] = tmp;
                EditorUtility.SetDirty(pool);
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(pool, "Remove Skill From Pool");
                pool.PossibleSkills.RemoveAt(removeIndex);
                EditorUtility.SetDirty(pool);
            }
        }

        private void DrawSkillPoolSkillPicker(SkillPoolSO pool, int entryIndex, SkillDataSO currentSkill)
        {
            string buttonText = currentSkill != null ? BuildSkillButtonLabel(currentSkill) : "None";
            string tooltip = currentSkill != null ? BuildSkillSummary(currentSkill) : "Select skill";
            Rect fieldRect = GUILayoutUtility.GetRect(new GUIContent(buttonText, tooltip), EditorStyles.popup, GUILayout.MinWidth(220), GUILayout.Height(20));

            if (EditorGUI.DropdownButton(fieldRect, new GUIContent(buttonText, tooltip), FocusType.Keyboard, EditorStyles.popup))
            {
                PopupWindow.Show(fieldRect, new SkillDataPickerPopup(currentSkill, _skills, selected =>
                {
                    if (pool == null || pool.PossibleSkills == null || entryIndex < 0 || entryIndex >= pool.PossibleSkills.Count)
                        return;

                    Undo.RecordObject(pool, "Edit Skill Pool Entry");
                    var entry = pool.PossibleSkills[entryIndex];
                    entry.Skill = selected;
                    pool.PossibleSkills[entryIndex] = entry;
                    EditorUtility.SetDirty(pool);
                    Repaint();
                }));
            }
        }

        private static string BuildSkillButtonLabel(SkillDataSO skill)
        {
            if (skill == null)
                return "None";

            string title = string.IsNullOrWhiteSpace(skill.SkillName) ? skill.name : skill.SkillName;
            return $"{title} ({skill.name})";
        }

        private static string BuildSkillSummary(SkillDataSO skill)
        {
            if (skill == null)
                return string.Empty;

            string title = string.IsNullOrWhiteSpace(skill.SkillName) ? skill.name : skill.SkillName;
            string active = skill.IsActive ? "Active" : "Passive";
            string path = AssetDatabase.GetAssetPath(skill);
            var parts = new List<string>
            {
                $"{title} · {active}",
                $"Mana: {skill.ManaCost:0.#} · Cooldown: {skill.Cooldown:0.##}s"
            };

            if (!string.IsNullOrWhiteSpace(skill.Description))
                parts.Add(skill.Description);

            if (!string.IsNullOrWhiteSpace(path))
                parts.Add(path);

            return string.Join("\n", parts);
        }

        private void DrawSkillPoolUsages(SkillPoolSO pool)
        {
            GUILayout.Label("Used by", EditorStyles.boldLabel);
            var usages = GetSkillPoolUsages(pool);

            if (usages.Count == 0)
            {
                EditorGUILayout.LabelField("No asset references found.", EditorStyles.miniLabel);
                return;
            }

            foreach (var path in usages)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                if (GUILayout.Button("Open", GUILayout.Width(52)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private List<string> GetSkillPoolUsages(SkillPoolSO pool)
        {
            if (_skillPoolUsageCacheTarget == pool)
                return _skillPoolUsageCache;

            _skillPoolUsageCacheTarget = pool;
            _skillPoolUsageCache = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null || asset == pool)
                    continue;

                var serialized = new SerializedObject(asset);
                var prop = serialized.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue == pool)
                    {
                        _skillPoolUsageCache.Add(path);
                        break;
                    }
                }
            }

            _skillPoolUsageCache.Sort();
            return _skillPoolUsageCache;
        }

        private void DrawLeftColumn(SkillRecipeSO recipe, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            GUILayout.Label("Step types (click to add)", EditorStyles.boldLabel);
            _typesScroll = EditorGUILayout.BeginScrollView(_typesScroll, GUILayout.ExpandHeight(true));
            foreach (var def in _stepDefs)
            {
                if (def == null) continue;
                if (GUILayout.Button(def.GetDisplayName(_displayRu), EditorStyles.miniButton))
                {
                    recipe.Steps.Add(new StepEntry { StepDefinition = def });
                    EditorUtility.SetDirty(recipe);
                    SelectStep(recipe.Steps.Count - 1);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCenterColumn(SkillRecipeSO recipe, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            GUILayout.Label($"Steps in skill ({recipe.Steps.Count})", EditorStyles.boldLabel);
            _stepsInSkillScroll = EditorGUILayout.BeginScrollView(_stepsInSkillScroll, GUILayout.ExpandHeight(true));

            int toRemove = -1;
            int moveFrom = -1;
            int moveDir = 0;

            for (int i = 0; i < recipe.Steps.Count; i++)
            {
                var step = recipe.Steps[i];
                float startPct = step.StartPercentPipeline * 100f;
                float endPct = step.EndPercentPipeline * 100f;
                string timeLabel = step.IsInstant
                    ? $"{startPct:F0}%"
                    : $"{startPct:F0}% вЂ“ {endPct:F0}%";

                string label = step.StepDefinition != null
                    ? step.StepDefinition.GetDisplayName(_displayRu)
                    : "(no type)";
                if (step.IsParallelGroup && step.SubSteps != null)
                    label += $" ({step.SubSteps.Count})";

                EditorGUILayout.BeginHorizontal();
                bool selected = _selectedStepIndex == i;
                if (selected) GUI.backgroundColor = new Color(0.5f, 0.6f, 0.8f);
                string fullLabel = label + "  [" + timeLabel + "]";
                if (GUILayout.Button(fullLabel, selected ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32)))
                    SelectStep(i);
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("в†‘", GUILayout.Width(20)))
                { moveFrom = i; moveDir = -1; }
                if (GUILayout.Button("в†“", GUILayout.Width(20)))
                { moveFrom = i; moveDir = 1; }
                if (GUILayout.Button("в€’", GUILayout.Width(20)))
                    toRemove = i;
                EditorGUILayout.EndHorizontal();
            }

            if (moveFrom >= 0 && moveDir != 0)
            {
                int to = moveFrom + moveDir;
                if (to >= 0 && to < recipe.Steps.Count)
                {
                    ResetInspectorInputState(resetSubStepSelection: true);
                    var tmp = recipe.Steps[moveFrom];
                    recipe.Steps[moveFrom] = recipe.Steps[to];
                    recipe.Steps[to] = tmp;
                    _selectedStepIndex = to;
                    EditorUtility.SetDirty(recipe);
                }
            }
            if (toRemove >= 0)
            {
                ResetInspectorInputState(resetSubStepSelection: true);
                recipe.Steps.RemoveAt(toRemove);
                EditorUtility.SetDirty(recipe);
                if (_selectedStepIndex >= recipe.Steps.Count) _selectedStepIndex = recipe.Steps.Count - 1;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRightColumn(SkillDataSO skill, SkillRecipeSO recipe, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Inspector", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            int mode = GUILayout.Toolbar(_inspectorMode == InspectorMode.Base ? 1 : 0, new[] { "Step", "Base" }, GUILayout.Width(120));
            _inspectorMode = mode == 1 ? InspectorMode.Base : InspectorMode.Step;
            EditorGUILayout.EndHorizontal();

            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll, GUILayout.ExpandHeight(true));

            if (_inspectorMode == InspectorMode.Base)
            {
                DrawSkillBaseInspector(skill);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            if (_selectedStepIndex < 0 || _selectedStepIndex >= recipe.Steps.Count)
            {
                EditorGUILayout.HelpBox("Select a step in the center list to edit its settings.", MessageType.None);
            }
            else
            {
                var step = recipe.Steps[_selectedStepIndex];
                string header = step.StepDefinition != null ? step.StepDefinition.GetDisplayName(_displayRu) : "Step";
                GUILayout.Label(header, EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                if (step.IsParallelGroup)
                {
                    DrawParallelGroupContent(recipe, step);
                }
                else
                {
                    DrawStepOverrides(recipe, step);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSkillBaseInspector(SkillDataSO skill)
        {
            if (skill == null)
            {
                EditorGUILayout.HelpBox("No skill selected.", MessageType.Info);
                return;
            }

            SerializedObject serializedSkill = new SerializedObject(skill);
            serializedSkill.Update();

            EditorGUILayout.LabelField("Base Skill Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            EditorGUILayout.PropertyField(serializedSkill.FindProperty("ID"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("SkillName"), new GUIContent("Skill Name"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("Description"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("Icon"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("NameKey"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("DescriptionKey"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Mechanics", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("IsActive"), new GUIContent("Is Active"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("Cooldown"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("ManaCost"), new GUIContent("Mana Cost"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Runtime Links", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("DamageContextTags"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("SkillPrefab"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("AnimationTrigger"));
            EditorGUILayout.PropertyField(serializedSkill.FindProperty("Recipe"));

            if (serializedSkill.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(skill);
                Repaint();
            }

            EditorGUILayout.Space(8f);
            DrawSkillLocalizationSection(skill);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Skill Asset"))
                {
                    Selection.activeObject = skill;
                    EditorGUIUtility.PingObject(skill);
                }

                using (new EditorGUI.DisabledScope(skill.Recipe != null))
                {
                    if (GUILayout.Button("Create Recipe"))
                    {
                        SkillRecipeSO recipe = CreateRecipeForSkill(skill);
                        if (recipe != null)
                        {
                            skill.Recipe = recipe;
                            EditorUtility.SetDirty(skill);
                            AssetDatabase.SaveAssets();
                            Refresh();
                        }
                    }
                }
            }
        }

        private void DrawSkillLocalizationSection(SkillDataSO skill)
        {
            GUILayout.Label("Localization (SkillsLabels EN / RU)", EditorStyles.boldLabel);

            if (_skillsLabelsCollection == null)
                _skillsLabelsCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.SkillsLabelsTable);

            var newCollection = (StringTableCollection)EditorGUILayout.ObjectField("SkillsLabels", _skillsLabelsCollection, typeof(StringTableCollection), false);
            if (newCollection != _skillsLabelsCollection)
            {
                _skillsLabelsCollection = newCollection;
                _lastLoadedSkillLocalizationState = string.Empty;
            }

            if (_skillsLabelsCollection == null)
            {
                EditorGUILayout.HelpBox("SkillsLabels table not found. Expected path: " + EditorPaths.SkillsLabelsTable, MessageType.Warning);
                return;
            }

            EnsureSkillLocalizationKeys(skill);

            string state = $"{skill.GetInstanceID()}|{skill.NameKey}|{skill.DescriptionKey}";
            if (_lastLoadedSkillLocalizationState != state)
            {
                LoadSkillLocalizationValues(skill);
                _lastLoadedSkillLocalizationState = state;
            }

            EditorGUILayout.HelpBox("RU/EN values are saved into SkillsLabels. The SO fields above stay as safe fallback values for missing localization.", MessageType.None);

            _skillLocNameEn = EditorGUILayout.TextField("Name EN", _skillLocNameEn ?? string.Empty);
            _skillLocNameRu = EditorGUILayout.TextField("Name RU", _skillLocNameRu ?? string.Empty);
            EditorGUILayout.LabelField("Description EN");
            _skillLocDescEn = EditorGUILayout.TextArea(_skillLocDescEn ?? string.Empty, GUILayout.MinHeight(44f));
            EditorGUILayout.LabelField("Description RU");
            _skillLocDescRu = EditorGUILayout.TextArea(_skillLocDescRu ?? string.Empty, GUILayout.MinHeight(44f));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload from table", GUILayout.Width(130)))
            {
                LoadSkillLocalizationValues(skill);
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Fill from SO fallback", GUILayout.Width(140)))
            {
                _skillLocNameEn = skill.SkillName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(_skillLocNameRu))
                    _skillLocNameRu = skill.SkillName ?? string.Empty;
                _skillLocDescEn = skill.Description ?? string.Empty;
                if (string.IsNullOrWhiteSpace(_skillLocDescRu))
                    _skillLocDescRu = skill.Description ?? string.Empty;
            }

            if (GUILayout.Button("Save localization"))
                SaveSkillLocalizationValues(skill);

            EditorGUILayout.EndHorizontal();
        }

        private void EnsureSkillLocalizationKeys(SkillDataSO skill)
        {
            if (skill == null)
                return;

            string baseId = !string.IsNullOrWhiteSpace(skill.ID) ? skill.ID : skill.name;
            baseId = SanitizeFileName(baseId);
            if (string.IsNullOrWhiteSpace(baseId))
                baseId = "Skill";

            bool changed = false;
            string defaultNameKey = $"skills.{baseId}";
            string defaultDescKey = $"skills.{baseId}.description";

            if (string.IsNullOrWhiteSpace(skill.NameKey))
            {
                skill.NameKey = defaultNameKey;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(skill.DescriptionKey))
            {
                skill.DescriptionKey = defaultDescKey;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(skill);
                _lastLoadedSkillLocalizationState = string.Empty;
            }
        }

        private void LoadSkillLocalizationValues(SkillDataSO skill)
        {
            if (skill == null)
                return;

            _skillLocNameEn = GetLocalizedString(_skillsLabelsCollection, skill.NameKey, "en");
            _skillLocNameRu = GetLocalizedString(_skillsLabelsCollection, skill.NameKey, "ru");
            _skillLocDescEn = GetLocalizedString(_skillsLabelsCollection, skill.DescriptionKey, "en");
            _skillLocDescRu = GetLocalizedString(_skillsLabelsCollection, skill.DescriptionKey, "ru");

            if (string.IsNullOrWhiteSpace(_skillLocNameEn))
                _skillLocNameEn = skill.SkillName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_skillLocDescEn))
                _skillLocDescEn = skill.Description ?? string.Empty;
        }

        private void SaveSkillLocalizationValues(SkillDataSO skill)
        {
            if (skill == null || _skillsLabelsCollection == null)
                return;

            EnsureSkillLocalizationKeys(skill);

            var enTable = _skillsLabelsCollection.GetTable("en") as StringTable
                ?? _skillsLabelsCollection.GetTable(new LocaleIdentifier("en")) as StringTable;
            var ruTable = _skillsLabelsCollection.GetTable("ru") as StringTable
                ?? _skillsLabelsCollection.GetTable(new LocaleIdentifier("ru")) as StringTable;

            if (enTable == null || ruTable == null)
            {
                EditorUtility.DisplayDialog("Skill Localization", "SkillsLabels: en or ru table not found.", "OK");
                return;
            }

            var sharedData = _skillsLabelsCollection.SharedData;
            if (sharedData != null)
            {
                if (!sharedData.Contains(skill.NameKey)) sharedData.AddKey(skill.NameKey);
                if (!sharedData.Contains(skill.DescriptionKey)) sharedData.AddKey(skill.DescriptionKey);
                EditorUtility.SetDirty(sharedData);
            }

            SetOrAddEntry(enTable, skill.NameKey, _skillLocNameEn ?? string.Empty);
            SetOrAddEntry(ruTable, skill.NameKey, _skillLocNameRu ?? string.Empty);
            SetOrAddEntry(enTable, skill.DescriptionKey, _skillLocDescEn ?? string.Empty);
            SetOrAddEntry(ruTable, skill.DescriptionKey, _skillLocDescRu ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(_skillLocNameEn))
                skill.SkillName = _skillLocNameEn;
            if (!string.IsNullOrWhiteSpace(_skillLocDescEn))
                skill.Description = _skillLocDescEn;

            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);
            AssetDatabase.SaveAssets();
        }

        private static string GetLocalizedString(StringTableCollection collection, string key, string locale)
        {
            if (collection == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var table = collection.GetTable(locale) as StringTable
                ?? collection.GetTable(new LocaleIdentifier(locale)) as StringTable;
            if (table == null)
                return string.Empty;

            var entry = table.GetEntry(key);
            return entry?.Value ?? string.Empty;
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key);
            if (entry != null)
                entry.Value = value;
            else
                table.AddEntry(key, value);
        }

        private void DrawStepOverrides(SkillRecipeSO recipe, StepEntry step)
        {
            EditorGUILayout.Space(2);
            GUILayout.Label("Timing (% of pipeline)", EditorStyles.miniBoldLabel);
            string stepId = step.StepDefinition != null ? step.StepDefinition.Id : "";
            bool isDuration = step.StepDefinition != null && (step.StepDefinition.IsDurationStep || stepId == "SpawnVFX");
            if (isDuration)
            {
                float startP = step.StartPercentPipeline * 100f;
                float endP = step.EndPercentPipeline * 100f;
                float newStart = EditorGUILayout.Slider("Start %", startP, 0f, 100f);
                float newEnd = EditorGUILayout.Slider("End %", endP, 0f, 100f);
                if (newEnd < newStart) newEnd = newStart;
                if (Mathf.Abs(newStart - startP) > 0.001f) { step.StartPercentPipeline = newStart / 100f; EditorUtility.SetDirty(recipe); }
                if (Mathf.Abs(newEnd - endP) > 0.001f) { step.EndPercentPipeline = newEnd / 100f; EditorUtility.SetDirty(recipe); }
            }
            else
            {
                float triggerPct = step.StartPercentPipeline * 100f;
                float newPct = EditorGUILayout.Slider("Trigger at %", triggerPct, 0f, 100f);
                if (Mathf.Abs(newPct - triggerPct) > 0.001f)
                {
                    step.StartPercentPipeline = step.EndPercentPipeline = newPct / 100f;
                    EditorUtility.SetDirty(recipe);
                }
            }

            EditorGUILayout.Space(6);
            GUILayout.Label("Step type", EditorStyles.miniBoldLabel);
            int popup = _stepDefs.IndexOf(step.StepDefinition);
            int newPopup = EditorGUILayout.Popup("Step type", Mathf.Max(0, popup), _stepDefs.Select(d => d.GetDisplayName(_displayRu)).ToArray());
            if (newPopup >= 0 && newPopup < _stepDefs.Count && newPopup != popup)
            {
                ResetInspectorInputState(resetSubStepSelection: true);
                step.StepDefinition = _stepDefs[newPopup];
                EditorUtility.SetDirty(recipe);
            }

            EditorGUILayout.Space(6);
            GUILayout.Label("Step settings", EditorStyles.miniBoldLabel);
            DrawStepTypeFields(recipe, step, false);
        }

        private void DrawParallelGroupContent(SkillRecipeSO recipe, StepEntry groupStep)
        {
            EditorGUILayout.Space(2);
            GUILayout.Label("Timing (% of pipeline)", EditorStyles.miniBoldLabel);
            float triggerPct = groupStep.StartPercentPipeline * 100f;
            float newPct = EditorGUILayout.Slider("Trigger at %", triggerPct, 0f, 100f);
            if (Mathf.Abs(newPct - triggerPct) > 0.001f)
            {
                groupStep.StartPercentPipeline = groupStep.EndPercentPipeline = newPct / 100f;
                EditorUtility.SetDirty(recipe);
            }

            EditorGUILayout.Space(6);
            GUILayout.Label("Sub-steps (run at same time)", EditorStyles.miniBoldLabel);
            if (groupStep.SubSteps == null) groupStep.SubSteps = new List<StepEntry>();
            int removeSub = -1;
            for (int i = 0; i < groupStep.SubSteps.Count; i++)
            {
                var sub = groupStep.SubSteps[i];
                string subLabel = sub.StepDefinition != null ? sub.StepDefinition.GetDisplayName(_displayRu) : "(no type)";
                EditorGUILayout.BeginHorizontal();
                bool subSelected = _selectedSubStepIndex == i;
                if (subSelected) GUI.backgroundColor = new Color(0.5f, 0.6f, 0.8f);
                if (GUILayout.Button(subLabel, GUILayout.ExpandWidth(true))) SelectSubStep(i);
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("в€’", GUILayout.Width(22))) removeSub = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeSub >= 0)
            {
                ResetInspectorInputState();
                groupStep.SubSteps.RemoveAt(removeSub);
                EditorUtility.SetDirty(recipe);
                if (_selectedSubStepIndex >= groupStep.SubSteps.Count) _selectedSubStepIndex = groupStep.SubSteps.Count - 1;
            }
            if (GUILayout.Button("+ Add sub-step"))
            {
                var sub = new StepEntry();
                if (_stepDefs.Count > 0) sub.StepDefinition = _stepDefs[0];
                groupStep.SubSteps.Add(sub);
                EditorUtility.SetDirty(recipe);
                SelectSubStep(groupStep.SubSteps.Count - 1);
            }

            if (_selectedSubStepIndex >= 0 && _selectedSubStepIndex < groupStep.SubSteps.Count)
            {
                var sub = groupStep.SubSteps[_selectedSubStepIndex];
                EditorGUILayout.Space(8);
                GUILayout.Label("Selected sub-step settings", EditorStyles.miniBoldLabel);
                int popup = _stepDefs.IndexOf(sub.StepDefinition);
                int newPopup = EditorGUILayout.Popup("Sub-step type", Mathf.Max(0, popup), _stepDefs.Select(d => d.GetDisplayName(_displayRu)).ToArray());
                if (newPopup >= 0 && newPopup < _stepDefs.Count && newPopup != popup)
                {
                    ResetInspectorInputState();
                    sub.StepDefinition = _stepDefs[newPopup];
                    EditorUtility.SetDirty(recipe);
                }
                DrawStepTypeFields(recipe, sub, true);
            }
        }

        private void DrawStepTypeFields(SkillRecipeSO recipe, StepEntry step, bool isSubStep)
        {
            string id = step.StepDefinition != null ? step.StepDefinition.Id : "";

            if (id == "WeaponWindup" || id == "WeaponRecovery" || id == "Wait")
            {
                EditorGUILayout.HelpBox("Use Start % and End % in Timing section above. Duration = End в€’ Start.", MessageType.None);
                return;
            }

            if (id == "SpawnVFX")
            {
                var prefab = step.GetObject<GameObject>("VfxPrefab");
                var newPrefab = (GameObject)EditorGUILayout.ObjectField("VFX Prefab", prefab, typeof(GameObject), false);
                if (newPrefab != prefab) { step.SetOverrideObject("VfxPrefab", newPrefab); EditorUtility.SetDirty(recipe); }
                int growthMode = step.GetInt("GrowthMode", 0);
                int newGrowthMode = EditorGUILayout.Popup(
                    new GUIContent(
                        "Growth mode",
                        "Centered = VFX grows in all directions from its center. Locked away from caster = if VFX is offset in front/behind/above/below, AOE scaling expands it only away from the character."),
                    growthMode,
                    new[] { "Centered", "Locked away from caster" });
                if (newGrowthMode != growthMode) { step.SetOverrideInt("GrowthMode", newGrowthMode); EditorUtility.SetDirty(recipe); }
                float sm = step.GetFloat("ScaleMultiplier", 1f);
                float nsm = EditorGUILayout.FloatField("Scale multiplier", sm);
                if (Mathf.Abs(nsm - sm) > 0.001f) { step.SetOverrideFloat("ScaleMultiplier", nsm); EditorUtility.SetDirty(recipe); }
                float ox = step.GetFloat("OffsetX", 0f);
                float nox = EditorGUILayout.FloatField("Offset X", ox);
                if (nox != ox) { step.SetOverrideFloat("OffsetX", nox); EditorUtility.SetDirty(recipe); }
                float oy = step.GetFloat("OffsetY", 0f);
                float noy = EditorGUILayout.FloatField("Offset Y", oy);
                if (noy != oy) { step.SetOverrideFloat("OffsetY", noy); EditorUtility.SetDirty(recipe); }
                if (newGrowthMode == 1)
                    EditorGUILayout.HelpBox("Locked away from caster: with positive Offset X the VFX grows to the right/front, with negative Offset X to the left/back. The same rule works for Offset Y above/below the character.", MessageType.None);
                if (isSubStep)
                {
                    EditorGUILayout.HelpBox("ParallelGroup sub-steps still use legacy duration in seconds. For regular Spawn VFX steps, lifetime now comes from Start % / End % in Timing.", MessageType.None);
                    float bd = step.GetFloat("BaseDuration", 0.5f);
                    float nbd = EditorGUILayout.FloatField("Base duration (sec)", bd);
                    if (nbd != bd) { step.SetOverrideFloat("BaseDuration", nbd); EditorUtility.SetDirty(recipe); }
                }
                else if (step.EndPercentPipeline <= step.StartPercentPipeline + 0.0001f)
                {
                    EditorGUILayout.HelpBox("Legacy mode: when End % equals Start %, Spawn VFX still falls back to Base duration. Set End % above Start % to make VFX fit a % window of the whole skill.", MessageType.None);
                    float bd = step.GetFloat("BaseDuration", 0.5f);
                    float nbd = EditorGUILayout.FloatField("Legacy base duration (sec)", bd);
                    if (nbd != bd) { step.SetOverrideFloat("BaseDuration", nbd); EditorUtility.SetDirty(recipe); }
                }
                else
                {
                    EditorGUILayout.HelpBox("VFX lifetime is driven by Start % / End % in Timing. Animator speed is adjusted to fit that window.", MessageType.None);
                }
                bool fadeOutEnabled = step.GetBool("FadeOutEnabled", true);
                bool newFadeOutEnabled = EditorGUILayout.Toggle("Fade out over lifetime", fadeOutEnabled);
                if (newFadeOutEnabled != fadeOutEnabled) { step.SetOverrideBool("FadeOutEnabled", newFadeOutEnabled); EditorUtility.SetDirty(recipe); }
                EditorGUI.BeginDisabledGroup(!newFadeOutEnabled);
                float fadeStartLifePct = step.GetFloat("FadeOutStartLifePercent", 0.5f) * 100f;
                float newFadeStartLifePct = EditorGUILayout.Slider("Fade start at life %", fadeStartLifePct, 0f, 100f);
                if (Mathf.Abs(newFadeStartLifePct - fadeStartLifePct) > 0.001f) { step.SetOverrideFloat("FadeOutStartLifePercent", newFadeStartLifePct / 100f); EditorUtility.SetDirty(recipe); }
                float fadeStartVisibilityPct = step.GetFloat("FadeStartAlphaMultiplier", 0.5f) * 100f;
                float newFadeStartVisibilityPct = EditorGUILayout.Slider("Fade start visibility %", fadeStartVisibilityPct, 0f, 100f);
                if (Mathf.Abs(newFadeStartVisibilityPct - fadeStartVisibilityPct) > 0.001f) { step.SetOverrideFloat("FadeStartAlphaMultiplier", newFadeStartVisibilityPct / 100f); EditorUtility.SetDirty(recipe); }
                EditorGUI.EndDisabledGroup();
                bool att = step.GetBool("AttachToParent", false);
                bool natt = EditorGUILayout.Toggle("Attach to parent", att);
                if (natt != att) { step.SetOverrideBool("AttachToParent", natt); EditorUtility.SetDirty(recipe); }
                bool inv = step.GetBool("InvertFacing", false);
                bool ninv = EditorGUILayout.Toggle("Invert facing", inv);
                if (ninv != inv) { step.SetOverrideBool("InvertFacing", ninv); EditorUtility.SetDirty(recipe); }
                return;
            }

            if (id == "DealDamageCircle")
            {
                EditorGUILayout.HelpBox("Если указан Source step index, круг берёт размер текущего кадра VFX, включая прозрачные пиксели. Size X / Size Y — это мультипликаторы от визуального размера. Если Source step index = -1, используется обычный Radius.", MessageType.None);
                int src = step.GetInt("SourceStepIndex", -1);
                int nsrc = EditorGUILayout.IntField("Source step index (Spawn VFX, -1 = от игрока)", src);
                if (nsrc != src) { step.SetOverrideInt("SourceStepIndex", nsrc); EditorUtility.SetDirty(recipe); }
                if (nsrc >= 0)
                {
                    float sx = step.GetFloat("SizeX", 1f);
                    float nsx = EditorGUILayout.FloatField("Size X multiplier", sx);
                    if (Mathf.Abs(nsx - sx) > 0.001f) { step.SetOverrideFloat("SizeX", nsx); EditorUtility.SetDirty(recipe); }
                    float sy = step.GetFloat("SizeY", 1f);
                    float nsy = EditorGUILayout.FloatField("Size Y multiplier", sy);
                    if (Mathf.Abs(nsy - sy) > 0.001f) { step.SetOverrideFloat("SizeY", nsy); EditorUtility.SetDirty(recipe); }
                }
                else
                {
                    float r = step.GetFloat("Radius", 1.5f);
                    float nr = EditorGUILayout.FloatField("Radius", r);
                    if (nr != r) { step.SetOverrideFloat("Radius", nr); EditorUtility.SetDirty(recipe); }
                }
                EditorGUI.BeginDisabledGroup(nsrc < 0);
                float vfxLife = step.GetFloat("VfxLifetimePercent", 0f);
                float nvfxLife = EditorGUILayout.Slider("Damage at VFX life %", vfxLife, 0f, 1f);
                if (Mathf.Abs(nvfxLife - vfxLife) > 0.001f) { step.SetOverrideFloat("VfxLifetimePercent", nvfxLife); EditorUtility.SetDirty(recipe); }
                EditorGUI.EndDisabledGroup();
                float ox = step.GetFloat("OffsetX", 0f);
                float nox = EditorGUILayout.FloatField("Offset X", ox);
                if (nox != ox) { step.SetOverrideFloat("OffsetX", nox); EditorUtility.SetDirty(recipe); }
                float oy = step.GetFloat("OffsetY", 0f);
                float noy = EditorGUILayout.FloatField("Offset Y", oy);
                if (noy != oy) { step.SetOverrideFloat("OffsetY", noy); EditorUtility.SetDirty(recipe); }
                float dm = step.GetFloat("DamageMultiplier", 1f);
                float ndm = EditorGUILayout.FloatField("Damage multiplier", dm);
                if (ndm != dm) { step.SetOverrideFloat("DamageMultiplier", ndm); EditorUtility.SetDirty(recipe); }
                return;
            }

            if (id == "DealDamageRectangle")
            {
                EditorGUILayout.HelpBox("Если указан Source step index, прямоугольник берёт размер текущего кадра VFX, включая прозрачные пиксели. Size X / Size Y — это мультипликаторы от визуального размера VFX.", MessageType.None);
                int src = step.GetInt("SourceStepIndex", -1);
                float sx = step.GetFloat("SizeX", src >= 0 ? 1f : 2f);
                float nsx = EditorGUILayout.FloatField("Size X multiplier", sx);
                if (nsx != sx) { step.SetOverrideFloat("SizeX", nsx); EditorUtility.SetDirty(recipe); }
                float sy = step.GetFloat("SizeY", 1f);
                float nsy = EditorGUILayout.FloatField("Size Y multiplier", sy);
                if (nsy != sy) { step.SetOverrideFloat("SizeY", nsy); EditorUtility.SetDirty(recipe); }
                float ang = step.GetFloat("Angle", 0f);
                float nang = EditorGUILayout.FloatField("Angle (deg)", ang);
                if (nang != ang) { step.SetOverrideFloat("Angle", nang); EditorUtility.SetDirty(recipe); }
                int nsrc = EditorGUILayout.IntField("Source step index (-1 = use offset)", src);
                if (nsrc != src) { step.SetOverrideInt("SourceStepIndex", nsrc); EditorUtility.SetDirty(recipe); }
                EditorGUI.BeginDisabledGroup(nsrc < 0);
                float vfxLifeR = step.GetFloat("VfxLifetimePercent", 0f);
                float nvfxLifeR = EditorGUILayout.Slider("Damage at VFX life %", vfxLifeR, 0f, 1f);
                if (Mathf.Abs(nvfxLifeR - vfxLifeR) > 0.001f) { step.SetOverrideFloat("VfxLifetimePercent", nvfxLifeR); EditorUtility.SetDirty(recipe); }
                EditorGUI.EndDisabledGroup();
                float ox = step.GetFloat("OffsetX", 0f);
                float nox = EditorGUILayout.FloatField("Offset X", ox);
                if (nox != ox) { step.SetOverrideFloat("OffsetX", nox); EditorUtility.SetDirty(recipe); }
                float oy = step.GetFloat("OffsetY", 0f);
                float noy = EditorGUILayout.FloatField("Offset Y", oy);
                if (noy != oy) { step.SetOverrideFloat("OffsetY", noy); EditorUtility.SetDirty(recipe); }
                float dm = step.GetFloat("DamageMultiplier", 1f);
                float ndm = EditorGUILayout.FloatField("Damage multiplier", dm);
                if (ndm != dm) { step.SetOverrideFloat("DamageMultiplier", ndm); EditorUtility.SetDirty(recipe); }
                return;
            }

            if (id == "ApplyStatusSelf")
            {
                DrawStatusEffectAssetField(recipe, step);
                int src = step.GetInt("SourceStepIndex", -1);
                int nsrc = EditorGUILayout.IntField("Source step index (-1 = instant)", src);
                if (nsrc != src) { step.SetOverrideInt("SourceStepIndex", nsrc); EditorUtility.SetDirty(recipe); }
                EditorGUI.BeginDisabledGroup(nsrc < 0);
                float vfxLife = step.GetFloat("VfxLifetimePercent", 0f);
                float nvfxLife = EditorGUILayout.Slider("Apply at VFX life %", vfxLife, 0f, 1f);
                if (Mathf.Abs(nvfxLife - vfxLife) > 0.001f) { step.SetOverrideFloat("VfxLifetimePercent", nvfxLife); EditorUtility.SetDirty(recipe); }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox("Применяет выбранный buff/debuff на самого владельца скилла. Если указан Source step index, применение можно сдвинуть на процент жизни VFX.", MessageType.None);
                return;
            }

            if (id == "ApplyStatusCircle")
            {
                DrawStatusEffectAssetField(recipe, step);
                EditorGUILayout.HelpBox("Круговая зона наложения статуса. Если указан Source step index, круг берёт размер текущего кадра VFX; иначе используется обычный Radius.", MessageType.None);
                int src = step.GetInt("SourceStepIndex", -1);
                int nsrc = EditorGUILayout.IntField("Source step index (Spawn VFX, -1 = от игрока)", src);
                if (nsrc != src) { step.SetOverrideInt("SourceStepIndex", nsrc); EditorUtility.SetDirty(recipe); }
                if (nsrc >= 0)
                {
                    float sx = step.GetFloat("SizeX", 1f);
                    float nsx = EditorGUILayout.FloatField("Size X multiplier", sx);
                    if (Mathf.Abs(nsx - sx) > 0.001f) { step.SetOverrideFloat("SizeX", nsx); EditorUtility.SetDirty(recipe); }
                    float sy = step.GetFloat("SizeY", 1f);
                    float nsy = EditorGUILayout.FloatField("Size Y multiplier", sy);
                    if (Mathf.Abs(nsy - sy) > 0.001f) { step.SetOverrideFloat("SizeY", nsy); EditorUtility.SetDirty(recipe); }
                }
                else
                {
                    float r = step.GetFloat("Radius", 1.5f);
                    float nr = EditorGUILayout.FloatField("Radius", r);
                    if (Mathf.Abs(nr - r) > 0.001f) { step.SetOverrideFloat("Radius", nr); EditorUtility.SetDirty(recipe); }
                }
                EditorGUI.BeginDisabledGroup(nsrc < 0);
                float vfxLife = step.GetFloat("VfxLifetimePercent", 0f);
                float nvfxLife = EditorGUILayout.Slider("Apply at VFX life %", vfxLife, 0f, 1f);
                if (Mathf.Abs(nvfxLife - vfxLife) > 0.001f) { step.SetOverrideFloat("VfxLifetimePercent", nvfxLife); EditorUtility.SetDirty(recipe); }
                EditorGUI.EndDisabledGroup();
                float ox = step.GetFloat("OffsetX", 0f);
                float nox = EditorGUILayout.FloatField("Offset X", ox);
                if (Mathf.Abs(nox - ox) > 0.001f) { step.SetOverrideFloat("OffsetX", nox); EditorUtility.SetDirty(recipe); }
                float oy = step.GetFloat("OffsetY", 0f);
                float noy = EditorGUILayout.FloatField("Offset Y", oy);
                if (Mathf.Abs(noy - oy) > 0.001f) { step.SetOverrideFloat("OffsetY", noy); EditorUtility.SetDirty(recipe); }
                return;
            }

            if (id == "ApplyStatusRectangle")
            {
                DrawStatusEffectAssetField(recipe, step);
                EditorGUILayout.HelpBox("Прямоугольная зона наложения статуса. Если указан Source step index, прямоугольник берёт размер текущего кадра VFX.", MessageType.None);
                int src = step.GetInt("SourceStepIndex", -1);
                int nsrc = EditorGUILayout.IntField("Source step index (-1 = use offset)", src);
                if (nsrc != src) { step.SetOverrideInt("SourceStepIndex", nsrc); EditorUtility.SetDirty(recipe); }
                float sx = step.GetFloat("SizeX", nsrc >= 0 ? 1f : 2f);
                float nsx = EditorGUILayout.FloatField("Size X multiplier", sx);
                if (Mathf.Abs(nsx - sx) > 0.001f) { step.SetOverrideFloat("SizeX", nsx); EditorUtility.SetDirty(recipe); }
                float sy = step.GetFloat("SizeY", 1f);
                float nsy = EditorGUILayout.FloatField("Size Y multiplier", sy);
                if (Mathf.Abs(nsy - sy) > 0.001f) { step.SetOverrideFloat("SizeY", nsy); EditorUtility.SetDirty(recipe); }
                float ang = step.GetFloat("Angle", 0f);
                float nang = EditorGUILayout.FloatField("Angle (deg)", ang);
                if (Mathf.Abs(nang - ang) > 0.001f) { step.SetOverrideFloat("Angle", nang); EditorUtility.SetDirty(recipe); }
                EditorGUI.BeginDisabledGroup(nsrc < 0);
                float vfxLife = step.GetFloat("VfxLifetimePercent", 0f);
                float nvfxLife = EditorGUILayout.Slider("Apply at VFX life %", vfxLife, 0f, 1f);
                if (Mathf.Abs(nvfxLife - vfxLife) > 0.001f) { step.SetOverrideFloat("VfxLifetimePercent", nvfxLife); EditorUtility.SetDirty(recipe); }
                EditorGUI.EndDisabledGroup();
                float ox = step.GetFloat("OffsetX", 0f);
                float nox = EditorGUILayout.FloatField("Offset X", ox);
                if (Mathf.Abs(nox - ox) > 0.001f) { step.SetOverrideFloat("OffsetX", nox); EditorUtility.SetDirty(recipe); }
                float oy = step.GetFloat("OffsetY", 0f);
                float noy = EditorGUILayout.FloatField("Offset Y", oy);
                if (Mathf.Abs(noy - oy) > 0.001f) { step.SetOverrideFloat("OffsetY", noy); EditorUtility.SetDirty(recipe); }
                return;
            }

            if (id == "MovementLock" || id == "MovementUnlock" || id == "WeaponStrike")
            {
                EditorGUILayout.HelpBox("No extra settings for this step type.", MessageType.None);
            }
        }

        private void CreateNewSkillPool()
        {
            EnsureSkillsFolderExists();

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Skill Pool",
                "NewSkillPool",
                "asset",
                "Choose where to create the new skill pool asset.",
                EditorPaths.SkillsFolder);

            if (string.IsNullOrWhiteSpace(path))
                return;

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
            var pool = CreateInstance<SkillPoolSO>();
            pool.PossibleSkills = new List<SkillPoolSO.SkillWeight>();

            AssetDatabase.CreateAsset(pool, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Refresh();

            var loadedPool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(uniquePath);
            int index = _skillPools.IndexOf(loadedPool);
            if (index >= 0)
                _selectedSkillPoolIndex = index;

            _editorTab = EditorTab.SkillPools;
            Selection.activeObject = loadedPool != null ? loadedPool : pool;
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private void CreateNewSkill()
        {
            EnsureSkillsFolderExists();

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Skill",
                "NewSkill",
                "asset",
                "Choose where to create the new skill asset.",
                EditorPaths.SkillsFolder);

            if (string.IsNullOrWhiteSpace(path))
                return;

            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            string safeName = SanitizeFileName(assetName);
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);

            var skill = CreateInstance<SkillDataSO>();
            skill.ID = safeName;
            skill.SkillName = ObjectNames.NicifyVariableName(assetName);
            skill.IsActive = true;
            skill.AnimationTrigger = "Attack";

            AssetDatabase.CreateAsset(skill, uniquePath);

            SkillRecipeSO recipe = CreateRecipeForSkill(skill);
            if (recipe != null)
            {
                skill.Recipe = recipe;
                EditorUtility.SetDirty(skill);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Refresh();
            int index = _skills.IndexOf(skill);
            if (index < 0)
            {
                var loadedSkill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(uniquePath);
                index = _skills.IndexOf(loadedSkill);
                skill = loadedSkill != null ? loadedSkill : skill;
            }

            if (index >= 0)
                SelectSkill(index);

            _selectedStepIndex = -1;
            _selectedSubStepIndex = -1;
            Selection.activeObject = skill;
            EditorGUIUtility.PingObject(skill);
        }

        private static void EnsureSkillsFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder(EditorPaths.SkillsFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Skills");
        }

        private void CreateDefaultStepDefinitions()
        {
            EnsureBuiltInStepDefinitions();
            Refresh();
        }

        private void EnsureBuiltInStepDefinitions()
        {
            string folder = EditorPaths.StepDefinitionsFolder;
            string[] parts = folder.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            var defaults = new[]
            {
                ("MovementLock", "Lock movement", "Р‘Р»РѕРє РґРІРёР¶РµРЅРёСЏ", 0f),
                ("MovementUnlock", "Unlock movement", "Р Р°Р·Р±Р»РѕРє РґРІРёР¶РµРЅРёСЏ", 0f),
                ("WeaponWindup", "Weapon windup", "Р—Р°РјР°С… РѕСЂСѓР¶РёСЏ", 35f),
                ("WeaponStrike", "Weapon strike", "РЈРґР°СЂ РѕСЂСѓР¶РёСЏ", 0f),
                ("WeaponRecovery", "Weapon recovery", "Р’РѕР·РІСЂР°С‚ РѕСЂСѓР¶РёСЏ", 65f),
                ("Wait", "Wait", "РћР¶РёРґР°РЅРёРµ", 10f),
                ("SpawnVFX", "Spawn VFX", "РЎРїР°РІРЅ VFX", 0f),
                ("DealDamageCircle", "Deal damage (circle)", "РЈСЂРѕРЅ РєСЂСѓРі", 0f),
                ("DealDamageRectangle", "Deal damage (rectangle)", "РЈСЂРѕРЅ РїСЂСЏРјРѕСѓРіРѕР»СЊРЅРёРє", 0f),
                ("ApplyStatusSelf", "Apply status (self)", "Наложить статус (на себя)", 0f),
                ("ApplyStatusCircle", "Apply status (circle)", "Наложить статус (круг)", 0f),
                ("ApplyStatusRectangle", "Apply status (rectangle)", "Наложить статус (прямоугольник)", 0f),
                ("ParallelGroup", "Parallel group", "РџР°СЂР°Р»Р»РµР»СЊРЅР°СЏ РіСЂСѓРїРїР°", 0f),
            };
            foreach (var (id, nameEn, nameRu, durationPercent) in defaults)
            {
                string path = folder + "/Step_" + id + ".asset";
                if (AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(path) != null) continue;
                var def = ScriptableObject.CreateInstance<StepDefinitionSO>();
                def.Id = id;
                def.NameEn = nameEn;
                def.NameRu = nameRu;
                if (durationPercent > 0)
                {
                    def.DefaultParams.Add(new StepParamValue { Key = "DurationPercent", Type = StepParamValue.ParamKind.Float, FloatVal = durationPercent });
                }
                AssetDatabase.CreateAsset(def, path);
            }
            AssetDatabase.SaveAssets();
        }

        private void DrawStatusEffectAssetField(SkillRecipeSO recipe, StepEntry step)
        {
            StatusEffectSO effect = step.GetObject<StatusEffectSO>("StatusEffect");

            Rect rect = EditorGUILayout.GetControlRect(false, 20f);
            Rect fieldRect = EditorGUI.PrefixLabel(rect, new GUIContent("Status effect"));
            string buttonText = effect != null ? BuildStatusEffectButtonLabel(effect) : "None";
            string tooltip = effect != null ? BuildStatusEffectSummary(effect) : "Select status effect";
            if (EditorGUI.DropdownButton(fieldRect, new GUIContent(buttonText, tooltip), FocusType.Keyboard))
            {
                PopupWindow.Show(fieldRect, new StatusEffectPickerPopup(effect, selected =>
                {
                    step.SetOverrideObject("StatusEffect", selected);
                    EditorUtility.SetDirty(recipe);
                }));
            }

            if (effect != null)
                EditorGUILayout.HelpBox(BuildStatusEffectSummary(effect), MessageType.None);
        }

        private static string BuildStatusEffectButtonLabel(StatusEffectSO effect)
        {
            if (effect == null)
                return "None";

            string name = effect.GetDisplayName(true);
            return $"{name} ({effect.Kind})";
        }

        private static string BuildStatusEffectSummary(StatusEffectSO effect)
        {
            if (effect == null)
                return string.Empty;

            string duration = $"{effect.BaseDurationSeconds:0.##}s";
            string modifiers = BuildStatusEffectModifierSummary(effect, 4);
            string description = !string.IsNullOrWhiteSpace(effect.DescriptionRu)
                ? effect.DescriptionRu
                : effect.DescriptionEn;

            var parts = new List<string> { $"{effect.Kind} · {duration}" };
            if (!string.IsNullOrWhiteSpace(modifiers))
                parts.Add(modifiers);
            if (!string.IsNullOrWhiteSpace(description))
                parts.Add(description);

            return string.Join("\n", parts);
        }

        private static string BuildStatusEffectModifierSummary(StatusEffectSO effect, int maxModifiers)
        {
            if (effect?.Modifiers == null || effect.Modifiers.Count == 0)
                return "No stat modifiers";

            int count = Mathf.Clamp(maxModifiers, 1, effect.Modifiers.Count);
            var labels = new List<string>();
            for (int i = 0; i < count; i++)
                labels.Add(FormatStatusModifier(effect.Modifiers[i]));

            if (effect.Modifiers.Count > count)
                labels.Add($"+{effect.Modifiers.Count - count} more");

            return string.Join(", ", labels);
        }

        private static string FormatStatusModifier(SerializableStatModifier modifier)
        {
            string sign = modifier.Type.GetDisplayPrefix(modifier.Value);
            string suffix = modifier.Type == StatModType.Flat ? string.Empty : "%";
            string statName = Scripts.Editor.Stats.StatPickerUtility.GetDisplayName(modifier.Stat);
            return $"{statName} {sign}{Mathf.Abs(modifier.Value):0.##}{suffix} [{modifier.Type}]";
        }

        private sealed class SkillDataPickerPopup : PopupWindowContent
        {
            private readonly SkillDataSO _current;
            private readonly System.Action<SkillDataSO> _onSelected;
            private readonly List<Entry> _entries;
            private Vector2 _scroll;
            private string _search = string.Empty;
            private bool _focusSearchRequested = true;

            public SkillDataPickerPopup(SkillDataSO current, IEnumerable<SkillDataSO> skills, System.Action<SkillDataSO> onSelected)
            {
                _current = current;
                _onSelected = onSelected;
                _entries = BuildEntries(skills);
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(560f, 560f);
            }

            public override void OnGUI(Rect rect)
            {
                DrawSearchField();
                EditorGUILayout.Space(4f);

                if (GUILayout.Button("None", EditorStyles.miniButton))
                {
                    Select(null);
                    return;
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                string search = (_search ?? string.Empty).Trim().ToLowerInvariant();
                IEnumerable<Entry> filtered = string.IsNullOrWhiteSpace(search)
                    ? _entries
                    : _entries.Where(entry => entry.SearchText.Contains(search));

                string currentGroup = null;
                foreach (Entry entry in filtered)
                {
                    if (!string.Equals(currentGroup, entry.Group, System.StringComparison.Ordinal))
                    {
                        currentGroup = entry.Group;
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.LabelField(currentGroup, EditorStyles.boldLabel);
                    }

                    DrawEntry(entry);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawSearchField()
            {
                GUI.SetNextControlName("SkillDataPickerSearch");
                _search = EditorGUILayout.TextField(_search);

                if (_focusSearchRequested && Event.current.type == EventType.Repaint)
                {
                    _focusSearchRequested = false;
                    EditorGUI.FocusTextInControl("SkillDataPickerSearch");
                }
            }

            private void DrawEntry(Entry entry)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 64f);
                bool isCurrent = entry.Skill == _current;
                bool isHovered = rowRect.Contains(Event.current.mousePosition);

                Color background = isCurrent
                    ? new Color(0.28f, 0.38f, 0.55f, 0.95f)
                    : isHovered
                        ? new Color(0.20f, 0.20f, 0.20f, 0.65f)
                        : new Color(0f, 0f, 0f, 0f);

                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rowRect, background);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    Select(entry.Skill);
                    return;
                }

                Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 10f, 44f, 44f);
                Rect nameRect = new Rect(iconRect.xMax + 8f, rowRect.y + 4f, rowRect.width - 88f, 18f);
                Rect metaRect = new Rect(iconRect.xMax + 8f, rowRect.y + 22f, rowRect.width - 88f, 15f);
                Rect descRect = new Rect(iconRect.xMax + 8f, rowRect.y + 37f, rowRect.width - 88f, 15f);
                Rect pathRect = new Rect(iconRect.xMax + 8f, rowRect.y + 51f, rowRect.width - 88f, 13f);
                Rect checkRect = new Rect(rowRect.xMax - 22f, rowRect.y + 23f, 16f, 16f);

                DrawSkillIcon(iconRect, entry.Skill);

                var nameStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal
                };
                var metaStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.76f, 0.76f, 0.76f, 1f) }
                };
                var pathStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.55f, 0.55f, 0.55f, 1f) }
                };

                GUI.Label(nameRect, entry.DisplayName, nameStyle);
                GUI.Label(metaRect, entry.MetaLine, metaStyle);
                GUI.Label(descRect, entry.DescriptionLine, metaStyle);
                GUI.Label(pathRect, entry.AssetPath, pathStyle);

                if (isCurrent)
                    GUI.Label(checkRect, "?", EditorStyles.boldLabel);
            }

            private static void DrawSkillIcon(Rect rect, SkillDataSO skill)
            {
                EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.11f, 1f));

                if (skill?.Icon == null)
                    return;

                Sprite sprite = skill.Icon;
                Texture texture = sprite.texture;
                if (texture == null)
                    return;

                Rect textureRect = sprite.textureRect;
                Rect uv = new Rect(
                    textureRect.x / texture.width,
                    textureRect.y / texture.height,
                    textureRect.width / texture.width,
                    textureRect.height / texture.height);

                GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            }

            private void Select(SkillDataSO skill)
            {
                _onSelected?.Invoke(skill);
                editorWindow?.Close();
                GUIUtility.ExitGUI();
            }

            private static List<Entry> BuildEntries(IEnumerable<SkillDataSO> skills)
            {
                return (skills ?? Enumerable.Empty<SkillDataSO>())
                    .Where(skill => skill != null)
                    .Distinct()
                    .Select(skill => new Entry(skill))
                    .OrderBy(entry => entry.Group)
                    .ThenBy(entry => entry.DisplayName)
                    .ThenBy(entry => entry.AssetPath)
                    .ToList();
            }

            private sealed class Entry
            {
                public readonly SkillDataSO Skill;
                public readonly string AssetPath;
                public readonly string DisplayName;
                public readonly string Group;
                public readonly string MetaLine;
                public readonly string DescriptionLine;
                public readonly string SearchText;

                public Entry(SkillDataSO skill)
                {
                    Skill = skill;
                    AssetPath = AssetDatabase.GetAssetPath(skill);
                    DisplayName = string.IsNullOrWhiteSpace(skill.SkillName) ? skill.name : skill.SkillName;
                    Group = GetSkillGroup(AssetPath);
                    string active = skill.IsActive ? "Active" : "Passive";
                    string recipe = skill.Recipe != null ? skill.Recipe.name : "No recipe";
                    MetaLine = $"{skill.ID} · {active} · Mana {skill.ManaCost:0.#} · CD {skill.Cooldown:0.##}s · {recipe}";
                    DescriptionLine = string.IsNullOrWhiteSpace(skill.Description) ? "No description" : skill.Description;
                    SearchText = $"{DisplayName} {skill.name} {skill.ID} {active} {recipe} {skill.ManaCost} {skill.Cooldown} {skill.Description} {Group} {AssetPath}".ToLowerInvariant();
                }

                private static string GetSkillGroup(string assetPath)
                {
                    const string root = "Assets/Resources/Skills/";
                    if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith(root))
                        return "Other";

                    string relative = assetPath.Substring(root.Length);
                    int slash = relative.IndexOf('/');
                    return slash > 0 ? relative.Substring(0, slash) : "Root";
                }
            }
        }

        private sealed class StatusEffectPickerPopup : PopupWindowContent
        {
            private readonly StatusEffectSO _current;
            private readonly System.Action<StatusEffectSO> _onSelected;
            private readonly List<Entry> _entries;
            private Vector2 _scroll;
            private string _search = string.Empty;
            private bool _focusSearchRequested = true;

            public StatusEffectPickerPopup(StatusEffectSO current, System.Action<StatusEffectSO> onSelected)
            {
                _current = current;
                _onSelected = onSelected;
                _entries = BuildEntries();
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(520f, 520f);
            }

            public override void OnGUI(Rect rect)
            {
                DrawSearchField();
                EditorGUILayout.Space(4f);

                if (GUILayout.Button("None", EditorStyles.miniButton))
                {
                    Select(null);
                    return;
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                string search = (_search ?? string.Empty).Trim().ToLowerInvariant();
                IEnumerable<Entry> filtered = string.IsNullOrWhiteSpace(search)
                    ? _entries
                    : _entries.Where(entry => entry.SearchText.Contains(search));

                string currentGroup = null;
                foreach (Entry entry in filtered)
                {
                    if (!string.Equals(currentGroup, entry.Group, System.StringComparison.Ordinal))
                    {
                        currentGroup = entry.Group;
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.LabelField(currentGroup, EditorStyles.boldLabel);
                    }

                    DrawEntry(entry);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawSearchField()
            {
                GUI.SetNextControlName("StatusEffectPickerSearch");
                _search = EditorGUILayout.TextField(_search);

                if (_focusSearchRequested && Event.current.type == EventType.Repaint)
                {
                    _focusSearchRequested = false;
                    EditorGUI.FocusTextInControl("StatusEffectPickerSearch");
                }
            }

            private void DrawEntry(Entry entry)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 52f);
                bool isCurrent = entry.Effect == _current;
                bool isHovered = rowRect.Contains(Event.current.mousePosition);

                Color background = isCurrent
                    ? new Color(0.28f, 0.38f, 0.55f, 0.95f)
                    : isHovered
                        ? new Color(0.20f, 0.20f, 0.20f, 0.65f)
                        : new Color(0f, 0f, 0f, 0f);

                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rowRect, background);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    Select(entry.Effect);
                    return;
                }

                Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 8f, 36f, 36f);
                Rect nameRect = new Rect(iconRect.xMax + 8f, rowRect.y + 3f, rowRect.width - 70f, 18f);
                Rect metaRect = new Rect(iconRect.xMax + 8f, rowRect.y + 20f, rowRect.width - 70f, 15f);
                Rect modRect = new Rect(iconRect.xMax + 8f, rowRect.y + 35f, rowRect.width - 70f, 15f);
                Rect checkRect = new Rect(rowRect.xMax - 22f, rowRect.y + 17f, 16f, 16f);

                DrawIcon(iconRect, entry.Effect);

                var nameStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal
                };
                var metaStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.76f, 0.76f, 0.76f, 1f) }
                };

                GUI.Label(nameRect, entry.DisplayName, nameStyle);
                GUI.Label(metaRect, entry.MetaLine, metaStyle);
                GUI.Label(modRect, entry.ModifierLine, metaStyle);

                if (isCurrent)
                    GUI.Label(checkRect, "?", EditorStyles.boldLabel);
            }

            private static void DrawIcon(Rect rect, StatusEffectSO effect)
            {
                EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.11f, 1f));
                if (effect?.Icon == null)
                    return;

                Texture texture = AssetPreview.GetAssetPreview(effect.Icon) ?? AssetPreview.GetMiniThumbnail(effect.Icon);
                if (texture != null)
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            }

            private void Select(StatusEffectSO effect)
            {
                _onSelected?.Invoke(effect);
                editorWindow?.Close();
                GUIUtility.ExitGUI();
            }

            private static List<Entry> BuildEntries()
            {
                var entries = new List<Entry>();
                foreach (string guid in AssetDatabase.FindAssets("t:StatusEffectSO"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    StatusEffectSO effect = AssetDatabase.LoadAssetAtPath<StatusEffectSO>(path);
                    if (effect != null)
                        entries.Add(new Entry(effect, path));
                }

                return entries
                    .OrderBy(entry => entry.Effect.Kind)
                    .ThenBy(entry => entry.DisplayName)
                    .ThenBy(entry => entry.AssetPath)
                    .ToList();
            }

            private sealed class Entry
            {
                public readonly StatusEffectSO Effect;
                public readonly string AssetPath;
                public readonly string DisplayName;
                public readonly string Group;
                public readonly string MetaLine;
                public readonly string ModifierLine;
                public readonly string SearchText;

                public Entry(StatusEffectSO effect, string assetPath)
                {
                    Effect = effect;
                    AssetPath = assetPath;
                    DisplayName = effect.GetDisplayName(true);
                    Group = effect.Kind.ToString();
                    MetaLine = $"{effect.Id} · {effect.Kind} · {effect.BaseDurationSeconds:0.##}s";
                    ModifierLine = BuildStatusEffectModifierSummary(effect, 3);

                    string description = $"{effect.NameEn} {effect.NameRu} {effect.DescriptionEn} {effect.DescriptionRu}";
                    string modifiers = BuildSearchableModifierText(effect);
                    SearchText = $"{DisplayName} {MetaLine} {ModifierLine} {description} {modifiers} {assetPath}".ToLowerInvariant();
                }

                private static string BuildSearchableModifierText(StatusEffectSO effect)
                {
                    if (effect?.Modifiers == null)
                        return string.Empty;

                    var parts = new List<string>();
                    foreach (SerializableStatModifier modifier in effect.Modifiers)
                    {
                        parts.Add(modifier.Stat.ToString());
                        parts.Add(Scripts.Editor.Stats.StatPickerUtility.GetDisplayName(modifier.Stat));
                        parts.Add(modifier.Type.ToString());
                        parts.Add(modifier.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }

                    return string.Join(" ", parts);
                }
            }
        }

        private void MigrateCleaveToRecipe()
        {
            CreateDefaultStepDefinitions();
            var stepDefs = new List<StepDefinitionSO>();
            foreach (var g in AssetDatabase.FindAssets("t:StepDefinitionSO"))
            {
                var d = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(AssetDatabase.GUIDToAssetPath(g));
                if (d != null) stepDefs.Add(d);
            }
            var byId = stepDefs.ToDictionary(d => d.Id, d => d);

            string recipePath = "Assets/Resources/Skills/TwoHandedWeapon/Axe/LeftButton/Recipe_Cleave.asset";
            string recipeDir = System.IO.Path.GetDirectoryName(recipePath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Skills")) AssetDatabase.CreateFolder("Assets/Resources", "Skills");
            EnsureFolder("Assets/Resources/Skills", "TwoHandedWeapon/Axe/LeftButton");

            var recipe = AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(recipePath);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<SkillRecipeSO>();
                recipe.Steps = new List<StepEntry>();
                AssetDatabase.CreateAsset(recipe, recipePath);
            }

            recipe.Steps.Clear();
            void AddStep(string id, float startPct = 0f, float endPct = 0f)
            {
                var entry = new StepEntry();
                if (byId.TryGetValue(id, out var def)) entry.StepDefinition = def;
                entry.StartPercentPipeline = startPct;
                entry.EndPercentPipeline = endPct;
                recipe.Steps.Add(entry);
            }
            void SetFloat(int stepIdx, string key, float val)
            {
                if (stepIdx < 0 || stepIdx >= recipe.Steps.Count) return;
                recipe.Steps[stepIdx].SetOverrideFloat(key, val);
            }
            void SetInt(int stepIdx, string key, int val)
            {
                if (stepIdx < 0 || stepIdx >= recipe.Steps.Count) return;
                recipe.Steps[stepIdx].Overrides.RemoveAll(x => x.Key == key);
                recipe.Steps[stepIdx].Overrides.Add(new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Int, IntVal = val });
            }
            void SetObj(int stepIdx, string key, Object obj)
            {
                if (stepIdx < 0 || stepIdx >= recipe.Steps.Count) return;
                recipe.Steps[stepIdx].SetOverrideObject(key, obj);
            }

            AddStep("MovementLock", 0f, 1f);
            AddStep("WeaponWindup", 0f, 0.35f);
            AddStep("WeaponStrike", 0.35f, 0.35f);
            AddStep("SpawnVFX", 0.35f, 0.75f);
            var vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/VFX_Cleave.prefab");
            if (vfxPrefab != null) SetObj(3, "VfxPrefab", vfxPrefab);
            SetFloat(3, "OffsetX", 0.2f);
            SetFloat(3, "OffsetY", 0f);
            AddStep("DealDamageCircle", 0.35f, 0.35f); SetInt(4, "SourceStepIndex", 3); SetFloat(4, "SizeX", 1f); SetFloat(4, "SizeY", 1f); SetFloat(4, "DamageMultiplier", 1f);
            AddStep("WeaponRecovery", 0.35f, 1f);
            AddStep("MovementUnlock", 1f, 1f);

            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();

            var cleaveSkill = AssetDatabase.LoadAssetAtPath<SkillDataSO>("Assets/Resources/Skills/TwoHandedWeapon/Axe/LeftButton/CleaveLB.asset");
            if (cleaveSkill != null)
            {
                cleaveSkill.Recipe = recipe;
                EditorUtility.SetDirty(cleaveSkill);
            }

            string prefabPath = "Assets/Prefabs/Skills/Skill_Cleave_Logic.prefab";
            string newPrefabPath = "Assets/Prefabs/Skills/Skill_Cleave_StepRunner.prefab";
            var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (oldPrefab != null && AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath) == null)
            {
                var roots = PrefabUtility.LoadPrefabContents(prefabPath);
                var cleave = roots.GetComponent<CleaveSkill>();
                var circle = roots.GetComponent<CircleHitbox>();
                int layerBits = 128;
                if (circle != null)
                {
                    var so = new SerializedObject(circle);
                    var layer = so.FindProperty("_targetLayer");
                    if (layer != null) layerBits = layer.intValue;
                }
                if (cleave != null)
                {
                    var runner = roots.AddComponent<SkillStepRunner>();
                    var soRun = new SerializedObject(runner);
                    var layerProp = soRun.FindProperty("_targetLayer");
                    if (layerProp != null) { layerProp.intValue = layerBits; soRun.ApplyModifiedPropertiesWithoutUndo(); }
                    Object.DestroyImmediate(cleave);
                }
                var circleC = roots.GetComponent<CircleHitbox>();
                var damageC = roots.GetComponent<SkillDamageDealer>();
                if (circleC != null) Object.DestroyImmediate(circleC);
                if (damageC != null) Object.DestroyImmediate(damageC);
                PrefabUtility.SaveAsPrefabAsset(roots, newPrefabPath);
                PrefabUtility.UnloadPrefabContents(roots);
                if (cleaveSkill != null)
                {
                    cleaveSkill.Recipe = recipe;
                    var prefabRef = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);
                    if (prefabRef != null)
                    {
                        var soSkill = new SerializedObject(cleaveSkill);
                        soSkill.FindProperty("SkillPrefab").objectReferenceValue = prefabRef;
                        soSkill.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorUtility.SetDirty(cleaveSkill);
                }
                AssetDatabase.SaveAssets();
            }

            Refresh();
            if (cleaveSkill != null) _selectedSkillIndex = _skills.IndexOf(cleaveSkill);
            EditorUtility.DisplayDialog("Rebuild Cleave recipe", "Recipe rebuilt with Start%/End% timing. CleaveLB assigned. Prefab Skill_Cleave_StepRunner created/kept if needed.", "OK");
        }

        private static void EnsureFolder(string parent, string path)
        {
            string current = parent;
            foreach (var part in path.Split('/'))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        private SkillRecipeSO CreateRecipeForSkill(SkillDataSO skill)
        {
            if (skill == null)
                return null;

            string skillPath = AssetDatabase.GetAssetPath(skill);
            if (string.IsNullOrEmpty(skillPath))
                return null;

            string directory = System.IO.Path.GetDirectoryName(skillPath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            string skillName = string.IsNullOrWhiteSpace(skill.SkillName) ? skill.name : skill.SkillName;
            string safeName = SanitizeFileName(skillName);
            string recipePath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/Recipe_{safeName}.asset");

            var recipe = ScriptableObject.CreateInstance<SkillRecipeSO>();
            recipe.Steps = new List<StepEntry>();
            AssetDatabase.CreateAsset(recipe, recipePath);
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static string SanitizeFileName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Skill";

            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = rawName
                .Select(ch => invalid.Contains(ch) ? '_' : ch)
                .ToArray();

            return new string(chars).Replace(' ', '_');
        }
    }
}
