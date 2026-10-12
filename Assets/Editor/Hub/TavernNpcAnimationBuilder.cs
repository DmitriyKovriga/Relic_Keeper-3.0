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
            AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
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
            importer.spritesheet = Enumerable.Range(0, 29).Select(i => new SpriteMetaData
            {
                name = "TavernNpcIdle_" + i.ToString("D2"),
                rect = new Rect(i * 28, 0, 28, 28), alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(ArtPath).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
            clip.frameRate = 1000f / 130f;
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i * 0.13f, value = sprite }).ToList();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault(x => x.state.name == "Idle").state;
            if (state == null) state = machine.AddState("Idle");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = frames[0];
                var animator = prefab.GetComponent<Animator>();
                if (animator == null) animator = prefab.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("Tavern NPC animation applied: 29 frames, 130 ms each, looping.");
        }
    }
}
