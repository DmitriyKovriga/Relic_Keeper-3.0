using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.PassiveTree
{
    public static class RogueTreePlanBuilder
    {
        public const string PlanPath = "Docs/GameDesign/RoguePassiveTree/rogue-passive-tree.json";
        public const string TreePath = "Assets/Resources/PassiveTrees/RoguePassiveSkillTree/RoguePassiveSkillTree.asset";
        public const string IconRoot = "Assets/Resources/PassiveTrees/Templates/Rogue/Icons/";
        [Serializable] public class Plan { public Entry[] templateRegistry; public Node[] nodes; }
        [Serializable] public class Entry { public string path; public Content content; public string[] nodeIds; }
        [Serializable] public class Content { public string nameEn, nameRu; public Mod[] modifiers; public Scale[] statScalingRules; }
        [Serializable] public class Mod { public string Stat, Type; public float Value; public int StatValue, TypeValue; }
        [Serializable] public class Scale { public string SourceStat, TargetStat, TargetModifierType; public float SourceAmountPerStep, TargetValuePerStep; public bool UseWholeSteps; }
        [Serializable] public class Node { public string id, nodeType, clusterId, templateMode, templatePath; public int placementMode, orbitIndex; public float orbitAngle; public Vector2 rawPosition; public string[] originalConnections, connections; }
        public static Plan ReadPlan() => JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));

        public static void ValidateGeometry(Plan plan, PassiveSkillTreeSO tree)
        {
            if (tree.Nodes.Count != 130 || plan.templateRegistry.Length != 43) throw new InvalidOperationException("Unexpected Rogue plan size.");
            foreach (var source in plan.nodes)
            {
                var node = tree.Nodes.Single(n => n.ID == source.id);
                if (node.NodeType.ToString() != source.nodeType || (int)node.PlacementMode != source.placementMode ||
                    Vector2.Distance(node.Position, source.rawPosition) > .001f || node.ClusterID != source.clusterId ||
                    node.OrbitIndex != source.orbitIndex || Mathf.Abs(node.OrbitAngle - source.orbitAngle) > .001f)
                    throw new InvalidOperationException("Newer geometry differs from the plan: " + source.id);
                if (!new HashSet<string>(node.ConnectionIDs).SetEquals(source.originalConnections) &&
                    !new HashSet<string>(node.ConnectionIDs).SetEquals(source.connections))
                    throw new InvalidOperationException("Newer connections differ from the plan: " + source.id);
            }
        }

        [MenuItem("Tools/Passive Tree/Apply Rogue Plan v4")]
        public static void Apply()
        {
            var plan = ReadPlan(); var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(TreePath);
            ValidateGeometry(plan, tree);
            // Preflight every enum and image before touching any existing asset.
            foreach (var entry in plan.templateRegistry)
            {
                if (!File.Exists(IconPath(entry))) throw new FileNotFoundException(IconPath(entry));
                foreach (var mod in entry.content.modifiers)
                    if ((int)Enum.Parse<StatType>(mod.Stat) != mod.StatValue || (int)Enum.Parse<StatModType>(mod.Type) != mod.TypeValue)
                        throw new InvalidOperationException("Enum changed: " + mod.Stat);
            }
            var tables = new[] { "en", "ru" }.Select(code => AssetDatabase.LoadAssetAtPath<StringTable>(
                $"Assets/Localization/LocalizationTables/MenuLabels_{code}.asset")).ToArray();
            foreach (var entry in plan.templateRegistry)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.path));
                ImportIcon(IconPath(entry));
                var template = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(entry.path);
                if (template == null) { template = ScriptableObject.CreateInstance<PassiveNodeTemplateSO>(); AssetDatabase.CreateAsset(template, entry.path); }
                template.Name = entry.content.nameEn; template.Description = "";
                template.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(entry));
                template.Modifiers = entry.content.modifiers.Select(mod => new SerializableStatModifier {
                    Stat = Enum.Parse<StatType>(mod.Stat), Type = Enum.Parse<StatModType>(mod.Type), Value = mod.Value }).ToList();
                template.StatScalingRules = entry.content.statScalingRules.Select(rule => new PassiveStatScalingRule {
                    SourceStat = Enum.Parse<StatType>(rule.SourceStat), SourceAmountPerStep = rule.SourceAmountPerStep,
                    UseWholeSteps = rule.UseWholeSteps, TargetStat = Enum.Parse<StatType>(rule.TargetStat),
                    TargetValuePerStep = rule.TargetValuePerStep, TargetModifierType = Enum.Parse<StatModType>(rule.TargetModifierType) }).ToList();
                template.ConditionalModifiers.Clear(); template.TriggeredEffects.Clear(); template.HitScalingRules.Clear();
                EditorUtility.SetDirty(template);
                for (int i = 0; i < tables.Length; i++)
                {
                    tables[i].AddEntry($"passive.node.{template.name}.name", i == 0 ? entry.content.nameEn : entry.content.nameRu);
                    tables[i].AddEntry($"passive.node.{template.name}.description", "");
                }
            }
            foreach (var source in plan.nodes)
            {
                var node = tree.Nodes.Single(n => n.ID == source.id);
                node.Template = source.templateMode == "none" ? null : AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(source.templatePath);
                if (source.templateMode != "none" && node.Template == null) throw new InvalidOperationException("Unresolved template: " + source.id);
                node.UniqueModifiers = new List<SerializableStatModifier>(); node.UniqueStatScalingRules = new List<PassiveStatScalingRule>();
                foreach (string neighbor in source.connections)
                    if (!node.ConnectionIDs.Contains(neighbor)) node.ConnectionIDs.Add(neighbor);
            }
            // Transport remains the existing shared asset; only add missing presentation entries.
            var transport = tree.Nodes.First(n => plan.nodes.Single(p => p.id == n.ID).templateMode == "reuse-existing").Template;
            tables[0].AddEntry($"passive.node.{transport.name}.name", "Travel"); tables[1].AddEntry($"passive.node.{transport.name}.name", "Транспорт");
            foreach (var table in tables) { EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData); }
            tree.InitLookup(); EditorUtility.SetDirty(tree); AssetDatabase.SaveAssets();
        }
        public static string IconPath(Entry entry) => IconRoot + Path.GetFileNameWithoutExtension(entry.path) + ".png";
        [MenuItem("Tools/Passive Tree/Refresh Rogue Icons")]
        public static void RefreshIcons()
        {
            foreach (var entry in ReadPlan().templateRegistry)
                ImportIcon(IconPath(entry));
        }
        private static void ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
