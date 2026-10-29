using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Hub;
using Scripts.Items.World;
using Scripts.UI;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class RoomResolutionFeedbackTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo BannerInstance = typeof(RoomClearedBanner).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private RoomClearedBanner _previousBanner;
        private GameObject _host;
        private RoomController _room;
        private DungeonPortal _portal;
        private List<EnemyHealth> _living;
        private readonly List<GameObject> _enemies = new List<GameObject>();
        private RoomAnnouncementState Notice => BannerInstance.GetValue(null) is RoomClearedBanner banner
            ? (RoomAnnouncementState)typeof(RoomClearedBanner).GetField("_state", Private).GetValue(banner) : null;

        [SetUp] public void Setup()
        {
            _previousBanner = (RoomClearedBanner)BannerInstance.GetValue(null); BannerInstance.SetValue(null, null);
            _host = new GameObject("Room resolution test"); _room = _host.AddComponent<RoomController>();
            var exit = new GameObject("Next exit"); exit.transform.SetParent(_host.transform, false); exit.AddComponent<BoxCollider2D>();
            _portal = exit.AddComponent<DungeonPortal>();
            _living = (List<EnemyHealth>)typeof(RoomController).GetField("_livingEnemies", Private).GetValue(_room);
            typeof(RoomController).GetField("_activeModifiers", Private).SetValue(_room, new DungeonModifierContext());
            SetEnemyCount(3); CallRoom("SetNextRoomPortalsActive", false);
        }
        [TearDown] public void Cleanup()
        {
            var ownBanner = (RoomClearedBanner)BannerInstance.GetValue(null);
            if (ownBanner != null && ownBanner != _previousBanner) Object.DestroyImmediate(ownBanner.gameObject);
            Object.DestroyImmediate(_host); _enemies.Clear(); BannerInstance.SetValue(null, _previousBanner);
        }
        private object CallRoom(string method, params object[] args) => typeof(RoomController).GetMethod(method, Private).Invoke(_room, args);
        private void SetEnemyCount(int count)
        {
            _living.Clear(); foreach (var enemy in _enemies) if (enemy != null) Object.DestroyImmediate(enemy); _enemies.Clear();
            for (int i = 0; i < count; i++)
            {
                var enemy = new GameObject("Fixture enemy " + i); enemy.transform.SetParent(_host.transform, false); _enemies.Add(enemy);
                _living.Add(enemy.AddComponent<EnemyHealth>());
            }
            typeof(RoomController).GetField("_initialEnemyCount", Private).SetValue(_room, count);
        }
        private EnemyHealth DefeatOne()
        {
            var health = _living[0]; CallRoom("OnSpawnedEnemyDeath", health); return health;
        }
        private bool RewardsResolved => (bool)typeof(RoomController).GetField("_roomClearRewardsSpawned", Private).GetValue(_room);

        [Test] public void ThresholdOpensExitWhileEnemiesRemainAndClearIsASeparateEvent()
        {
            DefeatOne(); Assert.That(_portal.CanInteract(), Is.False); Assert.That(Notice, Is.Null);
            DefeatOne(); Assert.That(_portal.CanInteract(), Is.True); Assert.That(_living.Count, Is.EqualTo(1));
            Assert.That(_room.IsCleared, Is.False); Assert.That(Notice.Current, Is.EqualTo(RoomNotice.PortalUnlocked)); Assert.That(RewardsResolved, Is.False);
            DefeatOne(); Assert.That(_room.IsCleared, Is.True);
            Assert.That(Notice.Current, Is.EqualTo(RoomNotice.PortalUnlocked | RoomNotice.Cleared)); Assert.That(RewardsResolved, Is.True);
        }
        [Test] public void DuplicateDeathAndUnlockDoNotRestartMessagesOrCreateUnconfiguredRewards()
        {
            DefeatOne(); DefeatOne(); var last = DefeatOne(); float end = Notice.ExpiresAt;
            CallRoom("OnSpawnedEnemyDeath", last); Assert.That((bool)CallRoom("TryUnlockNextRoomPortal", true), Is.False);
            Assert.That(Notice.ExpiresAt, Is.EqualTo(end)); Assert.That(RewardsResolved, Is.True);
            Assert.That(_host.GetComponentsInChildren<WorldLootCache>(true), Is.Empty);
            Assert.That(_host.GetComponentsInChildren<HubServiceNpc>(true), Is.Empty);
        }
        [Test] public void SingleEnemyHasOneCombinedClearAndUnlockAnnouncement()
        {
            SetEnemyCount(1); DefeatOne();
            Assert.That(_portal.CanInteract(), Is.True);
            Assert.That(Notice.Current, Is.EqualTo(RoomNotice.PortalUnlocked | RoomNotice.Cleared));
        }
        [Test] public void EmptyRoomStartsOpenWithoutAFabricatedClearOrUnlockCelebration()
        {
            SetEnemyCount(0); _room.OnRoomEntered(null, new DungeonModifierContext());
            Assert.That(_portal.CanInteract(), Is.True); Assert.That(_room.IsCleared, Is.True);
            Assert.That(Notice == null || Notice.Current == RoomNotice.None, Is.True);
            Assert.That((bool)typeof(DungeonPortal).GetField("_unlockPresented", Private).GetValue(_portal), Is.False);
            Assert.That(_host.GetComponentsInChildren<WorldLootCache>(true), Is.Empty);
        }
        [Test] public void LevelUpCannotOverwriteRoomUnlockAndClearFacts()
        {
            RoomClearedBanner.ShowCharacterLevelUp(); DefeatOne(); DefeatOne(); DefeatOne();
            Assert.That(Notice.Current, Is.EqualTo(RoomNotice.LevelUp | RoomNotice.PortalUnlocked | RoomNotice.Cleared));
        }
        [Test] public void OwnerCancellationDoesNotHideAnUnrelatedGlobalLevelUp()
        {
            DefeatOne(); DefeatOne(); RoomClearedBanner.HideForRoom(_room);
            Assert.That(Notice.Current, Is.EqualTo(RoomNotice.None));
            RoomClearedBanner.ShowCharacterLevelUp(); RoomClearedBanner.HideForRoom(_room);
            Assert.That(Notice.Current, Is.EqualTo(RoomNotice.LevelUp));
        }
    }
}
