using UnityEngine;

namespace Scripts.Visuals
{
    public static class GroundDashPuffSprites
    {
        private static Sprite[] _frames;
        public static Sprite[] Frames => _frames ??= Load();

        private static Sprite[] Load()
        {
            var frames = Resources.LoadAll<Sprite>("VFX/Grounds/GroundDashReference-Sheet");
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (frames.Length != 8) return Build();
            // Begin at the fully formed plume; retain the original source sheet.
            var visibleFrames = new Sprite[6];
            System.Array.Copy(frames, 2, visibleFrames, 0, visibleFrames.Length);
            return visibleFrames;
        }

        private static Sprite[] Build()
        {
            var frames = new Sprite[8];
            for (int f = 0; f < frames.Length; f++)
            {
                var pixels = new Color32[24 * 24];
                float t = f / 7f;
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, t));
                for (int y = 0; y < 24; y++)
                for (int x = 0; x < 24; x++)
                {
                    float edge = float.PositiveInfinity;
                    for (int lobe = 0; lobe < 4; lobe++)
                    {
                        float cx = 16 - lobe * 3 - t * (3 + lobe);
                        float cy = 1 + lobe * 2 + t * 4;
                        float radius = (2.3f + lobe * 0.2f) * (1f - t * 0.4f);
                        float dx = (x - Mathf.Round(cx)) / radius;
                        float dy = (y - Mathf.Round(cy)) / radius;
                        edge = Mathf.Min(edge, dx * dx + dy * dy);
                    }
                    if (edge <= 1f)
                        pixels[y * 24 + x] = edge > 0.55f
                            ? new Color32(136, 143, 128, (byte)(200 * fade))
                            : new Color32(213, 211, 188, (byte)(230 * fade));
                }
                var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                frames[f] = Sprite.Create(texture, new Rect(0, 0, 24, 24), Vector2.one * 0.5f, 24, 0, SpriteMeshType.FullRect);
                frames[f].hideFlags = HideFlags.HideAndDontSave;
            }
            return frames;
        }
    }
}
