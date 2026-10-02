using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveTreeSearchTests
    {
        [Test]
        public void EmptyQuery_MatchesEveryNode()
        {
            PassiveNodeDefinition node = Node("Thick Skin", StatType.Armor, 12f);

            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(node), "  "), Is.True);
        }

        [Test]
        public void NameWord_MatchesThatNodeOnly()
        {
            PassiveNodeDefinition armor = Node("Thick Skin", StatType.Armor, 12f);
            PassiveNodeDefinition fire = Node("Burning Blood", StatType.DamageFire, 10f);

            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(armor), "thick"), Is.True);
            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(fire), "thick"), Is.False);
        }

        [Test]
        public void StatWord_MatchesNodeThatChangesThatStat()
        {
            PassiveNodeDefinition node = Node("Quiet", StatType.DamageFire, 8f);

            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(node), "fire"), Is.True);
            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(node), "armor"), Is.False);
        }

        [Test]
        public void EveryWordMustMatch()
        {
            PassiveNodeDefinition node = Node("Burning Blood", StatType.DamageFire, 8f);

            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(node), "burning fire"), Is.True);
            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(node), "burning armor"), Is.False);
        }

        [Test]
        public void Alias_PhysMatchesPhysicalDamage()
        {
            PassiveNodeDefinition node = Node("Force", StatType.DamagePhysical, 5f);

            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(node), "phys"), Is.True);
            Assert.That(PassiveTreeSearch.Matches(PassiveTreeSearch.BuildSearchText(node), "физическ"), Is.True);
        }

        private static PassiveNodeDefinition Node(string name, StatType stat, float value)
        {
            var template = ScriptableObject.CreateInstance<PassiveNodeTemplateSO>();
            template.Name = name;
            template.Modifiers = new List<SerializableStatModifier>
            {
                new SerializableStatModifier { Stat = stat, Value = value, Type = StatModType.PercentAdd }
            };
            return new PassiveNodeDefinition
            {
                ID = name,
                NodeType = PassiveNodeType.Notable,
                Template = template
            };
        }
    }
}
