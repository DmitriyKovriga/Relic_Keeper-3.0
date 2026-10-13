using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class WarriorMovementVisualBuilder
    {
        public const string Root = "Assets/Resources/Heroes/WarriorResources/Refined/";

        [MenuItem("Tools/Visuals/Apply Refined Warrior Movement")]
        public static void Apply()
        {
            Import("WarriorRun", 9);
            Import("WarriorJump", 5);
            Import("WarriorFall", 5);
            var warrior = AssetDatabase.LoadAssetAtPath<CharacterDataSO>("Assets/Resources/Heroes/WarriorResources/Warrior.asset");
            warrior.SetMovementAnimationResourcePaths("Heroes/WarriorResources/Refined/WarriorRun-Sheet",
                "Heroes/WarriorResources/Refined/WarriorJump-Sheet", "Heroes/WarriorResources/Refined/WarriorFall-Sheet");
            warrior.RunAnimation.Frames = Array.Empty<Sprite>();
            warrior.JumpAnimation.Frames = Array.Empty<Sprite>();
            warrior.FallAnimation.Frames = Array.Empty<Sprite>();
            // A calmer gait; the one-pixel grip settle is shared by held equipment.
            warrior.RunAnimation.FrameDuration = 0.72f / 9;
            warrior.RunAnimation.WeaponVerticalOffsets = new[] { 0f, 0f, -1f, -1f, 0f, 0f, -1f, -1f, 0f }.Select(value => value / 24f).ToArray();
            warrior.JumpAnimation.FrameDuration = 0.064f;
            warrior.FallAnimation.FrameDuration = 0.064f;
            warrior.JumpAnimation.WeaponVerticalOffsets = Array.Empty<float>();
            warrior.FallAnimation.WeaponVerticalOffsets = Array.Empty<float>();
            warrior.RunAnimation.HorizontalScale = 1f;
            warrior.JumpAnimation.HorizontalScale = 0.94f;
            warrior.FallAnimation.HorizontalScale = 0.97f;
            // Airborne artwork animates the upper body and legs with a frame-tracked main-hand grip.
            var neutral = AssetDatabase.LoadAllAssetsAtPath(Root + "WarriorRun-Sheet.png")
                .OfType<Sprite>().OrderBy(sprite => sprite.name).First();
            // Airborne narrowing is authored in pixel poses, never transform-scaled.
            ConfigurePose(warrior.JumpAnimation, neutral, 0.075f, Enumerable.Repeat(Vector2.one, 5).ToArray());
            ConfigurePose(warrior.FallAnimation, neutral, 0.08f, Enumerable.Repeat(Vector2.one, 5).ToArray());
            ConfigurePose(warrior.LandingAnimation, neutral, 0.06f, new[] {
                new Vector2(1.16f, 0.85f), new Vector2(1.12f, 0.89f), new Vector2(0.97f, 1.03f),
                new Vector2(1.01f, 0.99f), Vector2.one });
            warrior.JumpAnimation.AirborneRestartFrame = 3;
            warrior.JumpAnimation.Frames = LoadPoseFrames("WarriorJump");
            warrior.FallAnimation.Frames = LoadPoseFrames("WarriorFall");
            // Offsets follow the AI-authored inner fingertip on the right/main-hand side.
            warrior.JumpAnimation.WeaponHorizontalOffsets = new[] { 0f, 0f, 2f, 3f, 3f }.Select(value => value / 24f).ToArray();
            warrior.FallAnimation.WeaponHorizontalOffsets = new[] { 3f, 3f, 3f, 3f, 3f }.Select(value => value / 24f).ToArray();
            warrior.JumpAnimation.WeaponVerticalOffsets = new[] { 0f, 0f, 1f, 3f, 4f }.Select(value => value / 24f).ToArray();
            warrior.FallAnimation.WeaponVerticalOffsets = new[] { 4f, 4f, 4f, 4f, 4f }.Select(value => value / 24f).ToArray();
            EditorUtility.SetDirty(warrior);
            AssetDatabase.SaveAssets();
        }

        private static Sprite[] LoadPoseFrames(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name + "-Sheet.png")
            .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();

        public static void AssembleAirborneFrames()
        {
            string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "codex-warrior-refresh");
            Assemble(source, "", "WarriorJump", 5, "AirbornePoses-sheet.png");
            Assemble(source, "", "WarriorFall", 5, "AirbornePoses-sheet.png", 4);
            Apply();
        }

        public static void AssembleGeneratedAirborneFrames()
        {
            string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "codex-warrior-refresh");
            for (int i = 0; i < 5; i++)
            {
                File.Copy(Path.Combine(source, "NaturalAir-" + i + ".png"), Path.Combine(source, "NaturalJump-" + i + ".png"), true);
                File.Copy(Path.Combine(source, "NaturalAir-4.png"), Path.Combine(source, "NaturalFall-" + i + ".png"), true);
            }
            Assemble(source, "NaturalJump", "WarriorJump", 5);
            Assemble(source, "NaturalFall", "WarriorFall", 5);
            Apply();
        }

        private static void ConfigurePose(CharacterDataSO.CharacterSpriteAnimation animation, Sprite neutral,
            float frameDuration, Vector2[] scales)
        {
            animation.Frames = Enumerable.Repeat(neutral, scales.Length).ToArray();
            animation.PresentationScales = scales;
            animation.HorizontalScale = 1;
            animation.FrameDuration = frameDuration;
            animation.Loop = false;
        }

        public static void AssembleGeneratedFrames()
        {
            string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "codex-warrior-refresh");
            Directory.CreateDirectory(Root);
            Assemble(source, "RunMini", "WarriorRun", 9, "FixedGripRun-sheet.png");
            Assemble(source, "WarriorJump", "WarriorJump", 5);
            Assemble(source, "WarriorFall", "WarriorFall", 5);
            Apply();
        }

        public static void AssembleRunFrames()
        {
            string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "codex-warrior-refresh");
            Assemble(source, "RunMini", "WarriorRun", 9, "FixedGripRun-sheet.png");
            Apply();
        }

        private static void Assemble(string source, string prefix, string name, int count, string sourceSheetName = null, int sourceFrameOffset = 0)
        {
            int rows = Mathf.CeilToInt(count / 4f);
            var sheet = new Texture2D(112, rows * 28, TextureFormat.RGBA32, false);
            var frame = new Texture2D(28, 28, TextureFormat.RGBA32, false);
            Texture2D sourceSheet = null;
            try
            {
                if (sourceSheetName != null)
                {
                    sourceSheet = new Texture2D(84, 84, TextureFormat.RGBA32, false);
                    if (!sourceSheet.LoadImage(File.ReadAllBytes(Path.Combine(source, sourceSheetName))) || sourceSheet.width != 84 || sourceSheet.height != 84)
                        throw new InvalidOperationException("Fixed-grip source sheet dimensions changed.");
                }
                sheet.SetPixels32(new Color32[112 * rows * 28]);
                for (int i = 0; i < count; i++)
                {
                    string path = Path.Combine(source, prefix + "-" + i + ".png");
                    Color32[] pixels;
                    if (sourceSheet != null)
                    {
                        int sourceFrame = i + sourceFrameOffset;
                        Color[] crop = sourceSheet.GetPixels(sourceFrame % 3 * 28, (2 - sourceFrame / 3) * 28, 28, 28);
                        frame.SetPixels(crop);
                        pixels = frame.GetPixels32();
                    }
                    else
                    {
                        if (!frame.LoadImage(File.ReadAllBytes(path)) || frame.width != 28 || frame.height != 28)
                            throw new InvalidOperationException("Generated frame dimensions changed: " + path);
                        pixels = frame.GetPixels32();
                    }
                    var all = sheet.GetPixels32();
                    int x = i % 4 * 28, y = (rows - 1 - i / 4) * 28;
                    for (int row = 0; row < 28; row++)
                        Array.Copy(pixels, row * 28, all, (y + row) * 112 + x, 28);
                    sheet.SetPixels32(all);
                }
                sheet.Apply(false, false);
                File.WriteAllBytes(Root + name + "-Sheet.png", sheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(sheet);
                if (sourceSheet != null) UnityEngine.Object.DestroyImmediate(sourceSheet);
            }
        }

        private static void Import(string name, int count)
        {
            string path = Root + name + "-Sheet.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            int rows = Mathf.CeilToInt(count / 4f);
            importer.spritesheet = Enumerable.Range(0, count).Select(i => new SpriteMetaData
            {
                name = name + "_" + i.ToString("D2"),
                rect = new Rect(i % 4 * 28, (rows - 1 - i / 4) * 28, 28, 28),
                alignment = (int)SpriteAlignment.Center,
                pivot = Vector2.one * 0.5f
            }).ToArray();
            importer.SaveAndReimport();
            if (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() != count)
                throw new InvalidOperationException(name + " frame import failed.");
        }
    }
}
