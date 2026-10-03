using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class CritChanceAffixWordingTests
    {
        [Test]
        public void LocalCritChance_UsesTheWeaponWordingKey()
        {
            ItemAffixSO affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            try
            {
                affix.TranslationKey = "affix_flat_critchance";
                affix.Tiers = new List<ItemAffixSO.AffixTierData>
                {
                    new ItemAffixSO.AffixTierData
                    {
                        Tier = 1,
                        Stats = new[]
                        {
                            new ItemAffixSO.AffixStatData
                            {
                                Stat = StatType.CritChance,
                                Type = StatModType.Flat,
                                Scope = StatScope.Local
                            }
                        }
                    }
                };

                Assert.That(affix.GetResolvedTranslationKey(), Is.EqualTo("affix_flat_critchance_local"));
            }
            finally
            {
                Object.DestroyImmediate(affix);
            }
        }

        [Test]
        public void GlobalCritChance_KeepsTheBaseWordingKey()
        {
            ItemAffixSO affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            try
            {
                affix.TranslationKey = "affix_increase_critchance";
                affix.Tiers = new List<ItemAffixSO.AffixTierData>
                {
                    new ItemAffixSO.AffixTierData
                    {
                        Tier = 1,
                        Stats = new[]
                        {
                            new ItemAffixSO.AffixStatData
                            {
                                Stat = StatType.CritChance,
                                Type = StatModType.PercentAdd,
                                Scope = StatScope.Global
                            }
                        }
                    }
                };

                Assert.That(affix.GetResolvedTranslationKey(), Is.EqualTo("affix_increase_critchance"));
            }
            finally
            {
                Object.DestroyImmediate(affix);
            }
        }
    }
}
