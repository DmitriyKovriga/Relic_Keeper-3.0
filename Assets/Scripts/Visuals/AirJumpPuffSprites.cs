using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Cloud cushion opening into a ring, with a short downward push-off jet.</summary>
    public static class AirJumpPuffSprites
    {
        private static Sprite[] _frames;
        public static Sprite[] Frames => _frames ??= Build();
        public const float ContactHeightRatio = 27f / 32f;

        private static Sprite[] Build()
        {
            var frames = new Sprite[8];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                float t = frame / 7f;
                float growth = Mathf.Min(1f, 0.6f + t * 2f);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, t));
                float radiusX = 7f + growth * 3f + t * 2f;
                float radiusY = 3.2f + growth * 1.5f;
                var pixels = new Color32[32 * 32];
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float shape = float.PositiveInfinity;
                    for (int lobe = 0; lobe < 10; lobe++)
                    {
                        float angle = lobe * Mathf.PI * 2f / 10f;
                        float cx = 15.5f + Mathf.Cos(angle) * radiusX;
                        float cy = 17f - t * 2f + Mathf.Sin(angle) * radiusY;
                        float radius = (3.1f + growth * 0.8f + Mathf.Sin(lobe * 1.7f) * 0.3f) * (1f - t * 0.3f);
                        float dx = (x - Mathf.Round(cx)) / radius;
                        float dy = (y - Mathf.Round(cy)) / radius;
                        shape = Mathf.Min(shape, dx * dx + dy * dy);
                    }
                    bool tail = y >= 3 && y < 13 && Mathf.Abs(x - 15.5f) <= (y - 2) * 0.22f * (1f - t) && frame < 5;
                    if (shape > 1f && !tail) continue;
                    byte alpha = (byte)Mathf.RoundToInt(245 * fade);
                    pixels[y * 32 + x] = !tail && shape > 0.55f
                        ? new Color32(162, 178, 180, alpha)
                        : new Color32(239, 242, 232, alpha);
                }
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
                { name = "Air jump cloud " + frame, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                frames[frame] = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 24, 0, SpriteMeshType.FullRect);
                frames[frame].hideFlags = HideFlags.HideAndDontSave;
            }
            return frames;
        }
    }
}
