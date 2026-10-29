using Scripts.Visuals;
using Scripts.Inventory;
using Scripts.Items;
using System;
using UnityEngine;

namespace Scripts.Items.World
{
    /// <summary>Cosmetic rarity reveal above the drop, followed by travel to already-stored loot.</summary>
    public sealed class WorldLootCacheFlightVisual : MonoBehaviour
    {
        public const float RiseDuration = .38f;
        public const float HoverDuration = .42f;
        public const float RevealDuration = .16f;
        public const float RiseHeight = 2.8f;
        public const float MinimumTravelDuration = .42f;
        public const float RevealStart = RiseDuration + HoverDuration;
        public const float TravelStart = RevealStart + RevealDuration;
        public const float FlightDuration = TravelStart + MinimumTravelDuration;
        public const float MinimumArcHeight = 0.85f;

        private static Sprite _moteSprite;
        private static Material _trailMaterial;
        private static Sprite[] _revealFrames;

        private WorldLootCache _target;
        private Vector3 _start;
        private float _elapsed;
        private float _travelDuration;
        private Color _rarityColor;
        private bool _travelStarted;
        private SpriteRenderer _core;
        private SpriteRenderer _icon;
        private SpriteRenderer _glow;
        private TrailRenderer _trail;
        private bool _arrived;
        private InventoryItem _item;
        private SpriteRenderer _reveal;

        public float Duration => TravelStart + _travelDuration;
        public Color FlightColor => _rarityColor;

        public static Color ResolveColor(InventoryItem item) => item != null ? ItemRarity.GetTooltipTitleColor(item) : ItemRarity.RareTitle;

        public static float GetTravelDuration(Vector3 start, Vector3 end) =>
            Mathf.Clamp(Vector3.Distance(start + Vector3.up * RiseHeight, end) / 15f, MinimumTravelDuration, 1.6f);

