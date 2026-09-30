using System;
using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    public static class PassiveZoneToolsPanel
    {
        const string SeedKey = "RK.ZoneGen.Seed";
        const string AdvancedKey = "RK.ZoneGen.Advanced";
        const string PiecesKey = "RK.ZoneGen.Pieces";
        const string NodesKey = "RK.ZoneGen.Nodes";
        const string SmallKey = "RK.ZoneGen.Small";
        const string MediumKey = "RK.ZoneGen.Medium";
        const string LargeKey = "RK.ZoneGen.Large";
        const string ComplexKey = "RK.ZoneGen.Complex";
        const string DeadKey = "RK.ZoneGen.Dead";

        static int _zone = 0;

        static string _status = "";

        public static void Draw(PassiveSkillTreeSO tree, PassiveTreeEditorCanvas canvas, Action refresh)
        {
            if (tree == null)
            {
                EditorGUILayout.HelpBox("Сначала выбери дерево.", MessageType.Info);
                return;
            }

            PassiveZoneOps.EnsureLinks(tree);
            EditorGUILayout.LabelField("Зоны каркаса", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            tree.ZoneToolsEnabled = EditorGUILayout.Toggle("Инструменты зон", tree.ZoneToolsEnabled);
            using (new EditorGUI.DisabledScope(!tree.ZoneToolsEnabled))
                tree.LockBackbone = EditorGUILayout.Toggle("Держать каркас", tree.LockBackbone);
            if (EditorGUI.EndChangeCheck())
            {
                if (tree.ZoneToolsEnabled && tree.LockBackbone)
                    PassiveZoneOps.CaptureBackboneEdges(tree);
                EditorUtility.SetDirty(tree);
            }

            int backbone = PassiveZoneMath.CountBackbone(tree);
            EditorGUILayout.LabelField("Нодов каркаса", backbone.ToString());
            if (backbone == 0)
            {
                EditorGUILayout.HelpBox(
                    "Пометь каркас, когда в дереве только основа: спицы, кольцо и углы. После этого генерация заполняет выбранный клин и не трогает эти ноды.",
                    MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Пометить все ноды"))
                    Run(tree, canvas, refresh, () => PassiveZoneOps.MarkAllAsBackbone(tree));
                if (GUILayout.Button("Снять пометку"))
                    Run(tree, canvas, refresh, () => PassiveZoneOps.ClearBackbone(tree));
            }

            List<PassiveNodeDefinition> selected = canvas != null ? canvas.GetSelectedNodeData() : null;
            using (new EditorGUI.DisabledScope(selected == null || selected.Count == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Выделение — каркас"))
                        Run(tree, canvas, refresh, () => PassiveZoneOps.MarkNodes(tree, selected, true));
                    if (GUILayout.Button("Выделение — обычные"))
                        Run(tree, canvas, refresh, () => PassiveZoneOps.MarkNodes(tree, selected, false));
                }
            }

            EditorGUILayout.Space(8f);
            _zone = GUILayout.Toolbar(_zone, PassiveZoneMath.ZoneNames);
            EditorGUILayout.LabelField("Наполнение", PassiveZoneMath.ContentCount(tree, _zone) + " нодов");

            using (new EditorGUI.DisabledScope(!tree.ZoneToolsEnabled || backbone == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Сгенерировать"))
                        Generate(tree, canvas, refresh, false);
                    if (GUILayout.Button("Ещё раз"))
                        Generate(tree, canvas, refresh, true);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(PassiveZoneMath.FlipIsHorizontal(_zone) ? "Флип по горизонтали" : "Флип по вертикали"))
                        Run(tree, canvas, refresh, () =>
                        {
                            PassiveZoneOps.FlipZone(tree, _zone);
                            PassiveZoneOps.SyncStructure(tree);
                        });
                    if (GUILayout.Button("Копия на противоположную"))
                        Run(tree, canvas, refresh, () => PassiveZoneOps.CopyZone(tree, _zone, PassiveZoneMath.Opposite(_zone)));
                }
                if (GUILayout.Button("Очистить зону"))
                    Run(tree, canvas, refresh, () =>
                    {
                        PassiveZoneOps.ClearZone(tree, _zone);
                        PassiveZoneOps.SyncStructure(tree);
                    });

                PassiveZoneLink pair = PassiveZoneOps.FindPair(tree, _zone);
                if (pair != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.ToggleLeft(
                        "Повторять правки в " + PassiveZoneMath.ZoneNames[PassiveZoneMath.Opposite(_zone)],
                        pair.Enabled && pair.SourceZone == _zone);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(tree, "Zone Link");
                        pair.Enabled = enabled;
                        if (enabled)
                        {
                            PassiveZoneOps.MakeDriver(tree, _zone);
                            PassiveZoneOps.SyncStructure(tree);
                        }
                        EditorUtility.SetDirty(tree);
                        canvas?.PopulateView(tree);
                        refresh?.Invoke();
                    }
                    if (pair.Enabled)
                    {
                        EditorGUILayout.HelpBox(
                            PassiveZoneMath.ZoneNames[pair.SourceZone] + " ведёт, " +
                            PassiveZoneMath.ZoneNames[pair.TargetZone] + " повторяет её поворотом на 180°. Правки ведомой зоны сотрутся.",
                            MessageType.None);
                    }
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Параметры генерации", EditorStyles.boldLabel);
            bool advanced = EditorGUILayout.Toggle("Подробный режим", EditorPrefs.GetBool(AdvancedKey, false));
            EditorPrefs.SetBool(AdvancedKey, advanced);
            if (!advanced)
            {
                IntPref(PiecesKey, "Кластеров и ветвей", 5, 0, 12);
                IntPref(NodesKey, "Лимит нодов", 32, 0, 80);
                EditorGUILayout.LabelField("Количество сущностей точное, состав автоматический. Подводящие ноды входят в лимит, каркас — нет. 0 — без лимита.", EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                IntPref(SmallKey, "Малые кластеры", 1, 0, 6);
                IntPref(MediumKey, "Средние кластеры", 1, 0, 4);
                IntPref(LargeKey, "Большие кластеры", 1, 0, 3);
                IntPref(ComplexKey, "Сложные (несколько орбит)", 0, 0, 3);
                IntPref(DeadKey, "Тупиковые ветки", 2, 0, 8);
                IntPref(NodesKey, "Лимит нодов", 36, 0, 80);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int seed = EditorPrefs.GetInt(SeedKey, 1);
                int next = EditorGUILayout.IntField("Зерно", seed);
                if (next != seed)
                    EditorPrefs.SetInt(SeedKey, next);
                if (GUILayout.Button("Случайное", GUILayout.Width(90f)))
                    EditorPrefs.SetInt(SeedKey, UnityEngine.Random.Range(1, int.MaxValue));
            }

            EditorGUILayout.HelpBox(
                "До среднего нода: минимум 2 малых у малого кластера и ветви, 3 у среднего, 4 у большого и сложного. Проверяется самый дешёвый путь от каркаса. Если весь заказ не помещается, прежняя зона сохраняется.",
                MessageType.Info);
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        static void Generate(PassiveSkillTreeSO tree, PassiveTreeEditorCanvas canvas, Action refresh, bool reroll)
        {
            if (reroll)
                EditorPrefs.SetInt(SeedKey, UnityEngine.Random.Range(1, int.MaxValue));
            var settings = new ZoneGenSettings
            {
                Advanced = EditorPrefs.GetBool(AdvancedKey, false),
                Pieces = EditorPrefs.GetInt(PiecesKey, 5),
                Nodes = EditorPrefs.GetInt(NodesKey, 32),
                Small = EditorPrefs.GetInt(SmallKey, 1),
                Medium = EditorPrefs.GetInt(MediumKey, 1),
                Large = EditorPrefs.GetInt(LargeKey, 1),
                Complex = EditorPrefs.GetInt(ComplexKey, 0),
                DeadEnds = EditorPrefs.GetInt(DeadKey, 2),
                Seed = EditorPrefs.GetInt(SeedKey, 1)
            };
            Run(tree, canvas, refresh, () =>
            {
                ZoneGenResult result = PassiveZoneGenerator.Generate(tree, _zone, settings);
                if (result.Success)
                {
                    PassiveZoneLink pair = PassiveZoneOps.FindPair(tree, _zone);
                    if (pair != null && pair.Enabled && pair.SourceZone != _zone)
                        PassiveZoneOps.MakeDriver(tree, _zone);
                    PassiveZoneOps.SyncStructure(tree);
                }
                _status = result.Message;
            });
        }

        static void IntPref(string key, string label, int fallback, int min, int max)
        {
            int value = Mathf.Clamp(EditorPrefs.GetInt(key, fallback), min, max);
            int next = EditorGUILayout.IntSlider(label, value, min, max);
            if (next != value)
                EditorPrefs.SetInt(key, next);
        }

        static void Run(PassiveSkillTreeSO tree, PassiveTreeEditorCanvas canvas, Action refresh, Action action)
        {
            Undo.RecordObject(tree, "Zone Tools");
            action();
            EditorUtility.SetDirty(tree);
            canvas?.PopulateView(tree);
            refresh?.Invoke();
        }
    }
}
