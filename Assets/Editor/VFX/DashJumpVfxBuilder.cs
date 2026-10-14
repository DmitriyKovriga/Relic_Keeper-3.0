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
            ImportSheet("DashJumpCloud", 48, 12, 3, true);
        }

        public static void ImportJumpCloud()
        {
            ImportSheet("JumpCloud", 24, 8, 2, false);
        }

        public static void ImportVerticalJumpCloud()
        {
            ImportSheet("VerticalJumpCloud", 24, 8, 2, false);
        }

        public static void ImportStationaryDodge()
        {
            ImportSheet("StationaryDodge", 40, 5, 1, false, "Dodge", false, 5);
        }

        private static void ImportSheet(string name, int size, int count, int rows, bool dashJump, string folder = "Grounds", bool anchorFloor = true, int columns = 4)
        {
            string path = "Assets/Resources/VFX/" + folder + "/" + name + "-Sheet.png";
            var source = new Texture2D(2, 2);
            var sheet = new Texture2D(size * count, size, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes("Docs/Art/MovementVfx/" + name + "Source.png")))
                    throw new InvalidOperationException(name + " source missing.");
                int cellWidth = source.width / columns, cellHeight = source.height / rows;
                int floorOffset = anchorFloor ? 2 : 0;
                if (!dashJump && anchorFloor)
                {
                    // Derive one shared anchor from the opening puff, never from later drifting frames.
                    floorOffset = -1;
                    for (int y = 0; y < size && floorOffset < 0; y++)
                    {
                        int visible = 0;
                        for (int x = 0; x < size; x++)
                        {
                            Color32 sample = source.GetPixel((int)((x+0.5f)*cellWidth/size),
                                (rows-1)*cellHeight + (int)((y+0.5f)*cellHeight/size));
                            if (sample.a >= 150 && sample.b <= sample.r+35 && sample.g <= sample.r+35) visible++;
                        }
                        if (visible >= 2) floorOffset = y;
                    }
                    if (floorOffset < 0) throw new InvalidOperationException("Opening jump cloud is empty.");
                }
                var pixels = new Color32[size * size * count];
                for (int frame = 0; frame < count; frame++)
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Keep the same cell coordinates in every frame: drifting remnants must not be recentered.
                    Color32 pixel = source.GetPixel(frame % columns * cellWidth + (int)((size-x-0.5f)*cellWidth/size),
                        (rows-1-frame/columns)*cellHeight + (int)((y+0.5f)*cellHeight/size));
                    if (pixel.a < 150 || pixel.b > pixel.r + 35 || pixel.g > pixel.r + 35) continue;
                    byte alpha = (byte)(pixel.a * (frame < count-3 ? 1f : frame == count-3 ? 0.8f : frame == count-2 ? 0.5f : 0.15f));
                    pixel = pixel.r > 205 ? new Color32(246,247,233,alpha)
                        : dashJump ? new Color32(179,196,196,alpha) : new Color32(210,222,219,alpha);
                    // One shared floor offset; fragments gain a little outward drift as the cloud breaks up.
                    int drift = dashJump ? Mathf.Max(0, frame-4) : 0;
                    int outX = x-drift/3, outY = y-floorOffset+drift/2;
                    if (outX >= 0 && outX < size && outY >= 0 && outY < size)
                        pixels[outY * size * count + frame * size + outX] = pixel;
                }
                if (name == "StationaryDodge")
                for (int frame = 0; frame < count; frame++)
                {
                    var visited = new bool[size*size];
                    for (int start = 0; start < visited.Length; start++)
                    {
                        var queue = new System.Collections.Generic.Queue<int>();
                        var component = new System.Collections.Generic.List<int>();
                        queue.Enqueue(start);
                        while (queue.Count > 0)
                        {
                            int index = queue.Dequeue();
                            int sheetIndex = index/size*size*count+frame*size+index%size;
                            if (visited[index] || pixels[sheetIndex].a == 0) continue;
                            visited[index] = true;
                            component.Add(sheetIndex);
                            if (index%size > 0) queue.Enqueue(index-1);
                            if (index%size < size-1) queue.Enqueue(index+1);
                            if (index >= size) queue.Enqueue(index-size);
                            if (index < size*(size-1)) queue.Enqueue(index+size);
                        }
                        if (component.Count < (frame < 6 ? 4 : 2))
                            foreach (int index in component) pixels[index] = default;
                    }
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
                importer.isReadable = !dashJump;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                var frames = new SpriteMetaData[count];
                for (int i = 0; i < count; i++) frames[i] = new SpriteMetaData
                {
                    name = name + "_" + i.ToString("D2"), rect = new Rect(i*size, 0, size, size),
                    alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f
                };
                importer.spritesheet = frames;
                importer.SaveAndReimport();
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(sheet); }
        }
    }
}
