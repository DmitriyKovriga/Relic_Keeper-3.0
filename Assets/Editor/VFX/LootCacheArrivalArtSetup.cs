using System.IO;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class LootCacheArrivalArtSetup
    {
        [System.Serializable] private class RectData { public int x, y, w, h; }
        [System.Serializable] private class Frame { public RectData frame; }
        [System.Serializable] private class Sheet { public Frame[] frames; }

        public static void Import()
        {
            ImportSheet("ValuableArrival");
            ImportSheet("UniqueArrival");
        }

        private static void ImportSheet(string name)
        {
            string path = "Assets/Resources/Visuals/LootCache/" + name + ".png";
            var json = JsonUtility.FromJson<Sheet>(File.ReadAllText("Docs/Art/GameJuicePlan/J01/" + name + "-PixelLab.json"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprites = new SpriteMetaData[json.frames.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                var r = json.frames[i].frame;
                sprites[i] = new SpriteMetaData { name = "Arrival_" + i.ToString("D2"),
                    rect = new Rect(r.x, texture.height - r.y - r.h, r.w, r.h),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(21f / 43f, 17f / 49f) };
            }
            importer.spritesheet = sprites; importer.SaveAndReimport();
        }
    }
}
