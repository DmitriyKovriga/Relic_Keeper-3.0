using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed class DefenseFeedbackArtImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Visuals/DefenseFeedback/";
        [Serializable] private sealed class Sheet { public Frame[] frames; public Meta meta; }
        [Serializable] private sealed class Meta { public Size size; }
        [Serializable] private sealed class Size { public int w, h; }
        [Serializable] private sealed class Frame { public SizeRect frame; public Pivot pivot; }
        [Serializable] private sealed class SizeRect { public int x, y, w, h; }
        [Serializable] private sealed class Pivot { public float x, y; }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.Ordinal)) return;
            string manifest = Path.ChangeExtension(assetPath, ".json"); if (!File.Exists(manifest)) return;
            var sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText(manifest));
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = 24;
            importer.npotScale = TextureImporterNPOTScale.None;
#pragma warning disable CS0618
            var rects = new SpriteMetaData[sheet.frames.Length];
            for (int i = 0; i < rects.Length; i++)
            {
                var frame = sheet.frames[i]; var rect = frame.frame;
                rects[i] = new SpriteMetaData { name = "Defense_" + i.ToString("D2"),
                    rect = new Rect(rect.x, sheet.meta.size.h - rect.y - rect.h, rect.w, rect.h),
                    alignment = 9, pivot = new Vector2(frame.pivot.x, 1 - frame.pivot.y) };
            }
            importer.spritesheet = rects;
#pragma warning restore CS0618
        }

        public static void ConfigureAll()
        {
            foreach (var file in Directory.GetFiles(Folder, "*.png")) AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            string path = Folder + "DefenseAccent.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
            {
                var shader = Shader.Find("RelicKeeper/Sprites/World Color Unlit");
                if (shader == null) throw new InvalidOperationException("World Color Unlit shader missing.");
                AssetDatabase.CreateAsset(new Material(shader) { name = "DefenseAccent" }, path); AssetDatabase.SaveAssets();
            }
        }
    }
}
