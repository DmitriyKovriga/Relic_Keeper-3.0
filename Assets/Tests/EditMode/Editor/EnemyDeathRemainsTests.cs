using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class EnemyDeathRemainsTests
    {
        [Test]
        public void PackedSlots_DoNotOverlapAndLaterDeathsGoBelow()
        {
            var layer = new EnemyDeathRemainsLayer();
            int first = layer.AllocateSpriteOrder();
            int second = layer.AllocateSpriteOrder();
            int third = layer.AllocateSpriteOrder();

            Assert.That(first, Is.EqualTo(EnemyDeathRemainsLayer.FirstSpriteOrder));
            Assert.That(second, Is.EqualTo(first - EnemyDeathRemainsLayer.SlotStride));
            Assert.That(third, Is.LessThan(second));
            Assert.That(layer.NextSpriteOrder, Is.LessThan(third));
        }

        [Test]
        public void PackedSlots_KeepUniqueMaskRanges()
        {
            var layer = new EnemyDeathRemainsLayer();
            var used = new HashSet<int>();

            for (int i = 0; i < 8; i++)
            {
                int sprite = layer.AllocateSpriteOrder();
                int[] band =
                {
                    EnemyDeathRemainsLayer.MaskBackOrder(sprite),
                    sprite,
                    EnemyDeathRemainsLayer.OverlayOrder(sprite),
                    EnemyDeathRemainsLayer.MaskFrontOrder(sprite)
                };

                Assert.That(band[3] - band[0], Is.EqualTo(EnemyDeathRemainsLayer.SlotStride - 1));
                for (int j = 0; j < band.Length; j++)
                    Assert.That(used.Add(band[j]), Is.True, $"order {band[j]} reused");
            }
        }

        [Test]
        public void Quality_StaysFullForEveryNewDeath()
        {
            var host = new GameObject("RemainsSheet");
            try
            {
                var sheet = host.AddComponent<EnemyDeathRemainsSheet>();
                for (int i = 0; i < 40; i++)
                    Assert.That(sheet.GetQuality(), Is.EqualTo(DeathEffectQuality.Full));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FragmentBudget_StopsAtConfiguredCap()
        {
            var host = new GameObject("RemainsSheet");
            try
            {
                var sheet = host.AddComponent<EnemyDeathRemainsSheet>();
                for (int i = 0; i < 96; i++)
                    Assert.That(sheet.TryReserveFragment(), Is.True);
                Assert.That(sheet.ActiveFragments, Is.EqualTo(96));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RemainsSheet_ReusesOneHostPerRoom()
        {
            var room = new GameObject("RoomRoot");
            var nested = new GameObject("SpawnerGroup");
            try
            {
                room.AddComponent<RoomController>();
                nested.transform.SetParent(room.transform, false);

                EnemyDeathRemainsSheet first = EnemyDeathRemainsSheet.GetOrCreate(nested.transform);
                EnemyDeathRemainsSheet second = EnemyDeathRemainsSheet.GetOrCreate(nested.transform);

                Assert.That(second, Is.SameAs(first));
                Assert.That(first.transform.parent, Is.EqualTo(room.transform));
                Assert.That(first.name, Is.EqualTo(EnemyDeathRemainsSheet.ObjectName));
            }
            finally
            {
                Object.DestroyImmediate(nested);
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void FlyingChunks_UseDarkenedMaskedSourceRegionsInsteadOfScaledEnemyCopies()
        {
            var room = new GameObject("DeathEffectRoom");
            var enemy = new GameObject("Enemy");
            const int sourceSize = 96;
            var texture = new Texture2D(sourceSize, sourceSize, TextureFormat.RGBA32, false);
            Sprite sourceSprite = null;
            EnemyDataSO data = null;

            try
            {
                room.AddComponent<RoomController>();
                enemy.transform.SetParent(room.transform, false);

                var entity = enemy.AddComponent<EnemyEntity>();
                var sourceRenderer = enemy.AddComponent<SpriteRenderer>();
                texture.SetPixels(CreateSolidPixels(sourceSize * sourceSize));
                texture.Apply();
                sourceSprite = Sprite.Create(texture, new Rect(0f, 0f, sourceSize, sourceSize), new Vector2(0.5f, 0.5f), EnemyDeathVisualFactory.PixelsPerUnit);
                sourceSprite.name = "WholeEnemySource";
                sourceRenderer.sprite = sourceSprite;

                data = ScriptableObject.CreateInstance<EnemyDataSO>();
                data.DeathEffect.Enabled = true;
                data.DeathEffect.ChunkCount = 1;
                typeof(EnemyEntity)
                    .GetField("_defaultData", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(entity, data);

                EnemyDeathEffectSpawner.Spawn(entity, sourceRenderer);

                EnemyDeathRemainsSheet sheet = room.GetComponentInChildren<EnemyDeathRemainsSheet>();
                Assert.That(sheet, Is.Not.Null);
                EnemyDeathFragment fragment = sheet.GetComponentInChildren<EnemyDeathFragment>();
                AssertChunkUsesDedicatedSilhouette(fragment, sourceSprite);

                fragment.ForceReturnToPool();
                EnemyDeathEffectSpawner.Spawn(entity, sourceRenderer);
                EnemyDeathFragment reusedFragment = sheet.GetComponentInChildren<EnemyDeathFragment>();
                AssertChunkUsesDedicatedSilhouette(reusedFragment, sourceSprite);
            }
            finally
            {
                if (data != null)
                    Object.DestroyImmediate(data);
                if (sourceSprite != null)
                    Object.DestroyImmediate(sourceSprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Knight_UsesThirtyPercentLargerDeathChunks()
        {
            EnemyDataSO knight = Resources.Load<EnemyDataSO>("Enemy/SO_Knight");
            Assert.That(knight, Is.Not.Null);
            Assert.That(knight.DeathEffect, Is.Not.Null);
            Assert.That(knight.DeathEffect.ChunkSizeMultiplier, Is.EqualTo(1.3f).Within(0.001f));
        }

        private static void AssertChunkUsesDedicatedSilhouette(EnemyDeathFragment fragment, Sprite sourceSprite)
        {
            Assert.That(fragment, Is.Not.Null);
            Transform outlineTransform = fragment.transform.Find("ChunkOutline");
            Transform maskTransform = fragment.transform.Find("ChunkMask");
            Transform detailTransform = fragment.transform.Find("ChunkDetail");
            Assert.That(outlineTransform, Is.Not.Null);
            Assert.That(maskTransform, Is.Not.Null);
            Assert.That(detailTransform, Is.Not.Null);

            SpriteRenderer outline = outlineTransform.GetComponent<SpriteRenderer>();
            SpriteMask mask = maskTransform.GetComponent<SpriteMask>();
            SpriteRenderer detail = detailTransform.GetComponent<SpriteRenderer>();
            Assert.That(outline.sprite, Is.Not.Null);
            Assert.That(outline.sprite, Is.Not.SameAs(sourceSprite));
            Assert.That(outline.sprite.name, Does.StartWith("ChunkMask_"));
            Assert.That(mask.sprite, Is.SameAs(outline.sprite));
            Assert.That(detail.sprite, Is.SameAs(sourceSprite));
            Assert.That(detail.maskInteraction, Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));
            Assert.That(detail.color.grayscale, Is.LessThan(Color.white.grayscale));
            Assert.That(outline.color.grayscale, Is.LessThan(detail.color.grayscale));
            Assert.That(maskTransform.lossyScale.x, Is.LessThan(outlineTransform.lossyScale.x));
            Assert.That(maskTransform.lossyScale.y, Is.LessThan(outlineTransform.lossyScale.y));
            Vector2 colliderSize = fragment.GetComponent<BoxCollider2D>().size;
            Assert.That(colliderSize.x, Is.LessThanOrEqualTo(0.49f));
            Assert.That(colliderSize.y, Is.LessThanOrEqualTo(0.46f));
        }

        private static Color[] CreateSolidPixels(int count)
        {
            var pixels = new Color[count];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            return pixels;
        }
    }
}
