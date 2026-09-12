using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Items.World;
using Scripts.Visuals;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

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
        public void GroundPlateMatchesTooltipRarityBands()
        {
            WorldDroppedItem magicDrop = CreateDroppedItem(Vector2.zero, affixCount: 3);
            WorldDroppedItem rareDrop = CreateDroppedItem(new Vector2(2f, 0f), affixCount: 4);

            SpriteRenderer magicCircle = magicDrop.GetComponent<SpriteRenderer>();
            SpriteRenderer rareCircle = rareDrop.GetComponent<SpriteRenderer>();

            Assert.That(ItemRarity.IsMagic(magicDrop.Item), Is.True);
            Assert.That(ItemRarity.IsRare(magicDrop.Item), Is.False);
            Assert.That(magicCircle.color, Is.EqualTo(ItemRarity.MagicPlate));
            Assert.That(ItemRarity.GetTooltipTitleColor(magicDrop.Item), Is.EqualTo(ItemRarity.MagicTitle));

            Assert.That(ItemRarity.IsRare(rareDrop.Item), Is.True);
            Assert.That(rareCircle.color, Is.EqualTo(ItemRarity.RarePlate));
            Assert.That(ItemRarity.GetTooltipTitleColor(rareDrop.Item), Is.EqualTo(ItemRarity.RareTitle));
        }

        [Test]
        public void DropVisualUsesLayerAboveWorldAndInspectionProgressIsRadial()
        {
            WorldDroppedItem droppedItem = CreateDroppedItem(Vector2.zero);

            SpriteRenderer circle = droppedItem.GetComponent<SpriteRenderer>();
            SpriteRenderer icon = droppedItem.transform.Find("Icon").GetComponent<SpriteRenderer>();
            Canvas progressCanvas = droppedItem.transform.Find("InspectionProgress").GetComponent<Canvas>();
            UnityEngine.UI.Image progressImage = progressCanvas.GetComponent<UnityEngine.UI.Image>();

            Assert.That(circle.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerVfx));
            Assert.That(icon.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerVfx));
            Assert.That(progressCanvas.sortingLayerName, Is.EqualTo(WorldRenderSorting.LayerVfx));
            Assert.That(progressCanvas.enabled, Is.False);

            droppedItem.SetInspectionProgress(0.25f, true);
            Assert.That(progressCanvas.enabled, Is.True);
            Assert.That(progressImage.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
            Assert.That(progressImage.fillMethod, Is.EqualTo(UnityEngine.UI.Image.FillMethod.Radial360));
            Assert.That(progressImage.fillAmount, Is.EqualTo(0.75f).Within(0.001f));

            droppedItem.SetInspectionProgress(1f, true);
            Assert.That(progressCanvas.enabled, Is.False);
        }

        [Test]
        public void InventoryFullNotice_FitsPixelCanvasAndKeepsPickupMeaning()
        {
            Assert.That(PlayerNoticeBanner.InventoryFullKey, Is.EqualTo("inventory.ui.pickupNoSpace"));
            Assert.That(PlayerNoticeBanner.ToastMaxWidth + PlayerNoticeBanner.TopPixels, Is.LessThanOrEqualTo(480));
            Assert.That(PlayerNoticeBanner.TopPixels, Is.LessThan(270));
            Assert.That(PlayerNoticeBanner.ToastMaxWidth, Is.LessThanOrEqualTo(480));

            VisualElement toast = PlayerNoticeBanner.CreateToast(PlayerNoticeBanner.InventoryFullFallback, out Label label);
            Assert.That(toast.style.top.value.value, Is.EqualTo(PlayerNoticeBanner.TopPixels));
            Assert.That(label.style.maxWidth.value.value, Is.EqualTo(PlayerNoticeBanner.ToastMaxWidth));
            Assert.That(label.style.fontSize.value.value, Is.EqualTo(PlayerNoticeBanner.FontSize));
            Assert.That(label.text, Does.Contain("inventory is full"));
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

        [Test]
        public void CombatInspectDelay_IsTwoSecondsUntilRoomIsCleared()
        {
            Assert.That(WorldItemInspection.ResolveTooltipDelay(0.5f, false), Is.EqualTo(2f));
            Assert.That(WorldItemInspection.ResolveTooltipDelay(0.5f, true), Is.EqualTo(0.5f));
        }

        [Test]
        public void InspectSource_PrefersMovingCursorThenMovingPlayer()
        {
            Assert.That(
                WorldItemInspection.ResolveSource(true, true, true, true),
                Is.EqualTo(WorldItemInspectSource.Cursor));
            Assert.That(
                WorldItemInspection.ResolveSource(false, true, true, true),
                Is.EqualTo(WorldItemInspectSource.Player));
            Assert.That(
                WorldItemInspection.ResolveSource(false, false, true, true),
                Is.EqualTo(WorldItemInspectSource.Cursor));
            Assert.That(
                WorldItemInspection.ResolveSource(false, true, true, false),
                Is.EqualTo(WorldItemInspectSource.Cursor));
            Assert.That(
                WorldItemInspection.ResolveSource(true, false, false, true),
                Is.EqualTo(WorldItemInspectSource.Player));
        }

        [Test]
        public void CursorClickPickup_OnlyWhenInspectingThatItemWithCursor()
        {
            WorldDroppedItem inspected = CreateDroppedItem(Vector2.zero);
            WorldDroppedItem other = CreateDroppedItem(new Vector2(1f, 0f));

            Assert.That(
                WorldItemInspection.CanPickupWithCursorClick(WorldItemInspectSource.Cursor, inspected, inspected),
                Is.True);
            Assert.That(
                WorldItemInspection.CanPickupWithCursorClick(WorldItemInspectSource.Player, inspected, inspected),
                Is.False);
            Assert.That(
                WorldItemInspection.CanPickupWithCursorClick(WorldItemInspectSource.Cursor, inspected, other),
                Is.False);
            Assert.That(
                WorldItemInspection.CanPickupWithCursorClick(WorldItemInspectSource.None, inspected, inspected),
                Is.False);
        }

        [Test]
        public void CursorMoveLinger_CountsRecentMotion()
        {
            Assert.That(WorldItemInspection.IsCursorMoving(2f, 0f, 0f), Is.True);
            Assert.That(WorldItemInspection.IsCursorMoving(0f, 1f, 1.05f), Is.True);
            Assert.That(WorldItemInspection.IsCursorMoving(0f, 1f, 1.2f), Is.False);
        }

        [Test]
        public void PairOffsets_UnstackItemsHorizontally()
        {
            WorldDroppedItemSpread.GetPairOffsets(Vector2.zero, Vector2.zero, 0.86f, out float left, out float right);
            Assert.That(right - left, Is.EqualTo(0.86f).Within(0.001f));
            Assert.That(left, Is.LessThan(0f));
            Assert.That(right, Is.GreaterThan(0f));
        }

        [Test]
        public void SpawnedItems_SpreadApartOnTheGround()
        {
            WorldDroppedItem first = WorldItemDropService.Spawn(CreateInventoryItem(), Vector2.zero);
            WorldDroppedItem second = WorldItemDropService.Spawn(CreateInventoryItem(), Vector2.zero);
            TrackSpawnedDrop(first);
            TrackSpawnedDrop(second);

            Assert.That(Mathf.Abs(first.GroundPosition.x - second.GroundPosition.x),
                Is.GreaterThanOrEqualTo(WorldDroppedItemSpread.MinSeparation - 0.02f));
        }

        [Test]
        public void Spread_DoesNotPushNeighborThroughWall()
        {
            GameObject wall = CreateGameObject("SpreadWall");
            wall.layer = 6;
            wall.transform.position = new Vector3(0.45f, 0.5f, 0f);
            BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.2f, 3f);
            Physics2D.SyncTransforms();

            WorldDroppedItem left = CreateDroppedItem(Vector2.zero);
            WorldDroppedItem right = CreateDroppedItem(new Vector2(0.05f, 0f));
            WorldDroppedItemSpread.SeparateFromNeighbors(right);

            float wallLeft = box.bounds.min.x;
            Assert.That(left.GroundPosition.x, Is.LessThan(wallLeft - 0.02f));
            Assert.That(right.GroundPosition.x, Is.LessThan(wallLeft - 0.02f));
            Assert.That(Mathf.Abs(left.GroundPosition.x - right.GroundPosition.x),
                Is.GreaterThanOrEqualTo(0.4f));
        }

        [Test]
        public void EmptyRoom_IsClearedBeforeEnemiesSpawn()
        {
            GameObject roomObject = CreateGameObject("EmptyRoom");
            RoomController room = roomObject.AddComponent<RoomController>();
            Assert.That(room.IsCleared, Is.True);
        }

        private void TrackSpawnedDrop(WorldDroppedItem dropped)
        {
            if (dropped != null)
                _createdObjects.Add(dropped.gameObject);

            GameObject dropRoot = GameObject.Find("WorldDroppedItems");
            if (dropRoot != null && !_createdObjects.Contains(dropRoot))
                _createdObjects.Add(dropRoot);
        }

        private InventoryItem CreateInventoryItem()
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            data.ID = $"drop_{_createdObjects.Count}";
            _createdObjects.Add(data);
            return new InventoryItem(data);
        }

        private WorldDroppedItem CreateDroppedItem(Vector2 position, int affixCount = 0)
        {
            ArmorItemSO data = ScriptableObject.CreateInstance<ArmorItemSO>();
            data.ID = $"test_{_createdObjects.Count}";
            _createdObjects.Add(data);

            InventoryItem item = new InventoryItem(data);
            for (int i = 0; i < affixCount; i++)
            {
                ItemAffixSO affix = ScriptableObject.CreateInstance<ItemAffixSO>();
                _createdObjects.Add(affix);
                item.Affixes.Add(new AffixInstance(affix, item));
            }

            GameObject dropObject = CreateGameObject("WorldDroppedItem");
            dropObject.transform.position = position;
            WorldDroppedItem droppedItem = dropObject.AddComponent<WorldDroppedItem>();
            droppedItem.Initialize(item, 24f);
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
