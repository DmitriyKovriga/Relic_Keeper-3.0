using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class TremorVisualBuilder
    {
        private const string ArtRoot = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/Tremor/";
        private const string PrefabPath = ArtRoot + "TremorsSkillVFXPrefub.prefab";
        private const int FrameWidth = 85;
        private const int FrameHeight = 19;
        private const int FrameCount = 13;
        private const float Fps = 24;

        [MenuItem("Tools/Skills/Apply Refined Tremor VFX")]
        public static void Apply()
        {
            string path = ArtRoot + "TremorRefined-Sheet.png";
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
                name = "TremorRefined_" + i.ToString("D2"),
                rect = new Rect((i % 4) * FrameWidth, (3 - i / 4) * FrameHeight, FrameWidth, FrameHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            if (frames.Length != FrameCount) throw new InvalidOperationException("Tremor frame import failed.");

            // Preserve the original seven-frame, 12 FPS one-shot duration.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "TremorSjillAnimation.anim");
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Fps, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = 13f / 24f, value = frames[FrameCount - 1] });
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
                var debris = prefab.GetComponent<Scripts.Skills.Projectiles.GroundProjectileDebris>() ??
                    prefab.AddComponent<Scripts.Skills.Projectiles.GroundProjectileDebris>();
                var serializedDebris = new SerializedObject(debris);
                serializedDebris.FindProperty("_stationaryImpact").boolValue = true;
                serializedDebris.FindProperty("_particleMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/GroundShake/GroundShakeDebris.mat");
                serializedDebris.FindProperty("_groundLayer").intValue = LayerMask.GetMask("Ground", "OneWayPlatform");
                serializedDebris.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
        }

        public static void CaptureRuntimeViews()
        {
            ColdSlashVisualBuilder.CaptureRuntimeClip(PrefabPath, ArtRoot + "TremorSjillAnimation.anim",
                "codex-tremor-refresh", "Tremor", FrameCount, Fps, 0.35f, true,
                offsetX: 1f, previewSeparation: 2f, attachToParent: false,
                groundRecipePath: ArtRoot + "Recipe_Tremor_Skill.asset");
        }
    }
}
