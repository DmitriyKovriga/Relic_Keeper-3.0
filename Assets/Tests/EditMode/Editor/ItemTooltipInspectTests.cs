using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class ItemTooltipInspectTests
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
        public void OneHandedWeapon_UsesOneHandedLabel()
        {
            WeaponItemSO weapon = CreateWeapon(twoHanded: false, EquipmentSlot.MainHand);

            Assert.That(ItemTooltipInspect.TryGetWeaponHandedness(weapon, out bool twoHanded), Is.True);
            Assert.That(twoHanded, Is.False);
            Assert.That(ItemTooltipInspect.GetWeaponHandKey(twoHanded), Is.EqualTo(ItemTooltipInspect.OneHandedKey));
        }

        [Test]
        public void TwoHandedWeapon_UsesTwoHandedLabel()
        {
            WeaponItemSO weapon = CreateWeapon(twoHanded: true, EquipmentSlot.MainHand);

            Assert.That(ItemTooltipInspect.TryGetWeaponHandedness(weapon, out bool twoHanded), Is.True);
            Assert.That(twoHanded, Is.True);
            Assert.That(ItemTooltipInspect.GetWeaponHandKey(twoHanded), Is.EqualTo(ItemTooltipInspect.TwoHandedKey));
        }

        [Test]
        public void DefensiveOffHand_HasNoWeaponHandLabel()
        {
            WeaponItemSO shield = CreateWeapon(twoHanded: false, EquipmentSlot.OffHand);

            Assert.That(ItemTooltipInspect.TryGetWeaponHandedness(shield, out _), Is.False);
        }

        [Test]
        public void Armor_HasNoWeaponHandLabel()
        {
            ArmorItemSO armor = Track(ScriptableObject.CreateInstance<ArmorItemSO>());

            Assert.That(ItemTooltipInspect.TryGetWeaponHandedness(armor, out _), Is.False);
        }

        [Test]
        public void AffixInspect_ShowsTierAndGenerationRange()
        {
            ItemAffixSO affix = CreateSingleAffix(3, 20f, 30f);
            InventoryItem item = new InventoryItem(Track(ScriptableObject.CreateInstance<ArmorItemSO>()));
            var instance = new AffixInstance(affix, 3, item);

            string text = ItemTooltipInspect.FormatAffixInspect(instance);

            Assert.That(text, Does.Contain("T3"));
            Assert.That(text, Does.Contain("20-30"));
        }

        [Test]
        public void AffixInspect_RangeRoll_ShowsBothGenerationBands()
        {
            ItemAffixSO affix = CreateRangeAffix(2, 3f, 6f, 7f, 12f);
            InventoryItem item = new InventoryItem(Track(ScriptableObject.CreateInstance<ArmorItemSO>()));
            var instance = new AffixInstance(affix, 2, item);

            string text = ItemTooltipInspect.FormatAffixInspect(instance);

            Assert.That(text, Does.Contain("T2"));
            Assert.That(text, Does.Contain("3-6 to 7-12"));
        }

        [Test]
        public void ItemLevel_FormatsLocalizedTemplate()
        {
            Assert.That(ItemTooltipInspect.FormatItemLevel(27, "Item Level: {0}"), Is.EqualTo("Item Level: 27"));
            Assert.That(ItemTooltipInspect.FormatItemLevel(27, null), Is.EqualTo("ilvl 27"));
        }

        [Test]
        public void GenerateRuntime_StoresItemLevelOnInstance()
        {
            ArmorItemSO data = Track(ScriptableObject.CreateInstance<ArmorItemSO>());
            data.DropLevel = 1;
            InventoryItem item = ItemGenerator.GenerateRuntime(data, 27, 0);

            Assert.That(item.ItemLevel, Is.EqualTo(27));
            Assert.That(item.ResolvedItemLevel, Is.EqualTo(27));
            Assert.That(item.GetSaveData(0).ItemLevel, Is.EqualTo(27));
        }

        [Test]
        public void ResolvedItemLevel_FallsBackToDropLevelWhenUnset()
        {
            ArmorItemSO data = Track(ScriptableObject.CreateInstance<ArmorItemSO>());
            data.DropLevel = 4;
            InventoryItem item = new InventoryItem(data);
            item.ItemLevel = 0;

            Assert.That(item.ResolvedItemLevel, Is.EqualTo(4));
        }

        private WeaponItemSO CreateWeapon(bool twoHanded, EquipmentSlot slot)
        {
            WeaponItemSO weapon = Track(ScriptableObject.CreateInstance<WeaponItemSO>());
            weapon.IsTwoHanded = twoHanded;
            weapon.Slot = slot;
            return weapon;
        }

        private ItemAffixSO CreateSingleAffix(int tier, float min, float max)
        {
            ItemAffixSO affix = Track(ScriptableObject.CreateInstance<ItemAffixSO>());
            affix.Tiers = new List<ItemAffixSO.AffixTierData>
            {
                new ItemAffixSO.AffixTierData
                {
                    Tier = tier,
                    Stats = new[]
                    {
                        new ItemAffixSO.AffixStatData
                        {
                            Stat = StatType.MaxHealth,
                            Type = StatModType.Flat,
                            ValueMode = AffixValueMode.Single,
                            MinValue = min,
                            MaxValue = max
                        }
                    }
                }
            };
            return affix;
        }

        private ItemAffixSO CreateRangeAffix(int tier, float min, float max, float rangeMin, float rangeMax)
        {
            ItemAffixSO affix = Track(ScriptableObject.CreateInstance<ItemAffixSO>());
            affix.Tiers = new List<ItemAffixSO.AffixTierData>
            {
                new ItemAffixSO.AffixTierData
                {
                    Tier = tier,
                    Stats = new[]
                    {
                        new ItemAffixSO.AffixStatData
                        {
                            Stat = StatType.DamageFire,
                            Type = StatModType.Flat,
                            ValueMode = AffixValueMode.Range,
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

        private T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }
    }
}
