using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Items.World;
using Scripts.Visuals;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Reflection;

namespace RelicKeeper.Tests.EditMode
{
    public class DungeonModifierTests
    {
        [Test]
        public void NumericModifiers_StackAdditivelyAndExposeMultipliers()
        {
            var context = new DungeonModifierContext();
            context.Add(new DungeonModifierValues
            {
                LootRarityPercent = 30f,
                ExperiencePercent = 30f,
                EnemyDamageDealtPercent = 100f
            });
            context.Add(new DungeonModifierValues
            {
                LootRarityPercent = 50f,
                ExperiencePercent = -10f
            });

            Assert.That(context.LootRarityMultiplier, Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(context.ExperienceMultiplier, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(context.EnemyDamageDealtMultiplier, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void NegativeModifier_CannotCreateNegativeMultiplier()
        {
            var context = new DungeonModifierContext();
            context.Add(new DungeonModifierValues { EnemyDamageTakenPercent = -150f });

            Assert.That(context.EnemyDamageTakenMultiplier, Is.Zero);
        }

        [Test]
        public void RarityMultiplier_ChangesQualityWithoutChangingDropRoll()
        {
            Assert.That(EnemyLootDropService.RollDrop(0.169f, 1f), Is.True);
            Assert.That(EnemyLootDropService.RollDrop(0.17f, 1f), Is.False);

            EnemyLootRarity normal = EnemyLootDropService.RollDroppedRarity(0.15f, 1f);
            EnemyLootRarity boosted = EnemyLootDropService.RollDroppedRarity(0.15f, 3f);

            Assert.That(normal, Is.EqualTo(EnemyLootRarity.Magic));
            Assert.That(boosted, Is.EqualTo(EnemyLootRarity.Rare));
        }

        [Test]
        public void SpecialModifier_AddsChestRangeAndRewardFlag()
        {
            DungeonModifierSO modifier = ScriptableObject.CreateInstance<DungeonModifierSO>();
            try
            {
                modifier.RewardEffects = DungeonRewardEffect.SpawnRewardChests |
                                         DungeonRewardEffect.GuaranteedRareWeapon;
                modifier.MinimumChests = 1;
                modifier.MaximumChests = 3;
                var context = new DungeonModifierContext();

                modifier.ApplyTo(context);

                Assert.That(context.MinimumChests, Is.EqualTo(1));
                Assert.That(context.MaximumChests, Is.EqualTo(3));
                Assert.That(context.RewardEffects.HasFlag(DungeonRewardEffect.GuaranteedRareWeapon), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(modifier);
            }
        }

        [Test]
        public void DoubleMonsterModifier_DoublesSpawnerCount()
        {
            var host = new GameObject("SpawnerTest");
            try
            {
                EnemySpawner spawner = host.AddComponent<EnemySpawner>();
                FieldInfo spawnCount = typeof(EnemySpawner).GetField("_spawnCount", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(spawnCount, Is.Not.Null);
                spawnCount.SetValue(spawner, 3);

                Assert.That(spawner.GetScaledSpawnCount(2f), Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RewardChest_UsesDynamicPhysicsLikeEnemies()
        {
            var created = new List<Object>();
            try
            {
                RewardChest chest = RewardChest.Spawn(new Vector3(2f, 5f, 0f), 1, null);
                created.Add(chest.gameObject);

                // Awake is not guaranteed to run for instantiated scene objects in EditMode.
                // Apply the same runtime physics configuration explicitly before asserting it.
                MethodInfo configurePhysics = typeof(RewardChest).GetMethod(
                    "ConfigurePhysics", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(configurePhysics, Is.Not.Null);
                configurePhysics.Invoke(chest, null);

                Rigidbody2D body = chest.GetComponent<Rigidbody2D>();
                BoxCollider2D box = chest.GetComponent<BoxCollider2D>();
                Assert.That(body, Is.Not.Null);
                Assert.That(box, Is.Not.Null);
                Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(body.gravityScale, Is.EqualTo(3f));
                Assert.That(body.freezeRotation, Is.True);
                Assert.That(box.isTrigger, Is.False);

                MethodInfo ensureVisual = typeof(RewardChest).GetMethod(
                    "EnsurePlaceholderVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(ensureVisual, Is.Not.Null);
                ensureVisual.Invoke(chest, null);

                SpriteRenderer renderer = chest.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerVfx));
                Assert.That(renderer.sortingOrder, Is.GreaterThan(WorldDroppedItem.TopVisualSortingOrder));
            }
            finally
            {
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i] != null)
                        Object.DestroyImmediate(created[i]);
                }
            }
        }

        [Test]
        public void ResolveRewardPosition_UsesPlayerCoordinatesNotPortal()
        {
            var created = new List<Object>();
            try
            {
                var roomGo = new GameObject("RewardRoom");
                created.Add(roomGo);
                RoomController room = roomGo.AddComponent<RoomController>();

                var portalGo = new GameObject("NextPortal");
                created.Add(portalGo);
                portalGo.transform.position = new Vector3(80f, 0f, 0f);

                var player = new GameObject("Player");
                created.Add(player);
                player.tag = "Player";
                player.transform.position = new Vector3(4f, 1.5f, 0f);

                MethodInfo method = typeof(RoomController).GetMethod(
                    "ResolveRewardPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                Vector2 result = (Vector2)method.Invoke(room, null);
                Assert.That(result.x, Is.EqualTo(4f).Within(0.01f));
                Assert.That(result.y, Is.EqualTo(2.12f).Within(0.01f));
            }
            finally
            {
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i] != null)
                        Object.DestroyImmediate(created[i]);
                }
            }
        }

        [Test]
        public void RoomClearedBanner_UsesSharedLocalizationKey()
        {
            Assert.That(RoomClearedBanner.LocalizationKey, Is.EqualTo("dungeon.ui.roomCleared"));
            Assert.That(RoomClearedBanner.FallbackText, Is.EqualTo("Room Cleared"));
            Assert.That(RoomClearedBanner.PortalUnlockedLocalizationKey, Is.EqualTo("dungeon.ui.portalUnlocked"));
            Assert.That(RoomClearedBanner.ClearedAndPortalUnlockedLocalizationKey,
                Is.EqualTo("dungeon.ui.roomClearedPortalUnlocked"));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        public void NextRoomPortal_RequiresHalfOfActuallySpawnedEnemiesRoundedUp(int enemies, int kills)
        {
            Assert.That(RoomController.RequiredPortalKills(enemies), Is.EqualTo(kills));
        }

        [Test]
        public void NextRoomPortal_StaysHiddenUntilKillThreshold_AndDoesNotAffectOtherPortals()
        {
            var roomObject = new GameObject("PortalTestRoom");
            try
            {
                RoomController room = roomObject.AddComponent<RoomController>();
                var nextPortalObject = new GameObject("NextRoomPortal");
                nextPortalObject.transform.SetParent(roomObject.transform);
                nextPortalObject.AddComponent<BoxCollider2D>();
                DungeonPortal nextPortal = nextPortalObject.AddComponent<DungeonPortal>();

                var hubPortalObject = new GameObject("HubPortal");
                hubPortalObject.transform.SetParent(roomObject.transform);
                hubPortalObject.AddComponent<BoxCollider2D>();
                DungeonPortal hubPortal = hubPortalObject.AddComponent<DungeonPortal>();
                typeof(DungeonPortal).GetField("_portalType", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hubPortal, PortalType.ReturnToHub);

                var living = (List<EnemyHealth>)typeof(RoomController)
                    .GetField("_livingEnemies", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(room);
                for (int i = 0; i < 3; i++)
                {
                    var enemy = new GameObject("Enemy" + i);
                    enemy.transform.SetParent(roomObject.transform);
                    living.Add(enemy.AddComponent<EnemyHealth>());
                }

                typeof(RoomController).GetField("_initialEnemyCount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(room, 3);
                MethodInfo setPortals = typeof(RoomController).GetMethod(
                    "SetNextRoomPortalsActive", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo tryUnlock = typeof(RoomController).GetMethod(
                    "TryUnlockNextRoomPortal", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setPortals, Is.Not.Null);
                Assert.That(tryUnlock, Is.Not.Null);

                setPortals.Invoke(room, new object[] { false });
                Assert.That(nextPortalObject.activeSelf, Is.False);
                Assert.That(hubPortalObject.activeSelf, Is.True);

                living.RemoveAt(0);
                Assert.That((bool)tryUnlock.Invoke(room, new object[] { true }), Is.False);
                Assert.That(nextPortalObject.activeSelf, Is.False);

                living.RemoveAt(0);
                Assert.That((bool)tryUnlock.Invoke(room, new object[] { true }), Is.True);
                Assert.That(nextPortalObject.activeSelf, Is.True);
                Assert.That(hubPortalObject.activeSelf, Is.True);
                Assert.That((bool)tryUnlock.Invoke(room, new object[] { true }), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void LocationLevel_ScalesWithDungeonMinLevelAndCompletedRooms()
        {
            Assert.That(DungeonRunProgress.ResolveLocationLevel(1, 0, 0), Is.EqualTo(1));
            Assert.That(DungeonRunProgress.ResolveLocationLevel(1, 0, 9), Is.EqualTo(10));
            Assert.That(DungeonRunProgress.ResolveLocationLevel(1, 10, 0), Is.EqualTo(11));
            Assert.That(DungeonRunProgress.ResolveLocationLevel(5, 10, 4), Is.EqualTo(19));
        }

        [Test]
        public void LocationLevel_AddsOnePercentLootRarityAndQuantityPerLevel()
        {
            var firstRoom = new DungeonModifierContext();
            firstRoom.Add(DungeonRunProgress.CreateLocationLevelLootModifier(1));
            Assert.That(firstRoom.LootDropChancePercent, Is.EqualTo(1f));
            Assert.That(firstRoom.LootRarityPercent, Is.EqualTo(1f));
            Assert.That(firstRoom.LootDropChanceMultiplier, Is.EqualTo(1.01f).Within(0.0001f));
            Assert.That(firstRoom.LootRarityMultiplier, Is.EqualTo(1.01f).Within(0.0001f));

            var endlessRoom = new DungeonModifierContext();
            endlessRoom.Add(DungeonRunProgress.CreateLocationLevelLootModifier(11));
            Assert.That(endlessRoom.LootDropChancePercent, Is.EqualTo(11f));
            Assert.That(endlessRoom.LootRarityPercent, Is.EqualTo(11f));
            Assert.That(endlessRoom.LootDropChanceMultiplier, Is.EqualTo(1.11f).Within(0.0001f));
            Assert.That(endlessRoom.LootRarityMultiplier, Is.EqualTo(1.11f).Within(0.0001f));
            Assert.That(firstRoom.EnemyCountPercent, Is.EqualTo(2f));
            Assert.That(endlessRoom.EnemyCountPercent, Is.EqualTo(22f));
            Assert.That(endlessRoom.EnemyCountMultiplier, Is.EqualTo(1.22f).Within(0.0001f));
        }

        [Test]
        public void LocationLevelLoot_AppearsInHudModifierDescriptions()
        {
            var lines = new List<string>();
            DungeonRunProgress.AddLocationLevelLootDescriptions(lines, 11);

            Assert.That(lines, Does.Contain("Шанс выпадения предметов +11%"));
            Assert.That(lines, Does.Contain("Редкость предметов +11%"));
            Assert.That(lines, Does.Contain("Количество монстров +22%"));
        }

        [Test]
        public void ModifierHudWrap_UsesFullLineBudgetButStillWrapsLongText()
        {
            int innerWidth = DungeonModifierHud.PanelWidth
                - DungeonModifierHud.PanelPaddingLeft
                - DungeonModifierHud.PanelPaddingRight;
            Assert.That(DungeonModifierHud.MaxCharactersPerLine * 3, Is.LessThanOrEqualTo(innerWidth + 2));
            Assert.That(DungeonModifierHud.MaxCharactersPerLine, Is.GreaterThan(27));

            string lootLine = "Шанс выпадения предметов +11%";
            string wrappedLoot = DungeonModifierHud.WrapForHud(lootLine, out int lootLines);
            Assert.That(lootLines, Is.EqualTo(1));
            Assert.That(wrappedLoot, Does.Not.Contain("\n"));

            string countLine = "Количество монстров +22%";
            string wrappedCount = DungeonModifierHud.WrapForHud(countLine, out int countLines);
            Assert.That(countLines, Is.EqualTo(1));
            Assert.That(wrappedCount, Does.Not.Contain("\n"));

            string longLine = "Шанс выпадения предметов +11% и дополнительный эффект на монстров в этой комнате";
            string wrappedLong = DungeonModifierHud.WrapForHud(longLine, out int longLines);
            Assert.That(longLines, Is.GreaterThan(1));
            Assert.That(wrappedLong, Does.Contain("\n"));
        }

        [Test]
        public void EndlessSegment_DisplaysRoomsPastTheFirstTen()
        {
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomNumber(0, 0), Is.EqualTo(1));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomNumber(10, 0), Is.EqualTo(11));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomCount(0, 10), Is.EqualTo(10));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomCount(10, 10), Is.EqualTo(20));
        }

        [Test]
        public void FloorSkip_ContinueChoiceSitsBetweenCheckpointAndNextRoom()
        {
            Assert.That(DungeonRunProgress.ResolveSegmentRoomCount(0, 10), Is.EqualTo(10));
            Assert.That(DungeonRunProgress.ResolveNextRoomAfterSegment(0, 10), Is.EqualTo(11));

            int skipTen = DungeonRunProgress.ResolveStartingRoomsCompleted(10);
            int skipTenCount = DungeonRunProgress.ResolveSegmentRoomCount(skipTen, 10);
            Assert.That(skipTenCount, Is.EqualTo(1));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomNumber(skipTen, 0), Is.EqualTo(10));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomCount(skipTen, skipTenCount), Is.EqualTo(10));
            Assert.That(DungeonRunProgress.ResolveNextRoomAfterSegment(skipTen, skipTenCount), Is.EqualTo(11));
            Assert.That(DungeonRunProgress.ResolveSegmentRoomCount(10, 10), Is.EqualTo(10));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomNumber(10, 0), Is.EqualTo(11));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomCount(10, 10), Is.EqualTo(20));

            int skipTwenty = DungeonRunProgress.ResolveStartingRoomsCompleted(20);
            int skipTwentyCount = DungeonRunProgress.ResolveSegmentRoomCount(skipTwenty, 10);
            Assert.That(skipTwentyCount, Is.EqualTo(1));
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomNumber(skipTwenty, 0), Is.EqualTo(20));
            Assert.That(DungeonRunProgress.ResolveNextRoomAfterSegment(skipTwenty, skipTwentyCount), Is.EqualTo(21));
            Assert.That(DungeonRunProgress.ResolveSegmentRoomCount(20, 10), Is.EqualTo(10));
        }

        [Test]
        public void RoomController_SetRuntimeLevel_OverridesPrefabLevel()
        {
            var host = new GameObject("RuntimeLevelRoom");
            try
            {
                RoomController room = host.AddComponent<RoomController>();
                room.SetRuntimeLevel(11);
                Assert.That(room.RoomLevel, Is.EqualTo(11));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ReturnToSettlementButton_FitsBottomRightOfPixelCanvas()
        {
            Button button = DungeonModifierChoiceUI.CreateReturnToSettlementButton();

            Assert.That(DungeonModifierChoiceUI.ReturnButtonWidth + DungeonModifierChoiceUI.ReturnButtonInset, Is.LessThanOrEqualTo(480));
            Assert.That(DungeonModifierChoiceUI.ReturnButtonHeight + DungeonModifierChoiceUI.ReturnButtonInset, Is.LessThanOrEqualTo(270));
            Assert.That(button.style.right.value.value, Is.EqualTo(DungeonModifierChoiceUI.ReturnButtonInset));
            Assert.That(button.style.bottom.value.value, Is.EqualTo(DungeonModifierChoiceUI.ReturnButtonInset));
            Assert.That(button.style.width.value.value, Is.EqualTo(DungeonModifierChoiceUI.ReturnButtonWidth));
            Assert.That(button.style.height.value.value, Is.EqualTo(DungeonModifierChoiceUI.ReturnButtonHeight));
            Assert.That(button.text, Is.EqualTo("В поселение"));
        }

        [Test]
        public void ModifierChoiceCloseButton_FitsBottomLeftOfPixelCanvas()
        {
            Button button = DungeonModifierChoiceUI.CreateCloseButton();

            Assert.That(button.style.left.value.value, Is.EqualTo(DungeonModifierChoiceUI.ReturnButtonInset));
            Assert.That(button.style.bottom.value.value, Is.EqualTo(DungeonModifierChoiceUI.ReturnButtonInset));
            Assert.That(button.style.width.value.value, Is.EqualTo(DungeonModifierChoiceUI.CloseButtonWidth));
            Assert.That(button.style.height.value.value, Is.EqualTo(DungeonModifierChoiceUI.ReturnButtonHeight));
            Assert.That(button.text, Is.EqualTo("Закрыть"));
            Assert.That(DungeonModifierChoiceUI.CloseButtonWidth + DungeonModifierChoiceUI.ReturnButtonInset,
                Is.LessThanOrEqualTo(480));
        }

        [Test]
        public void ClosingRoomChoice_KeepsTheSameThreeModifiersForReopening()
        {
            var controllerObject = new GameObject("ChoiceCacheTest");
            DungeonDataSO dungeon = ScriptableObject.CreateInstance<DungeonDataSO>();
            var modifiers = new List<DungeonModifierSO>();
            try
            {
                DungeonController controller = controllerObject.AddComponent<DungeonController>();
                for (int i = 0; i < 4; i++)
                    modifiers.Add(ScriptableObject.CreateInstance<DungeonModifierSO>());

                typeof(DungeonDataSO).GetField("_roomModifierPool", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(dungeon, modifiers);
                typeof(DungeonDataSO).GetField("_roomChoiceCount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(dungeon, 3);
                typeof(DungeonController).GetField("_currentDungeon", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, dungeon);

                MethodInfo getChoices = typeof(DungeonController).GetMethod(
                    "GetOrCreatePendingRoomChoices", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo closeChoice = typeof(DungeonController).GetMethod(
                    "ClosePendingRoomChoice", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(getChoices, Is.Not.Null);
                Assert.That(closeChoice, Is.Not.Null);

                var first = (List<DungeonModifierSO>)getChoices.Invoke(controller, null);
                closeChoice.Invoke(controller, null);
                var reopened = (List<DungeonModifierSO>)getChoices.Invoke(controller, null);

                Assert.That(first, Has.Count.EqualTo(3));
                Assert.That(reopened, Is.SameAs(first));
                Assert.That(reopened, Is.EquivalentTo(first));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(dungeon);
                foreach (DungeonModifierSO modifier in modifiers)
                    Object.DestroyImmediate(modifier);
            }
        }

        [Test]
        public void ContinueRunWindow_FitsPixelCanvasAndExposesBothChoices()
        {
            VisualElement window = DungeonRunContinueUI.CreateWindow(
                out Button continueButton, out Button returnButton, out Button closeButton);

            Assert.That(window.style.width.value.value, Is.EqualTo(DungeonRunContinueUI.WindowWidth));
            Assert.That(window.style.height.value.value, Is.EqualTo(DungeonRunContinueUI.WindowHeight));
            Assert.That(DungeonRunContinueUI.WindowWidth, Is.LessThanOrEqualTo(480));
            Assert.That(DungeonRunContinueUI.WindowHeight, Is.LessThanOrEqualTo(270));
            Assert.That(continueButton.text, Is.EqualTo("Дальше"));
            Assert.That(returnButton.text, Is.EqualTo("В поселение"));
            Assert.That(closeButton.text, Is.EqualTo("Закрыть"));
            Assert.That((DungeonRunContinueUI.ButtonWidth * 2) + 8, Is.LessThanOrEqualTo(DungeonRunContinueUI.WindowWidth));
            Assert.That(
                DungeonRunContinueUI.FormatTitle(10),
                Is.EqualTo("10 этаж пройден.\nИдти дальше или в поселение?"));
            Assert.That(
                DungeonRunContinueUI.FormatTitle(20),
                Is.EqualTo("20 этаж пройден.\nИдти дальше или в поселение?"));
        }

        [Test]
        public void FloorCheckpoints_UnlockEveryTenReachedRooms()
        {
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(0), Is.Empty);
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(9), Is.Empty);
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(10), Is.EqualTo(new[] { 10 }));
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(19), Is.EqualTo(new[] { 10 }));
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(20), Is.EqualTo(new[] { 10, 20 }));
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(25), Is.EqualTo(new[] { 10, 20 }));
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(29), Is.EqualTo(new[] { 10, 20 }));
            Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(30), Is.EqualTo(new[] { 10, 20, 30 }));
        }

