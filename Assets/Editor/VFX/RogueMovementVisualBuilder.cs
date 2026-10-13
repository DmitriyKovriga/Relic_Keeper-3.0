using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class RogueMovementVisualBuilder
    {
        public const string Root = "Assets/Resources/Heroes/RogueResources/Refined/";
        private const string CharacterPath = "Assets/Resources/Heroes/RogueResources/Rogue.asset";
        private const string Source = "Docs/Art/RogueVariants/SelectedAnimation";
        // Inner/lower glove coordinates, in sprite pixels measured from the bottom.
        private static readonly Vector2Int[] RunGrips = new[] { new Vector2Int(21, 5), new Vector2Int(21, 6), new Vector2Int(20, 7), new Vector2Int(21, 7), new Vector2Int(21, 6), new Vector2Int(21, 6), new Vector2Int(20, 7), new Vector2Int(21, 6) };
        private static readonly Vector2Int[] JumpGrips = new[] { new Vector2Int(21, 5), new Vector2Int(21, 7), new Vector2Int(21, 9), new Vector2Int(21, 9), new Vector2Int(21, 9) };

        public static void AssembleGeneratedFrames()
        {
            string source = Source;
            // Validate every input before replacing any accepted asset.
            foreach (string path in Enumerable.Range(0, 8).Select(i => Path.Combine(source, "RogueRun-" + i + ".png"))
                .Concat(Enumerable.Range(0, 5).Select(i => Path.Combine(source, "RogueAir-" + i + ".png")))
                .Append(Path.Combine(source, "RogueBase.png")))
            {
                var image = new Texture2D(2, 2);
                try
                {
                    if (!image.LoadImage(File.ReadAllBytes(path)) || image.width != 28 || image.height != 28)
                        throw new InvalidOperationException("Rogue source frame must remain 28x28: " + path);
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            Directory.CreateDirectory(Root);
            File.Copy(Path.Combine(source, "RogueBase.png"), Root + "RogueBase.png", true);
            Pack(source, "RogueRun", "RogueRun", Enumerable.Range(0, 8).ToArray());
            // Later generated poses spread the arms; hold the compact airborne pose instead.
            Pack(source, "RogueAir", "RogueJump", new[] { 0, 1, 2, 2, 2 });
            Pack(source, "RogueAir", "RogueFall", new[] { 2 });
            Apply();
        }

        [MenuItem("Tools/Visuals/Apply Refined Rogue Movement")]
        public static void Apply()
        {
            Import("RogueBase", 1, true);
            Import("RogueRun", 8);
            Import("RogueJump", 5);
            Import("RogueFall", 1);
            var rogue = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(CharacterPath);
            if (rogue == null) throw new InvalidOperationException("Rogue character asset missing.");
            rogue.SetPortrait(AssetDatabase.LoadAssetAtPath<Sprite>(Root + "RogueBase.png"));
            rogue.SetMovementAnimationResourcePaths("Heroes/RogueResources/Refined/RogueRun-Sheet",
                "Heroes/RogueResources/Refined/RogueJump-Sheet", "Heroes/RogueResources/Refined/RogueFall-Sheet");
            Configure(rogue.RunAnimation, LoadFrames("RogueRun"), 0.08f, true, RunGrips, false);
            Configure(rogue.JumpAnimation, LoadFrames("RogueJump"), 0.075f, false, JumpGrips, true);
            rogue.JumpAnimation.AirborneRestartFrame = 3;
            Configure(rogue.FallAnimation, LoadFrames("RogueFall"), 0.08f, false, new[] { JumpGrips[4] }, true);
            Configure(rogue.LandingAnimation, Enumerable.Repeat(rogue.Portrait, 5).ToArray(), 0.06f, false,
                Enumerable.Repeat(RunGrips[0], 5).ToArray(), true);
            rogue.LandingAnimation.PresentationScales = new[]
            {
                new Vector2(1.12f, 0.88f), new Vector2(1.08f, 0.93f),
                new Vector2(0.98f, 1.02f), new Vector2(1.01f, 0.99f), Vector2.one
            };
            EditorUtility.SetDirty(rogue);
            AssetDatabase.SaveAssets();
        }

        private static void Configure(CharacterDataSO.CharacterSpriteAnimation animation, Sprite[] frames,
            float frameDuration, bool loop, Vector2Int[] grips, bool authored)
        {
            if (frames.Length != grips.Length) throw new InvalidOperationException("Rogue grip/frame count mismatch.");
            animation.Frames = frames;
            animation.FrameDuration = frameDuration;
            animation.Loop = loop;
            animation.HorizontalScale = 1f;
            animation.PresentationScales = authored ? Enumerable.Repeat(Vector2.one, frames.Length).ToArray() : Array.Empty<Vector2>();
            animation.WeaponHorizontalOffsets = grips.Select(point => (point.x - 19) / 24f).ToArray();
            animation.WeaponVerticalOffsets = grips.Select(point => (point.y - 4) / 24f).ToArray();
        }

        private static Sprite[] LoadFrames(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name + "-Sheet.png")
            .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();

        private static void Pack(string source, string prefix, string name, int[] indices)
        {
            int rows = Mathf.CeilToInt(indices.Length / 4f);
            var sheet = new Texture2D(112, rows * 28, TextureFormat.RGBA32, false);
            var frame = new Texture2D(2, 2);
            try
            {
                sheet.SetPixels32(new Color32[112 * rows * 28]);
                for (int i = 0; i < indices.Length; i++)
                {
                    // Use the exact resting source; the service can slightly quantize its input frame.
                    string filename = prefix == "RogueRun" && indices[i] == 0 ? "RogueBase.png" : prefix + "-" + indices[i] + ".png";
                    frame.LoadImage(File.ReadAllBytes(Path.Combine(source, filename)));
                    sheet.SetPixels(i % 4 * 28, (rows - 1 - i / 4) * 28, 28, 28, frame.GetPixels());
                }
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
                    rect = new Rect(i % 4 * 28, (rows - 1 - i / 4) * 28, 28, 28),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = Vector2.one * 0.5f
                }).ToArray();
            }
            importer.SaveAndReimport();
            if (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() != count)
                throw new InvalidOperationException("Rogue sprite import failed: " + name);
        }
    }
}
