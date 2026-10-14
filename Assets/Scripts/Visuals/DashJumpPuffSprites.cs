using UnityEngine;

namespace Scripts.Visuals
{
    public static class DashJumpPuffSprites
    {
        private static Sprite[] _frames;
        public static Sprite[] Frames => _frames ??= Load();

        private static Sprite[] Load()
        {
            var frames = Resources.LoadAll<Sprite>("VFX/Grounds/DashJumpCloud-Sheet");
            System.Array.Sort(frames, (a,b) => string.CompareOrdinal(a.name,b.name));
            return frames.Length == 12 ? frames : Build();
        }

        private static Sprite[] Build()
        {
            var frames = new Sprite[12];
            for (int f = 0; f < frames.Length; f++)
            {
                float t = f / (float)(frames.Length - 1);
                float bloom = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.35f));
                float breakup = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.3f, 0.95f, t));
                float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.7f, 1f, t));
                var pixels = new Color32[1024];
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float shape = float.PositiveInfinity;
                    // Floor billows spread first, then separate and drift away.
                    for (int lobe = 0; lobe < 4; lobe++)
                    {
                        float cx = 10 + lobe * 4 + (lobe - 1.5f) * bloom * 2 - breakup * 2;
                        float cy = 0.6f + (lobe % 2) * 0.8f + breakup * (1 + lobe % 2);
                        float rx = (3.8f + bloom * 1.2f) * (1 - breakup * 0.8f);
                        float ry = (2.2f + (lobe % 2) * 0.8f) * (1 - breakup * 0.65f);
                        float dx = (x - Mathf.Round(cx)) / rx;
                        float dy = (y - cy) / ry;
                        shape = Mathf.Min(shape, dx * dx + dy * dy);
                    }
                    for (int plume = 0; plume < 3; plume++)
                    for (int step = 0; step < 18; step++)
                    {
                        float u = step / 17f * Mathf.Lerp(0.65f, 1, bloom);
                        // Curving fingers dissolve from the roots and split into short ribbons.
                        if (u < breakup * 0.8f) continue;
                        if (breakup > 0.35f && step % 6 < 2) continue;
                        float cx = (1-u)*(1-u)*24 + 2*(1-u)*u*(9+plume*7-bloom*3)
                            + u*u*(4+plume*11-bloom*2) - breakup*(3-plume);
                        float cy = (1-u)*(1-u)*2 + 2*(1-u)*u*(12+plume*4)
                            + u*u*(26-plume*3) + breakup*3;
                        float radius = (3.1f*(1-u)+0.7f)*(1+bloom*0.25f)*(1-breakup*0.65f);
                        radius *= 1 + 0.2f * Mathf.Sin(step*1.8f+plume);
                        float dx = (x-Mathf.Round(cx))/radius, dy = (y-Mathf.Round(cy))/radius;
                        shape = Mathf.Min(shape, dx*dx+dy*dy);
                    }
                    // Detached tip curls expand into thin open arcs during dissipation.
                    if (t > 0.32f)
                    for (int wisp = 0; wisp < 3; wisp++)
                    {
                        float cx = 6 + wisp*9 - breakup*(3-wisp);
                        float cy = 20 - wisp*3 + breakup*6;
                        float dx = (x-cx)/(2+breakup*3), dy = (y-cy)/(3+breakup*2);
                        float ring = dx*dx+dy*dy;
                        if (ring > 0.45f+breakup*0.25f && ring < 1 && dx < 0.55f && dy > -0.65f)
                            shape = Mathf.Min(shape, 0.4f+ring*0.4f);
                    }
                    if (shape > 1) continue;
                    byte alpha = (byte)(245*fade);
                    pixels[y*32+x] = shape > 0.55f ? new Color32(169,185,183,alpha) : new Color32(246,247,233,alpha);
                }
                var texture = new Texture2D(32,32,TextureFormat.RGBA32,false)
                { name="Dash jump burst "+f, filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp, hideFlags=HideFlags.HideAndDontSave };
                texture.SetPixels32(pixels);
                texture.Apply(false,false);
                frames[f]=Sprite.Create(texture,new Rect(0,0,32,32),Vector2.one*0.5f,24,0,SpriteMeshType.FullRect);
                frames[f].hideFlags=HideFlags.HideAndDontSave;
            }
            return frames;
        }
    }
}
