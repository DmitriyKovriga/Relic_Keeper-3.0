using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public class ItemTooltipScreenFitTests
    {
        private sealed class TestWindow : EditorWindow { }
        private TestWindow _window;
        private GameObject _owner;
        private ItemTooltipController _controller;

        [TearDown]
        public void TearDown()
        {
            if (_owner != null) Object.DestroyImmediate(_owner);
            if (_window != null) _window.Close();
        }

        [TestCase(150f, 100f, 480f, 270f, 1f)]
        [TestCase(150f, 524f, 480f, 270f, 0.5f)]
        [TestCase(944f, 100f, 480f, 270f, 0.5f)]
        [TestCase(944f, 1048f, 480f, 270f, 0.25f)]
        [TestCase(150f, 524f, 320f, 200f, 192f / 524f)]
        public void FitUsesBothScreenDimensionsWithoutEnlarging(float width, float height,
            float screenWidth, float screenHeight, float expected)
        {
            Assert.That(ItemTooltipController.CalculateTooltipFitScale(new Vector2(width, height),
                new Vector2(screenWidth, screenHeight), 4f), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void MissingLayoutDoesNotProduceInvalidTransform()
        {
            Assert.That(ItemTooltipController.CalculateTooltipFitScale(new Vector2(float.NaN, 200f),
                new Vector2(480f, 270f), 4f), Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ActualItemAndComparisonBoundsFitAndReturnToNormalSize()
        {
            _window = ScriptableObject.CreateInstance<TestWindow>();
            var root = new VisualElement();
            root.style.width = 480f;
            root.style.height = 270f;
            _window.rootVisualElement.Add(root);
            _window.Show();
            var anchor = Box(root, 24f, 24f);
            anchor.style.left = 450f;
            anchor.style.top = 240f;
            var item = Box(root, 150f, 600f);
            item.Add(new Label("Long item description / Длинное описание предмета"));
            var comparison = Box(root, 150f, 900f);
            comparison.Add(new Label("Comparison / Сравнение"));
            comparison.style.display = DisplayStyle.None;
            _owner = new GameObject("TooltipFitTest");
            _owner.SetActive(false);
            _controller = _owner.AddComponent<ItemTooltipController>();
            Set("_root", root);
            Set("_targetAnchorSlot", anchor);
            Set("_currentTargetItem", new InventoryItem(null));
            Set("_itemTooltipBox", item);
            Set("_compareTooltipBox", comparison);

            yield return WaitForLayout(item);
            Reposition();
            yield return null;
            AssertFits(item, root);
            Assert.That(item.transform.scale.x, Is.LessThan(1f));

            comparison.style.display = DisplayStyle.Flex;
            Set("_compareActive", true);
            yield return WaitForLayout(comparison);
            Reposition();
            yield return null;
            AssertFits(item, root);
            AssertFits(comparison, root);
            Assert.That(item.transform.scale.x, Is.EqualTo(comparison.transform.scale.x));

            Set("_compareActive", false);
            comparison.style.display = DisplayStyle.None;
            item.style.height = 100f;
            yield return null;
            Reposition();
            yield return null;
            AssertFits(item, root);
            Assert.That(item.transform.scale.x, Is.EqualTo(1f));

            item.style.width = 1000f;
            yield return null;
            Reposition();
            yield return null;
            AssertFits(item, root);

            root.style.width = 200f;
            root.style.height = 120f;
            yield return null;
            Reposition();
            yield return null;
            AssertFits(item, root);
        }

        private static VisualElement Box(VisualElement root, float width, float height)
        {
            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.width = width;
            box.style.height = height;
            box.style.display = DisplayStyle.Flex;
            root.Add(box);
            return box;
        }

        private static IEnumerator WaitForLayout(VisualElement element)
        {
            for (int i = 0; i < 30 && (float.IsNaN(element.layout.width) || element.layout.width <= 0f); i++)
                yield return null;
            yield return null;
            Assert.That(element.layout.width, Is.GreaterThan(0f));
        }

        private static void AssertFits(VisualElement box, VisualElement root)
        {
            var min = root.WorldToLocal(box.worldBound.min);
            var max = root.WorldToLocal(box.worldBound.max);
            Assert.That(min.x, Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(min.y, Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(max.x, Is.LessThanOrEqualTo(root.layout.width - 3.99f));
            Assert.That(max.y, Is.LessThanOrEqualTo(root.layout.height - 3.99f));
        }

        private void Set(string field, object value) => typeof(ItemTooltipController)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, value);
        private void Reposition() => typeof(ItemTooltipController)
            .GetMethod("RecalculatePosition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, null);
    }
}
