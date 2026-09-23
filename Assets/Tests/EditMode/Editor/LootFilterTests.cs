using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Items.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public class LootFilterTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private bool _hadSavedThreshold;
        private int _savedThreshold;

        [SetUp]
        public void SetUp()
        {
            _hadSavedThreshold = PlayerPrefs.HasKey(LootFilterSettings.PlayerPrefsKey);
            _savedThreshold = PlayerPrefs.GetInt(LootFilterSettings.PlayerPrefsKey, 0);
            PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, (int)LootFilterThreshold.None);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }
            _createdObjects.Clear();

            GameObject root = GameObject.Find("WorldDroppedItems");
            if (root != null)
                Object.DestroyImmediate(root);

            if (_hadSavedThreshold)
                PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, _savedThreshold);
            else
                PlayerPrefs.DeleteKey(LootFilterSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void ThresholdHidesSelectedRarityAndEveryLowerRarity()
        {
            InventoryItem common = CreateItem(0);
            InventoryItem magic = CreateItem(2);
            InventoryItem rare = CreateItem(4);

            Assert.That(LootFilterSettings.ShouldHide(common, LootFilterThreshold.None), Is.False);
            Assert.That(LootFilterSettings.ShouldHide(common, LootFilterThreshold.Common), Is.True);
            Assert.That(LootFilterSettings.ShouldHide(magic, LootFilterThreshold.Common), Is.False);
            Assert.That(LootFilterSettings.ShouldHide(common, LootFilterThreshold.Magic), Is.True);
            Assert.That(LootFilterSettings.ShouldHide(magic, LootFilterThreshold.Magic), Is.True);
            Assert.That(LootFilterSettings.ShouldHide(rare, LootFilterThreshold.Magic), Is.False);
            Assert.That(LootFilterSettings.ShouldHide(common, LootFilterThreshold.Rare), Is.True);
            Assert.That(LootFilterSettings.ShouldHide(magic, LootFilterThreshold.Rare), Is.True);
            Assert.That(LootFilterSettings.ShouldHide(rare, LootFilterThreshold.Rare), Is.True);
        }

        [Test]
        public void HiddenDropDoesNotSpreadOrInteractUntilFilterIsRelaxed()
        {
            LootFilterSettings.SetThreshold(LootFilterThreshold.Common);

            WorldDroppedItem hiddenCommon = WorldItemDropService.Spawn(CreateItem(0), Vector2.zero);
            WorldDroppedItem visibleMagic = WorldItemDropService.Spawn(CreateItem(1), Vector2.zero);
            Track(hiddenCommon);
            Track(visibleMagic);

            Assert.That(hiddenCommon.IsLootFilterHidden, Is.True, "Common item should be filtered.");
            Assert.That(hiddenCommon.CanInteract(), Is.False, "Filtered item must not be interactable.");
            Assert.That(hiddenCommon.GetComponent<CircleCollider2D>().enabled, Is.False, "Filtered item collider must be disabled.");
            Assert.That(hiddenCommon.GetComponent<SpriteRenderer>().enabled, Is.False, "Filtered item renderer must be disabled.");
            Assert.That(hiddenCommon.transform.Find("Icon").GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(hiddenCommon.ParticipatesInWorldLayout, Is.False);
            Assert.That(visibleMagic.IsLootFilterHidden, Is.False, "Magic item should remain visible at the Common threshold.");
            Assert.That(visibleMagic.ParticipatesInWorldLayout, Is.True);
            Assert.That(hiddenCommon.GroundPosition, Is.EqualTo(Vector2.zero));
            Assert.That(visibleMagic.GroundPosition, Is.EqualTo(Vector2.zero));

            LootFilterSettings.SetThreshold(LootFilterThreshold.None);

            Assert.That(hiddenCommon.IsLootFilterHidden, Is.False);
            Assert.That(hiddenCommon.CanInteract(), Is.True);
            Assert.That(hiddenCommon.GetComponent<CircleCollider2D>().enabled, Is.True);
            Assert.That(hiddenCommon.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(hiddenCommon.transform.Find("Icon").GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(hiddenCommon.ParticipatesInWorldLayout, Is.True);
            Assert.That(
                Mathf.Abs(hiddenCommon.GroundPosition.x - visibleMagic.GroundPosition.x),
                Is.GreaterThanOrEqualTo(WorldDroppedItemSpread.MinSeparation - 0.02f));
        }

        [Test]
        public void SettingsUiContainsCompactLootFilterSelectorAndLocalizedChoices()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/SettingsUI/SettingUXML.uxml");
            Assert.That(tree, Is.Not.Null);

            TemplateContainer root = tree.Instantiate();
            Assert.That(root.Q<Button>("LootFilterButton"), Is.Not.Null);
            VisualElement popup = root.Q<VisualElement>("LootFilterPopup");
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.ClassListContains("settings-loot-filter-popup"), Is.True);
            Assert.That(popup.childCount, Is.EqualTo(4));
            Assert.That(root.Q<Button>("LootFilterOptionNone"), Is.Not.Null);
            Assert.That(root.Q<Button>("LootFilterOptionCommon"), Is.Not.Null);
            Assert.That(root.Q<Button>("LootFilterOptionMagic"), Is.Not.Null);
            Assert.That(root.Q<Button>("LootFilterOptionRare"), Is.Not.Null);

            Assert.That(LanguageSelector.GetLootFilterText(LootFilterThreshold.None, true), Is.EqualTo("Не скрывать"));
            Assert.That(LanguageSelector.GetLootFilterText(LootFilterThreshold.Common, true), Is.EqualTo("Только белые"));
            Assert.That(LanguageSelector.GetLootFilterText(LootFilterThreshold.Magic, true), Is.EqualTo("Белые и волшебные"));
            Assert.That(LanguageSelector.GetLootFilterText(LootFilterThreshold.Rare, true), Is.EqualTo("Белые, волшебные и редкие"));
            Assert.That(LanguageSelector.GetLootFilterText(LootFilterThreshold.None, false), Is.EqualTo("Do not hide"));
        }

        private InventoryItem CreateItem(int affixCount)
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            data.ID = $"loot_filter_{_createdObjects.Count}";
            _createdObjects.Add(data);

            var item = new InventoryItem(data);
            for (int i = 0; i < affixCount; i++)
            {
                ItemAffixSO affix = ScriptableObject.CreateInstance<ItemAffixSO>();
                _createdObjects.Add(affix);
                item.Affixes.Add(new AffixInstance(affix, item));
            }
            return item;
        }

        private void Track(WorldDroppedItem dropped)
        {
            if (dropped != null)
                _createdObjects.Add(dropped.gameObject);
        }
    }
}
