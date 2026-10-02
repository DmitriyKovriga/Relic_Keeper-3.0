using Scripts.UI;
using Scripts.Visuals.SpriteFX;
using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>
    /// Yellow flash, rising arrow, and a localized notice when the player gains a level.
    /// </summary>
    public static class PlayerLevelUpFeedback
    {
        public const string LevelUpKey = "player.levelUp";
        public const string LevelUpEnglish = "Character Level Up";
        public const string LevelUpRussian = "Уровень персонажа повышен";

        private static readonly Color FlashColor = new Color(1f, 0.9f, 0.15f, 1f);
        private static readonly Color ArrowColor = new Color(1f, 0.86f, 0.18f, 1f);
        private const float FlashDuration = 0.55f;
        private const float ArrowDuration = 0.7f;
        private const float ArrowRise = 0.8f;
        private const int ArrowWidth = 7;
        private const int ArrowHeight = 13;
        private const float ArrowPixelsPerUnit = 24f;

        private static Sprite _arrowSprite;

        public static string FormatMessage()
        {
            return RuntimeLocalization.Resolve(LevelUpKey, LevelUpEnglish, LevelUpRussian);
        }

        public static void Play(PlayerStats player)
        {
            if (player == null || !Application.isPlaying)
                return;

            Flash(player);
            SpawnArrow(player);
            RoomClearedBanner.ShowCharacterLevelUp();
        }

        private static void Flash(PlayerStats player)
        {
            // The visible body copies this renderer's material property block every frame.
            // A separate overlay sprite uses the wrong shader and draws behind the hero.
            SpriteRenderer source = player.GetComponent<SpriteRenderer>();
            if (source == null)
                return;

            SpriteFxController fx = source.GetComponent<SpriteFxController>();
            if (fx == null)
                fx = source.gameObject.AddComponent<SpriteFxController>();

            fx.Flash(FlashColor, FlashDuration);
        }

        private static void SpawnArrow(PlayerStats player)
        {
            SpriteRenderer body = ResolveBodyRenderer(player);
            Vector3 origin = body != null
                ? new Vector3(body.bounds.center.x, body.bounds.max.y + 0.06f, body.transform.position.z)
                : player.transform.position + Vector3.up * 0.8f;

            var arrow = new GameObject("LevelUpArrow");
            arrow.transform.position = origin;
            var motion = arrow.AddComponent<LevelUpArrowMotion>();
            motion.Initialize(origin, GetArrowSprite(), ArrowColor, ArrowDuration, ArrowRise);
            WorldRenderSorting.ConfigureAutoSorter(arrow, RenderDepthCategory.HeroAttackVfx, origin.y, localOffset: 4);
        }

        private static SpriteRenderer ResolveBodyRenderer(PlayerStats player)
        {
            PlayerMovementVisual movement = player.GetComponent<PlayerMovementVisual>();
            if (movement != null && movement.DisplayRenderer != null)
                return movement.DisplayRenderer;

            return player.GetComponent<SpriteRenderer>();
        }

        private static Sprite GetArrowSprite()
        {
            if (_arrowSprite != null)
                return _arrowSprite;

            var texture = new Texture2D(ArrowWidth, ArrowHeight, TextureFormat.RGBA32, false)
            {
                name = "LevelUpArrowTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[ArrowWidth * ArrowHeight];
            Color32 yellow = new Color32(255, 214, 48, 255);
            int mid = ArrowWidth / 2;
            for (int y = 0; y < 8; y++)
                pixels[mid + y * ArrowWidth] = yellow;
            pixels[mid + 8 * ArrowWidth] = yellow;
            pixels[(mid - 1) + 9 * ArrowWidth] = yellow;
            pixels[mid + 9 * ArrowWidth] = yellow;
            pixels[(mid + 1) + 9 * ArrowWidth] = yellow;
            pixels[(mid - 2) + 10 * ArrowWidth] = yellow;
            pixels[(mid - 1) + 10 * ArrowWidth] = yellow;
            pixels[mid + 10 * ArrowWidth] = yellow;
            pixels[(mid + 1) + 10 * ArrowWidth] = yellow;
            pixels[(mid + 2) + 10 * ArrowWidth] = yellow;
            pixels[mid + 11 * ArrowWidth] = yellow;
            pixels[mid + 12 * ArrowWidth] = yellow;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _arrowSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, ArrowWidth, ArrowHeight),
                new Vector2(0.5f, 0f),
                ArrowPixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            _arrowSprite.name = "LevelUpArrow";
            _arrowSprite.hideFlags = HideFlags.HideAndDontSave;
            return _arrowSprite;
        }
    }

    public sealed class LevelUpArrowMotion : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Vector3 _origin;
        private Color _color;
        private float _duration;
        private float _rise;
        private float _elapsed;

        public void Initialize(Vector3 origin, Sprite sprite, Color color, float duration, float rise)
        {
            _origin = origin;
            _color = color;
            _duration = Mathf.Max(0.05f, duration);
            _rise = rise;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = sprite;
            _renderer.color = color;
            transform.position = origin;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            transform.position = _origin + Vector3.up * (_rise * t);
            if (_renderer != null)
                _renderer.color = new Color(_color.r, _color.g, _color.b, 1f - t);
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
