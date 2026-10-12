using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class CleaveVisualBuilder
    {
        private const string ArtRoot = "Assets/Resources/VFX/CleaveSFX/";
        private const string PrefabPath = "Assets/Prefabs/VFX/VFX_Cleave.prefab";
        private const int FrameSize = 36;
        private const int FrameCount = 12;
        private const float Fps = 24;

        [MenuItem("Tools/Skills/Apply Refined Cleave VFX")]
        public static void Apply()
        {
            string path = ArtRoot + "CleaveRefined-Sheet.png";
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
                name = "CleaveRefined_" + i.ToString("D2"),
                rect = new Rect((i % 4) * FrameSize, (2 - i / 4) * FrameSize, FrameSize, FrameSize),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            if (frames.Length != FrameCount) throw new InvalidOperationException("Cleave frame import failed.");

            // This is the controller's active/default clip. Keep its original 7/12-second
            // duration so the existing skill lifetime and fade retain their timing.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "CleaveSFXAnim2.anim");
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Fps, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = 13f / Fps, value = frames[FrameCount - 1] });
            clip.frameRate = Fps;
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
            ColdSlashVisualBuilder.CaptureRuntimeClip(PrefabPath, ArtRoot + "CleaveSFXAnim2.anim",
                "codex-cleave-refresh", "Cleave", FrameCount, Fps, 0.4f, true);
        }
    }
}
