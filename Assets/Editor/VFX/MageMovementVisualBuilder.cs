using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class MageMovementVisualBuilder
    {
        public const string Root = "Assets/Resources/Heroes/MageResources/Refined/";
        public const int Width = 32;
        public const int Height = 40;
        private const string CharacterPath = "Assets/Resources/Heroes/MageResources/Mage.asset";
        private const string Source = "Docs/Art/MageWizardRobeVariants/SelectedAnimation";
        // Inner/lower glove coordinates, in sprite pixels measured from the bottom.
        private static readonly Vector2Int[] RunGrips = new[] { new Vector2Int(23, 6), new Vector2Int(23, 7), new Vector2Int(23, 6), new Vector2Int(23, 7), new Vector2Int(23, 7), new Vector2Int(23, 7), new Vector2Int(23, 7), new Vector2Int(23, 7) };
        private static readonly Vector2Int[] JumpGrips = new[] { new Vector2Int(23, 6), new Vector2Int(23, 7), new Vector2Int(24, 8), new Vector2Int(24, 8), new Vector2Int(24, 8) };

        public static void AssembleGeneratedFrames()
        {
            string source = Source;
            // Validate every input before replacing any accepted asset.
            foreach (string path in Enumerable.Range(0, 8).Select(i => Path.Combine(source, "MageRun-" + i + ".png"))
                .Concat(Enumerable.Range(0, 5).Select(i => Path.Combine(source, "MageAir-" + i + ".png")))
                .Append(Path.Combine(source, "MageBase.png")))
            {
                var image = new Texture2D(2, 2);
                try
                {
                    if (!image.LoadImage(File.ReadAllBytes(path)) || image.width != Width || image.height != Height)
                        throw new InvalidOperationException("Mage source frame must remain 32x40: " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            Directory.CreateDirectory(Root);
            WriteBase(Path.Combine(source, "MageBase.png"));
            Pack(source, "MageRun", "MageRun", Enumerable.Range(0, 8).ToArray());
            // Later generated poses spread the arms; hold the compact airborne pose instead.
            Pack(source, "MageAir", "MageJump", new[] { 0, 1, 2, 2, 2 });
            Pack(source, "MageAir", "MageFall", new[] { 2 });
            Apply();
        }

        [MenuItem("Tools/Visuals/Apply Refined Mage Movement")]
        public static void Apply()
        {
            Import("MageBase", 1, true);
            Import("MageRun", 8);
            Import("MageJump", 5);
            Import("MageFall", 1);
            var mage = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(CharacterPath);
            if (mage == null) throw new InvalidOperationException("Mage character asset missing.");
            mage.SetPortrait(AssetDatabase.LoadAssetAtPath<Sprite>(Root + "MageBase.png"));
            mage.SetMovementAnimationResourcePaths("Heroes/MageResources/Refined/MageRun-Sheet",
                "Heroes/MageResources/Refined/MageJump-Sheet", "Heroes/MageResources/Refined/MageFall-Sheet");
            Configure(mage.RunAnimation, LoadFrames("MageRun"), 0.08f, true, RunGrips, false);
            Configure(mage.JumpAnimation, LoadFrames("MageJump"), 0.075f, false, JumpGrips, true);
            mage.JumpAnimation.AirborneRestartFrame = 3;
            Configure(mage.FallAnimation, LoadFrames("MageFall"), 0.08f, false, new[] { JumpGrips[4] }, true);
            Configure(mage.LandingAnimation, Enumerable.Repeat(mage.Portrait, 5).ToArray(), 0.06f, false,
                Enumerable.Repeat(RunGrips[0], 5).ToArray(), true);
            mage.LandingAnimation.PresentationScales = new[]
            {
                new Vector2(1.08f, 0.92f), new Vector2(1.05f, 0.96f),
                new Vector2(0.99f, 1.01f), new Vector2(1.01f, 0.99f), Vector2.one
            };
            EditorUtility.SetDirty(mage);
            AssetDatabase.SaveAssets();
        }

        private static void Configure(CharacterDataSO.CharacterSpriteAnimation animation, Sprite[] frames,
            float frameDuration, bool loop, Vector2Int[] grips, bool authored)
        {
            if (frames.Length != grips.Length) throw new InvalidOperationException("Mage grip/frame count mismatch.");
            animation.Frames = frames;
            animation.FrameDuration = frameDuration;
            animation.Loop = loop;
            animation.HorizontalScale = 1f;
            animation.PresentationScales = authored ? Enumerable.Repeat(Vector2.one, frames.Length).ToArray() : Array.Empty<Vector2>();
            animation.WeaponHorizontalOffsets = grips.Select(point => (point.x - (Width / 2 + 5)) / 24f).ToArray();
            animation.WeaponVerticalOffsets = grips.Select(point => (point.y - 4) / 24f).ToArray();
        }

        private static Sprite[] LoadFrames(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name + "-Sheet.png")
            .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();

        // Move the canvas origin to the lowest opaque boot pixel, without scaling or painting.
        private static Color32[] GroundAlignedPixels(Texture2D frame)
        {
            var raw = frame.GetPixels32();
            int bottom = 0;
            while (bottom < Height && !Enumerable.Range(bottom * Width, Width).Any(i => raw[i].a != 0)) bottom++;
            if (bottom == Height) throw new InvalidOperationException("Mage source frame is empty.");
            var pixels = new Color32[Width * Height];
            Array.Copy(raw, bottom * Width, pixels, 0, (Height - bottom) * Width);
            return pixels;
        }

        private static void WriteBase(string path)
        {
            var raw = new Texture2D(2, 2);
            var output = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                raw.LoadImage(File.ReadAllBytes(path));
                output.SetPixels32(GroundAlignedPixels(raw));
                output.Apply(false, false);
                File.WriteAllBytes(Root + "MageBase.png", output.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(raw);
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private static void Pack(string source, string prefix, string name, int[] indices)
        {
            int rows = Mathf.CeilToInt(indices.Length / 4f);
            var sheet = new Texture2D(Width * 4, rows * Height, TextureFormat.RGBA32, false);
            var frame = new Texture2D(2, 2);
            try
            {
                var pixels = new Color32[Width * 4 * rows * Height];
                for (int i = 0; i < indices.Length; i++)
                {
                    string filename = prefix == "MageRun" && indices[i] == 0 ? "MageBase.png" : prefix + "-" + indices[i] + ".png";
                    frame.LoadImage(File.ReadAllBytes(Path.Combine(source, filename)));
                    var aligned = GroundAlignedPixels(frame);
                    for (int y = 0; y < Height; y++)
                        Array.Copy(aligned, y * Width, pixels, ((rows - 1 - i / 4) * Height + y) * Width * 4 + i % 4 * Width, Width);
                }
                sheet.SetPixels32(pixels);
                sheet.Apply(false, false);
                File.WriteAllBytes(Root + name + "-Sheet.png", sheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(frame);
            }
        }

        private static void Import(string name, int count, bool single = false)
        {
            string path = Root + name + (single ? ".png" : "-Sheet.png");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = single ? SpriteImportMode.Single : SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = Vector2.one * 0.5f;
            importer.SetTextureSettings(settings);
            if (!single)
            {
                int rows = Mathf.CeilToInt(count / 4f);
                importer.spritesheet = Enumerable.Range(0, count).Select(i => new SpriteMetaData
                {
                    name = name + "_" + i.ToString("D2"),
                    rect = new Rect(i % 4 * Width, (rows - 1 - i / 4) * Height, Width, Height),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = Vector2.one * 0.5f
                }).ToArray();
            }
            importer.SaveAndReimport();
            if (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() != count)
                throw new InvalidOperationException("Mage sprite import failed: " + name);
        }
    }
}
