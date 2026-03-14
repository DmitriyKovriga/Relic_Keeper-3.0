using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public class InventoryUIStashSnapTests
    {
        private readonly List<GameObject> _createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdGameObjects)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }

            _createdGameObjects.Clear();
        }

        [Test]
        public void GetSmartStashTargetIndex_ForBottomLeftCell_ReturnsLastRowFirstColumn()
        {
            var uiGo = new GameObject("inventory-ui-stash-snap-test");
            _createdGameObjects.Add(uiGo);

            var ui = uiGo.AddComponent<InventoryUI>();
            var stashGrid = new VisualElement();

            SetField(ui, "_stashGridContainer", stashGrid);
            SetField(ui, "_stashSlotSize", 19f);

            float slotSize = 19f;
            Vector2 dropCenter = new Vector2(slotSize * 0.5f, (StashManager.STASH_ROWS - 1) * slotSize + slotSize * 0.5f);

            int targetIndex = (int)InvokePrivate(
                ui,
                "GetSmartStashTargetIndex",
                dropCenter,
                1,
                1);

            Assert.AreEqual((StashManager.STASH_ROWS - 1) * StashManager.STASH_COLS, targetIndex);
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException(target.GetType().Name, methodName);

            return method.Invoke(target, args);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(target.GetType().Name, fieldName);

            field.SetValue(target, value);
        }
    }
}
