using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
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

                Rigidbody2D body = chest.GetComponent<Rigidbody2D>();
                BoxCollider2D box = chest.GetComponent<BoxCollider2D>();
                Assert.That(body, Is.Not.Null);
                Assert.That(box, Is.Not.Null);
                Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(body.gravityScale, Is.EqualTo(3f));
                Assert.That(body.freezeRotation, Is.True);
                Assert.That(box.isTrigger, Is.False);
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
            Assert.That(DungeonRunProgress.ResolveDisplayedRoomCount(10, 10), Is.EqualTo(20));
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
        public void ContinueRunWindow_FitsPixelCanvasAndExposesBothChoices()
        {
            VisualElement window = DungeonRunContinueUI.CreateWindow(out Button continueButton, out Button returnButton);

            Assert.That(window.style.width.value.value, Is.EqualTo(DungeonRunContinueUI.WindowWidth));
            Assert.That(window.style.height.value.value, Is.EqualTo(DungeonRunContinueUI.WindowHeight));
            Assert.That(DungeonRunContinueUI.WindowWidth, Is.LessThanOrEqualTo(480));
            Assert.That(DungeonRunContinueUI.WindowHeight, Is.LessThanOrEqualTo(270));
            Assert.That(continueButton.text, Is.EqualTo("Дальше"));
            Assert.That(returnButton.text, Is.EqualTo("В поселение"));
            Assert.That((DungeonRunContinueUI.ButtonWidth * 2) + 8, Is.LessThanOrEqualTo(DungeonRunContinueUI.WindowWidth));
        }
    }
}
