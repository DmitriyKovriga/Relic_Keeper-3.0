using System;
using System.IO;
using System.Linq;
using Scripts.Hub;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Hub
{
    public static class WarehouseNpcAnimationBuilder
    {
        public const string ArtPath = Hub20Builder.ArtRoot + "/Characters/WarehouseNpcIdle.png";
        public const string ClipPath = Hub20Builder.ArtRoot + "/Characters/WarehouseNpcIdle.anim";
        public const string ControllerPath = Hub20Builder.ArtRoot + "/Characters/WarehouseNpc.controller";
        public const string PrefabPath = "Assets/Prefabs/Spawner/StashNPC.prefab";
        public const int FrameCount = 9;
        public const float FrameDuration = 0.2f;

        [MenuItem("Tools/Hub/Apply Warehouse NPC Animation")]
        public static void Apply()
        {
            TavernNpcAnimationBuilder.ApplyIdle(ArtPath, ClipPath, ControllerPath, PrefabPath,
                "WarehouseNpcIdle", FrameCount, FrameDuration);
        }

        public static void CaptureRuntimeViews()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var npc = Object.FindObjectsByType<HubServiceNpc>(FindObjectsSortMode.None).Single(x => x.Service == HubService.Stash);
            var animator = npc.GetComponent<Animator>();
            var clip = animator.runtimeAnimatorController.animationClips.Single();
            var original = npc.GetComponent<SpriteRenderer>().sprite;
            var go = new GameObject("Warehouse animation verification camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            camera.scene = npc.gameObject.scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48f;
            camera.aspect = 480f / 270f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            camera.transform.position = new Vector3(npc.transform.position.x, 3.4166665f, -10f);
            var target = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            string folder = Path.Combine(Path.GetTempPath(), "codex-warehouse-npc");
            Directory.CreateDirectory(folder);
            try
            {
                target.Create();
                for (int i = 0; i < FrameCount; i++)
                {
                    clip.SampleAnimation(npc.gameObject, i * FrameDuration + 0.01f);
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "warehouse-" + i + ".png"), texture.EncodeToPNG());
                }
                Debug.Log("Warehouse animation views saved to " + folder + "; Animator state: " + animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            }
            finally
            {
                npc.GetComponent<SpriteRenderer>().sprite = original;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(go);
            }
        }
    }
}