        [Test]
        public void FloorSkip_StartsAtChosenRoomAndKeepsThirtyPercentBonus()
        {
            Assert.That(DungeonRunProgress.ResolveStartingRoomsCompleted(10), Is.EqualTo(9));
            Assert.That(DungeonRunProgress.ResolveLocationLevel(1, 9, 0), Is.EqualTo(10));

            DungeonModifierValues bonus = DungeonRunProgress.CreateFloorSkipBonusModifier();
            Assert.That(bonus.LootDropChancePercent, Is.EqualTo(30f));
            Assert.That(bonus.LootRarityPercent, Is.EqualTo(30f));
            Assert.That(bonus.ExperiencePercent, Is.EqualTo(30f));
            Assert.That(bonus.EnemyDamageDealtPercent, Is.EqualTo(30f));
            Assert.That(bonus.EnemyCountPercent, Is.EqualTo(30f));
        }

        [Test]
        public void FloorPortal_IsDetectedByNameIncludingCloneSuffix()
        {
            Assert.That(DungeonRunProgress.IsFloorSelectPortalName("FloorPortal"), Is.True);
            Assert.That(DungeonRunProgress.IsFloorSelectPortalName("FloorPortal(Clone)"), Is.True);
            Assert.That(DungeonRunProgress.IsFloorSelectPortalName("FloorPortal (1)"), Is.True);
            Assert.That(DungeonRunProgress.IsFloorSelectPortalName("NextRoomPortal"), Is.False);
        }

