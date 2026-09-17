using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveAilmentChanceTests
    {
        [Test]
        public void SmallPoisonNode_AppliesItsChanceToPlayerStats()
        {
            PassiveNodeTemplateSO template = Resources.Load<PassiveNodeTemplateSO>(
                "PassiveTrees/Templates/Ailments/NewPassiveNode/SmallPoison");
            Assert.That(template, Is.Not.Null);

            var host = new GameObject("PassiveAilmentChanceTestHost");
            PassiveSkillTreeSO tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                const string nodeId = "poison-chance-node";
                tree.Nodes.Add(new PassiveNodeDefinition { ID = nodeId, Template = template });

                PlayerStats stats = host.AddComponent<PlayerStats>();
                PassiveTreeManager manager = host.AddComponent<PassiveTreeManager>();
                SetPlayerStats(manager, stats);
                manager.SetTreeData(tree);
                manager.LoadState(new List<string> { nodeId });

                Assert.That(stats.GetValue(StatType.PoisonChance), Is.EqualTo(10f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PassiveAilmentChanceModifiers_UseFlatValues()
        {
            var chanceStats = new HashSet<StatType>
            {
                StatType.BleedChance,
                StatType.PoisonChance,
                StatType.IgniteChance,
                StatType.FreezeChance,
                StatType.ShockChance
            };

            PassiveNodeTemplateSO[] templates = Resources.LoadAll<PassiveNodeTemplateSO>("PassiveTrees/Templates");
            Assert.That(templates, Is.Not.Empty);

            foreach (PassiveNodeTemplateSO template in templates)
            {
                if (template?.Modifiers == null)
                    continue;

                foreach (SerializableStatModifier modifier in template.Modifiers.Where(modifier => chanceStats.Contains(modifier.Stat)))
                {
                    Assert.That(
                        modifier.Type,
                        Is.EqualTo(StatModType.Flat),
                        $"{template.name}: ailment chance must grant a direct chance value, not scale a zero base chance.");
                }
            }
        }

        private static void SetPlayerStats(PassiveTreeManager manager, PlayerStats stats)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo field = typeof(PassiveTreeManager).GetField("_playerStats", flags);
            Assert.That(field, Is.Not.Null);
            field.SetValue(manager, stats);
        }
    }
}
