using NUnit.Framework;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PickupFlightTests
    {
        [Test]
        public void Homing_TravelsSimilarDistanceAtThirtyAndThreeHundredFps()
        {
            float distanceAt30 = HomingDistanceAfterSeconds(1f / 30f, 0.5f);
            float distanceAt300 = HomingDistanceAfterSeconds(1f / 300f, 0.5f);

            Assert.That(distanceAt300, Is.EqualTo(distanceAt30).Within(0.15f));
            Assert.That(distanceAt300, Is.GreaterThan(2f));
        }

        [Test]
        public void Homing_DoesNotStallWhenStepIsSmallerThanPixelGrid()
        {
            Vector3 position = Vector3.zero;
            Vector2 velocity = Vector2.zero;
            Vector3 snapped = PickupFlight.SnapToPixelGrid(position);

            PickupFlight.IntegrateHoming(
                ref position,
                ref velocity,
                new Vector3(4f, 0f, 0f),
                7.2f,
                13.5f,
                18f,
                1f / 300f);

            Assert.That(position.x, Is.GreaterThan(0f));
            Assert.That(PickupFlight.SnapToPixelGrid(position), Is.EqualTo(snapped));
        }

        private static float HomingDistanceAfterSeconds(float dt, float duration)
        {
            Vector3 position = Vector3.zero;
            Vector2 velocity = Vector2.zero;
            Vector3 target = new Vector3(20f, 0f, 0f);
            int steps = Mathf.Max(1, Mathf.RoundToInt(duration / dt));
            for (int i = 0; i < steps; i++)
            {
                PickupFlight.IntegrateHoming(ref position, ref velocity, target, 7.2f, 13.5f, 18f, dt);
            }

            return position.magnitude;
        }
    }
}
