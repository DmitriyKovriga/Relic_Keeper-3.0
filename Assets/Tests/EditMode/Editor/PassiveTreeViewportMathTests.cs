using NUnit.Framework;
using Scripts.Skills.PassiveTree.UI;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveTreeViewportMathTests
    {
        [Test]
        public void ZoomToward_KeepsTheSameTreePointUnderTheCursor()
        {
            Vector2 content = new Vector2(-80f, 40f);
            Vector2 mouse = new Vector2(140f, 90f);
            const float oldZoom = 1f;
            const float newZoom = 1.1f;

            Vector2 treePoint = (mouse - content) / oldZoom;
            Vector2 zoomed = PassiveTreeViewportMath.ZoomToward(content, oldZoom, newZoom, mouse);
            Vector2 treePointAfter = (mouse - zoomed) / newZoom;

            Assert.That(treePointAfter.x, Is.EqualTo(treePoint.x).Within(0.001f));
            Assert.That(treePointAfter.y, Is.EqualTo(treePoint.y).Within(0.001f));
        }

        [Test]
        public void ZoomWhilePanning_WithoutResync_SnapsBackToPreZoomOrigin()
        {
            Vector2 content = new Vector2(12f, -30f);
            Vector2 mouse = new Vector2(100f, 80f);
            Vector2 zoomed = PassiveTreeViewportMath.ZoomToward(content, 1f, 1.1f, mouse);

            Vector2 snappedBack = PassiveTreeViewportMath.Pan(content, mouse, mouse);

            Assert.That(snappedBack, Is.EqualTo(content));
            Assert.That(snappedBack, Is.Not.EqualTo(zoomed));
        }

        [Test]
        public void ZoomWhilePanning_WithResync_KeepsZoomedPositionOnZeroDeltaPan()
        {
            Vector2 content = new Vector2(12f, -30f);
            Vector2 mouse = new Vector2(100f, 80f);
            Vector2 zoomed = PassiveTreeViewportMath.ZoomToward(content, 1f, 1.1f, mouse);

            Vector2 afterPan = PassiveTreeViewportMath.Pan(zoomed, mouse, mouse);

            Assert.That(afterPan.x, Is.EqualTo(zoomed.x).Within(0.001f));
            Assert.That(afterPan.y, Is.EqualTo(zoomed.y).Within(0.001f));
        }

        [Test]
        public void StepZoom_ScrollUpZoomsIn()
        {
            float zoomed = PassiveTreeViewportMath.StepZoom(1f, -1f, 0.1f, 0.3f, 2f);
            Assert.That(zoomed, Is.EqualTo(1.1f).Within(0.0001f));
        }
    }
}
