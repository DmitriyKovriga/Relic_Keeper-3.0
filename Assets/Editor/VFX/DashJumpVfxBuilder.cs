using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class DashJumpVfxBuilder
    {
        public static void ImportCloud()
        {
            const int size = 48, count = 12;
            const string path = "Assets/Resources/VFX/Grounds/DashJumpCloud-Sheet.png";
            var source = new Texture2D(2, 2);
            var sheet = new Texture2D(size * count, size, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes("Docs/Art/MovementVfx/DashJumpCloudSource.png")))
                    throw new InvalidOperationException("Dash jump cloud source missing.");
                int cellWidth = source.width / 4, cellHeight = source.height / 3;
                var pixels = new Color32[size * size * count];
                for (int frame = 0; frame < count; frame++)
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Keep the same cell coordinates in every frame: drifting remnants must not be recentered.
                    Color32 pixel = source.GetPixel(frame % 4 * cellWidth + (int)((size-x-0.5f)*cellWidth/size),
                        (2-frame/4)*cellHeight + (int)((y+0.5f)*cellHeight/size));
                    if (pixel.a < 150 || pixel.b > pixel.r + 35 || pixel.g > pixel.r + 35) continue;
                    byte alpha = (byte)(pixel.a * (frame < 9 ? 1f : frame == 9 ? 0.8f : frame == 10 ? 0.5f : 0.15f));
                    pixel = pixel.r > 205 ? new Color32(246,247,233,alpha) : new Color32(179,196,196,alpha);
                    // One shared floor offset; fragments gain a little outward drift as the cloud breaks up.
                    int drift = Mathf.Max(0, frame-4);
                    int outX = x-drift/3, outY = y-2+drift/2;
                    if (outX >= 0 && outX < size && outY >= 0 && outY < size)
                        pixels[outY * size * count + frame * size + outX] = pixel;
                }
                sheet.SetPixels32(pixels);
                sheet.Apply(false, false);
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 24;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                var frames = new SpriteMetaData[count];
                for (int i = 0; i < count; i++) frames[i] = new SpriteMetaData
                {
                    name = "DashJumpCloud_" + i.ToString("D2"), rect = new Rect(i*size, 0, size, size),
                    alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f
                };
                importer.spritesheet = frames;
                importer.SaveAndReimport();
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(sheet); }
        }
    }
}
