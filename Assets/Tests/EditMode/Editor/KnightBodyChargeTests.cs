using NUnit.Framework;
using Scripts.Enemies;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class KnightBodyChargeTests
    {
        [Test]
        public void BodyOverlap_IsTrueOnlyWhileThePlayerStandsInTheKnight()
        {
            var knight = new GameObject("KnightBody");
            var player = new GameObject("PlayerInKnight");
            try
            {
                var knightBox = knight.AddComponent<BoxCollider2D>();
                knightBox.size = new Vector2(0.85f, 1.25f);
                var playerBox = player.AddComponent<BoxCollider2D>();
                playerBox.size = new Vector2(0.5f, 0.9f);
                Physics2D.SyncTransforms();

                var entity = knight.AddComponent<EnemyEntity>();
                Assert.That(entity.OverlapsBody(player.transform), Is.True);

                player.transform.position = new Vector3(1.6f, 0f, 0f);
                Physics2D.SyncTransforms();
                Assert.That(entity.OverlapsBody(player.transform), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(knight);
            }
        }

        [Test]
        public void BodyChargeHitbox_CoversAPlayerStandingInTheTorso()
        {
            Vector2 shoulderCenter = new Vector2(0.18f, -0.58f);
            Vector2 shoulderSize = new Vector2(0.65f, 1.5f);
            var body = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(0.85f, 1.25f, 0f));
            Vector3 playerInTorso = new Vector3(0.05f, 0.4f, 0f);

            var shoulder = new Bounds(shoulderCenter, shoulderSize);
            Assert.That(shoulder.Contains(playerInTorso), Is.False);

            EnemyAttackController.EncapsulateHitbox(ref shoulderCenter, ref shoulderSize, body);
            var covered = new Bounds(shoulderCenter, shoulderSize);
            Assert.That(covered.Contains(playerInTorso), Is.True);
            Assert.That(covered.Contains(body.center), Is.True);
        }
    }
}
