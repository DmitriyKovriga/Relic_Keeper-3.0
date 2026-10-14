using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class NeutralDodgeVisualBuilder
    {
        public static void Apply()
        {
            foreach (string hero in new[] { "Warrior", "Rogue", "Mage" }) Import(hero);
            AssetDatabase.SaveAssets();
        }

        private static void Import(string hero)
        {
            string source = "Docs/Art/MovementVfx/NeutralDodge/" + hero + "/";
            const int count = 9;
            int size = hero == "Mage" ? 56 : 40;
            var sheet = new Texture2D(size * count, size, TextureFormat.RGBA32, false);
            var frame = new Texture2D(2, 2);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (!frame.LoadImage(File.ReadAllBytes(source + i.ToString("D2") + ".png")) || frame.width != size || frame.height != size)
                        throw new InvalidOperationException("Unexpected neutral dodge canvas: " + hero + " frame " + i);
                    // Preserve the generated cells exactly. Regrounding each pose would make the tumble bounce.
                    sheet.SetPixels(i * size, 0, size, size, frame.GetPixels());
                }
                sheet.Apply();
                string root = "Assets/Resources/Heroes/" + hero + "Resources/Refined/";
                string path = root + hero + "NeutralDodge-Sheet.png";
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 24;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.isReadable = true;
                importer.alphaIsTransparency = true;
                importer.spritesheet = Enumerable.Range(0, count).Select(i => new SpriteMetaData
                {
                    name = hero + "NeutralDodge_" + i.ToString("D2"),
                    rect = new Rect(i * size, 0, size, size),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = Vector2.one * 0.5f
                }).ToArray();
                importer.SaveAndReimport();
                var data = AssetDatabase.LoadAssetAtPath<CharacterDataSO>("Assets/Resources/Heroes/" + hero + "Resources/" + hero + ".asset");
                var animation = data.NeutralDodgeAnimation;
                animation.Frames = Array.Empty<Sprite>();
                animation.SpriteSheetResourcesPath = "Heroes/" + hero + "Resources/Refined/" + hero + "NeutralDodge-Sheet";
                animation.FrameDuration = 0.24f / count;
                animation.Loop = false;
                EditorUtility.SetDirty(data);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }
    }
}
