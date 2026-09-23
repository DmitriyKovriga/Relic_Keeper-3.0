using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Enemies
{
    public static class EnemyDeathVisualFactory
    {
        public const float PixelsPerUnit = 24f;
        private static Sprite s_maskSprite;
        private static readonly List<Sprite> s_bloodPixelSprites = new List<Sprite>();
        private static readonly List<Sprite> s_bloodCrumbSprites = new List<Sprite>();
        private static readonly List<Sprite> s_groundPuddleSprites = new List<Sprite>();
        private static readonly List<Sprite> s_wallDripSprites = new List<Sprite>();
        private static readonly List<Sprite> s_chunkMaskSprites = new List<Sprite>();
        private static readonly List<Sprite> s_goreSprites = new List<Sprite>();

        /// <summary>Build runtime death sprites outside combat to avoid the first-kill hitch.</summary>
        public static void Prewarm()
        {
            EnsureSpritesBuilt();
        }

        public static Sprite GetMaskSprite()
        {
            if (s_maskSprite != null)
                return s_maskSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            s_maskSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            s_maskSprite.name = "DeathMaskSprite";
            return s_maskSprite;
        }

        public static Sprite GetRandomBloodPixelSprite()
        {
            EnsureSpritesBuilt();
            return s_bloodPixelSprites[UnityEngine.Random.Range(0, s_bloodPixelSprites.Count)];
        }

        public static Sprite GetRandomBloodCrumbSprite()
        {
            EnsureSpritesBuilt();
            return s_bloodCrumbSprites[UnityEngine.Random.Range(0, s_bloodCrumbSprites.Count)];
        }

        public static Sprite GetRandomGroundPuddleSprite()
        {
            EnsureSpritesBuilt();
            return s_groundPuddleSprites[UnityEngine.Random.Range(0, s_groundPuddleSprites.Count)];
        }

        public static Sprite GetRandomWallDripSprite()
        {
            EnsureSpritesBuilt();
            return s_wallDripSprites[UnityEngine.Random.Range(0, s_wallDripSprites.Count)];
        }

        public static Sprite GetRandomChunkMaskSprite()
        {
            EnsureSpritesBuilt();
            return s_chunkMaskSprites[UnityEngine.Random.Range(0, s_chunkMaskSprites.Count)];
        }

        public static Sprite GetRandomGoreSprite()
        {
            EnsureSpritesBuilt();
            return s_goreSprites[UnityEngine.Random.Range(0, s_goreSprites.Count)];
        }

        private static void EnsureSpritesBuilt()
        {
            if (s_bloodPixelSprites.Count > 0 && s_bloodCrumbSprites.Count > 0 && s_groundPuddleSprites.Count > 0 && s_wallDripSprites.Count > 0 && s_chunkMaskSprites.Count > 0 && s_goreSprites.Count > 0)
                return;

            for (int i = 0; i < 7; i++)
                s_bloodPixelSprites.Add(BuildPixelClusterSprite(10, 1000 + i * 37, $"BloodPixelCluster_{i}", 2, 3, 7, 11));

            for (int i = 0; i < 8; i++)
                s_bloodCrumbSprites.Add(BuildPixelClusterSprite(6, 1400 + i * 29, $"BloodCrumb_{i}", 1, 2, 5, 8));

            for (int i = 0; i < 6; i++)
                s_groundPuddleSprites.Add(BuildGroundPuddleSprite(24, 10, 1500 + i * 31, $"GroundPuddle_{i}"));

            for (int i = 0; i < 6; i++)
                s_wallDripSprites.Add(BuildWallDripSprite(12, 22, 1700 + i * 33, $"WallDrip_{i}"));

            for (int i = 0; i < 8; i++)
                s_chunkMaskSprites.Add(BuildChunkMaskSprite(18, 1800 + i * 41, $"ChunkMask_{i}"));

            for (int i = 0; i < 5; i++)
                s_goreSprites.Add(BuildBlobSprite(14, 2000 + i * 53, 4, 0.82f, $"GoreChunk_{i}"));
        }

        private static Sprite BuildChunkMaskSprite(int size, int seed, string name)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            var random = new System.Random(seed);
            Vector2 center = new Vector2(
                Mathf.Lerp(size * 0.43f, size * 0.57f, (float)random.NextDouble()),
                Mathf.Lerp(size * 0.43f, size * 0.57f, (float)random.NextDouble()));
            float radius = Mathf.Lerp(size * 0.31f, size * 0.38f, (float)random.NextDouble());
            float aspectX = Mathf.Lerp(0.78f, 1.18f, (float)random.NextDouble());
            float aspectY = Mathf.Lerp(0.78f, 1.18f, (float)random.NextDouble());
            float phaseA = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());
            float phaseB = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());

            for (int y = 1; y < size - 1; y++)
            {
                for (int x = 1; x < size - 1; x++)
                {
                    Vector2 delta = new Vector2((x - center.x) / aspectX, (y - center.y) / aspectY);
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float tornRadius = radius *
                        (1f + Mathf.Sin(angle * 3f + phaseA) * 0.19f +
                         Mathf.Sin(angle * 5f + phaseB) * 0.13f +
                         Mathf.Sin(angle * 9f + seed * 0.031f) * 0.07f);
                    float pixelNoise = Hash01(x, y, seed) * 0.9f - 0.45f;
                    if (delta.magnitude <= tornRadius + pixelNoise)
                        pixels[x + y * size] = Color.white;
                }
            }

            int biteCount = random.Next(2, 5);
            for (int i = 0; i < biteCount; i++)
            {
                float angle = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());
                Vector2 biteCenter = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * Mathf.Lerp(0.72f, 1.02f, (float)random.NextDouble());
                float biteRadius = Mathf.Lerp(1.1f, 2.2f, (float)random.NextDouble());
                ClearCircle(pixels, size, biteCenter, biteRadius);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static float Hash01(int x, int y, int seed)
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static void ClearCircle(Color[] pixels, int size, Vector2 center, float radius)
        {
            float radiusSquared = radius * radius;
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + radius));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if ((new Vector2(x, y) - center).sqrMagnitude <= radiusSquared)
                        pixels[x + y * size] = Color.clear;
                }
            }
        }

        private static Sprite BuildPixelClusterSprite(int size, int seed, string name, int minBlockSize, int maxBlockSize, int minCount, int maxCount)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            var random = new System.Random(seed);
            int centerX = size / 2;
            int centerY = size / 2;
            int count = random.Next(minCount, maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                int x = Mathf.Clamp(centerX + random.Next(-3, 4), 0, size - 1);
                int y = Mathf.Clamp(centerY + random.Next(-2, 3), 0, size - 1);
                int blockSize = random.Next(minBlockSize, maxBlockSize + 1);
                FillBlock(pixels, size, x, y, blockSize);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static void FillBlock(Color[] pixels, int size, int x, int y, int blockSize)
        {
            for (int oy = 0; oy < blockSize; oy++)
            {
                for (int ox = 0; ox < blockSize; ox++)
                {
                    int px = Mathf.Clamp(x + ox, 0, size - 1);
                    int py = Mathf.Clamp(y + oy, 0, size - 1);
                    pixels[px + py * size] = Color.white;
                }
            }
        }

        private static Sprite BuildBlobSprite(int size, int seed, int lobeCount, float fillThreshold, string name)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            var random = new System.Random(seed);
            var lobes = new (Vector2 center, float radiusX, float radiusY)[lobeCount];
            for (int i = 0; i < lobeCount; i++)
            {
                float cx = Mathf.Lerp(size * 0.22f, size * 0.78f, (float)random.NextDouble());
                float cy = Mathf.Lerp(size * 0.22f, size * 0.78f, (float)random.NextDouble());
                float rx = Mathf.Lerp(size * 0.14f, size * 0.28f, (float)random.NextDouble());
                float ry = Mathf.Lerp(size * 0.12f, size * 0.26f, (float)random.NextDouble());
                lobes[i] = (new Vector2(cx, cy), rx, ry);
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float influence = 0f;
                    for (int i = 0; i < lobes.Length; i++)
                    {
                        Vector2 delta = new Vector2(x, y) - lobes[i].center;
                        float nx = delta.x / Mathf.Max(0.0001f, lobes[i].radiusX);
                        float ny = delta.y / Mathf.Max(0.0001f, lobes[i].radiusY);
                        float d = 1f - (nx * nx + ny * ny);
                        if (d > 0f)
                            influence += d;
                    }

                    int pixelIndex = x + y * size;
                    if (influence >= fillThreshold)
                        pixels[pixelIndex] = Color.white;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static Sprite BuildGroundPuddleSprite(int width, int height, int seed, string name)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            var random = new System.Random(seed);
            int baseline = random.Next(2, 4);
            for (int x = 1; x < width - 1; x += random.Next(1, 3))
            {
                int blobWidth = random.Next(2, 5);
                int blobHeight = random.Next(2, 4);
                int y = baseline + random.Next(-1, 2);
                FillEllipse(pixels, width, height, x, y, blobWidth, blobHeight);
            }

            for (int i = 0; i < 3; i++)
            {
                int x = random.Next(2, width - 6);
                int y = baseline + random.Next(-1, 2);
                FillBlock(pixels, width, x, y, random.Next(2, 4));
            }

            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.18f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static Sprite BuildWallDripSprite(int width, int height, int seed, string name)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[width * height];
            var random = new System.Random(seed);
            float centerX = width * Mathf.Lerp(0.42f, 0.58f, (float)random.NextDouble());
            float headY = height * Mathf.Lerp(0.58f, 0.76f, (float)random.NextDouble());
            float headRadius = width * Mathf.Lerp(0.22f, 0.31f, (float)random.NextDouble());
            float dripLength = height * Mathf.Lerp(0.34f, 0.64f, (float)random.NextDouble());

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float dx = x - centerX;
                    float dy = y - headY;
                    bool head = dx * dx / (headRadius * headRadius) + dy * dy / (headRadius * headRadius * 0.72f) <= 1f;
                    float progress = Mathf.Clamp01((headY - y) / dripLength);
                    float pathX = centerX + Mathf.Sin(progress * 4.2f + seed * 0.017f) * 1.15f;
                    float halfWidth = Mathf.Lerp(headRadius * 0.44f, 0.6f, progress);
                    bool tail = y <= headY && y >= headY - dripLength && Mathf.Abs(x - pathX) <= halfWidth;
                    if (!head && !tail)
                        continue;
                    float highlight = head && dy > -headRadius * 0.15f ? 1.06f : tail ? 0.55f : 0.76f;
                    pixels[x + y * width] = new Color(highlight, highlight * 0.8f, highlight * 0.8f, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.08f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static void FillEllipse(Color[] pixels, int width, int height, int x, int y, int ellipseWidth, int ellipseHeight)
        {
            float rx = Mathf.Max(1f, ellipseWidth * 0.5f);
            float ry = Mathf.Max(1f, ellipseHeight * 0.5f);
            float cx = x + rx;
            float cy = y + ry;

            int minX = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(x + ellipseWidth), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(y), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(y + ellipseHeight), 0, height - 1);

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    float nx = (px - cx) / rx;
                    float ny = (py - cy) / ry;
                    if (nx * nx + ny * ny <= 1f)
                        pixels[px + py * width] = Color.white;
                }
            }
        }
    }
}
