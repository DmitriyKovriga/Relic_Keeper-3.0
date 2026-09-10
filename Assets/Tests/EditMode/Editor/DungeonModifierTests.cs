using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
using UnityEngine;
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
    }
}
