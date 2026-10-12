using System;
using System.IO;
using System.Linq;
using Scripts.Skills.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public static class ColdSlashVisualBuilder
    {
        private const string Root = "Assets/Resources/Skills/2HWeapon/Universal2H/Slash/";
        private const int FrameSize = 32;
        private const int FrameCount = 12;
        private const float Fps = 16;

        [MenuItem("Tools/Skills/Apply Refined Cold Slash VFX")]
        public static void Apply()
        {
            string path = Root + "ColdSlashRefined-Sheet.png";
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
                name = "ColdSlashRefined_" + i.ToString("D2"),
                rect = new Rect((i % 4) * FrameSize, (2 - i / 4) * FrameSize, FrameSize, FrameSize),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            if (frames.Length != FrameCount) throw new InvalidOperationException("Cold Slash frame import failed.");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "ColdSlashVFX.anim");
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Fps, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = FrameCount / Fps, value = frames[FrameCount - 1] });
            clip.frameRate = Fps;
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            var prefab = PrefabUtility.LoadPrefabContents(Root + "ColdSlashVFX.prefab");
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = frames[0];
                PrefabUtility.SaveAsPrefabAsset(prefab, Root + "ColdSlashVFX.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
        }

        public static void CaptureRuntimeViews()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Temporary Cold Slash verification");
            root.transform.position = new Vector3(2000, 0, 0);
            var camera = new GameObject("Cold Slash verification camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48;
            camera.aspect = 480f / 270;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.15f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(2000, 0, -10);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var target = new RenderTexture(480, 270, 24);
            var texture = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var module = root.AddComponent<SkillVFX>();
                var serialized = new SerializedObject(module);
                serialized.FindProperty("_vfxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "ColdSlashVFX.prefab");
                serialized.FindProperty("_offset").vector2Value = new Vector2(1.2f, 0);
                serialized.FindProperty("_attachToParent").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                const float lifetime = 0.35f;
                var right = module.PlayForLifetime(root.transform, 1, Vector2.one, lifetime, false, 0.5f, 0.5f, out _);
                var left = module.PlayForLifetime(root.transform, -1, Vector2.one, lifetime, false, 0.5f, 0.5f, out _);
                var actors = new[] { right, left };
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "ColdSlashVFX.anim");
                foreach (var actor in actors)
                {
                    actor.GetComponent<AutoDestroyVFX>().enabled = false;
                    var animator = actor.GetComponent<Animator>();
                    if (Mathf.Abs(animator.speed * lifetime - clip.length) > 0.001f)
                        throw new InvalidOperationException("Cold Slash does not fit the skill's VFX lifetime.");
                    animator.enabled = false;
                }
                target.Create();
                string folder = Path.Combine(Path.GetTempPath(), "codex-cold-slash-refresh");
                Directory.CreateDirectory(folder);
                for (int i = 0; i < FrameCount; i++)
                {
                    foreach (var actor in actors) clip.SampleAnimation(actor, i / Fps);
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "ColdSlash-" + i.ToString("D2") + ".png"), texture.EncodeToPNG());
                }
                Debug.Log("Cold Slash runtime renders captured, both facing directions and fitted lifetime: " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
