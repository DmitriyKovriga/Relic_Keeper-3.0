using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Hub;
using Scripts.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class HubFogTests
    {
        [Test]
        public void FogHasIndependentSlowLoopAndAnimatorCannotOverwriteIt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath);
            var fog = prefab.GetComponentInChildren<HubFogVisual>();
            Assert.That(fog, Is.Not.Null);
            var data = new SerializedObject(fog);
            int count = data.FindProperty("_frames").arraySize;
            float interval = data.FindProperty("_frameDuration").floatValue;
            Assert.That(count, Is.EqualTo(12));
            Assert.That(count * interval, Is.EqualTo(17.28f).Within(0.03f));
            Assert.That(data.FindProperty("_opacity").floatValue, Is.EqualTo(0.4f));
            var animator = prefab.GetComponentInChildren<Animator>();
            var clip = animator.runtimeAnimatorController.animationClips.First();
            string path = AnimationUtility.CalculateTransformPath(fog.transform, animator.transform);
            Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(x => x.path == path), Is.False);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(x => x.path == path), Is.False);
        }

        [Test]
        public void FramesCrossfadeSmoothlyAndWrapWithoutOpacityJump()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath));
            try
            {
                var fog = root.GetComponentInChildren<HubFogVisual>();
                var data = new SerializedObject(fog);
                var current = (SpriteRenderer)data.FindProperty("_current").objectReferenceValue;
                var next = (SpriteRenderer)data.FindProperty("_next").objectReferenceValue;
                var first = (Sprite)data.FindProperty("_frames").GetArrayElementAtIndex(0).objectReferenceValue;
                float interval = data.FindProperty("_frameDuration").floatValue;
                fog.Sample(0);
                Assert.That(current.sprite, Is.SameAs(first));
                Assert.That(current.color.a, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(next.color.a, Is.Zero);
                fog.Sample(interval / 2);
                Assert.That(current.color.a, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(next.color.a, Is.EqualTo(0.2f).Within(0.001f));
                fog.Sample(interval * data.FindProperty("_frames").arraySize);
                Assert.That(current.sprite, Is.SameAs(first));
                Assert.That(current.color.a, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(next.color.a, Is.Zero.Within(0.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ShaderFadesUpwardToFullyInvisibleAtTop()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var white = new Texture2D(2, 2);
            white.SetPixels(Enumerable.Repeat(Color.white, 4).ToArray());
            white.Apply();
            var sprite = Sprite.Create(white, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1);
            var material = new Material(Shader.Find("RelicKeeper/Hub Fog"));
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var go = new GameObject("Fog fade GPU test");
                SceneManager.MoveGameObjectToScene(go, scene);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.color = new Color(1, 1, 1, 0.4f);
                go.transform.position = new Vector3(0, 3.25f, 0);
                go.transform.localScale = new Vector3(4, 4, 1);
                var cameraObject = new GameObject("Fog test camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 4;
                camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.transform.position = new Vector3(0, 3.25f, -10);
                camera.allowHDR = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                image.Apply();
                Assert.That(image.GetPixel(32, 6).a, Is.EqualTo(0.4f).Within(0.02f));
                Assert.That(image.GetPixel(32, 32).a, Is.InRange(0.15f, 0.25f));
                Assert.That(image.GetPixel(32, 60).a, Is.Zero.Within(0.01f));
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(white);
            }
        }
    }
}
