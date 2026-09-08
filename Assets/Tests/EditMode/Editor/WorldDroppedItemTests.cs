using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.World;
using Scripts.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace RelicKeeper.Tests.EditMode
{
    public class WorldDroppedItemTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void DropVisualUsesLayerAboveWorldAndInspectionProgressIsRadial()
        {
            WorldDroppedItem droppedItem = CreateDroppedItem(Vector2.zero);

            SpriteRenderer circle = droppedItem.GetComponent<SpriteRenderer>();
            SpriteRenderer icon = droppedItem.transform.Find("Icon").GetComponent<SpriteRenderer>();
            Canvas progressCanvas = droppedItem.transform.Find("InspectionProgress").GetComponent<Canvas>();
            Image progressImage = progressCanvas.GetComponent<Image>();

            Assert.That(circle.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerVfx));
            Assert.That(icon.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerVfx));
            Assert.That(progressCanvas.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerVfx));
            Assert.That(progressCanvas.enabled, Is.False);

            droppedItem.SetInspectionProgress(0.25f, true);
            Assert.That(progressCanvas.enabled, Is.True);
            Assert.That(progressImage.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(progressImage.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
            Assert.That(progressImage.fillAmount, Is.EqualTo(0.75f).Within(0.001f));

            droppedItem.SetInspectionProgress(1f, true);
            Assert.That(progressCanvas.enabled, Is.False);
        }

        [Test]
        public void InteractionSelectsNearestDroppedItem()
        {
            GameObject player = CreateGameObject("Player");
            PlayerInteractController controller = player.AddComponent<PlayerInteractController>();
            SetPrivateField(controller, "_interactRadius", 4f);

            WorldDroppedItem farther = CreateDroppedItem(new Vector2(1.5f, 0f));
            WorldDroppedItem nearer = CreateDroppedItem(new Vector2(0.5f, 0f));
            Physics2D.SyncTransforms();

            MethodInfo findMethod = typeof(PlayerInteractController).GetMethod(
                "FindNearbyInteractable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var selected = findMethod.Invoke(controller, null) as IInteractable;

            Assert.That(selected, Is.SameAs(nearer));
            Assert.That(selected, Is.Not.SameAs(farther));
        }

        [Test]
        public void TryDropAtPlayer_SpawnsNearPlayerEvenIfCursorWouldBeElsewhere()
        {
            GameObject player = CreateGameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(6f, 2f, 0f);

            Assert.IsTrue(WorldItemDropService.TryDropAtPlayer(CreateInventoryItem()));

            WorldDroppedItem spawned = Object.FindFirstObjectByType<WorldDroppedItem>();
            Assert.That(spawned, Is.Not.Null);
            _createdObjects.Add(spawned.gameObject);

            GameObject dropRoot = GameObject.Find("WorldDroppedItems");
            if (dropRoot != null)
                _createdObjects.Add(dropRoot);

            Assert.That(spawned.transform.position.x, Is.EqualTo(6f).Within(0.01f));
            Assert.That(spawned.transform.position.y, Is.EqualTo(2.62f).Within(0.01f));
        }

        [Test]
        public void ProjectToGroundUnder_WithoutCollider_LiftsFromOrigin()
        {
            MethodInfo method = typeof(WorldItemDropService).GetMethod(
                "ProjectToGroundUnder",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Vector2 origin = new Vector2(1.5f, 4f);
            Vector2 result = (Vector2)method.Invoke(null, new object[] { origin });
            Assert.That(result, Is.EqualTo(origin + Vector2.up * 0.62f));
        }

        private InventoryItem CreateInventoryItem()
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            data.ID = $"drop_{_createdObjects.Count}";
            _createdObjects.Add(data);
            return new InventoryItem(data);
        }

        private WorldDroppedItem CreateDroppedItem(Vector2 position)
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            data.ID = $"test_{_createdObjects.Count}";
            _createdObjects.Add(data);

            GameObject dropObject = CreateGameObject("WorldDroppedItem");
            dropObject.transform.position = position;
            WorldDroppedItem droppedItem = dropObject.AddComponent<WorldDroppedItem>();
            droppedItem.Initialize(new InventoryItem(data), 24f);
            return droppedItem;
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found.");
            field.SetValue(target, value);
        }
    }
}
