using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyAttackTelegraphVfx : MonoBehaviour
    {
        private const int AuraWidth = 18;
        private const int AuraHeight = 8;
        private const float AuraPixelsPerUnit = 24f;

        private static Sprite s_auraSprite;

        private EnemyEntity _entity;
        private SpriteRenderer _sourceRenderer;
        private SpriteRenderer _flashRenderer;
        private SpriteRenderer _auraRenderer;
        private float _duration;
        private float _elapsed;

        public bool IsPlaying { get; private set; }

        public void Play(EnemyEntity entity, float duration)
        {
            _entity = entity != null ? entity : GetComponent<EnemyEntity>();
            SpriteRenderer renderer = _entity != null
                ? _entity.VisualRenderer
                : GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
                return;

            if (_sourceRenderer != renderer)
            {
                DestroyVisualObjects();
                _sourceRenderer = renderer;
            }

            EnsureVisualObjects();
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            IsPlaying = true;
            enabled = true;
            SetVisualsActive(true);
            UpdateVisuals();
        }

        public void Stop()
        {
            IsPlaying = false;
            _elapsed = 0f;
            SetVisualsActive(false);
            enabled = false;
        }

        private void OnDisable()
        {
            IsPlaying = false;
            SetVisualsActive(false);
        }

        private void LateUpdate()
        {
            if (!IsPlaying || _sourceRenderer == null)
            {
                Stop();
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                Stop();
                return;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_sourceRenderer == null || _flashRenderer == null || _auraRenderer == null)
                return;

            float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration));
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float pulse = Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI * 4f));

            _flashRenderer.sprite = _sourceRenderer.sprite;
            _flashRenderer.flipX = _sourceRenderer.flipX;
            _flashRenderer.flipY = _sourceRenderer.flipY;
            _flashRenderer.sharedMaterial = _sourceRenderer.sharedMaterial;
            _flashRenderer.maskInteraction = _sourceRenderer.maskInteraction;
            _flashRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
            _flashRenderer.sortingOrder = _sourceRenderer.sortingOrder + 1;
            _flashRenderer.color = new Color(1f, 0.08f, 0.04f, (0.18f + pulse * 0.52f) * envelope);

            Bounds bounds = _entity != null ? _entity.GetVisualBounds() : _sourceRenderer.bounds;
            _auraRenderer.transform.position = new Vector3(
                bounds.center.x,
                bounds.max.y + 0.04f + progress * 0.06f,
                transform.position.z);
            float baseWidth = Mathf.Clamp(
                bounds.size.x / Mathf.Max(0.01f, s_auraSprite.bounds.size.x),
                0.75f,
                1.6f);
            _auraRenderer.transform.localScale = new Vector3(baseWidth, 1f + pulse * 0.08f, 1f);
            _auraRenderer.color = new Color(1f, 0.16f, 0.05f, (0.35f + pulse * 0.45f) * envelope);
        }

        private void EnsureVisualObjects()
        {
            if (_flashRenderer == null)
            {
                var flash = new GameObject("AttackTelegraphFlash");
                flash.transform.SetParent(_sourceRenderer.transform, false);
                _flashRenderer = flash.AddComponent<SpriteRenderer>();
            }

            if (_auraRenderer == null)
            {
                var aura = new GameObject("AttackTelegraphAura");
                aura.transform.SetParent(transform, false);
                _auraRenderer = aura.AddComponent<SpriteRenderer>();
                _auraRenderer.sprite = GetOrCreateAuraSprite();
                var auraSorter = aura.AddComponent<WorldDepthSort>();
                auraSorter.ConfigureFixed(RenderDepthCategory.GameplayVfx, localOffset: 12);
            }
        }

        private void SetVisualsActive(bool active)
        {
            if (_flashRenderer != null)
                _flashRenderer.gameObject.SetActive(active);
            if (_auraRenderer != null)
                _auraRenderer.gameObject.SetActive(active);
        }

        private void DestroyVisualObjects()
        {
            if (_flashRenderer != null)
                Destroy(_flashRenderer.gameObject);
            if (_auraRenderer != null)
                Destroy(_auraRenderer.gameObject);
            _flashRenderer = null;
            _auraRenderer = null;
        }

        private static Sprite GetOrCreateAuraSprite()
        {
            if (s_auraSprite != null)
                return s_auraSprite;

            var texture = new Texture2D(AuraWidth, AuraHeight, TextureFormat.RGBA32, false)
            {
                name = "EnemyAttackTelegraphAuraTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[AuraWidth * AuraHeight];
            Color32 red = new(255, 32, 12, 220);
            Color32 orange = new(255, 105, 24, 245);
            DrawHorizontal(pixels, 2, 15, 0, new Color32(255, 22, 8, 110));
            DrawHorizontal(pixels, 4, 13, 1, red);
            DrawVertical(pixels, 4, 1, 3, red);
            DrawVertical(pixels, 8, 1, 5, orange);
            DrawVertical(pixels, 9, 1, 6, orange);
            DrawVertical(pixels, 13, 1, 4, red);
            pixels[3 + 4 * AuraWidth] = orange;
            pixels[14 + 5 * AuraWidth] = orange;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            s_auraSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, AuraWidth, AuraHeight),
                new Vector2(0.5f, 0f),
                AuraPixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            s_auraSprite.name = "EnemyAttackTelegraphAura";
            s_auraSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_auraSprite;
        }

        private static void DrawHorizontal(Color32[] pixels, int fromX, int toX, int y, Color32 color)
        {
            for (int x = fromX; x <= toX; x++)
                pixels[x + y * AuraWidth] = color;
        }

        private static void DrawVertical(Color32[] pixels, int x, int fromY, int toY, Color32 color)
        {
            for (int y = fromY; y <= toY; y++)
                pixels[x + y * AuraWidth] = color;
        }
    }
}
