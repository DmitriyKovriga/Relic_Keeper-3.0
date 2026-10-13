using NUnit.Framework;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class RangedWeaponAffixPoolTests
    {
        private const string RangedPoolName = "RangedWeaponAffixPool";
        private const string TwoHandedPhysicalRangedPoolName = "TwoHandedGenericPhysRangedAffixPool";
        private const string BowPoolName = "BowAffixPool";
        private const string ChakramPath = "Assets/Resources/Items/2HWeapon/Chakram/Adventurer/Adventurer's Chakram.asset";
        private const string BowPath = "Assets/Resources/Items/2HWeapon/Bow/Adventurer's Bow.asset";
        private const string BodyEvasionPoolPath = "Assets/Resources/Affixes/Pools/Helmets/Armore/Pool/BodyEvasionPool.asset";

        [Test]
        public void ForkAndExtraProjectiles_OnlyRollOnRangedWeaponPool()
        {
            string[] guids = AssetDatabase.FindAssets("t:AffixPoolSO");
            Assert.That(guids.Length, Is.GreaterThan(0));

            bool foundRangedPool = false;
            for (int i = 0; i < guids.Length; i++)
            {
                AffixPoolSO pool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                Assert.That(pool, Is.Not.Null);

                bool isRangedPool = pool.name == RangedPoolName || pool.name == TwoHandedPhysicalRangedPoolName || pool.name == BowPoolName;
                bool hasFork = PoolHasStat(pool, StatType.ProjectileFork);
                bool hasCount = PoolHasStat(pool, StatType.ProjectileCount);

                if (isRangedPool)
                {
                    foundRangedPool = true;
                    Assert.That(hasFork, Is.True, "Ranged weapon pool should include fork");
                    Assert.That(hasCount, Is.True, "Ranged weapon pool should include extra projectiles");
                    continue;
                }

                Assert.That(hasFork, Is.False, $"{pool.name} should not roll fork");
                Assert.That(hasCount, Is.False, $"{pool.name} should not roll extra projectiles");
            }

            Assert.That(foundRangedPool, Is.True);
        }

        [Test]
        public void GeneralItemPools_KeepChainAndPierce()
        {
            AffixPoolSO evasion = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(BodyEvasionPoolPath);
            Assert.That(evasion, Is.Not.Null);
            Assert.That(PoolHasStat(evasion, StatType.ProjectileChain), Is.True);
            Assert.That(PoolHasStat(evasion, StatType.ProjectilePierce), Is.True);
        }

        [TestCase(ChakramPath, TwoHandedPhysicalRangedPoolName)]
        [TestCase(BowPath, BowPoolName)]
        public void TwoHandedRangedWeapon_UsesItsAssignedRangedPool(string weaponPath, string expectedPool)
        {
            var chakram = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(ChakramPath);
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(weaponPath);
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.AffixPool, Is.Not.Null);
            Assert.That(weapon.AffixPool.name, Is.EqualTo(expectedPool));
            if (weaponPath == BowPath) Assert.That(weapon.AffixPool, Is.Not.SameAs(chakram.AffixPool));
            Assert.That(weapon.AffixPool.Affixes, Is.Not.Empty);
            foreach (var affix in weapon.AffixPool.Affixes)
                Assert.That(affix, Is.Not.Null, "Ranged pool must not contain missing affix references.");
        }

        [TestCase(StatType.DamageFire)]
        [TestCase(StatType.DamageCold)]
        [TestCase(StatType.DamageLightning)]
        public void Bow_HasLocalFlatAndIncreasedElementalDamage(StatType stat)
        {
            var pool = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(BowPath).AffixPool;
            bool flat = false, increased = false;
            foreach (var affix in pool.Affixes)
                foreach (var entry in affix.GetStatsForTier(affix.GetDefaultTier()))
                {
                    if (entry.Stat != stat || entry.Scope != StatScope.Local) continue;
                    flat |= entry.Type == StatModType.Flat;
                    increased |= entry.Type == StatModType.PercentAdd;
                }
            Assert.That(flat, Is.True, "Bow must roll local added elemental damage.");
            Assert.That(increased, Is.True, "Bow must roll local increased elemental damage.");
        }

        [Test]
        public void Bow_SupportsPhysicalAndElementalAilments_WithoutDuplicateAffixes()
        {
            var pool = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(BowPath).AffixPool;
            foreach (var stat in new[]
            {
                StatType.BleedChance, StatType.BleedDamage, StatType.BleedDuration,
                StatType.PoisonChance, StatType.PoisonDamage, StatType.PoisonDuration,
                StatType.IgniteChance, StatType.IgniteDamage, StatType.IgniteDamageMult, StatType.IgniteDuration, StatType.MaxIgniteStacks,
                StatType.FreezeChance, StatType.FreezeDuration,
                StatType.ShockChance, StatType.ShockDuration, StatType.ShockApplicationEffectiveness, StatType.ShockEffectMagnitude
            })
                Assert.That(PoolHasStat(pool, stat), Is.True, stat.ToString());
            Assert.That(new System.Collections.Generic.HashSet<ItemAffixSO>(pool.Affixes).Count, Is.EqualTo(pool.Affixes.Count));
            Assert.That(pool.GetAvailableAffixGroupCount(1), Is.GreaterThan(0));
        }

        private static bool PoolHasStat(AffixPoolSO pool, StatType stat)
        {
            if (pool?.Affixes == null)
                return false;

            for (int i = 0; i < pool.Affixes.Count; i++)
            {
                ItemAffixSO affix = pool.Affixes[i];
                if (AffixHasStat(affix, stat))
                    return true;
            }

            return false;
        }

        private static bool AffixHasStat(ItemAffixSO affix, StatType stat)
        {
            if (affix == null)
                return false;

            ItemAffixSO.AffixStatData[] stats = affix.GetStatsForTier(affix.GetDefaultTier());
            for (int i = 0; i < stats.Length; i++)
            {
                if (stats[i].Stat == stat)
                    return true;
            }

            return false;
        }
    }
}
