using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class CraftingOrbEffectsTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private Random.State _previousRandomState;

        [SetUp]
        public void SetUp()
        {
            _previousRandomState = Random.state;
            Random.InitState(90210);
        }

        [TearDown]
        public void TearDown()
        {
            Random.state = _previousRandomState;
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void CreationAndElevationCreateExpectedRarities()
        {
            InventoryItem creationTarget = CreateItem(6);
            Assert.That(ItemGenerator.TryApplyCraftingOrb(creationTarget, CraftingOrbEffectId.CreateMagic), Is.True);
            Assert.That(creationTarget.Affixes, Has.Count.InRange(1, 3));
            Assert.That(ItemRarity.IsMagic(creationTarget), Is.True);
            Assert.That(ItemGenerator.CanApplyCraftingOrb(creationTarget, CraftingOrbEffectId.CreateMagic), Is.False);

            InventoryItem elevationTarget = CreateItem(6);
            Assert.That(ItemGenerator.TryApplyCraftingOrb(elevationTarget, CraftingOrbEffectId.CreateRare), Is.True);
            Assert.That(elevationTarget.Affixes, Has.Count.InRange(4, 6));
            Assert.That(ItemRarity.IsRare(elevationTarget), Is.True);
        }

        [Test]
        public void PerfectioFillsMagicItemToExactlyFourAffixes()
        {
            InventoryItem item = CreateItem(6);
            AddKnownAffixes(item, 2);

            Assert.That(ItemGenerator.TryApplyCraftingOrb(item, CraftingOrbEffectId.UpgradeMagicToRare), Is.True);
            Assert.That(item.Affixes, Has.Count.EqualTo(4));
            Assert.That(ItemRarity.IsRare(item), Is.True);
        }

        [Test]
        public void ExcelsiorAddsOneAffixAndStopsAtSix()
        {
            InventoryItem item = CreateItem(6);
            AddKnownAffixes(item, 4);

            Assert.That(ItemGenerator.TryApplyCraftingOrb(item, CraftingOrbEffectId.AddRareAffix), Is.True);
            Assert.That(item.Affixes, Has.Count.EqualTo(5));
            Assert.That(ItemGenerator.TryApplyCraftingOrb(item, CraftingOrbEffectId.AddRareAffix), Is.True);
            Assert.That(item.Affixes, Has.Count.EqualTo(6));
            Assert.That(ItemGenerator.CanApplyCraftingOrb(item, CraftingOrbEffectId.AddRareAffix), Is.False);
        }

        [Test]
        public void PurgingOrbsUpdateRarityThroughAffixCount()
        {
            InventoryItem item = CreateItem(6);
            AddKnownAffixes(item, 4);

            Assert.That(ItemGenerator.TryApplyCraftingOrb(item, CraftingOrbEffectId.RemoveAffix), Is.True);
            Assert.That(item.Affixes, Has.Count.EqualTo(3));
            Assert.That(ItemRarity.IsMagic(item), Is.True);

            Assert.That(ItemGenerator.TryApplyCraftingOrb(item, CraftingOrbEffectId.PurgeAll), Is.True);
            Assert.That(item.Affixes, Is.Empty);
            Assert.That(ItemRarity.IsMagic(item), Is.False);
            Assert.That(ItemRarity.IsRare(item), Is.False);
        }

        [Test]
        public void FortuneRerollsRareItemWithinRareRange()
        {
            InventoryItem item = CreateItem(6);
            AddKnownAffixes(item, 4);

            Assert.That(ItemGenerator.TryApplyCraftingOrb(item, CraftingOrbEffectId.RerollRare), Is.True);
            Assert.That(item.Affixes, Has.Count.InRange(4, 6));
            Assert.That(ItemRarity.IsRare(item), Is.True);
        }

        [Test]
        public void OrbCannotApplyWhenPoolDoesNotHaveEnoughGroups()
        {
            InventoryItem item = CreateItem(3);

            Assert.That(ItemGenerator.CanApplyCraftingOrb(item, CraftingOrbEffectId.CreateRare), Is.False);
            Assert.That(ItemGenerator.TryApplyCraftingOrb(item, CraftingOrbEffectId.CreateRare), Is.False);
            Assert.That(item.Affixes, Is.Empty);
        }

        private InventoryItem CreateItem(int affixCount)
        {
            ArmorItemSO data = Create<ArmorItemSO>();
            data.ID = "crafting_test_item";
            data.DropLevel = 1;
            data.AffixPool = Create<AffixPoolSO>();
            data.AffixPool.Affixes = new List<ItemAffixSO>();

            for (int i = 0; i < affixCount; i++)
            {
                ItemAffixSO affix = Create<ItemAffixSO>();
                affix.name = $"craft_affix_{i}";
                affix.UniqueID = affix.name;
                affix.GroupID = $"craft_group_{i}";
                affix.Tier = 5;
                data.AffixPool.Affixes.Add(affix);
            }

            return new InventoryItem(data);
        }

        private static void AddKnownAffixes(InventoryItem item, int count)
        {
            for (int i = 0; i < count; i++)
                item.Affixes.Add(new AffixInstance(item.Data.AffixPool.Affixes[i], item));
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }
    }
}
