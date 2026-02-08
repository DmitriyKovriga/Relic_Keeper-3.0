using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Modules;

namespace Scripts.Editor.Skills
{
    public class SkillEditorWindow : EditorWindow
    {
        private List<SkillRecipeSO> _recipes = new List<SkillRecipeSO>();
        private List<SkillDataSO> _skills = new List<SkillDataSO>();
        private List<StepDefinitionSO> _stepDefs = new List<StepDefinitionSO>();
        private int _selectedSkillIndex;
        private bool _displayRu = true;
        private Vector2 _stepsInSkillScroll;
        private Vector2 _typesScroll;
        private Vector2 _inspectorScroll;
        private int _selectedStepIndex = -1;
        private int _selectedSubStepIndex = -1;
        private const float LeftColFraction = 0.30f;
        private const float CenterColFraction = 0.40f;
        private const float RightColFraction = 0.30f;

        [MenuItem("Tools/Skill Editor")]
        public static void Open()
        {
            var w = GetWindow<SkillEditorWindow>();
            w.titleContent = new GUIContent("Skill Editor");
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            _recipes.Clear();
            _skills.Clear();
            _stepDefs.Clear();
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
            foreach (var g in AssetDatabase.FindAssets("t:StepDefinitionSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var d = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(path);
                if (d != null) _stepDefs.Add(d);
            }
            _recipes = _recipes.OrderBy(x => x.name).ToList();
            _skills = _skills.OrderBy(x => x.name).ToList();
            _stepDefs = _stepDefs.OrderBy(x => x.GetDisplayName(false)).ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Skill", GUILayout.Width(36));
            var skillNames = _skills.Select(s => s.SkillName ?? s.name).ToArray();
            _selectedSkillIndex = EditorGUILayout.Popup(_selectedSkillIndex, skillNames);
            if (GUILayout.Button("Refresh", GUILayout.Width(60))) Refresh();
            if (GUILayout.Button("Create default step defs", GUILayout.Width(140))) CreateDefaultStepDefinitions();
            if (GUILayout.Button("Rebuild Cleave recipe", GUILayout.Width(160))) MigrateCleaveToRecipe();
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
            DrawRightColumn(recipe, rightW);

            EditorGUILayout.EndHorizontal();
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
                    _selectedStepIndex = recipe.Steps.Count - 1;
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
                    : $"{startPct:F0}% – {endPct:F0}%";

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
                    _selectedStepIndex = i;
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("↑", GUILayout.Width(20)))
                { moveFrom = i; moveDir = -1; }
                if (GUILayout.Button("↓", GUILayout.Width(20)))
                { moveFrom = i; moveDir = 1; }
                if (GUILayout.Button("−", GUILayout.Width(20)))
                    toRemove = i;
                EditorGUILayout.EndHorizontal();
            }

