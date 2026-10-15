using NUnit.Framework;
using Scripts.Enemies;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class KnightBodyChargeTests
    {
        [TestCase(1, false)]
        [TestCase(-1, false)]
        [TestCase(1, true)]
        [TestCase(-1, true)]
        public void Charge_CrossesPlatformEdgesButStillStopsAtWalls(int direction, bool wall)
        {
            var data = Object.Instantiate(Resources.Load<EnemyDataSO>("Enemy/SO_Knight"));
            var root = new GameObject("Knight charge ledge test");
            try
            {
                var floor = new GameObject("Platform") { layer = 6 };
                floor.transform.SetParent(root.transform);
                floor.transform.position = new Vector3(1000f, -0.5f);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(2f, 1f);
                var knight = new GameObject("Knight at edge");
                knight.transform.SetParent(root.transform);
                knight.transform.position = new Vector3(1000f + direction * 0.8f, 0.51f);
                knight.AddComponent<BoxCollider2D>().size = Vector2.one;
                var locomotion = knight.AddComponent<EnemyLocomotion2D>();
                Physics2D.SyncTransforms();
                locomotion.Initialize(null, data);
                var body = knight.GetComponent<Rigidbody2D>();
                var step = typeof(EnemyLocomotion2D).GetMethod("FixedUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                // Ordinary movement must retain its existing ledge safety.
                locomotion.SetMoveInput(direction);
                Physics2D.SyncTransforms();
                step.Invoke(locomotion, null);
                Assert.That(locomotion.IsApproachingLedge, Is.True);
                Assert.That(body.linearVelocity.x, Is.Zero);

                if (wall)
                {
                    var obstacle = new GameObject("Wall") { layer = 6 };
                    obstacle.transform.SetParent(root.transform);
                    obstacle.transform.position = new Vector3(1000f + direction * 1.4f, 1f);
                    obstacle.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 3f);
                }

                locomotion.SetForcedHorizontalVelocity(direction * data.ChargeAttack.DashSpeed,
                    data.ChargeAttack.IgnoreLedgesDuringDash);
                Physics2D.SyncTransforms();
                step.Invoke(locomotion, null);
                Assert.That(body.linearVelocity.x,
                    Is.EqualTo(wall ? 0f : direction * data.ChargeAttack.DashSpeed).Within(0.001f));

                if (!wall)
                {
                    // Once beyond the platform the charge must preserve falling motion.
                    knight.transform.position += Vector3.right * direction * 2f;
                    body.linearVelocity = new Vector2(body.linearVelocity.x, -2f);
                    Physics2D.SyncTransforms();
                    step.Invoke(locomotion, null);
                    Assert.That(locomotion.IsGrounded, Is.False);
                    Assert.That(body.linearVelocity.x, Is.EqualTo(direction * data.ChargeAttack.DashSpeed));
                    Assert.That(body.linearVelocity.y, Is.EqualTo(-2f));
                    Assert.That(body.gravityScale, Is.GreaterThan(0f));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
            }
        }

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
