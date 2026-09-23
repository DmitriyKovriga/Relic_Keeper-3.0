using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Items.World
{
    /// <summary>Gold currency-like arc used when a ground item is stored in a loot sphere.</summary>
    public sealed class WorldLootCacheFlightVisual : MonoBehaviour
    {
        public const float FlightDuration = 0.42f;
        public const float MinimumArcHeight = 0.85f;

        private static Sprite _moteSprite;
        private static Material _trailMaterial;

        private WorldLootCache _target;
        private Vector3 _start;
        private float _elapsed;
        private float _arcHeight;
        private SpriteRenderer _core;
        private SpriteRenderer _glow;
        private TrailRenderer _trail;
        private bool _arrived;

        public static void Spawn(Vector3 start, WorldLootCache target)
        {
            if (target == null)
                return;

            var host = new GameObject("LootToCacheFlight");
            host.transform.position = start;
            var flight = host.AddComponent<WorldLootCacheFlightVisual>();
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
            float distance = Vector2.Distance(start, target.transform.position);
            _arcHeight = Mathf.Max(MinimumArcHeight, 0.65f + distance * 0.28f);
            BuildVisuals();
        }

        private void Update()
        {
            if (_arrived)
                return;
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / FlightDuration);
            transform.position = EvaluateArc(_start, _target.transform.position, t, _arcHeight);
            float pulse = 1f + Mathf.Sin(_elapsed * 34f) * 0.16f;
            if (_glow != null)
                _glow.transform.localScale = Vector3.one * pulse * 2.2f;

            if (t < 1f)
                return;

            _arrived = true;
            if (_core != null)
                _core.enabled = false;
            if (_glow != null)
                _glow.enabled = false;
            if (_trail != null)
                _trail.emitting = false;
            Destroy(gameObject, _trail != null ? _trail.time : 0.1f);
        }

        private void BuildVisuals()
        {
            _core = gameObject.AddComponent<SpriteRenderer>();
            _core.sprite = GetMoteSprite();
            _core.color = new Color(1f, 0.76f, 0.20f, 1f);
            _core.sortingLayerName = WorldRenderSorting.LayerVfx;
            _core.sortingOrder = WorldDroppedItem.TopVisualSortingOrder + 5;

            var glowObject = new GameObject("GoldenGlow");
            glowObject.transform.SetParent(transform, false);
            _glow = glowObject.AddComponent<SpriteRenderer>();
            _glow.sprite = GetMoteSprite();
            _glow.color = new Color(1f, 0.58f, 0.08f, 0.34f);
            _glow.sortingLayerName = WorldRenderSorting.LayerVfx;
            _glow.sortingOrder = _core.sortingOrder - 1;
            _glow.transform.localScale = Vector3.one * 2.2f;

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
            _trail.startColor = new Color(1f, 0.78f, 0.25f, 0.86f);
            _trail.endColor = new Color(1f, 0.40f, 0.04f, 0f);
            _trail.sortingLayerName = WorldRenderSorting.LayerVfx;
            _trail.sortingOrder = _core.sortingOrder - 2;
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
