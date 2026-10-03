using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class IgniteSpreadTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
        }

        [Test]
        public void SpreadTarget_PrefersEnemyWithoutIgnite()
        {
            var candidates = new List<AilmentController.IgniteSpreadCandidate>
            {
                new AilmentController.IgniteSpreadCandidate { StackCount = 2, MaxStacks = 2, DistanceSqr = 0.2f },
                new AilmentController.IgniteSpreadCandidate { StackCount = 0, MaxStacks = 2, DistanceSqr = 4f },
                new AilmentController.IgniteSpreadCandidate { StackCount = 1, MaxStacks = 2, DistanceSqr = 1f }
            };

            Assert.That(AilmentController.SelectIgniteSpreadTarget(candidates), Is.EqualTo(1));
        }

        [Test]
        public void SpreadTarget_WhenEveryoneBurns_PicksTheNearest()
        {
            var candidates = new List<AilmentController.IgniteSpreadCandidate>
            {
                new AilmentController.IgniteSpreadCandidate { StackCount = 2, MaxStacks = 2, DistanceSqr = 3f },
                new AilmentController.IgniteSpreadCandidate { StackCount = 2, MaxStacks = 2, DistanceSqr = 0.5f }
            };

            Assert.That(AilmentController.SelectIgniteSpreadTarget(candidates), Is.EqualTo(1));
        }

        [Test]
        public void MaxIgniteStacks_WeakerHitRefreshesOneStack()
        {
            AilmentController ailments = CreateBurningEnemy("burning", Vector3.zero, out PlayerStats source);
            source.GetStat(StatType.IgniteChance).BaseValue = 100f;
            source.GetStat(StatType.IgniteDamageMult).BaseValue = 20f;
            source.GetStat(StatType.IgniteDuration).BaseValue = 4f;
            source.GetStat(StatType.MaxIgniteStacks).BaseValue = 1f;

            Assert.That(ailments.TryApplyIgnite(source, source, new DamageSnapshot(source) { Fire = 100f }), Is.True);
            Tick(ailments, 3.5f);
            Assert.That(ailments.TryApplyIgnite(source, source, new DamageSnapshot(source) { Fire = 10f }), Is.True);
            Tick(ailments, 1f);

            Assert.That(ailments.GetStackCount(AilmentType.Ignite), Is.EqualTo(1));
        }

        [Test]
        public void Ignite_SpreadsToCleanNeighborAfterTwoSeconds()
        {
            AilmentController burning = CreateBurningEnemy("burning", Vector3.zero, out PlayerStats source);
            AilmentController clean = CreateBurningEnemy("clean", new Vector3(1.5f, 0f, 0f), out _);
            source.GetStat(StatType.IgniteChance).BaseValue = 100f;
            source.GetStat(StatType.IgniteDamageMult).BaseValue = 20f;
            source.GetStat(StatType.IgniteDuration).BaseValue = 4f;
            source.GetStat(StatType.IgniteSpreadDuration).BaseValue = 2f;

            Assert.That(burning.TryApplyIgnite(source, source, new DamageSnapshot(source) { Fire = 80f }), Is.True);
            Physics2D.SyncTransforms();
            Tick(burning, 1.9f);
            Assert.That(clean.GetStackCount(AilmentType.Ignite), Is.EqualTo(0));

            Tick(burning, 0.2f);
            Assert.That(clean.GetStackCount(AilmentType.Ignite), Is.EqualTo(1));
        }

        [Test]
        public void Ignite_ShowsFlamesWhileBurning()
        {
            var host = new GameObject("IgniteVisual");
            _created.Add(host);
            host.AddComponent<SpriteRenderer>();
            IgniteVisualController visual = host.AddComponent<IgniteVisualController>();

            visual.SetBurning(true);
            Assert.That(host.transform.Find("IgniteFlames"), Is.Not.Null);
            Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(4));

            visual.SetBurning(false);
            Assert.That(host.transform.Find("IgniteFlames").gameObject.activeSelf, Is.False);
        }

        private AilmentController CreateBurningEnemy(string name, Vector3 position, out PlayerStats source)
        {
            var host = new GameObject(name);
            host.transform.position = position;
            _created.Add(host);
            source = host.AddComponent<PlayerStats>();
            EnemyStats enemyStats = host.AddComponent<EnemyStats>();
            enemyStats.AddModifier(StatType.MaxHealth, new StatModifier(10000f, StatModType.Flat, host));
            EnemyHealth health = host.AddComponent<EnemyHealth>();
            health.Initialize();
            var collider = host.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;
            return host.AddComponent<AilmentController>();
        }

        private static void Tick(AilmentController ailments, float seconds)
        {
            MethodInfo update = typeof(AilmentController).GetMethod("UpdateIgnite", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(ailments, new object[] { seconds });
        }
    }
}
