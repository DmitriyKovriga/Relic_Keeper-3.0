using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Items.World;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LootCacheFlightTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private WorldLootCache _cache;
        private bool _hadFilter; private int _filter;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void Setup()
        {
            _hadFilter = PlayerPrefs.HasKey(LootFilterSettings.PlayerPrefsKey);
            _filter = PlayerPrefs.GetInt(LootFilterSettings.PlayerPrefsKey, 0);
            PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, 0);
            _cache = WorldLootCache.Create(Vector2.right * 1000, null); _owned.Add(_cache.gameObject);
        }
        [TearDown] public void Cleanup()
        {
            if (_cache != null) typeof(WorldLootCache).GetMethod("OnDisable", Private).Invoke(_cache, null);
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            if (_hadFilter) PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, _filter);
            else PlayerPrefs.DeleteKey(LootFilterSettings.PlayerPrefsKey);
        }

        [Test] public void FlightRisesRevealsAboveTheDropThenTravelsToTheCache()
        {
            Vector3 start = new Vector3(1, 2, 0), end = new Vector3(10, -1, 0);
            float travel = WorldLootCacheFlightVisual.GetTravelDuration(start, end);
            var top = start + Vector3.up * WorldLootCacheFlightVisual.RiseHeight;
            Assert.That(WorldLootCacheFlightVisual.EvaluateFlight(start, end, 0, travel), Is.EqualTo(start));
            var rising = WorldLootCacheFlightVisual.EvaluateFlight(start, end, .2f, travel);
            Assert.That(rising.x, Is.EqualTo(start.x)); Assert.That(rising.y, Is.InRange(start.y, top.y));
            var hover = WorldLootCacheFlightVisual.EvaluateFlight(start, end, .6f, travel);
            Assert.That(hover.x, Is.EqualTo(start.x)); Assert.That(hover.y, Is.EqualTo(top.y).Within(1f / 24f));
            var reveal = WorldLootCacheFlightVisual.EvaluateFlight(start, end, WorldLootCacheFlightVisual.RevealStart + .08f, travel);
            Assert.That(reveal, Is.EqualTo(top));
            var middle = WorldLootCacheFlightVisual.EvaluateFlight(start, end, WorldLootCacheFlightVisual.TravelStart + travel / 2, travel);
            Assert.That(middle.x, Is.InRange(start.x + .1f, end.x - .1f));
            Assert.That(WorldLootCacheFlightVisual.EvaluateFlight(start, end, WorldLootCacheFlightVisual.TravelStart + travel, travel), Is.EqualTo(end));
        }

        [Test] public void TravelTimeScalesWithDistanceButHasAFinitePresentationLimit()
        {
            float near = WorldLootCacheFlightVisual.GetTravelDuration(Vector3.zero, Vector3.right);
            float far = WorldLootCacheFlightVisual.GetTravelDuration(Vector3.zero, Vector3.right * 100);
            Assert.That(near, Is.EqualTo(.42f).Within(.001f));
            Assert.That(far, Is.EqualTo(1.6f).Within(.001f));
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(4, false)]
        [TestCase(0, true)]
        public void RarityTintAndRevealAreUsedBeforeArrivalWithoutDelayingStoredItems(int affixes, bool unique)
        {
            var data = ScriptableObject.CreateInstance<ArmorItemSO>(); data.Width = data.Height = 1; data.IsUnique = unique;
            var texture = new Texture2D(12, 24); _owned.Add(texture);
            data.Icon = Sprite.Create(texture, new Rect(0, 0, 12, 24), Vector2.one * .5f, 100f); _owned.Add(data.Icon);
            _owned.Add(data); var item = new InventoryItem(data);
            for (int i = 0; i < affixes; i++)
            {
                var affix = ScriptableObject.CreateInstance<ItemAffixSO>(); _owned.Add(affix);
                item.Affixes.Add(new AffixInstance(affix, item));
            }
            _cache.TryAddItemPreferringTab(item, 0);
            WorldLootCacheFlightVisual.Spawn(_cache.transform.position + Vector3.left * 4, _cache, item);
            var flight = _cache.GetComponentInChildren<WorldLootCacheFlightVisual>();
            var oldDropHost = new GameObject("Original item bubble"); _owned.Add(oldDropHost);
            var oldDrop = oldDropHost.AddComponent<WorldDroppedItem>(); oldDrop.Initialize(item, 24f);
            var plate = flight.GetComponent<SpriteRenderer>();
            var icon = flight.transform.Find("Icon").GetComponent<SpriteRenderer>();
            var originalIcon = oldDrop.transform.Find("Icon").GetComponent<SpriteRenderer>();
            Assert.That(plate.sprite, Is.SameAs(oldDrop.GetComponent<SpriteRenderer>().sprite));
            Assert.That(plate.color, Is.EqualTo(oldDrop.GetComponent<SpriteRenderer>().color));
            Assert.That(icon.sprite, Is.SameAs(data.Icon)); Assert.That(icon.color, Is.EqualTo(Color.white));
            Assert.That(icon.bounds.size, Is.EqualTo(originalIcon.bounds.size));
            Assert.That(icon.sortingOrder, Is.GreaterThan(plate.sortingOrder));
            Assert.That(flight.GetComponent<WorldDroppedItem>(), Is.Null);
            Assert.That(flight.FlightColor, Is.EqualTo(ItemRarity.GetTooltipTitleColor(item)));
            var trail = flight.GetComponent<TrailRenderer>();
            // Unity stores trail gradient colors with 8-bit precision.
            Assert.That(trail.startColor.r, Is.EqualTo(flight.FlightColor.r).Within(1f / 255f));
            Assert.That(trail.startColor.g, Is.EqualTo(flight.FlightColor.g).Within(1f / 255f));
            Assert.That(trail.startColor.b, Is.EqualTo(flight.FlightColor.b).Within(1f / 255f));
            typeof(WorldLootCacheFlightVisual).GetField("_elapsed", Private).SetValue(flight, WorldLootCacheFlightVisual.RevealStart + .06f);
            typeof(WorldLootCacheFlightVisual).GetMethod("Update", Private).Invoke(flight, null);
            Assert.That(flight.transform.Find("RarityReveal").GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(_cache.Items[0], Is.SameAs(item)); Assert.That(_cache.CanInteract(), Is.True);
            Assert.That(_cache.ValuableArrivalStrength, Is.Zero, "A reveal over the slain enemy is not a cache arrival.");
            Assert.That(flight.GetComponent<Collider2D>(), Is.Null);
            typeof(WorldLootCacheFlightVisual).GetField("_elapsed", Private).SetValue(flight, flight.Duration);
            typeof(WorldLootCacheFlightVisual).GetMethod("Update", Private).Invoke(flight, null);
            Assert.That(_cache.Items[0], Is.SameAs(item));
            Assert.That(_cache.GetComponentInChildren<WorldLootCacheFlightVisual>(), Is.Null,
                "The complete bubble and icon must retire on arrival, without leaving a loose drop.");
        }
    }
}
