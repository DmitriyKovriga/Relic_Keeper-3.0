using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class AilmentDurationTests
    {
        private GameObject _host;
        private PlayerStats _stats;
        private AilmentController _ailments;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AilmentDurationHost");
            _stats = _host.AddComponent<PlayerStats>();
            _ailments = _host.AddComponent<AilmentController>();
            _stats.GetStat(StatType.PoisonChance).BaseValue = 100f;
            _stats.GetStat(StatType.PoisonDamageMult).BaseValue = 10f;
            _stats.GetStat(StatType.PoisonDuration).BaseValue = 2f;
            _stats.GetStat(StatType.BleedChance).BaseValue = 100f;
            _stats.GetStat(StatType.BleedDamageMult).BaseValue = 50f;
            _stats.GetStat(StatType.BleedDuration).BaseValue = 6f;
            _stats.GetStat(StatType.MaxBleedStack).BaseValue = 5f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void PoisonRefresh_KeepsEveryStackOnTheLatestDuration()
        {
            Assert.That(ApplyPoison(100f), Is.True);
            TickPoison(1.5f);
            Assert.That(ApplyPoison(100f), Is.True);
            TickPoison(1.9f);

            Assert.That(_ailments.GetStackCount(AilmentType.Poison), Is.EqualTo(2));

            TickPoison(0.2f);
            Assert.That(_ailments.GetStackCount(AilmentType.Poison), Is.EqualTo(0));
        }

        [Test]
        public void BleedStacks_ExpireOnTheirOwnTimers()
        {
            Assert.That(ApplyBleed(100f), Is.True);
            TickBleed(5f);
            Assert.That(ApplyBleed(40f), Is.True);
            TickBleed(1.1f);

            Assert.That(_ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(1));

            TickBleed(5f);
            Assert.That(_ailments.GetStackCount(AilmentType.Bleed), Is.EqualTo(0));
        }

        private bool ApplyPoison(float physical)
        {
            var hit = new DamageSnapshot(_stats) { Physical = physical };
            return _ailments.TryApplyPoison(_stats, _stats, hit);
        }

        private bool ApplyBleed(float physical)
        {
            var hit = new DamageSnapshot(_stats) { Physical = physical };
            return _ailments.TryApplyBleed(_stats, _stats, hit);
        }

        private void TickPoison(float seconds)
        {
            Invoke("UpdatePoison", seconds);
        }

        private void TickBleed(float seconds)
        {
            Invoke("UpdateBleed", seconds);
        }

        private void Invoke(string methodName, float seconds)
        {
            MethodInfo method = typeof(AilmentController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_ailments, new object[] { seconds });
        }
    }
}
