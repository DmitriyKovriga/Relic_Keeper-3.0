using System.IO;
using System.Reflection;
using NUnit.Framework;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CornerTraversalTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(PlayerMovement move, string field, object value) => typeof(PlayerMovement).GetField(field, Private).SetValue(move, value);
        private static void Call(PlayerMovement move, string method, params object[] args) => typeof(PlayerMovement).GetMethod(method, Private).Invoke(move, args);

        [TestCase("wall", 5, 1)] [TestCase("wall", 5, -1)]
        [TestCase("wall", 10, 1)] [TestCase("wall", 10, -1)]
        [TestCase("ceiling", 5, 1)] [TestCase("ceiling", 5, -1)]
        [TestCase("ceiling", 10, 1)] [TestCase("ceiling", 10, -1)]
        [TestCase("clear-corner", 5, 1)] [TestCase("clear-corner", 5, -1)]
        [TestCase("clear-corner", 10, 1)] [TestCase("clear-corner", 10, -1)]
        [TestCase("one-way", 5, 1)] [TestCase("one-way", 5, -1)]
        [TestCase("one-way", 10, 1)] [TestCase("one-way", 10, -1)]
        public void PhysicalJumpRespectsCornersWithoutCorrection(string scenario, float speed, int direction)
        {
            var oldMode = Physics2D.simulationMode;
            var root = new GameObject("Corner review objects");
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                var player = new GameObject("Corner review player", typeof(BoxCollider2D)); player.transform.SetParent(root.transform);
                Vector2 origin = new Vector2(900, 900); player.transform.position = origin;
                var collider = player.GetComponent<BoxCollider2D>();
                collider.size = new Vector2(.50969076f, .91494656f); collider.offset = new Vector2(0, -.042526722f);
                var move = player.AddComponent<PlayerMovement>(); var body = player.GetComponent<Rigidbody2D>();
                body.constraints = RigidbodyConstraints2D.FreezeRotation; body.gravityScale = 2.5f;
                body.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Materials/Player_Material.physicsMaterial2D");
                Assert.That(body.sharedMaterial, Is.Not.Null); Assert.That(body.sharedMaterial.friction, Is.Zero);
                var stats = player.GetComponent<PlayerStats>(); stats.GetStat(StatType.MoveSpeed).BaseValue = speed;
                Set(move, "_rb", body); Set(move, "_mainCollider", collider); Set(move, "_stats", stats);
                Set(move, "_baseGravityScale", 2.5f); Set(move, "_horizontalInput", (float)direction);
                Set(move, "_groundLayer", (LayerMask)(1 << LayerMask.NameToLayer("Ground")));
                Set(move, "_oneWayPlatformLayer", (LayerMask)(1 << LayerMask.NameToLayer("OneWayPlatform")));
                var obstacle = new GameObject("Corner obstacle", typeof(BoxCollider2D)); obstacle.transform.SetParent(root.transform);
                obstacle.layer = LayerMask.NameToLayer(scenario == "one-way" ? "OneWayPlatform" : "Ground");
                var wall = obstacle.GetComponent<BoxCollider2D>();
                if (scenario == "wall") { obstacle.transform.position = origin + new Vector2(direction, 1); wall.size = new Vector2(.5f, 8); }
                else if (scenario == "ceiling") { obstacle.transform.position = origin + new Vector2(0, 1.6f); wall.size = new Vector2(40, 1); }
                else if (scenario == "clear-corner")
                {
                    // The head clears a ceiling lip by 0.04 units (about one source pixel), travelling away from it.
                    obstacle.transform.position = origin + new Vector2(-direction * (.25484538f + .04f + .5f), 1.5f);
                    wall.size = Vector2.one;
                }
                else
                {
                    obstacle.transform.position = origin + new Vector2(0, .7f); wall.size = new Vector2(40, .2f);
                    obstacle.AddComponent<PlatformEffector2D>().useOneWay = true; wall.usedByEffector = true;
                }
                Physics2D.SyncTransforms(); Call(move, "ApplyJumpForce", 1f, null);
                // Scripted EditMode physics does not advance Time.time. Vertical
                // velocity still rejects rising support; expire only the launch probe lock.
                Set(move, "_jumpStartedTime", Time.time - 1);
                float peak = 0; bool landedOnPlatform = false, penetrated = false;
                int wallOverlapFrames = 0;
                var trace = new System.Text.StringBuilder("step,x,y,vx,vy,grounded\n");
                for (int step = 0; step < 90; step++)
                {
                    Call(move, "CheckGround"); Call(move, "RefreshJumpCountIfLanded");
                    Call(move, "UpdateFastFallState"); Call(move, "ApplyMovement");
                    Physics2D.Simulate(.02f);
                    peak = Mathf.Max(peak, body.position.y - origin.y);
                    if (scenario == "wall")
                    {
                        bool verticalOverlap = collider.bounds.max.y > wall.bounds.min.y && collider.bounds.min.y < wall.bounds.max.y;
                        float travel = direction * (body.position.x - origin.x);
                        wallOverlapFrames = verticalOverlap && travel > .52f ? wallOverlapFrames + 1 : 0;
                        // Permit a transient discrete-solver overlap, but reject
                        // persistent embedding or crossing the wall's far face.
                        if (verticalOverlap && (travel > 1.25f || wallOverlapFrames > 2)) penetrated = true;
                    }
                    if (scenario == "ceiling" && collider.bounds.max.y > origin.y + 1.12f) penetrated = true;
                    if (scenario == "one-way" && move.IsGrounded && collider.bounds.min.y > origin.y + .75f) landedOnPlatform = true;
                    trace.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:F5},{2:F5},{3:F5},{4:F5},{5}\n", step,
                        body.position.x-origin.x, body.position.y-origin.y, body.linearVelocity.x, body.linearVelocity.y, move.IsGrounded);
                    Set(move, "_wasGroundedLastFixedUpdate", move.IsGrounded);
                }
                Directory.CreateDirectory("Logs/CornerReview");
                File.WriteAllText($"Logs/CornerReview/{scenario}-{speed}-{direction}.csv", trace.ToString());
                Assert.That(penetrated, Is.False, "Solid geometry must continue to block the body.");
                if (scenario == "ceiling") Assert.That(peak, Is.LessThan(.8f), "A real ceiling hit should stop ascent.");
                else Assert.That(peak, Is.GreaterThan(1.7f), "Side contact or a clear corner must not cancel the jump.");
                if (scenario == "one-way") Assert.That(landedOnPlatform, Is.True, "Pass upward, then land on the one-way top.");
                else Assert.That(body.position.y, Is.LessThan(origin.y - 1), "The body must fall away rather than cling to the wall/corner.");
            }
            finally { Object.DestroyImmediate(root); Physics2D.simulationMode = oldMode; }
        }
    }
}
