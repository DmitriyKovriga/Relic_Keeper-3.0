using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;
using Scripts.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CraftingChangeSnapshotTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private InventoryItem _item;
        [SetUp] public void Setup()
        {
            var data = ScriptableObject.CreateInstance<ArmorItemSO>(); _owned.Add(data);
            _item = new InventoryItem(data);
        }
        [TearDown] public void Cleanup() { foreach (var obj in _owned) Object.DestroyImmediate(obj); _owned.Clear(); }
        private AffixInstance Add(float value, ItemAffixSO data = null, float? upper = null)
        {
            if (data == null) { data = ScriptableObject.CreateInstance<ItemAffixSO>(); _owned.Add(data); }
            var affix = new AffixInstance(data, _item) { Tier = 3 };
            affix.Modifiers.Add(new AffixModifierInstance(StatType.Armor, StatScope.Global,
                new StatModifier(value, StatModType.Flat), upper.HasValue ? new StatModifier(upper.Value, StatModType.Flat) : null));
            _item.Affixes.Add(affix); return affix;
        }

        [Test] public void SnapshotPreservesOldRollWhenTheSameAffixIsRerolledLower()
        {
            var affix = Add(30); var before = CraftingChangeSnapshot.Capture(_item);
            affix.Modifiers[0].PrimaryMod = new StatModifier(12, StatModType.Flat);
            var change = before.CompareTo(CraftingChangeSnapshot.Capture(_item)).Single();
            Assert.That(change.Before.Rolls[0].Min, Is.EqualTo(30));
            Assert.That(change.After.Rolls[0].Min, Is.EqualTo(12)); Assert.That(change.Changed, Is.True);
        }
        [Test] public void PurgeShowsEveryRemovedAffixAfterTheActualListIsCleared()
        {
            Add(10); Add(20); var before = CraftingChangeSnapshot.Capture(_item);
            Assert.That(ItemGenerator.TryApplyCraftingOrb(_item, CraftingOrbEffectId.PurgeAll), Is.True);
            var changes = before.CompareTo(CraftingChangeSnapshot.Capture(_item));
            Assert.That(changes.Count, Is.EqualTo(2)); Assert.That(changes.All(c => c.Before != null && c.After == null && c.Changed), Is.True);
        }
        [Test] public void ReplacedAffixReportsBothTheRemovalAndTheAddition()
        {
            Add(10); var before = CraftingChangeSnapshot.Capture(_item); _item.Affixes.Clear(); Add(40);
            var changes = before.CompareTo(CraftingChangeSnapshot.Capture(_item));
            Assert.That(changes.Count(c => c.Before == null), Is.EqualTo(1));
            Assert.That(changes.Count(c => c.After == null), Is.EqualTo(1));
        }
        [Test] public void UnchangedRollsAreNotReportedAsAnImprovementOrMutation()
        {
            Add(10); var before = CraftingChangeSnapshot.Capture(_item);
            Assert.That(before.CompareTo(CraftingChangeSnapshot.Capture(_item)).Single().Changed, Is.False);
        }
        [Test] public void RangeUpperBoundAndAdditionalModifiersAreCopiedIndependently()
        {
            var affix = Add(10, upper: 20);
            affix.Modifiers.Add(new AffixModifierInstance(StatType.Armor, StatScope.Local, new StatModifier(5, StatModType.PercentAdd)));
            var before = CraftingChangeSnapshot.Capture(_item);
            affix.Modifiers[0].SecondaryMod = new StatModifier(15, StatModType.Flat); affix.Modifiers.RemoveAt(1);
            var change = before.CompareTo(CraftingChangeSnapshot.Capture(_item)).Single();
            Assert.That(change.Before.Rolls.Count, Is.EqualTo(2)); Assert.That(change.Before.Rolls[0].Max, Is.EqualTo(20));
            Assert.That(change.After.Rolls[0].Max, Is.EqualTo(15)); Assert.That(change.Changed, Is.True);
        }
        [Test] public void DuplicateSourcesRemainSeparateRatherThanLosingARecordedRoll()
        {
            var first = Add(10); Add(20, first.Data); var before = CraftingChangeSnapshot.Capture(_item);
            Assert.That(before.CompareTo(CraftingChangeSnapshot.Capture(_item)).Count, Is.EqualTo(2));
        }
        [Test] public void CraftReceiptYieldsToTheExistingDetailedAffixInspection()
        {
            var host = new GameObject("Craft receipt inspection"); host.SetActive(false); _owned.Add(host);
            var tooltip = host.AddComponent<ItemTooltipController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(ItemTooltipController); var container = new VisualElement();
            type.GetField("_craftingResultItem", flags).SetValue(tooltip, _item);
            type.GetField("_craftingChanges", flags).SetValue(tooltip, new List<CraftingChangeSnapshot.Change>());
            type.GetField("_statsContainer", flags).SetValue(tooltip, container);
            type.GetField("_inspectDetailsVisible", flags).SetValue(tooltip, true);
            Assert.That(type.GetMethod("AppendCraftingAffixes", flags).Invoke(tooltip, new object[] { _item }), Is.False);
            Assert.That(container.childCount, Is.Zero, "Alt inspection must keep the normal current-affix range/tier rows.");
        }
    }
}