        [Test]
        public void PortalWorldTitles_UseMortfallAndFloorPortalLiterals()
        {
            DungeonDataSO dungeon = ScriptableObject.CreateInstance<DungeonDataSO>();
            try
            {
                dungeon.DisplayName = "Mortfall";
                Assert.That(DungeonPortal.ResolveWorldTitle(false, dungeon), Is.EqualTo("Mortfall"));
                Assert.That(DungeonPortal.ResolveWorldTitle(true, dungeon), Is.EqualTo("FloorPortal"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dungeon);
            }
        }

        [Test]
        public void FloorSelectWindow_FitsPixelCanvas()
        {
            VisualElement window = DungeonFloorSelectUI.CreateWindow(
                out Label title,
                out Label emptyLabel,
                out VisualElement list,
                out Button closeButton);

            Assert.That(DungeonFloorSelectUI.WindowWidth, Is.LessThanOrEqualTo(480));
            Assert.That(DungeonFloorSelectUI.WindowHeight, Is.LessThanOrEqualTo(270));
            Assert.That(window.style.width.value.value, Is.EqualTo(DungeonFloorSelectUI.WindowWidth));
            Assert.That(window.style.height.value.value, Is.EqualTo(DungeonFloorSelectUI.WindowHeight));
            Assert.That(title, Is.Not.Null);
            Assert.That(emptyLabel, Is.Not.Null);
            Assert.That(list.style.maxHeight.value.value, Is.EqualTo(DungeonFloorSelectUI.ListMaxHeight));
            Assert.That(closeButton.text, Is.EqualTo("Закрыть"));
        }

        [Test]
        public void DungeonUnlocks_KeepHighestReachedRoomPerDungeon()
        {
            DungeonRunUnlocks.Clear();
            try
            {
                DungeonRunUnlocks.RecordReachedRoom("Mortfall", 3);
                DungeonRunUnlocks.RecordReachedRoom("Mortfall", 12);
                DungeonRunUnlocks.RecordReachedRoom("Mortfall", 8);
                Assert.That(DungeonRunUnlocks.GetHighestDisplayedRoom("Mortfall"), Is.EqualTo(12));
                Assert.That(DungeonRunProgress.ResolveUnlockedFloorCheckpoints(12), Is.EqualTo(new[] { 10 }));
            }
            finally
            {
                DungeonRunUnlocks.Clear();
            }
        }
    }
}
