using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class WarriorOutfitVisualBuilder
    {
        public const string Root = "Assets/Resources/Heroes/WarriorResources/Refined/";
        public const int Width = 28;
        public const int Height = 28;
        private const string CharacterPath = "Assets/Resources/Heroes/WarriorResources/Warrior.asset";
        private const string Source = "Docs/Art/WarriorVariantsRound2/SelectedAnimation";
        // Inner/lower glove coordinates, in sprite pixels measured from the bottom.
        private static readonly Vector2Int[] RunGrips = new[] { new Vector2Int(19, 6), new Vector2Int(19, 7), new Vector2Int(19, 6), new Vector2Int(19, 6), new Vector2Int(19, 7), new Vector2Int(19, 7), new Vector2Int(19, 6), new Vector2Int(19, 6), new Vector2Int(19, 7) };
        private static readonly Vector2Int[] JumpGrips = new[] { new Vector2Int(19, 6), new Vector2Int(19, 4), new Vector2Int(21, 5), new Vector2Int(22, 7), new Vector2Int(22, 8) };

        public static void AssembleGeneratedFrames()
        {
            string source = Source;
            // Validate every input before replacing any accepted asset.
            foreach (string path in Enumerable.Range(0, 9).Select(i => Path.Combine(source, "WarriorRun-" + i + ".png"))
                .Concat(Enumerable.Range(0, 5).Select(i => Path.Combine(source, "WarriorAir-" + i + ".png")))
                .Append(Path.Combine(source, "WarriorBase.png")))
            {
                var image = new Texture2D(2, 2);
                try
                {
                    if (!image.LoadImage(File.ReadAllBytes(path)) || image.width != 32 || image.height != 32)
                        throw new InvalidOperationException("Warrior source frame must remain 32x32: " + path);
                    GroundAlignedPixels(image);
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            Directory.CreateDirectory(Root);
            WriteBase(Path.Combine(source, "WarriorBase.png"));
            Pack(source, "WarriorRun", "WarriorRun", Enumerable.Range(0, 9).ToArray());
            // Retain the accepted takeoff poses and hold the final apex throughout descent.
            Pack(source, "WarriorAir", "WarriorJump", new[] { 0, 1, 2, 3, 4 });
            Pack(source, "WarriorAir", "WarriorFall", new[] { 4, 4, 4, 4, 4 });
            Apply();
        }

        [MenuItem("Tools/Visuals/Apply Selected Warrior Outfit")]
        public static void Apply()
        {
            Import("WarriorBase", 1, true);
            Import("WarriorRun", 9);
            Import("WarriorJump", 5);
            Import("WarriorFall", 5);
            var warrior = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(CharacterPath);
            if (warrior == null) throw new InvalidOperationException("Warrior character asset missing.");
            warrior.SetPortrait(AssetDatabase.LoadAssetAtPath<Sprite>(Root + "WarriorBase.png"));
            warrior.SetMovementAnimationResourcePaths("Heroes/WarriorResources/Refined/WarriorRun-Sheet",
                "Heroes/WarriorResources/Refined/WarriorJump-Sheet", "Heroes/WarriorResources/Refined/WarriorFall-Sheet");
            Configure(warrior.RunAnimation, LoadFrames("WarriorRun"), 0.08f, true, RunGrips, false);
            Configure(warrior.JumpAnimation, LoadFrames("WarriorJump"), 0.075f, false, JumpGrips, true);
            warrior.JumpAnimation.AirborneRestartFrame = 3;
            Configure(warrior.FallAnimation, LoadFrames("WarriorFall"), 0.08f, false, Enumerable.Repeat(JumpGrips[4], 5).ToArray(), true);
            Configure(warrior.LandingAnimation, Enumerable.Repeat(warrior.Portrait, 5).ToArray(), 0.06f, false,
                Enumerable.Repeat(RunGrips[0], 5).ToArray(), true);
            warrior.LandingAnimation.PresentationScales = new[]
            {
                new Vector2(1.16f, 0.85f), new Vector2(1.12f, 0.89f),
                new Vector2(0.97f, 1.03f), new Vector2(1.01f, 0.99f), Vector2.one
            };
            EditorUtility.SetDirty(warrior);
            AssetDatabase.SaveAssets();
        }

        private static void Configure(CharacterDataSO.CharacterSpriteAnimation animation, Sprite[] frames,
            float frameDuration, bool loop, Vector2Int[] grips, bool authored)
        {
            if (frames.Length != grips.Length) throw new InvalidOperationException("Warrior grip/frame count mismatch.");
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
            var source = frame.GetPixels32();
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                if ((x < 2 || x >= 30 || y < 2 || y >= 30) && source[y * 32 + x].a != 0)
                    throw new InvalidOperationException("Generated outfit exceeds the original 28x28 canvas; cropping would remove pixels.");
            var raw = frame.GetPixels(2, 2, Width, Height).Select(color => (Color32)color).ToArray();
            int bottom = 0;
            while (bottom < Height && !Enumerable.Range(bottom * Width, Width).Any(i => raw[i].a != 0)) bottom++;
            if (bottom == Height) throw new InvalidOperationException("Warrior source frame is empty.");
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
                File.WriteAllBytes(Root + "WarriorBase.png", output.EncodeToPNG());
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
                    string filename = prefix == "WarriorRun" && indices[i] == 0 ? "WarriorBase.png" : prefix + "-" + indices[i] + ".png";
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
                throw new InvalidOperationException("Warrior sprite import failed: " + name);
        }
    }
}
