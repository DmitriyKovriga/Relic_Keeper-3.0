using System;
using System.Reflection;
using NUnit.Framework;
using Scripts.Dungeon;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class DungeonBackgroundTests
    {
        [Test]
        public void RoomAlignmentCentersTheSourceArtworkWithoutStretchingIt()
        {
            using (var view = new BackgroundView())
            {
                view.Apply();
                Vector3 center = new Vector3(12, 8, 0);
                view.Align(center);
                Vector2 alignedSize = view.Renderer.size;
                view.Camera.transform.position = center + new Vector3(0, 0, -10);
                Texture2D aligned = view.Capture();
                Texture2D reference = null;
                try
                {
                    view.Renderer.size = view.Renderer.sprite.rect.size / view.Renderer.sprite.pixelsPerUnit;
                    reference = view.Capture();
                    Assert.That(aligned.GetPixels32(), Is.EqualTo(reference.GetPixels32()),
                        "The center of a large tiled backdrop must match one unscaled copy of the source artwork.");
                    view.Renderer.size = alignedSize;
                    view.Align(center);
                    Vector2 size = view.Renderer.size;
                    view.Align(center);
                    Assert.That(view.Renderer.size, Is.EqualTo(size), "Room re-entry must not grow the tiled area.");
                }
                finally
                {
                    Object.DestroyImmediate(aligned);
                    if (reference != null) Object.DestroyImmediate(reference);
                }
            }
        }

        [Test]
        public void DungeonZoomIsIndependentOfHubZoomAndRestoresOnReturn()
        {
            using (var view = new BackgroundView())
            {
                var cameraObject = view.NewObject("Location camera");
                var camera = cameraObject.AddComponent<CinemachineCamera>();
                camera.Lens.OrthographicSize = 5.625f;
                var confiner = cameraObject.AddComponent<CinemachineConfiner2D>();
                var hubBounds = view.NewObject("Hub camera bounds").AddComponent<BoxCollider2D>();
                hubBounds.size = new Vector2(65, 14.5f);
                confiner.BoundingShape2D = hubBounds;
                view.SetField("_cameraConfiner", confiner);
                var room = view.NewObject("Room").AddComponent<RoomController>();
                var roomBounds = view.NewObject("Room camera bounds").AddComponent<BoxCollider2D>();
                roomBounds.transform.SetParent(room.transform);
                roomBounds.size = new Vector2(32, 24);
                typeof(RoomController).GetField("_cameraBoundsSource", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(room, roomBounds);
                view.Call("ApplyRoomCameraBounds", room);
                Assert.That(camera.Lens.OrthographicSize, Is.EqualTo(7.5f));
                view.Call("RestoreHubCameraBounds");
                Assert.That(camera.Lens.OrthographicSize, Is.EqualTo(5.625f));
                Assert.That(confiner.BoundingShape2D, Is.SameAs(hubBounds));
            }
        }

        [Test]
        public void ParallaxAfterRoomEntryUsesRoomAnchorInsteadOfPreviousHubCamera()
        {
            using (var view = new BackgroundView())
            {
                var parallax = view.Renderer.gameObject.AddComponent<ParallaxEffect>();
                Vector3 center = new Vector3(12, 8, 0);
                view.Renderer.transform.position = new Vector3(-70, 90, 10);
                view.Camera.transform.position = new Vector3(-25, 40, -10);
                parallax.ResetAnchor(center + new Vector3(0, 0, 10), center);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ParallaxEffect).GetField("_cameraTransform", flags).SetValue(parallax, view.Camera.transform);
                view.Camera.transform.position = center + new Vector3(4, -2, -10);
                typeof(ParallaxEffect).GetMethod("LateUpdate", flags).Invoke(parallax, null);
                Assert.That(parallax.transform.position.x, Is.EqualTo(15.6f).Within(0.001f));
                Assert.That(parallax.transform.position.y, Is.EqualTo(6.2f).Within(0.001f));
                Assert.That(parallax.transform.position.z, Is.EqualTo(10f));
            }
        }

        [Test]
        public void BottomLeftPivotBackgroundIsCenteredAndRestoresHiddenHubBackdrop()
        {
            using (var view = new BackgroundView())
            {
                Sprite source = view.Dungeon.LoadBackgroundSprite();
                Vector2 originalPivot = source.pivot;
                view.Apply();
                Assert.That(view.Renderer.size, Is.EqualTo(new Vector2(160, 100)), "Assigning the dungeon sprite must preserve the configured tiled area.");
                Assert.That(view.Renderer.bounds.center.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(view.Renderer.bounds.center.y, Is.EqualTo(5f).Within(0.001f));
                Assert.That(view.Renderer.sprite, Is.SameAs(source));
                Assert.That(source.pivot, Is.EqualTo(originalPivot), "Do not modify the imported artwork.");
                view.Restore();
                Assert.That(view.Renderer.enabled, Is.False);
                Assert.That(view.Renderer.sprite, Is.Null);
                view.Apply();
                Assert.That(view.Renderer.enabled, Is.True, "Re-entry must restore the dungeon background.");
                Assert.That(view.Renderer.size, Is.EqualTo(new Vector2(160, 100)));
            }
        }

        [TestCase(0f, 5f)]
        [TestCase(-30f, -20f)]
        [TestCase(30f, 25f)]
        public void MortfallBackgroundCoversViewportOnBothSidesOfWorldOrigin(float x, float y)
        {
            using (var view = new BackgroundView())
            {
                view.Apply();
                view.Camera.transform.position = new Vector3(x, y, -10f);
                Texture2D image = view.Capture();
                try
                {
                    for (int py = 1; py < image.height; py += 30)
                    for (int px = 1; px < image.width; px += 40)
                    {
                        Color pixel = image.GetPixel(px, py);
                        Assert.That(pixel.r + pixel.g + pixel.b, Is.GreaterThan(0.01f),
                            $"Camera ({x}, {y}): uncovered background at pixel ({px}, {py}).");
                    }
                }
                finally { Object.DestroyImmediate(image); }
            }
        }

        private sealed class BackgroundView : IDisposable
        {
            private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly Scene _scene;
            private readonly DungeonController _controller;
            private readonly Material _material;
            public readonly SpriteRenderer Renderer;
            public readonly Camera Camera;
            public readonly DungeonDataSO Dungeon;

            public BackgroundView()
            {
                _scene = EditorSceneManager.NewPreviewScene();
                var controllerObject = NewObject("Background controller test");
                controllerObject.SetActive(false); // Avoid gameplay singleton/transition side effects.
                _controller = controllerObject.AddComponent<DungeonController>();
                Renderer = NewObject("DungeonSharedBackground").AddComponent<SpriteRenderer>();
                Renderer.transform.position = new Vector3(0, 5, 10);
                Renderer.drawMode = SpriteDrawMode.Tiled;
                Renderer.size = new Vector2(160, 100);
                Renderer.enabled = false;
                _material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                Renderer.sharedMaterial = _material;
                typeof(DungeonController).GetField("_sharedBackgroundRenderer", Private).SetValue(_controller, Renderer);
                Dungeon = Resources.Load<DungeonDataSO>("Dungeons/MortFallDungeonSO");
                Camera = NewObject("Background capture camera").AddComponent<Camera>();
                Camera.enabled = false;
                Camera.scene = _scene;
                Camera.orthographic = true;
                Camera.orthographicSize = 270f / 48f;
                Camera.aspect = 480f / 270f;
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = Color.black;
                Camera.allowHDR = false;
                Camera.allowMSAA = false;
                Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                Camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            }

            public GameObject NewObject(string name)
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, _scene);
                return go;
            }

            public void Apply() => typeof(DungeonController).GetMethod("ApplyDungeonBackground", Private).Invoke(_controller, new object[] { Dungeon });
            public void Restore() => typeof(DungeonController).GetMethod("RestoreHubBackground", Private).Invoke(_controller, null);
            public void Align(Vector3 center) => Call("AlignDungeonBackground", center);
            public void SetField(string name, object value) => typeof(DungeonController).GetField(name, Private).SetValue(_controller, value);
            public void Call(string name, params object[] args) => typeof(DungeonController).GetMethod(name, Private).Invoke(_controller, args);

            public Texture2D Capture()
            {
                var target = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
                try
                {
                    target.Create();
                    RenderPipeline.SubmitRenderRequest(Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    image.Apply();
                    return image;
                }
                catch { Object.DestroyImmediate(image); throw; }
                finally
                {
                    RenderTexture.active = previous;
                    target.Release();
                    Object.DestroyImmediate(target);
                }
            }

            public void Dispose()
            {
                EditorSceneManager.ClosePreviewScene(_scene);
                Object.DestroyImmediate(_material);
            }
        }
    }
}
