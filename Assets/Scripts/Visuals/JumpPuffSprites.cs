using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>A small pixel cloud: compression, rising lobes, then scattered wisps.</summary>
    public static class JumpPuffSprites
    {
        private static Sprite[] _frames;
        public static Sprite[] Frames => _frames ??= Build();

        private static Sprite[] Build()
        {
            const int size = 24;
            var frames = new Sprite[8];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                float t = frame / 7f;
                float growth = Mathf.Min(1f, t * 3f);
                float shrink = 1f - Mathf.SmoothStep(0f, 0.9f, Mathf.InverseLerp(0.4f, 1f, t));
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float shape = float.PositiveInfinity;
                    for (int lobe = 0; lobe < 5; lobe++)
                    {
                        float side = lobe == 0 ? 0f : lobe % 2 == 0 ? 1f : -1f;
                        float cx = 11.5f + side * (lobe < 3 ? 3f + t * 5f : 6f + t * 4f);
                        float cy = lobe == 0 ? 3f + growth * 4f + t * 6f : lobe < 3 ? 3f + t * 5f : t * 2f;
                        float radius = (lobe == 0 ? 3.5f : lobe < 3 ? 3f : 2f) * (0.7f + growth * 0.3f) * shrink;
                        float dx = (x - Mathf.Round(cx)) / Mathf.Max(0.5f, radius);
                        float dy = (y - Mathf.Round(cy)) / Mathf.Max(0.5f, radius * (frame == 0 ? 0.6f : 1f));
                        shape = Mathf.Min(shape, dx * dx + dy * dy);
                    }
                    if (shape > 1f) continue;
                    byte alpha = (byte)Mathf.RoundToInt(235f * (1f - Mathf.SmoothStep(0f, 0.93f, t)));
                    pixels[y * size + x] = shape > 0.6f
                        ? new Color32(139, 142, 127, alpha)
                        : new Color32(207, 205, 181, alpha);
                }
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Jump puff " + frame, filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                frames[frame] = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 24f, 0, SpriteMeshType.FullRect);
                frames[frame].hideFlags = HideFlags.HideAndDontSave;
            }
            return frames;
        }
    }
}
