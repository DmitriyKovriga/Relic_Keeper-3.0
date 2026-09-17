using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class CharacterWindowStatCatalogTests
    {
        [Test]
        public void HeroStats_AreNotDumpedIntoOther()
        {
            Assert.That(CharacterWindowStatCatalog.ShouldShowInOther(StatType.Armor), Is.False);
            Assert.That(CharacterWindowStatCatalog.ShouldShowInOther(StatType.Evasion), Is.False);
            Assert.That(CharacterWindowStatCatalog.ShouldShowInOther(StatType.MaxMysticShield), Is.False);
            Assert.That(CharacterWindowStatCatalog.ShouldShowInOther(StatType.FireResist), Is.False);
            Assert.That(CharacterWindowStatCatalog.ShouldShowInOther(StatType.DamagePhysical), Is.False);
            Assert.That(CharacterWindowStatCatalog.ShouldShowInOther(StatType.BleedDamage), Is.False);
        }

        [Test]
        public void HiddenStats_StayOutOfTheWindow()
        {
            Assert.That(CharacterWindowStatCatalog.IsHidden(StatType.Accuracy), Is.True);
            Assert.That(CharacterWindowStatCatalog.IsHidden(StatType.PhysicalToFire), Is.True);
            Assert.That(CharacterWindowStatCatalog.IsHidden(StatType.TakePhysAsFire), Is.True);
            Assert.That(CharacterWindowStatCatalog.IsHidden(StatType.HelmetSkillCooldownRecovery), Is.True);
            Assert.That(CharacterWindowStatCatalog.IsHidden(StatType.HealthRegenPercent), Is.True);
            Assert.That(CharacterWindowStatCatalog.IsHidden(StatType.PushbackResist), Is.True);
        }

        [Test]
        public void OtherStats_KeepUsefulCombatNumbers()
        {
            IReadOnlyList<StatType> other = CharacterWindowStatCatalog.GetOtherStats();
            Assert.That(other, Has.Member(StatType.CritChance));
            Assert.That(other, Has.Member(StatType.AttackSpeed));
            Assert.That(other, Has.Member(StatType.MaxHealth));
            Assert.That(other, Has.No.Member(StatType.Accuracy));
            Assert.That(other, Has.No.Member(StatType.FireToCold));
            Assert.That(other, Has.Member(StatType.ChanseToAvoidBleed));
            Assert.That(other, Has.Member(StatType.ExtraTargetsForMeleeHits));
            Assert.That(other, Has.Member(StatType.Pushback));
        }

        [Test]
        public void AvoidChances_AreNotTreatedAsConversion()
        {
            Assert.That(CharacterWindowStatCatalog.IsConversion(StatType.ChanseToAvoidBleed), Is.False);
            Assert.That(CharacterWindowStatCatalog.IsConversion(StatType.PhysicalToFire), Is.True);
            Assert.That(CharacterWindowStatCatalog.IsConversion(StatType.TakePhysAsFire), Is.True);
            Assert.That(CharacterWindowStatCatalog.IsConversion(StatType.ExtraTargetsForMeleeHits), Is.False);
        }

        [Test]
        public void CappedPercents_ShowEffectiveValueAndCap()
        {
            Assert.That(CharacterWindowStatCatalog.FormatCappedPercent(100f, 90f, 90f, true), Is.EqualTo("90/90%"));
            Assert.That(CharacterWindowStatCatalog.FormatCappedPercent(50f, 90f, 90f, true), Is.EqualTo("50/90%"));
            Assert.That(CharacterWindowStatCatalog.FormatCappedPercent(33f, 0f, 75f, true), Is.EqualTo("33/75%"));
            Assert.That(CharacterWindowStatCatalog.FormatOvercap(100f, 90f, 90f), Is.EqualTo("+10%"));
            Assert.That(CharacterWindowStatCatalog.FormatOvercap(50f, 90f, 90f), Is.EqualTo(string.Empty));
        }

        [Test]
        public void PhysicalResist_DisplaysSameArmorAndStatSumAsDamageMitigation()
        {
            var stats = new DefenseStats
            {
                Armor = 1542f,
                PhysicalResist = 10f,
                MaxPhysicalResist = 75f
            };

            float effective = ArmorMitigation.ResolveTotalPhysicalResist(
                stats, out _, out float fromArmor, out float fromStat, out float cap);

            Assert.That(fromArmor, Is.GreaterThan(65f));
            Assert.That(fromStat, Is.EqualTo(10f));
            Assert.That(effective, Is.EqualTo(75f));
            Assert.That(cap, Is.EqualTo(75f));
            Assert.That(CharacterWindowStatCatalog.FormatPhysicalResist(stats), Is.EqualTo("75/75%"));

            stats.PhysicalResist = -10f;
            Assert.That(CharacterWindowStatCatalog.FormatPhysicalResist(stats), Is.EqualTo("55/75%"));

            stats.Armor = 0f;
            Assert.That(CharacterWindowStatCatalog.FormatPhysicalResist(stats), Is.EqualTo("-10/75%"));
        }

        [Test]
        public void PhysicalResist_UsesCombatCapEvenWhenConfiguredCapIsHigher()
        {
            var stats = new DefenseStats
            {
                Armor = 10000f,
                PhysicalResist = 20f,
                MaxPhysicalResist = 100f
            };

            Assert.That(CharacterWindowStatCatalog.FormatPhysicalResist(stats), Is.EqualTo("90/90%"));
        }

        [Test]
        public void SectionOrder_PutsDefensesBeforeDamage()
        {
            Assert.That(CharacterWindowStatCatalog.Resists[0], Is.EqualTo(StatType.FireResist));
            Assert.That(CharacterWindowStatCatalog.Damages[0], Is.EqualTo(StatType.DamagePhysical));
            Assert.That(CharacterWindowStatCatalog.AilmentDamage[0], Is.EqualTo(StatType.BleedDamage));
        }

        private sealed class DefenseStats : IStatsProvider
        {
            public float Armor;
            public float PhysicalResist;
            public float MaxPhysicalResist;

            public float GetValue(StatType type)
            {
                switch (type)
                {
                    case StatType.Armor: return Armor;
                    case StatType.PhysicalResist: return PhysicalResist;
                    case StatType.MaxPhysicalResist: return MaxPhysicalResist;
                    default: return 0f;
                }
            }

            public bool TryGetStat(StatType type, out CharacterStat stat)
            {
                stat = null;
                return false;
            }
        }
    }
}
