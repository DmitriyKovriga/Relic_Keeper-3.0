using System.Linq;
using NUnit.Framework;
using Scripts.Editor.Affixes;
using Scripts.Items;
using Scripts.Items.Affixes;
using UnityEditor;

namespace RelicKeeper.Tests.EditMode
{
    public class PushbackAffixFamilyTests
    {
        [Test]
        public void PushbackAndResist_HaveGeneratedAffixFamilies()
        {
            AffixContentMaintenance.GeneratePushbackFamilies();

            ItemAffixSO[] pushback = LoadFamily("Pushback");
            ItemAffixSO[] resist = LoadFamily("PushbackResist");

            Assert.That(pushback.Length, Is.GreaterThanOrEqualTo(15), "Pushback should have Full Light/Medium/Strong families.");
            Assert.That(resist.Length, Is.GreaterThanOrEqualTo(15), "PushbackResist should have Full Light/Medium/Strong families.");
            Assert.That(pushback.Select(affix => affix.GroupID), Has.Member("Pushback_Flat_Medium"));
            Assert.That(pushback.Select(affix => affix.GroupID), Has.Member("Pushback_Increase_Medium"));
            Assert.That(resist.Select(affix => affix.GroupID), Has.Member("PushbackResist_Flat_Medium"));

            var weaponPool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(
                "Assets/Resources/Affixes/Pools/TwoHandedMelee/Axe/pool/OneHandedGenericPhysAffixPool.asset");
            var armorPool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(
                "Assets/Resources/Affixes/Pools/Helmets/Armore/Pool/BodyArmorePool.asset");
            Assert.That(weaponPool, Is.Not.Null);
            Assert.That(armorPool, Is.Not.Null);
            Assert.That(PoolHasGroup(weaponPool, "Pushback_Flat_Medium"), Is.True);
            Assert.That(PoolHasGroup(weaponPool, "Pushback_Increase_Medium"), Is.True);
            Assert.That(PoolHasGroup(armorPool, "PushbackResist_Flat_Medium"), Is.True);
        }

        private static bool PoolHasGroup(AffixPoolSO pool, string groupId)
        {
            return pool.Affixes != null && pool.Affixes.Any(affix => affix != null && affix.GroupID == groupId);
        }

        private static ItemAffixSO[] LoadFamily(string statName)
        {
            return AssetDatabase.FindAssets($"t:ItemAffixSO {statName}_")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains($"/{statName}/"))
                .Select(AssetDatabase.LoadAssetAtPath<ItemAffixSO>)
                .Where(affix => affix != null)
                .ToArray();
        }
    }
}
