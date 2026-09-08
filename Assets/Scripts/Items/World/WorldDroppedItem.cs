using Scripts.Dungeon;
using Scripts.Inventory;
using Scripts.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace Scripts.Items.World
{
    [DisallowMultipleComponent]
    public sealed class WorldDroppedItem : MonoBehaviour, IInteractable
    {
        private const int CirclePixels = 18;
        private const int SortingOrder = 32000;
        private const float HoverAmplitude = 0.08f;
        private const float HoverCyclesPerSecond = 0.8f;

        private static Sprite _circleSprite;

        private InventoryItem _item;
        private float _pixelsPerUnit = 24f;
        private SpriteRenderer _circleRenderer;
        private SpriteRenderer _iconRenderer;
        private Canvas _inspectionCanvas;
        private Image _inspectionOverlay;
        private float _hoverBaseLocalY;
        private float _hoverPhase;
        private bool _isInitialized;

        public InventoryItem Item => _item;
        public Vector3 TooltipWorldPosition => transform.position + Vector3.up * 0.48f;

        public void Initialize(InventoryItem item, float pixelsPerUnit)
        {
            _item = item;
            _pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            _hoverBaseLocalY = transform.localPosition.y;
            _hoverPhase = Mathf.Abs(GetInstanceID() % 1000) * 0.013f;
            BuildVisual();
            BuildCollider();
            BuildInspectionProgress();
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized)
                return;

            Vector3 localPosition = transform.localPosition;
            localPosition.y = _hoverBaseLocalY + Mathf.Sin((Time.time + _hoverPhase) * Mathf.PI * 2f * HoverCyclesPerSecond) * HoverAmplitude;
            transform.localPosition = localPosition;
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
            ItemTooltipController.Instance?.HideWorldTooltip(this);
            Destroy(gameObject);
        }

        public void SetInspectionProgress(float normalizedProgress, bool visible)
        {
            if (_inspectionOverlay == null)
                return;

            float progress = Mathf.Clamp01(normalizedProgress);
            _inspectionCanvas.enabled = visible && progress < 0.999f;
            _inspectionOverlay.fillAmount = 1f - progress;
        }

        private void BuildVisual()
        {
            _circleRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (_circleRenderer == null)
                _circleRenderer = gameObject.AddComponent<SpriteRenderer>();

            _circleRenderer.sprite = GetCircleSprite();
            _circleRenderer.color = GetRarityColor(_item);
            _circleRenderer.sortingLayerName = WorldRenderSorting.LayerVfx;
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
            _iconRenderer.sortingLayerName = WorldRenderSorting.LayerVfx;
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

        private void BuildInspectionProgress()
        {
            Transform existing = transform.Find("InspectionProgress");
            GameObject progressObject = existing != null
                ? existing.gameObject
                : new GameObject("InspectionProgress", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image));
            progressObject.transform.SetParent(transform, false);

            RectTransform rect = progressObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(CirclePixels, CirclePixels);
            rect.localPosition = new Vector3(0f, 0f, -0.02f);
            rect.localScale = Vector3.one / _pixelsPerUnit;

            _inspectionCanvas = progressObject.GetComponent<Canvas>();
            _inspectionCanvas.renderMode = RenderMode.WorldSpace;
            _inspectionCanvas.overrideSorting = true;
            _inspectionCanvas.sortingLayerName = WorldRenderSorting.LayerVfx;
            _inspectionCanvas.sortingOrder = SortingOrder + 2;

            _inspectionOverlay = progressObject.GetComponent<Image>();
            _inspectionOverlay.sprite = GetCircleSprite();
            _inspectionOverlay.color = new Color(0.05f, 0.05f, 0.05f, 0.68f);
            _inspectionOverlay.raycastTarget = false;
            _inspectionOverlay.type = Image.Type.Filled;
            _inspectionOverlay.fillMethod = Image.FillMethod.Radial360;
            _inspectionOverlay.fillOrigin = 2;
            _inspectionOverlay.fillClockwise = true;
            _inspectionOverlay.fillAmount = 1f;
            _inspectionCanvas.enabled = false;
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
            if (affixCount >= 4)
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
