using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RelicKeeper.Editor.Hub
{
    public static class TavernNpcAnimationBuilder
    {
        public const string ArtPath = Hub20Builder.ArtRoot + "/Characters/TavernNpcIdle.png";
        public const string ClipPath = Hub20Builder.ArtRoot + "/Characters/TavernNpcIdle.anim";
        public const string ControllerPath = Hub20Builder.ArtRoot + "/Characters/TavernNpc.controller";
        public const string PrefabPath = "Assets/Prefabs/Spawner/TavernNPC.prefab";

        [MenuItem("Tools/Hub/Apply Tavern NPC Animation")]
        public static void Apply()
        {
            ApplyIdle(ArtPath, ClipPath, ControllerPath, PrefabPath, "TavernNpcIdle", 29, 0.13f);
        }

        public static void ApplyIdle(string artPath, string clipPath, string controllerPath,
            string prefabPath, string frameName, int frameCount, float frameDuration)
        {
            AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(artPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            // The canvas center places the opaque feet 12 pixels below the existing NPC root.
            importer.spritesheet = Enumerable.Range(0, frameCount).Select(i => new SpriteMetaData
            {
                name = frameName + "_" + i.ToString("D2"),
                rect = new Rect(i * 28, 0, 28, 28), alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.frameRate = 1f / frameDuration;
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i * frameDuration, value = sprite }).ToList();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault(x => x.state.name == "Idle").state;
            if (state == null) state = machine.AddState("Idle");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = frames[0];
                var animator = prefab.GetComponent<Animator>();
                if (animator == null) animator = prefab.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log($"{frameName} applied: {frames.Length} frames, {frameDuration * 1000} ms each, looping.");
        }
    }
}
