using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Items.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LootCacheIntakeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _owned = new List<Object>();
        private WorldLootCache _cache;
        private bool _hadFilter;
        private int _filter;

        [SetUp] public void Setup()
        {
            _hadFilter = PlayerPrefs.HasKey(LootFilterSettings.PlayerPrefsKey);
            _filter = PlayerPrefs.GetInt(LootFilterSettings.PlayerPrefsKey, 0);
            PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, 0);
            _cache = WorldLootCache.Create(new Vector2(1000, 0), null);
            _owned.Add(_cache.gameObject);
        }

        [TearDown] public void Cleanup()
        {
            if (_cache != null)
                typeof(WorldLootCache).GetMethod("OnDisable", Private).Invoke(_cache, null);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            if (_hadFilter) PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, _filter);
            else PlayerPrefs.DeleteKey(LootFilterSettings.PlayerPrefsKey);
        }

        private InventoryItem Item(int affixes = 0, bool unique = false)
        {
            var data = ScriptableObject.CreateInstance<ArmorItemSO>();
            data.ID = "intake_" + _owned.Count; data.Width = data.Height = 1;
            data.IsUnique = unique; _owned.Add(data);
            var item = new InventoryItem(data);
            for (int i = 0; i < affixes; i++)
            {
                var affix = ScriptableObject.CreateInstance<ItemAffixSO>(); _owned.Add(affix);
                item.Affixes.Add(new AffixInstance(affix, item));
            }
            return item;
        }

        private void Tick(float time) => typeof(WorldLootCache).GetMethod("UpdateIntake", Private).Invoke(_cache, new object[] { time });
        private Transform Visual => _cache.transform.Find("Visual");
        private float Age => (float)typeof(WorldLootCache).GetField("_intakeAge", Private).GetValue(_cache);

        private WorldLootCacheFlightVisual Flight(InventoryItem item)
        {
            WorldLootCacheFlightVisual.Spawn(_cache.transform.position + Vector3.left * 4, _cache, item);
            return _cache.GetComponentInChildren<WorldLootCacheFlightVisual>();
        }

        private static void Arrive(WorldLootCacheFlightVisual flight)
        {
            typeof(WorldLootCacheFlightVisual).GetField("_elapsed", Private).SetValue(flight, flight.Duration);
            typeof(WorldLootCacheFlightVisual).GetMethod("Update", Private).Invoke(flight, null);
        }

        [Test] public void StorageIsImmediateAndOnlyFlightCompletionStartsTheResponse()
        {
            var item = Item(); _cache.TryAddItemPreferringTab(item, 0);
            var flight = Flight(item);
            Assert.That(_cache.Items[0], Is.SameAs(item));
            Assert.That(_cache.CanInteract(), Is.True);
            Tick(.05f); Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
            int changes = 0; _cache.Changed += () => changes++;
            Arrive(flight); Tick(.09f);
            Assert.That(Visual.localScale.y, Is.LessThan(.9f));
            Assert.That(_cache.Items[0], Is.SameAs(item));
            Assert.That(changes, Is.Zero, "Presentation must not refresh an open cache grid.");
        }

        [Test] public void TwentyArrivalsMergeWithoutRestartingOrQueuingAndLeaveColliderFixed()
        {
            var items = new List<InventoryItem>();
            for (int i = 0; i < 20; i++) { var item = Item(); items.Add(item); _cache.TryAddItemPreferringTab(item, 0); }
            Vector3 position = _cache.transform.position;
            var collider = _cache.GetComponent<CircleCollider2D>(); float radius = collider.radius;
            _cache.NotifyLootArrived(items[0]); Tick(.06f);
            for (int i = 1; i < items.Count; i++) _cache.NotifyLootArrived(items[i]);
            Assert.That(Age, Is.EqualTo(.06f).Within(.001f), "A burst must not reset the envelope twenty times.");
            Tick(.03f);
            Assert.That(Visual.localScale.x, Is.InRange(1f, 1.121f));
            Assert.That(Visual.localScale.y, Is.InRange(.839f, 1f));
            Assert.That(_cache.transform.position, Is.EqualTo(position));
            Assert.That(_cache.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(collider.radius, Is.EqualTo(radius)); Assert.That(collider.enabled, Is.True);
            Tick(WorldLootCache.IntakeDuration); Tick(1f);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(_cache.Items, Is.EqualTo(items));
        }

        [Test] public void TakingLootMidFlightCancelsArrivalWithoutTransferringAnythingElse()
        {
            var item = Item(); _cache.TryAddItemPreferringTab(item, 0); var flight = Flight(item);
            Assert.That(_cache.TakeItem(0, 0), Is.SameAs(item));
            Assert.That(_cache.CanPresentArrival(item), Is.False);
            Arrive(flight); Tick(.09f);
            Assert.That(flight == null, Is.True);
            Assert.That(_cache.Count, Is.Zero); Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
        }

        [Test] public void HiddenArrivalsDoNotCreateFlightsAndRevealDoesNotCelebrateOldLoot()
        {
            var item = Item(); _cache.TryAddItemPreferringTab(item, 0);
            LootFilterSettings.SetThreshold(LootFilterThreshold.Common);
            Assert.That(Flight(item), Is.Null);
            _cache.NotifyLootArrived(item); Tick(.09f);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
            LootFilterSettings.SetThreshold(LootFilterThreshold.None); Tick(.09f);
            Assert.That(_cache.CanInteract(), Is.True);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(_cache.Items[0], Is.SameAs(item));
        }

        [Test] public void HidingAnInFlightItemCancelsItsFlightEvenWhenOtherLootRemainsVisible()
        {
            var item = Item(); _cache.TryAddItemPreferringTab(item, 0); var flight = Flight(item);
            var other = Item(); other.Data.IsUnique = true; _cache.TryAddItemPreferringTab(other, 0);
            LootFilterSettings.SetThreshold(LootFilterThreshold.Common); Arrive(flight);
            Assert.That(flight == null, Is.True);
            Assert.That(_cache.Items, Is.EqualTo(new[] { item, other }));
            Assert.That(_cache.CanInteract(), Is.True); Tick(.09f);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
        }

        [Test] public void RemovingContributorsResetsAnActivePulseAndDisableDoesNotReplayIt()
        {
            var item = Item(); _cache.TryAddItemPreferringTab(item, 0);
            _cache.NotifyLootArrived(item); Tick(.09f);
            Assert.That(Visual.localScale.y, Is.LessThan(1f));
            _cache.TakeItem(0, 0);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
            _cache.TryAddItemPreferringTab(item, 0); _cache.NotifyLootArrived(item); Tick(.09f);
            _cache.enabled = false;
            // Non-ExecuteAlways behaviours do not receive this callback in EditMode.
            typeof(WorldLootCache).GetMethod("OnDisable", Private).Invoke(_cache, null);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
            _cache.enabled = true; Tick(.09f);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
        }

        [Test] public void PauseHoldsTheEnvelopeAndFilterChangesCancelItImmediately()
        {
            var item = Item(); _cache.TryAddItemPreferringTab(item, 0);
            _cache.NotifyLootArrived(item); Tick(.09f); Vector3 scale = Visual.localScale;
            Tick(0f); Assert.That(Visual.localScale, Is.EqualTo(scale));
            LootFilterSettings.SetThreshold(LootFilterThreshold.Common);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
            LootFilterSettings.SetThreshold(LootFilterThreshold.None); Tick(.09f);
            Assert.That(Visual.localScale, Is.EqualTo(Vector3.one));
        }

        [Test] public void DestroyingCacheOwnsFlightsAndLeavesNoOrphanTrail()
        {
            var item = Item(); _cache.TryAddItemPreferringTab(item, 0); var flight = Flight(item);
            Assert.That(flight.transform.parent, Is.EqualTo(_cache.transform));
            var trail = flight.GetComponent<TrailRenderer>();
            typeof(WorldLootCache).GetMethod("OnDisable", Private).Invoke(_cache, null);
            Object.DestroyImmediate(_cache.gameObject);
            Assert.That(flight == null, Is.True); Assert.That(trail == null, Is.True);
        }

        [Test] public void DiscardOriginSurvivesFallbackDropInitialization()
        {
            var host = new GameObject("Discard"); _owned.Add(host);
            var drop = host.AddComponent<WorldDroppedItem>(); var item = Item();
            drop.Initialize(item, 24, isPlayerDiscard: true);
            Assert.That(drop.IsPlayerDiscard, Is.True);
            Assert.That(drop.Item, Is.SameAs(item));
        }

        [TestCase(0, false, 0)]
        [TestCase(1, false, 0)]
        [TestCase(4, false, 1)]
        [TestCase(0, true, 2)]
        public void OnlyRareAndUniqueArrivalsStartWavesAndUniqueHasTwoBursts(int affixes, bool unique, int strength)
        {
            var item = Item(affixes, unique); _cache.TryAddItemPreferringTab(item, 0);
            Assert.That(_cache.ValuableArrivalStrength, Is.Zero);
            _cache.NotifyLootArrived(item); Tick(.25f);
            Assert.That(_cache.ValuableArrivalStrength, Is.EqualTo(strength));
            var root = _cache.transform.Find("ValuableArrival");
            if (strength == 0) { Assert.That(root, Is.Null); return; }
            int waves = 0;
            for (int i = 0; i < 2; i++)
            {
                var wave = root.Find("WaveAndStars" + i).GetComponent<SpriteRenderer>();
                if (!wave.enabled) continue;
                waves++; Assert.That(wave.sprite, Is.Not.Null);
                Assert.That(wave.sprite.pixelsPerUnit, Is.EqualTo(24));
                Assert.That(wave.transform.localScale, Is.EqualTo(Vector3.one * (unique ? 2f : 1f)));
                Assert.That(wave.sprite.texture.name, Is.EqualTo(unique ? "UniqueArrival" : "ValuableArrival"));
            }
            Assert.That(waves, Is.EqualTo(strength));
            Assert.That(_cache.Count, Is.EqualTo(1));
        }

        [Test] public void RareBurstMergesAndUniqueEscalatesWithoutRestartingOrQueuing()
        {
            for (int i = 0; i < 20; i++)
            {
                var item = Item(4); _cache.TryAddItemPreferringTab(item, 0); _cache.NotifyLootArrived(item);
            }
            Tick(.3f);
            var unique = Item(unique: true); _cache.TryAddItemPreferringTab(unique, 0); _cache.NotifyLootArrived(unique);
            Assert.That(_cache.ValuableArrivalStrength, Is.EqualTo(2));
            Assert.That((float)typeof(WorldLootCache).GetField("_valuableAge", Private).GetValue(_cache), Is.EqualTo(.3f).Within(.001f));
            Tick(2f); Tick(1f);
            Assert.That(_cache.ValuableArrivalStrength, Is.Zero);
            foreach (var renderer in _cache.transform.Find("ValuableArrival").GetComponentsInChildren<SpriteRenderer>())
                Assert.That(renderer.enabled, Is.False);
            Assert.That(_cache.Count, Is.EqualTo(21));
        }

        [Test] public void HiddenOrTakenRareLootCancelsStarsAndRevealDoesNotCelebrate()
        {
            var rare = Item(4); var common = Item();
            _cache.TryAddItemPreferringTab(rare, 0); _cache.TryAddItemPreferringTab(common, 0);
            _cache.NotifyLootArrived(rare); Tick(.1f);
            LootFilterSettings.SetThreshold(LootFilterThreshold.Rare);
            Assert.That(_cache.ValuableArrivalStrength, Is.Zero);
            LootFilterSettings.SetThreshold(LootFilterThreshold.None); Tick(.1f);
            Assert.That(_cache.ValuableArrivalStrength, Is.Zero);
            Tick(1.1f); _cache.NotifyLootArrived(rare); Tick(.1f);
            _cache.TakeItem(0, 0);
            Assert.That(_cache.ValuableArrivalStrength, Is.Zero);
            Assert.That(_cache.Items, Is.EqualTo(new[] { common }));
        }

        [Test] public void RateLimitSuppressesRepeatedRareButDoesNotSwallowUniqueUpgrade()
        {
            var rare = Item(4); _cache.TryAddItemPreferringTab(rare, 0); _cache.NotifyLootArrived(rare);
            Tick(.75f);
            var second = Item(4); _cache.TryAddItemPreferringTab(second, 0); _cache.NotifyLootArrived(second);
            Assert.That(_cache.ValuableArrivalStrength, Is.Zero);
            var unique = Item(unique: true); _cache.TryAddItemPreferringTab(unique, 0); _cache.NotifyLootArrived(unique);
            Assert.That(_cache.ValuableArrivalStrength, Is.EqualTo(2));
        }
    }
}
