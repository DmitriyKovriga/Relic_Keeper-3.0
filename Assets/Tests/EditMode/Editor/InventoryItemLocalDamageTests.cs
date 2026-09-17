using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Saving;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryItemLocalDamageTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void LocalPhysIncrease_DoesNotScaleLocalFlatAffix_AndGoesIntoWeaponDamageStat()
        {
            InventoryItem item = CreateWeaponWithLocalDamage(
                StatType.DamagePhysical,
                baseMin: 100f,
                baseMax: 200f,
                flatMin: 100f,
                flatMax: 100f,
                increasedPercent: 100f);

            AffixModifierInstance flat = item.Affixes[0].Modifiers[0];
            AffixModifierInstance increased = item.Affixes[1].Modifiers[0];
            flat.GetRolledRange(out float rolledMin, out float rolledMax);

            Assert.That(rolledMin, Is.EqualTo(100f));
            Assert.That(rolledMax, Is.EqualTo(100f));
            Assert.That(increased.PrimaryMod.Value, Is.EqualTo(100f));
            Assert.That(item.GetCalculatedStat(StatType.DamagePhysical, 100f), Is.EqualTo(400f));
            Assert.That(item.GetCalculatedStatUpperBound(StatType.DamagePhysical, 200f), Is.EqualTo(600f));
            Assert.That(flat.PrimaryMod.Value, Is.EqualTo(100f));
            Assert.That(flat.SecondaryMod.Value, Is.EqualTo(100f));
        }

        [Test]
        public void LocalFireIncrease_DoesNotScaleLocalFlatAffix_AndGoesIntoWeaponDamageStat()
        {
            InventoryItem item = CreateWeaponWithLocalDamage(
                StatType.DamageFire,
                baseMin: 100f,
                baseMax: 200f,
                flatMin: 100f,
                flatMax: 100f,
                increasedPercent: 100f);

            item.Affixes[0].Modifiers[0].GetRolledRange(out float rolledMin, out float rolledMax);

            Assert.That(rolledMin, Is.EqualTo(100f));
            Assert.That(rolledMax, Is.EqualTo(100f));
            Assert.That(item.GetCalculatedStat(StatType.DamageFire, 100f), Is.EqualTo(400f));
            Assert.That(item.GetCalculatedStatUpperBound(StatType.DamageFire, 200f), Is.EqualTo(600f));
        }

        [Test]
        public void LocalPhysIncrease_IsBakedIntoWeaponFlat_AndNotEmittedAsCharacterPercent()
        {
            InventoryItem item = CreateWeaponWithLocalDamage(
                StatType.DamagePhysical,
                baseMin: 100f,
                baseMax: 200f,
                flatMin: 100f,
                flatMax: 100f,
                increasedPercent: 100f);

            var characterStat = new CharacterStat(0f);
            int percentAdds = 0;
            int flats = 0;
            foreach (var (statType, modifier) in item.GetAllModifiers())
            {
                if (statType != StatType.DamagePhysical)
                    continue;

                characterStat.AddModifier(modifier);
                if (modifier.Type == StatModType.Flat)
                    flats++;
                else if (modifier.Type.IsAdditivePercent())
                    percentAdds++;
            }

            Assert.That(flats, Is.EqualTo(1));
            Assert.That(percentAdds, Is.EqualTo(0));
            Assert.That(characterStat.GetRawFlatValue(), Is.EqualTo(500f));
            Assert.That(characterStat.Value, Is.EqualTo(500f));
        }

        [Test]
        public void LocalColdAndLightning_FollowTheSameHeaderFormula()
        {
            InventoryItem cold = CreateWeaponWithLocalDamage(
                StatType.DamageCold,
                baseMin: 20f,
                baseMax: 40f,
                flatMin: 10f,
                flatMax: 10f,
                increasedPercent: 50f);
            InventoryItem lightning = CreateWeaponWithLocalDamage(
                StatType.DamageLightning,
                baseMin: 20f,
                baseMax: 40f,
                flatMin: 10f,
                flatMax: 10f,
                increasedPercent: 50f);

            Assert.That(cold.GetCalculatedStat(StatType.DamageCold, 20f), Is.EqualTo(45f));
            Assert.That(cold.GetCalculatedStatUpperBound(StatType.DamageCold, 40f), Is.EqualTo(75f));
            Assert.That(lightning.GetCalculatedStat(StatType.DamageLightning, 20f), Is.EqualTo(45f));
            Assert.That(lightning.GetCalculatedStatUpperBound(StatType.DamageLightning, 40f), Is.EqualTo(75f));
            Assert.That(cold.Affixes[0].Modifiers[0].PrimaryMod.Value, Is.EqualTo(10f));
            Assert.That(lightning.Affixes[0].Modifiers[0].PrimaryMod.Value, Is.EqualTo(10f));
        }

        [Test]
        public void EveryWeaponPool_OnlyOffersLocalFlatAndIncreasedCritChance()
        {
            string[] poolGuids = AssetDatabase.FindAssets(
                "t:AffixPoolSO", new[] { "Assets/Resources/Affixes/Pools" });
            int weaponPoolCount = 0;

            foreach (string guid in poolGuids)
            {
                AffixPoolSO pool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (pool == null || pool.Slot != EquipmentSlot.MainHand)
                    continue;

                weaponPoolCount++;
                bool hasFlat = false;
                bool hasIncrease = false;
                foreach (ItemAffixSO affix in pool.Affixes)
                {
                    if (affix == null)
                        continue;

                    foreach (ItemAffixSO.AffixTierData tier in affix.Tiers)
                    {
                        if (tier?.Stats == null)
                            continue;

                        foreach (ItemAffixSO.AffixStatData stat in tier.Stats)
                        {
                            if (stat.Stat != StatType.CritChance)
                                continue;

                            Assert.That(stat.Scope, Is.EqualTo(StatScope.Local),
                                $"{pool.name}: {affix.name} tier {tier.Tier} must not add global crit chance");
                            hasFlat |= stat.Type == StatModType.Flat;
                            hasIncrease |= stat.Type == StatModType.PercentAdd;
                        }
                    }
                }

                Assert.That(hasFlat, Is.True, $"{pool.name} has no local flat crit chance");
                Assert.That(hasIncrease, Is.True, $"{pool.name} has no local increased crit chance");
            }

            Assert.That(weaponPoolCount, Is.EqualTo(7));
        }

        [Test]
        public void SavedWeaponCritAffixes_BakeFlatAndIncreasedIntoWeaponOnly()
        {
            const string folder = "Assets/Resources/Affixes/ByStat/Critical/CritChance/";
            ItemAffixSO flat = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(folder + "Local_CritChance_Flat_Medium.asset");
            ItemAffixSO increased = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(folder + "Local_CritChance_Increase_Medium.asset");
            Assert.That(flat, Is.Not.Null);
            Assert.That(increased, Is.Not.Null);
            Assert.That(increased.UniqueID, Is.EqualTo("Local_CritChance_Increase_Medium"));

            WeaponItemSO weapon = Track(ScriptableObject.CreateInstance<WeaponItemSO>());
            weapon.BaseCritChance = 5f;
            InventoryItem item = new InventoryItem(weapon);
            item.Affixes.Add(new AffixInstance(flat, 1, CreateAffixSave(2f), item));
            item.Affixes.Add(new AffixInstance(increased, 1, CreateAffixSave(10f), item));

            Assert.That(item.Affixes[0].Modifiers[0].Scope, Is.EqualTo(StatScope.Local));
            Assert.That(item.Affixes[1].Modifiers[0].Scope, Is.EqualTo(StatScope.Local));
            Assert.That(item.GetCalculatedStat(StatType.CritChance, 5f), Is.EqualTo(7.7f).Within(0.01f));

            var critModifiers = item.GetAllModifiers().FindAll(entry => entry.Item1 == StatType.CritChance);
            Assert.That(critModifiers, Has.Count.EqualTo(1));
            Assert.That(critModifiers[0].Item2.Type, Is.EqualTo(StatModType.Flat));
            Assert.That(critModifiers[0].Item2.Value, Is.EqualTo(7.7f).Within(0.01f));
        }

        [Test]
        public void LocalWeaponCritChance_DoesNotLeakFromInactiveDualWieldWeapon()
        {
            const string folder = "Assets/Resources/Affixes/ByStat/Critical/CritChance/";
            ItemAffixSO flat = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(folder + "Local_CritChance_Flat_Medium.asset");
            ItemAffixSO increased = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(folder + "Local_CritChance_Increase_Medium.asset");
            Assert.That(flat, Is.Not.Null);
            Assert.That(increased, Is.Not.Null);

            WeaponItemSO weapon = Track(ScriptableObject.CreateInstance<WeaponItemSO>());
            weapon.BaseCritChance = 5f;
            InventoryItem active = new InventoryItem(weapon);
            InventoryItem inactive = new InventoryItem(weapon);
            active.Affixes.Add(new AffixInstance(flat, 1, CreateAffixSave(1f), active));
            active.Affixes.Add(new AffixInstance(increased, 1, CreateAffixSave(20f), active));
            inactive.Affixes.Add(new AffixInstance(flat, 1, CreateAffixSave(2f), inactive));
            inactive.Affixes.Add(new AffixInstance(increased, 1, CreateAffixSave(10f), inactive));

            var critStat = new CharacterStat();
            foreach (var (type, modifier) in active.GetAllModifiers())
            {
                if (type == StatType.CritChance)
                    critStat.AddModifier(modifier);
            }
            foreach (var (type, modifier) in inactive.GetAllModifiers())
            {
                if (type == StatType.CritChance)
                    critStat.AddModifier(modifier);
            }

            IStatsProvider scoped = new WeaponHandStatsProvider(new CritStats(critStat), inactive);
            Assert.That(scoped.GetValue(StatType.CritChance), Is.EqualTo(7.2f).Within(0.01f));
        }

        [Test]
        public void GlobalCritChanceAsset_RemainsGlobalAndDistinctFromWeaponLocalCopy()
        {
            const string folder = "Assets/Resources/Affixes/ByStat/Critical/CritChance/";
            ItemAffixSO global = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(folder + "CritChance_Increase_Medium.asset");
            ItemAffixSO local = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(folder + "Local_CritChance_Increase_Medium.asset");
            Assert.That(global, Is.Not.Null);
            Assert.That(local, Is.Not.Null);
            Assert.That(global.UniqueID, Is.EqualTo("CritChance_Increase_Medium"));
            Assert.That(local.UniqueID, Is.EqualTo("Local_CritChance_Increase_Medium"));

            for (int tier = 1; tier <= 5; tier++)
            {
                ItemAffixSO.AffixStatData globalStat = global.GetStatsForTier(tier)[0];
                ItemAffixSO.AffixStatData localStat = local.GetStatsForTier(tier)[0];
                Assert.That(globalStat.Scope, Is.EqualTo(StatScope.Global));
                Assert.That(localStat.Scope, Is.EqualTo(StatScope.Local));
                Assert.That(localStat.MinValue, Is.EqualTo(globalStat.MinValue));
                Assert.That(localStat.MaxValue, Is.EqualTo(globalStat.MaxValue));
            }
        }

        private InventoryItem CreateWeaponWithLocalDamage(
            StatType channel,
            float baseMin,
            float baseMax,
            float flatMin,
            float flatMax,
            float increasedPercent)
        {
            WeaponItemSO weapon = Track(ScriptableObject.CreateInstance<WeaponItemSO>());
            SetWeaponBase(weapon, channel, baseMin, baseMax);

            InventoryItem item = new InventoryItem(weapon);
            item.Affixes.Add(new AffixInstance(
                CreateDamageAffix(channel, StatModType.Flat, StatScope.Local, AffixValueMode.Range, flatMin, flatMax, flatMin, flatMax),
                1,
                CreateAffixSave(flatMin, flatMax),
                item));
            item.Affixes.Add(new AffixInstance(
                CreateDamageAffix(channel, StatModType.PercentAdd, StatScope.Local, AffixValueMode.Single, increasedPercent, increasedPercent),
                1,
                CreateAffixSave(increasedPercent),
                item));
            return item;
        }

        private static void SetWeaponBase(WeaponItemSO weapon, StatType channel, float min, float max)
        {
            switch (channel)
            {
                case StatType.DamageFire:
                    weapon.MinFireDamage = min;
                    weapon.MaxFireDamage = max;
                    break;
                case StatType.DamageCold:
                    weapon.MinColdDamage = min;
                    weapon.MaxColdDamage = max;
                    break;
                case StatType.DamageLightning:
                    weapon.MinLightningDamage = min;
                    weapon.MaxLightningDamage = max;
                    break;
                default:
                    weapon.MinPhysicalDamage = min;
                    weapon.MaxPhysicalDamage = max;
                    break;
            }
        }

        private ItemAffixSO CreateDamageAffix(
            StatType stat,
            StatModType type,
            StatScope scope,
            AffixValueMode valueMode,
            float min,
            float max,
            float rangeMin = 0f,
            float rangeMax = 0f)
        {
            ItemAffixSO affix = Track(ScriptableObject.CreateInstance<ItemAffixSO>());
            affix.Tiers = new List<ItemAffixSO.AffixTierData>
            {
                new ItemAffixSO.AffixTierData
                {
                    Tier = 1,
                    Stats = new[]
                    {
                        new ItemAffixSO.AffixStatData
                        {
                            Stat = stat,
                            Type = type,
                            Scope = scope,
                            ValueMode = valueMode,
                            MinValue = min,
                            MaxValue = max,
                            RangeMinValue = rangeMin,
                            RangeMaxValue = rangeMax
                        }
                    }
                }
            };
            return affix;
        }

        private static AffixSaveData CreateAffixSave(params float[] values)
        {
            return new AffixSaveData { Values = new List<float>(values) };
        }

        private T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }

        private sealed class CritStats : IStatsProvider
        {
            private readonly CharacterStat _crit;

            public CritStats(CharacterStat crit)
            {
                _crit = crit;
            }

            public float GetValue(StatType type)
            {
                return type == StatType.CritChance ? _crit.Value : 0f;
            }

            public bool TryGetStat(StatType type, out CharacterStat stat)
            {
                stat = type == StatType.CritChance ? _crit : null;
                return stat != null;
            }
        }
    }
}
