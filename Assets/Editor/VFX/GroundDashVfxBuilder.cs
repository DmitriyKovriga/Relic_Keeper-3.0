using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class GroundDashVfxBuilder
    {
        public static void ImportReferenceCloud()
        {
            var source = new Texture2D(2, 2);
            var sheet = new Texture2D(256, 32, TextureFormat.RGBA32, false);
            const string path = "Assets/Resources/VFX/Grounds/GroundDashReference-Sheet.png";
            try
            {
                if (!source.LoadImage(System.IO.File.ReadAllBytes("Docs/Art/MovementVfx/GroundDashReferenceSource.png")))
                    throw new System.InvalidOperationException("Reference cloud image missing.");
                int width = source.width / 4, height = source.height / 2;
                var output = new Color32[256 * 32];
                for (int frame = 0; frame < 8; frame++)
                {
                    var pixels = new Color32[32 * 32];
                    for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        Color32 pixel = source.GetPixel(frame % 4 * width + (int)((x + 0.5f) * width / 32),
                            (1 - frame / 4) * height + (int)((y + 0.5f) * height / 32));
                        if (pixel.a >= 128) pixels[y * 32 + x] = pixel;
                    }
                    // Remove isolated export speckles while retaining cloud curls and fragments.
                    var visited = new bool[1024];
                    for (int start = 0; start < pixels.Length; start++)
                    {
                        if (visited[start] || pixels[start].a == 0) continue;
                        var queue = new System.Collections.Generic.Queue<int>();
                        var component = new System.Collections.Generic.List<int>();
                        queue.Enqueue(start);
                        while (queue.Count > 0)
                        {
                            int index = queue.Dequeue();
                            if (visited[index] || pixels[index].a == 0) continue;
                            visited[index] = true;
                            component.Add(index);
                            if (index % 32 > 0) queue.Enqueue(index - 1);
                            if (index % 32 < 31) queue.Enqueue(index + 1);
                            if (index >= 32) queue.Enqueue(index - 32);
                            if (index < 992) queue.Enqueue(index + 32);
                        }
                        if (component.Count < 4) foreach (int index in component) pixels[index] = default;
                    }
                    int bottom = 32;
                    for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                        if (pixels[y * 32 + x].a > 0) bottom = Mathf.Min(bottom, y);
                    if (bottom == 32) throw new System.InvalidOperationException("Reference cloud frame is empty: " + frame);
                    for (int y = bottom; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        Color32 pixel = pixels[y * 32 + x];
                        float fade = frame < 5 ? 1f : frame == 5 ? 0.85f : frame == 6 ? 0.5f : 0.15f;
                        pixel.a = (byte)(pixel.a * fade);
                        output[(y - bottom) * 256 + frame * 32 + x] = pixel;
                    }
                }
                sheet.SetPixels32(output);
                sheet.Apply(false, false);
                System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
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
                var frames = new SpriteMetaData[8];
                for (int i = 0; i < 8; i++) frames[i] = new SpriteMetaData
                { name = "GroundDashReference_" + i.ToString("D2"), rect = new Rect(i * 32, 0, 32, 32), alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f };
                importer.spritesheet = frames;
                importer.SaveAndReimport();
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(sheet); }
        }

        public static void CreateAfterimageMaterial()
        {
            const string root = "Assets/Resources/VFX/Materials/";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(root + "GroundDashAfterimage.shader");
            if (shader == null) throw new System.InvalidOperationException("Afterimage shader missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(root + "GroundDashAfterimage.mat");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, root + "GroundDashAfterimage.mat");
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}
