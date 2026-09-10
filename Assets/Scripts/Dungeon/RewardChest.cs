using Scripts.Enemies;
using Scripts.Inventory;
using Scripts.Items.World;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Dungeon
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class RewardChest : MonoBehaviour, IInteractable
    {
        private const string PrefabResourcePath = "Prefabs/Dungeon/RewardChest";
        private const int PlaceholderPixels = 24;
        private static Sprite _placeholderSprite;

        [SerializeField, Min(1)] private int _minimumDrops = 1;
        [SerializeField, Min(1)] private int _maximumDrops = 5;

        private int _itemLevel = 1;
        private bool _opened;

        public static RewardChest Spawn(Vector3 position, int itemLevel, Transform parent)
        {
            RewardChest prefab = Resources.Load<RewardChest>(PrefabResourcePath);
            RewardChest chest;
            if (prefab != null)
                chest = Instantiate(prefab, position, Quaternion.identity, parent);
            else
            {
                var host = new GameObject("RewardChest_Placeholder");
                host.transform.SetParent(parent, true);
                host.transform.position = position;
                chest = host.AddComponent<RewardChest>();
            }

            chest.Initialize(itemLevel);
            return chest;
        }

        public void Initialize(int itemLevel)
        {
            _itemLevel = Mathf.Max(1, itemLevel);
            name = $"Reward Chest (Level {_itemLevel})";
        }

        private void Awake()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = Vector2.one;
            EnsurePlaceholderVisual();
        }

        public string GetPrompt() => "Открыть сундук";
        public bool CanInteract() => !_opened;

        public void Interact()
        {
            if (_opened)
                return;

            _opened = true;
            int count = Random.Range(Mathf.Max(1, _minimumDrops), Mathf.Max(_minimumDrops, _maximumDrops) + 1);
            for (int i = 0; i < count; i++)
            {
                InventoryItem item = EnemyLootDropService.CreateGuaranteedItem(_itemLevel, Random.value);
                if (item == null)
                    continue;

                float centeredIndex = i - (count - 1) * 0.5f;
                Vector2 position = (Vector2)transform.position + new Vector2(centeredIndex * 0.32f, 0f);
                WorldItemDropService.SpawnOnGround(item, position);
            }

            Destroy(gameObject);
        }

        private void EnsurePlaceholderVisual()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = gameObject.AddComponent<SpriteRenderer>();

            if (renderer.sprite == null)
                renderer.sprite = GetPlaceholderSprite();
            renderer.color = Color.white;
            WorldRenderSorting.ConfigureOneShotRenderer(
                renderer,
                RenderDepthCategory.GameplayVfx,
                transform.position.y);
        }

        private static Sprite GetPlaceholderSprite()
        {
            if (_placeholderSprite != null)
                return _placeholderSprite;

            var texture = new Texture2D(PlaceholderPixels, PlaceholderPixels, TextureFormat.RGBA32, false)
            {
                name = "RewardChest_WhitePlaceholder_24px",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[PlaceholderPixels * PlaceholderPixels];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply(false, true);

            _placeholderSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, PlaceholderPixels, PlaceholderPixels),
                new Vector2(0.5f, 0.5f),
                PlaceholderPixels);
            _placeholderSprite.name = "RewardChest_WhitePlaceholder_24px";
            _placeholderSprite.hideFlags = HideFlags.HideAndDontSave;
            return _placeholderSprite;
        }
    }
}
