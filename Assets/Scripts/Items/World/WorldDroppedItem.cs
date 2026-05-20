using Scripts.Dungeon;
using Scripts.Inventory;
using UnityEngine;

namespace Scripts.Items.World
{
    [DisallowMultipleComponent]
    public sealed class WorldDroppedItem : MonoBehaviour, IInteractable
    {
        private const int CirclePixels = 18;
        private const int SortingOrder = 130;

        private static Sprite _circleSprite;

        private InventoryItem _item;
        private float _pixelsPerUnit = 24f;
        private SpriteRenderer _circleRenderer;
        private SpriteRenderer _iconRenderer;

        public InventoryItem Item => _item;

        public void Initialize(InventoryItem item, float pixelsPerUnit)
        {
            _item = item;
            _pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            BuildVisual();
            BuildCollider();
        }

        public string GetPrompt()
        {
            return _item?.Data != null ? $"Pick up {_item.Data.ItemName}" : "Pick up";
        }

        public bool CanInteract()
        {
            return _item?.Data != null;
        }

        public void Interact()
        {
            if (_item?.Data == null)
                return;

            if (InventoryManager.Instance == null)
            {
                Debug.LogWarning("[WorldDroppedItem] Cannot pick up item: InventoryManager is missing.");
                return;
            }

            if (!InventoryManager.Instance.AddItem(_item))
            {
                Debug.Log($"[WorldDroppedItem] Inventory is full. Could not pick up '{_item.Data.ItemName}'.");
                return;
            }

            _item = null;
            Destroy(gameObject);
        }

        private void BuildVisual()
        {
            _circleRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (_circleRenderer == null)
                _circleRenderer = gameObject.AddComponent<SpriteRenderer>();

            _circleRenderer.sprite = GetCircleSprite();
            _circleRenderer.color = GetRarityColor(_item);
            _circleRenderer.sortingOrder = SortingOrder;

            Transform iconTransform = transform.Find("Icon");
            if (iconTransform == null)
            {
                var iconGo = new GameObject("Icon");
                iconTransform = iconGo.transform;
                iconTransform.SetParent(transform, false);
            }

            _iconRenderer = iconTransform.GetComponent<SpriteRenderer>();
            if (_iconRenderer == null)
                _iconRenderer = iconTransform.gameObject.AddComponent<SpriteRenderer>();

            _iconRenderer.sprite = _item?.Data != null ? _item.Data.Icon : null;
            _iconRenderer.color = Color.white;
            _iconRenderer.sortingOrder = SortingOrder + 1;
            _iconRenderer.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            FitIconInsideCircle();
        }

        private void BuildCollider()
        {
            var collider = gameObject.GetComponent<CircleCollider2D>();
            if (collider == null)
                collider = gameObject.AddComponent<CircleCollider2D>();

            collider.isTrigger = true;
            collider.radius = (CirclePixels / _pixelsPerUnit) * 0.65f;
        }

        private void FitIconInsideCircle()
        {
            if (_iconRenderer == null || _iconRenderer.sprite == null)
                return;

            _iconRenderer.transform.localScale = Vector3.one;
            Bounds bounds = _iconRenderer.bounds;
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y);
            if (maxSize <= 0f)
                return;

            float targetSize = (CirclePixels - 4f) / _pixelsPerUnit;
            float scale = targetSize / maxSize;
            _iconRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static Color GetRarityColor(InventoryItem item)
        {
            int affixCount = item?.Affixes?.Count ?? 0;
            if (affixCount >= 3)
                return new Color(1f, 0.82f, 0.22f, 0.95f);
            if (affixCount > 0)
                return new Color(0.25f, 0.48f, 1f, 0.95f);
            return new Color(0.72f, 0.72f, 0.72f, 0.9f);
        }

        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null)
                return _circleSprite;

            var texture = new Texture2D(CirclePixels, CirclePixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector2 center = new Vector2((CirclePixels - 1) * 0.5f, (CirclePixels - 1) * 0.5f);
            float radius = CirclePixels * 0.5f;
            for (int y = 0; y < CirclePixels; y++)
            {
                for (int x = 0; x < CirclePixels; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
                }
            }
            texture.Apply(false, true);

            _circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, CirclePixels, CirclePixels),
                new Vector2(0.5f, 0.5f),
                24f);
            _circleSprite.name = "RuntimeDroppedItemCircle";
            _circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return _circleSprite;
        }
    }
}
