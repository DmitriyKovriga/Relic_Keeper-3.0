using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class LungeSlashVisualBuilder
    {
        private const string ArtRoot = "Assets/Resources/Skills/2HWeapon/Sword/Adventurer/LungeSlash/";
        private const string PrefabPath = ArtRoot + "LungeSlashVFXPrefab.prefab";
        private const int FrameWidth = 108;
        private const int FrameHeight = 25;
        private const int FrameCount = 13;
        private const float Fps = 20;

        [MenuItem("Tools/Skills/Apply Refined Lunge Slash VFX")]
        public static void Apply()
        {
            string path = ArtRoot + "LungeSlashRefined-Sheet.png";
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
            importer.spritesheet = Enumerable.Range(0, FrameCount).Select(i => new SpriteMetaData
            {
                name = "LungeSlashRefined_" + i.ToString("D2"),
                rect = new Rect((i % 4) * FrameWidth, (3 - i / 4) * FrameHeight, FrameWidth, FrameHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            if (frames.Length != FrameCount) throw new InvalidOperationException("Lunge Slash frame import failed.");

            // Thirteen denser poses with the original 8/12-second clip duration.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "LungeSlashAnimation.anim");
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Fps, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = 15f / 24f, value = frames[FrameCount - 1] });
            clip.frameRate = 24;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = frames[0];
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
        }

        public static void CaptureRuntimeViews()
        {
            ColdSlashVisualBuilder.CaptureRuntimeClip(PrefabPath, ArtRoot + "LungeSlashAnimation.anim",
                "codex-lunge-slash-refresh", "LungeSlash", FrameCount, Fps, 0.35f, true,
                offsetX: 0.5f, previewSeparation: 3f, attachToParent: false);
        }
    }
}
