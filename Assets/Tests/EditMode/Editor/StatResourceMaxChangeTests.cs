using NUnit.Framework;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class StatResourceMaxChangeTests
    {
        [Test]
        public void MaxIncrease_RaisesCurrentByTheSameAmount()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);
            resource.SetCurrent(100f);

            max.BaseValue = 600f;
            resource.ReevaluateMax();

            Assert.That(resource.Max, Is.EqualTo(600f));
            Assert.That(resource.Current, Is.EqualTo(200f));
        }

        [Test]
        public void MaxIncreaseWhileFull_StaysFull()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);

            max.BaseValue = 600f;
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(600f));
            Assert.That(resource.Max, Is.EqualTo(600f));
        }

        [Test]
        public void MaxDecreaseWhileFull_ClampsToNewCap()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);

            max.BaseValue = 400f;
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(400f));
            Assert.That(resource.Max, Is.EqualTo(400f));
        }

        [Test]
        public void MaxDecreaseWhileInjured_DoesNotPullCurrentDownOrKill()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);
            resource.SetCurrent(100f);

            max.BaseValue = 400f;
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(100f));
            Assert.That(resource.Max, Is.EqualTo(400f));
        }

        [Test]
        public void AdjustCurrentForMaxChange_MatchesHealthAndManaPolicy()
        {
            Assert.That(StatResource.AdjustCurrentForMaxChange(500f, 500f, 400f), Is.EqualTo(400f));
            Assert.That(StatResource.AdjustCurrentForMaxChange(100f, 500f, 400f), Is.EqualTo(100f));
            Assert.That(StatResource.AdjustCurrentForMaxChange(100f, 500f, 600f), Is.EqualTo(200f));
            Assert.That(StatResource.AdjustCurrentForMaxChange(0f, 500f, 400f), Is.EqualTo(0f));
        }

        [Test]
        public void SetCurrent_DoesNotTreatExistingMaxAsAnIncrease()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);
            resource.SetCurrent(100f);
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(100f));
            Assert.That(resource.Max, Is.EqualTo(500f));
        }

        [Test]
        public void PassiveMaxResourceNode_CannotHealByRepeatedAllocateAndRefund()
        {
            var host = new GameObject("PassiveResourceExploitTest");
            PassiveSkillTreeSO tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                PlayerStats stats = host.AddComponent<PlayerStats>();
                InvokeAwake(stats);
                stats.GetStat(StatType.MaxHealth).BaseValue = 100f;
                stats.GetStat(StatType.MaxMana).BaseValue = 100f;
                stats.NotifyChanged();
                stats.Health.SetCurrent(40f);
                stats.Mana.SetCurrent(25f);

                const string startId = "start";
                const string resourceId = "max-resources";
                tree.Nodes.Add(new PassiveNodeDefinition
                {
                    ID = startId,
                    NodeType = PassiveNodeType.Start,
                    ConnectionIDs = new List<string> { resourceId }
                });
                tree.Nodes.Add(new PassiveNodeDefinition
                {
                    ID = resourceId,
                    NodeType = PassiveNodeType.Small,
                    ConnectionIDs = new List<string> { startId },
                    UniqueModifiers = new List<SerializableStatModifier>
                    {
                        new SerializableStatModifier { Stat = StatType.MaxHealth, Value = 100f, Type = StatModType.Flat },
                        new SerializableStatModifier { Stat = StatType.MaxMana, Value = 100f, Type = StatModType.Flat }
                    }
                });
                tree.InitLookup();

                PassiveTreeManager manager = host.AddComponent<PassiveTreeManager>();
                InvokeAwake(manager);
                manager.SetTreeData(tree);
                manager.LoadState(new List<string> { startId });
                stats.Leveling.RefundPoint(1);

                manager.AllocateNode(resourceId);
                Assert.That(stats.Health.Current, Is.EqualTo(80f).Within(0.01f));
                Assert.That(stats.Mana.Current, Is.EqualTo(50f).Within(0.01f));

                manager.RefundNode(resourceId);
                Assert.That(stats.Health.Current, Is.EqualTo(40f).Within(0.01f));
                Assert.That(stats.Mana.Current, Is.EqualTo(25f).Within(0.01f));

                manager.AllocateNode(resourceId);
                manager.RefundNode(resourceId);
                Assert.That(stats.Health.Current, Is.EqualTo(40f).Within(0.01f));
                Assert.That(stats.Mana.Current, Is.EqualTo(25f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(tree);
            }
        }

        private static void InvokeAwake(object target)
        {
            MethodInfo awake = target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(target, null);
        }
    }
}
