using System;
using System.Reflection;
using NUnit.Framework;
using Scripts.Dungeon;
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

            private GameObject NewObject(string name)
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, _scene);
                return go;
            }

            public void Apply() => typeof(DungeonController).GetMethod("ApplyDungeonBackground", Private).Invoke(_controller, new object[] { Dungeon });
            public void Restore() => typeof(DungeonController).GetMethod("RestoreHubBackground", Private).Invoke(_controller, null);

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
