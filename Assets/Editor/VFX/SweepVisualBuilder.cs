using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class SweepVisualBuilder
    {
        private const string ArtRoot = "Assets/Resources/VFX/SweepVFX/";
        private const string PrefabPath = "Assets/Prefabs/VFX/VFX_Sweep.prefab";
        private const int FrameWidth = 96;
        private const int FrameHeight = 24;
        private const int FrameCount = 13;
        private const float Fps = 16;

        [MenuItem("Tools/Skills/Apply Refined Sweep VFX")]
        public static void Apply()
        {
            string path = ArtRoot + "SweepRefined-Sheet.png";
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
                name = "SweepRefined_" + i.ToString("D2"),
                rect = new Rect((i % 4) * FrameWidth, (3 - i / 4) * FrameHeight, FrameWidth, FrameHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            if (frames.Length != FrameCount) throw new InvalidOperationException("Sweep frame import failed.");

            // Replace the active/default clip; keep its original 10/12-second duration.
            // Frame spacing is 16 FPS, with a final blank hold ending at 20/24 seconds.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "SweepVFX2.anim");
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Fps, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = 19f / 24f, value = frames[FrameCount - 1] });
            clip.frameRate = 24;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            foreach (string prefabPath in new[] { PrefabPath, ArtRoot + "SweepPrefab.prefab" })
            {
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    prefab.GetComponent<SpriteRenderer>().sprite = frames[0];
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void CaptureRuntimeViews()
        {
            ColdSlashVisualBuilder.CaptureRuntimeClip(PrefabPath, ArtRoot + "SweepVFX2.anim",
                "codex-sweep-refresh", "Sweep", FrameCount, Fps, 0.46f, true,
                offsetX: 0f, fadeOutStart: 0.75f, fadeStartAlpha: 0.35f, previewSeparation: 3f);
        }
    }
}
