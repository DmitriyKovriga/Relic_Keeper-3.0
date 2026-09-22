using NUnit.Framework;
using Scripts.Skills.PassiveTree;
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

        [Test]
        public void ClampPan_LargeTreeCannotBeDraggedPastViewportEdges()
        {
            Rect bounds = new Rect(0f, 0f, 1000f, 800f);
            Vector2 viewport = new Vector2(480f, 270f);

            Vector2 tooFarBottomRight = PassiveTreeViewportMath.ClampPan(
                new Vector2(500f, 500f), bounds, 1f, viewport, 48f);
            Vector2 tooFarTopLeft = PassiveTreeViewportMath.ClampPan(
                new Vector2(-1000f, -1000f), bounds, 1f, viewport, 48f);

            Assert.That(tooFarBottomRight, Is.EqualTo(new Vector2(48f, 48f)));
            Assert.That(tooFarTopLeft, Is.EqualTo(new Vector2(-568f, -578f)));
        }

        [Test]
        public void ClampPan_TreeSmallerThanViewportStaysCentered()
        {
            Vector2 clamped = PassiveTreeViewportMath.ClampPan(
                new Vector2(900f, -700f),
                new Rect(100f, 100f, 200f, 100f),
                1f,
                new Vector2(480f, 270f),
                48f);

            Assert.That(clamped, Is.EqualTo(new Vector2(40f, -15f)));
        }

        [Test]
        public void StepZoom_AllowsPassiveTreeOverviewScale()
        {
            float zoomed = 0.3f;
            for (int i = 0; i < 20; i++)
                zoomed = PassiveTreeViewportMath.StepZoom(zoomed, 1f, 0.1f, 0.14f, 2f);

            Assert.That(zoomed, Is.EqualTo(0.14f).Within(0.0001f));
        }

        [Test]
        public void MageTree_FitsInsideReferenceViewportAtOverviewZoom()
        {
            PassiveSkillTreeSO tree = Resources.Load<PassiveSkillTreeSO>(
                "PassiveTrees/MagePassiveSkillTree/MagePassiveSkillTree");
            Assert.That(tree, Is.Not.Null);

            Rect bounds = tree.GetTreeContentBounds(80f);
            float zoom = PassiveTreeViewportMath.CalculateFitZoom(
                bounds,
                new Vector2(480f, 270f),
                padding: 40f,
                minZoom: 0.14f,
                maxZoom: 2f);

            Assert.That(bounds.width * zoom, Is.LessThanOrEqualTo(400.01f));
            Assert.That(bounds.height * zoom, Is.LessThanOrEqualTo(190.01f));
        }
    }
}
