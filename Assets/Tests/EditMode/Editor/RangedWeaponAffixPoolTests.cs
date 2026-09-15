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
        private const string ChakramPath = "Assets/Resources/Items/2HWeapon/Chakram/Adventurer/Adventurer's Chakram.asset";
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

                bool isRangedPool = pool.name == RangedPoolName;
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

        [Test]
        public void Chakram_UsesRangedWeaponAffixPool()
        {
            var chakram = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(ChakramPath);
            Assert.That(chakram, Is.Not.Null);
            Assert.That(chakram.AffixPool, Is.Not.Null);
            Assert.That(chakram.AffixPool.name, Is.EqualTo(RangedPoolName));
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
