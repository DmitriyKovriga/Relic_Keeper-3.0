using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.PassiveTree;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Tests.EditMode
{
    public class RogueTreePlanTests
    {
        private static PassiveSkillTreeSO Tree => AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(RogueTreePlanBuilder.TreePath);
        public static IEnumerable NodeIds => RogueTreePlanBuilder.ReadPlan().nodes.Select(n => n.id);
        [TestCaseSource(nameof(NodeIds))]
        public void EveryNodeMatchesCanonicalPlan(string id)
        {
            var plan = RogueTreePlanBuilder.ReadPlan(); var spec = plan.nodes.Single(n => n.id == id);
            var node = Tree.Nodes.Single(n => n.ID == id);
            Assert.That(node.NodeType.ToString(), Is.EqualTo(spec.nodeType));
            Assert.That((int)node.PlacementMode, Is.EqualTo(spec.placementMode));
            Assert.That(node.Position, Is.EqualTo(spec.rawPosition)); Assert.That(node.ClusterID, Is.EqualTo(spec.clusterId));
            Assert.That(node.OrbitIndex, Is.EqualTo(spec.orbitIndex)); Assert.That(node.OrbitAngle, Is.EqualTo(spec.orbitAngle).Within(.001));
            Assert.That(node.ConnectionIDs, Is.EquivalentTo(spec.connections));
            Assert.That(node.UniqueModifiers, Is.Empty); Assert.That(node.UniqueStatScalingRules, Is.Empty);
            if (spec.templateMode == "none") { Assert.That(node.Template, Is.Null); return; }
            Assert.That(AssetDatabase.GetAssetPath(node.Template), Is.EqualTo(spec.templatePath));
            if (spec.templateMode == "reuse-existing")
            {
                Assert.That(node.GetFinalModifiers().Select(m => (m.Stat, m.Value, m.Type)), Is.EquivalentTo(new[] {
                    (StatType.MaxHealth, 3f, StatModType.Flat), (StatType.Evasion, 5f, StatModType.Flat) }));
                return;
            }
            var entry = plan.templateRegistry.Single(e => e.path == spec.templatePath);
            Assert.That(node.Template.Modifiers.Select(m => (m.Stat.ToString(), m.Value, m.Type.ToString())),
                Is.EqualTo(entry.content.modifiers.Select(m => (m.Stat, m.Value, m.Type))));
            Assert.That(node.Template.StatScalingRules.Count, Is.EqualTo(entry.content.statScalingRules.Length));
            Assert.That(node.Template.HasRuntimeEffects, Is.False);
            Assert.That(node.Template.Icon, Is.Not.Null); Assert.That(node.Template.Icon.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(node.Template.Icon.rect.width, Is.EqualTo(node.NodeType == PassiveNodeType.Keystone ? 48 : 32));
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/MenuLabels_{code}.asset");
                Assert.That(table.GetEntry($"passive.node.{node.Template.name}.name")?.Value,
                    Is.EqualTo(code == "ru" ? entry.content.nameRu : entry.content.nameEn));
            }
        }
        [Test] public void SharedTemplatesAndTopologyAreExact()
        {
            Assert.That(Tree.Nodes.Count, Is.EqualTo(130));
            Assert.That(Tree.Nodes.Where(n => n.Template != null).Select(n => n.Template).Distinct().Count(), Is.EqualTo(44));
            Assert.That(Tree.Nodes.Count(n => n.Template != null && n.Template.name == "transportRogue"), Is.EqualTo(24));
            Assert.That(Tree.Nodes.Sum(n => n.ConnectionIDs.Count) / 2, Is.EqualTo(150));
            RogueTreePlanBuilder.ValidateGeometry(RogueTreePlanBuilder.ReadPlan(), Tree);
        }
        [TestCase(999f, 0f)] [TestCase(1000f, 5f)] [TestCase(2500f, 10f)]
        public void DanceOfBladesTracksFinalEvasionAndRefundRemovesItsBonus(float evasion, float bonus)
        {
            var owner = new GameObject("Rogue scaling test"); owner.SetActive(false);
            try
            {
                var stats = owner.AddComponent<PlayerStats>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(PlayerStats).GetMethod("Awake", flags).Invoke(stats, null);
                stats.GetStat(StatType.Evasion).BaseValue = evasion; stats.GetStat(StatType.CritMultiplier).BaseValue = 150;
                var manager = owner.AddComponent<PassiveTreeManager>();
                typeof(PassiveTreeManager).GetField("_playerStats", flags).SetValue(manager, stats);
                typeof(PassiveTreeManager).GetMethod("OnEnable", flags).Invoke(manager, null);
                manager.SetTreeData(Tree); manager.LoadState(new List<string> { "start", "n3" });
                Assert.That(stats.GetValue(StatType.CritMultiplier), Is.EqualTo(150 + bonus).Within(.001));
                manager.LoadState(new List<string> { "start" });
                Assert.That(stats.GetValue(StatType.CritMultiplier), Is.EqualTo(150).Within(.001));
            }
            finally
            {
                var manager = owner.GetComponent<PassiveTreeManager>();
                if (manager != null) typeof(PassiveTreeManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                Object.DestroyImmediate(owner);
            }
        }
    }
}
