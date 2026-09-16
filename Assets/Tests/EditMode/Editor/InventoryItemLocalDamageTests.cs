using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Saving;
using Scripts.Stats;
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
    }
}