            if (moveFrom >= 0 && moveDir != 0)
            {
                int to = moveFrom + moveDir;
                if (to >= 0 && to < recipe.Steps.Count)
                {
                    var tmp = recipe.Steps[moveFrom];
                    recipe.Steps[moveFrom] = recipe.Steps[to];
                    recipe.Steps[to] = tmp;
                    _selectedStepIndex = to;
                    EditorUtility.SetDirty(recipe);
                }
            }
            if (toRemove >= 0)
            {
                recipe.Steps.RemoveAt(toRemove);
                EditorUtility.SetDirty(recipe);
                if (_selectedStepIndex >= recipe.Steps.Count) _selectedStepIndex = recipe.Steps.Count - 1;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRightColumn(SkillRecipeSO recipe, float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            GUILayout.Label("Inspector", EditorStyles.boldLabel);
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll, GUILayout.ExpandHeight(true));

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

        private void DrawStepOverrides(SkillRecipeSO recipe, StepEntry step)
        {
            EditorGUILayout.Space(2);
            GUILayout.Label("Timing (% of pipeline)", EditorStyles.miniBoldLabel);
            bool isDuration = step.StepDefinition != null && step.StepDefinition.IsDurationStep;
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
                step.StepDefinition = _stepDefs[newPopup];
                EditorUtility.SetDirty(recipe);
            }

            EditorGUILayout.Space(6);
            GUILayout.Label("Step settings", EditorStyles.miniBoldLabel);
            DrawStepTypeFields(recipe, step);
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
                if (GUILayout.Button(subLabel, GUILayout.ExpandWidth(true))) _selectedSubStepIndex = i;
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("−", GUILayout.Width(22))) removeSub = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeSub >= 0)
            {
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
                _selectedSubStepIndex = groupStep.SubSteps.Count - 1;
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
                    sub.StepDefinition = _stepDefs[newPopup];
                    EditorUtility.SetDirty(recipe);
                }
                DrawStepTypeFields(recipe, sub);
            }
        }

        private void DrawStepTypeFields(SkillRecipeSO recipe, StepEntry step)
        {
            string id = step.StepDefinition != null ? step.StepDefinition.Id : "";

            if (id == "WeaponWindup" || id == "WeaponRecovery" || id == "Wait")
            {
                EditorGUILayout.HelpBox("Use Start % and End % in Timing section above. Duration = End − Start.", MessageType.None);
                return;
            }

            if (id == "SpawnVFX")
            {
                var prefab = step.GetObject<GameObject>("VfxPrefab");
                var newPrefab = (GameObject)EditorGUILayout.ObjectField("VFX Prefab", prefab, typeof(GameObject), false);
                if (newPrefab != prefab) { step.SetOverrideObject("VfxPrefab", newPrefab); EditorUtility.SetDirty(recipe); }
                float sm = step.GetFloat("ScaleMultiplier", 1f);
                float nsm = EditorGUILayout.FloatField("Scale multiplier", sm);
                if (Mathf.Abs(nsm - sm) > 0.001f) { step.SetOverrideFloat("ScaleMultiplier", nsm); EditorUtility.SetDirty(recipe); }
                float ox = step.GetFloat("OffsetX", 0f);
                float nox = EditorGUILayout.FloatField("Offset X", ox);
                if (nox != ox) { step.SetOverrideFloat("OffsetX", nox); EditorUtility.SetDirty(recipe); }
                float oy = step.GetFloat("OffsetY", 0f);
                float noy = EditorGUILayout.FloatField("Offset Y", oy);
                if (noy != oy) { step.SetOverrideFloat("OffsetY", noy); EditorUtility.SetDirty(recipe); }
                float bd = step.GetFloat("BaseDuration", 0.5f);
                float nbd = EditorGUILayout.FloatField("Base duration (sec)", bd);
                if (nbd != bd) { step.SetOverrideFloat("BaseDuration", nbd); EditorUtility.SetDirty(recipe); }
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
                EditorGUILayout.HelpBox("Source step index = индекс степа Spawn VFX. Центр и масштаб — из того степа. Если задан «Damage at VFX life %» > 0, урон наносится в этот момент жизни VFX, а не по Trigger at % пайплайна.", MessageType.None);
                float r = step.GetFloat("Radius", 1.5f);
                float nr = EditorGUILayout.FloatField("Radius", r);
                if (nr != r) { step.SetOverrideFloat("Radius", nr); EditorUtility.SetDirty(recipe); }
                int src = step.GetInt("SourceStepIndex", -1);
                int nsrc = EditorGUILayout.IntField("Source step index (Spawn VFX, −1 = от игрока)", src);
                if (nsrc != src) { step.SetOverrideInt("SourceStepIndex", nsrc); EditorUtility.SetDirty(recipe); }
                EditorGUI.BeginDisabledGroup(src < 0);
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
                EditorGUILayout.HelpBox("Source step index = индекс степа Spawn VFX. «Damage at VFX life %» > 0 = урон в этот момент жизни VFX.", MessageType.None);
                float sx = step.GetFloat("SizeX", 2f);
                float nsx = EditorGUILayout.FloatField("Size X", sx);
                if (nsx != sx) { step.SetOverrideFloat("SizeX", nsx); EditorUtility.SetDirty(recipe); }
                float sy = step.GetFloat("SizeY", 1f);
                float nsy = EditorGUILayout.FloatField("Size Y", sy);
                if (nsy != sy) { step.SetOverrideFloat("SizeY", nsy); EditorUtility.SetDirty(recipe); }
                float ang = step.GetFloat("Angle", 0f);
                float nang = EditorGUILayout.FloatField("Angle (deg)", ang);
                if (nang != ang) { step.SetOverrideFloat("Angle", nang); EditorUtility.SetDirty(recipe); }
                int src = step.GetInt("SourceStepIndex", -1);
                int nsrc = EditorGUILayout.IntField("Source step index (−1 = use offset)", src);
                if (nsrc != src) { step.SetOverrideInt("SourceStepIndex", nsrc); EditorUtility.SetDirty(recipe); }
                EditorGUI.BeginDisabledGroup(src < 0);
                float vfxLifeR = step.GetFloat("VfxLifetimePercent", 0f);
                float nvfxLifeR = EditorGUILayout.Slider("Damage at VFX life %", vfxLifeR, 0f, 1f);
                if (Mathf.Abs(nvfxLifeR - vfxLifeR) > 0.001f) { step.SetOverrideFloat("VfxLifetimePercent", nvfxLifeR); EditorUtility.SetDirty(recipe); }
                EditorGUI.EndDisabledGroup();
                float dm = step.GetFloat("DamageMultiplier", 1f);
                float ndm = EditorGUILayout.FloatField("Damage multiplier", dm);
                if (ndm != dm) { step.SetOverrideFloat("DamageMultiplier", ndm); EditorUtility.SetDirty(recipe); }
                return;
            }

            if (id == "MovementLock" || id == "MovementUnlock" || id == "WeaponStrike")
            {
                EditorGUILayout.HelpBox("No extra settings for this step type.", MessageType.None);
            }
        }

        private void CreateDefaultStepDefinitions()
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
                ("MovementLock", "Lock movement", "Блок движения", 0f),
                ("MovementUnlock", "Unlock movement", "Разблок движения", 0f),
                ("WeaponWindup", "Weapon windup", "Замах оружия", 35f),
                ("WeaponStrike", "Weapon strike", "Удар оружия", 0f),
                ("WeaponRecovery", "Weapon recovery", "Возврат оружия", 65f),
                ("Wait", "Wait", "Ожидание", 10f),
                ("SpawnVFX", "Spawn VFX", "Спавн VFX", 0f),
                ("DealDamageCircle", "Deal damage (circle)", "Урон круг", 0f),
                ("DealDamageRectangle", "Deal damage (rectangle)", "Урон прямоугольник", 0f),
                ("ParallelGroup", "Parallel group", "Параллельная группа", 0f),
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
            Refresh();
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
            AddStep("SpawnVFX", 0.35f, 0.35f);
            var vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/VFX_Cleave.prefab");
            if (vfxPrefab != null) SetObj(3, "VfxPrefab", vfxPrefab);
            SetFloat(3, "OffsetX", 0.2f);
            SetFloat(3, "OffsetY", 0f);
            SetFloat(3, "BaseDuration", 0.4f);
            AddStep("DealDamageCircle", 0.35f, 0.35f); SetInt(4, "SourceStepIndex", 3); SetFloat(4, "Radius", 1.5f); SetFloat(4, "DamageMultiplier", 1f);
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
    }
}