        public static Vector3 EvaluateFlight(Vector3 start, Vector3 end, float elapsed, float travelDuration)
        {
            Vector3 top = start + Vector3.up * RiseHeight;
            if (elapsed < RiseDuration)
            {
                float t = Mathf.Clamp01(elapsed / RiseDuration);
                return Vector3.Lerp(start, top, 1f - (1f - t) * (1f - t));
            }
            if (elapsed < RevealStart)
                return top + Vector3.up * Mathf.Sin((elapsed - RiseDuration) * 8f) / 24f;
            if (elapsed < TravelStart) return top;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - TravelStart) / Mathf.Max(.01f, travelDuration)));
            Vector3 control = Vector3.Lerp(top, end, .5f) + Vector3.up * .65f;
            return (1f - u) * (1f - u) * top + 2f * (1f - u) * u * control + u * u * end;
        }

        public static void Spawn(Vector3 start, WorldLootCache target, InventoryItem item = null)
        {
            if (target == null || !target.isActiveAndEnabled || !target.CanInteract() ||
                (item != null && !target.CanPresentArrival(item)))
                return;

            var host = new GameObject("LootToCacheFlight");
            // Cache ownership cancels flights and their tails immediately when emptied.
            host.transform.SetParent(target.transform, true);
            host.transform.position = start;
            var flight = host.AddComponent<WorldLootCacheFlightVisual>();
            flight._item = item;
            flight.Initialize(start, target);
        }

        public static Vector3 EvaluateArc(Vector3 start, Vector3 end, float normalizedTime, float arcHeight)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector3 control = new Vector3(
                (start.x + end.x) * 0.5f,
                Mathf.Max(start.y, end.y) + Mathf.Max(MinimumArcHeight, arcHeight),
                Mathf.Lerp(start.z, end.z, 0.5f));
            float inverse = 1f - eased;
            return inverse * inverse * start + 2f * inverse * eased * control + eased * eased * end;
        }

        private void Initialize(Vector3 start, WorldLootCache target)
        {
            _target = target;
            _start = start;
            _travelDuration = GetTravelDuration(start, target.transform.position);
            _rarityColor = ResolveColor(_item);
            BuildVisuals();
        }

        private void Update()
        {
            if (_target == null || !_target.isActiveAndEnabled || !_target.CanInteract() ||
                (_item != null && !_target.CanPresentArrival(_item)))
            {
                Retire();
                return;
            }
            if (_arrived)
                return;

            _elapsed += Time.deltaTime;
            transform.position = PickupFlight.SnapToPixelGrid(EvaluateFlight(_start, _target.transform.position, _elapsed, _travelDuration));
            bool travelling = _elapsed >= TravelStart;
            if (travelling && !_travelStarted) { _travelStarted = true; _trail.Clear(); }
            _trail.emitting = _elapsed < RiseDuration || travelling;
            float pulse = 1f + Mathf.Sin(_elapsed * 34f) * 0.16f;
            if (_glow != null)
                _glow.transform.localScale = Vector3.one * pulse * (_item?.Data != null ? 1.15f : 2.2f);

            float reveal = Mathf.Clamp01((_elapsed - RevealStart) / RevealDuration);
            _reveal.enabled = _elapsed >= RevealStart && _elapsed < TravelStart && _revealFrames.Length > 0;
            if (_reveal.enabled)
            {
                _reveal.sprite = _revealFrames[Mathf.Min(_revealFrames.Length - 1, Mathf.FloorToInt(reveal * 5f))];
                _reveal.color = new Color(_rarityColor.r, _rarityColor.g, _rarityColor.b, 1f - reveal);
                _reveal.transform.localScale = Vector3.one * Mathf.Lerp(.4f, 1f, reveal);
            }

            if (_elapsed < Duration)
                return;

            _arrived = true;
            _reveal.enabled = false;
            if (_item != null)
                _target.NotifyLootArrived(_item);
            if (_core != null)
                _core.enabled = false;
            if (_icon != null)
                _icon.enabled = false;
            if (_glow != null)
                _glow.enabled = false;
            if (_trail != null)
                _trail.emitting = false;
            Retire(_trail != null ? _trail.time : 0.1f);
        }

        private void Retire(float delay = 0f)
        {
            if (Application.isPlaying) Destroy(gameObject, delay);
            else DestroyImmediate(gameObject);
        }

        private void BuildVisuals()
        {
            bool showItem = _item?.Data != null;
            _core = gameObject.AddComponent<SpriteRenderer>();
            _core.sprite = showItem ? WorldDroppedItem.GetCircleSprite() : GetMoteSprite();
            _core.color = showItem ? ItemRarity.GetGroundPlateColor(_item) : _rarityColor;
            _core.sortingLayerName = WorldRenderSorting.LayerVfx;
            _core.sortingOrder = WorldDroppedItem.TopVisualSortingOrder + 5;

            if (showItem)
            {
                // Reuse the familiar ground-drop bubble without its pickup or layout behavior.
                var iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(transform, false);
                iconObject.transform.localPosition = new Vector3(0f, 0f, -.01f);
                _icon = iconObject.AddComponent<SpriteRenderer>();
                _icon.sprite = _item.Data.Icon;
                _icon.color = Color.white;
                _icon.sortingLayerName = _core.sortingLayerName;
                _icon.sortingOrder = _core.sortingOrder + 1;
                WorldDroppedItem.FitIconInsideCircle(_icon, 24f);
            }

            var glowObject = new GameObject("RarityGlow");
            glowObject.transform.SetParent(transform, false);
            _glow = glowObject.AddComponent<SpriteRenderer>();
            _glow.sprite = _core.sprite;
            _glow.color = new Color(_rarityColor.r, _rarityColor.g, _rarityColor.b, showItem ? .16f : .34f);
            _glow.sortingLayerName = WorldRenderSorting.LayerVfx;
            _glow.sortingOrder = _core.sortingOrder - 1;
            _glow.transform.localScale = Vector3.one * (showItem ? 1.15f : 2.2f);

            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.24f;
            _trail.minVertexDistance = 0.025f;
            _trail.startWidth = 0.16f;
            _trail.endWidth = 0.015f;
            _trail.numCapVertices = 0;
            _trail.numCornerVertices = 0;
            _trail.textureMode = LineTextureMode.Stretch;
            _trail.alignment = LineAlignment.View;
            _trail.material = GetTrailMaterial();
            _trail.startColor = new Color(_rarityColor.r, _rarityColor.g, _rarityColor.b, .86f);
            _trail.endColor = new Color(_rarityColor.r, _rarityColor.g, _rarityColor.b, 0f);
            _trail.sortingLayerName = WorldRenderSorting.LayerVfx;
            _trail.sortingOrder = _core.sortingOrder - 2;

            if (_revealFrames == null)
            {
                _revealFrames = Resources.LoadAll<Sprite>("Visuals/LootCache/ValuableArrival");
                Array.Sort(_revealFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            var revealObject = new GameObject("RarityReveal"); revealObject.transform.SetParent(transform, false);
            _reveal = revealObject.AddComponent<SpriteRenderer>();
            _reveal.sharedMaterial = Resources.Load<Material>("Visuals/SourceGlow/Halo");
            _reveal.sortingLayerName = _core.sortingLayerName; _reveal.sortingOrder = _core.sortingOrder + 2;
            _reveal.enabled = false;
        }

        private static Sprite GetMoteSprite()
        {
            if (_moteSprite != null)
                return _moteSprite;

            const int size = 6;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeLootCacheMote",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Vector2 center = new Vector2(2.5f, 2.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), center) <= 2.7f ? Color.white : Color.clear);
            texture.Apply(false, true);

            _moteSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 24f);
            _moteSprite.name = "RuntimeLootCacheMote";
            _moteSprite.hideFlags = HideFlags.HideAndDontSave;
            return _moteSprite;
        }

        private static Material GetTrailMaterial()
        {
            if (_trailMaterial != null)
                return _trailMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            _trailMaterial = new Material(shader)
            {
                name = "RuntimeLootCacheTrail",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _trailMaterial;
        }
    }
}
