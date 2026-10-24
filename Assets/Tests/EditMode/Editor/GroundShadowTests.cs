using System;
using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class GroundShadowTests
    {
        [Test]
        public void GroundedShadowUsesFloorInsteadOfAnimationOutline()
        {
            using (var actor = new ShadowActor())
            {
                actor.Root.transform.position = new Vector3(1000, 0.55f);
                actor.Visual.transform.localPosition = new Vector3(0, -0.3f);
                Assert.That(actor.Anchor().y, Is.EqualTo(0f).Within(0.001f));
                actor.Visual.transform.localPosition = new Vector3(0, 0.15f);
                Assert.That(actor.Anchor().y, Is.EqualTo(0f).Within(0.001f), "Changing the pose must not move the floor contact.");
            }
        }

        [Test]
        public void AirborneShadowIgnoresPlatformsAboveTheFeet()
        {
            using (var actor = new ShadowActor())
            {
                actor.Root.transform.position = new Vector3(1000, 3f);
                var overhead = new GameObject("Platform above feet");
                try
                {
                    overhead.layer = LayerMask.NameToLayer("Ground");
                    overhead.transform.position = new Vector3(1000, 3.3f);
                    overhead.AddComponent<BoxCollider2D>().size = new Vector2(3, 0.2f);
                    Assert.That(actor.Anchor().y, Is.EqualTo(0f).Within(0.001f));
                }
                finally { Object.DestroyImmediate(overhead); }
            }
        }

        [Test]
        public void OvalStraddlesTheSurfaceWithoutSinkingIntoTheFloor()
        {
            using (var actor = new ShadowActor())
            {
                actor.Root.transform.position = new Vector3(1000, 0.55f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                Assert.That(actor.Shadow.bounds.center.y, Is.InRange(-0.05f, 0.05f));
                Assert.That(actor.Shadow.bounds.min.y, Is.GreaterThan(-0.15f));
                Assert.That(actor.Shadow.bounds.size.y, Is.GreaterThanOrEqualTo(3.9f / 24f));
            }
        }

        [TestCase("Default")]
        [TestCase("Background")]
        public void RenderedShadowDarkensOpaqueTerrainAroundTheFeet(string terrainLayer)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            using (var actor = new ShadowActor())
            {
                var scene = EditorSceneManager.NewPreviewScene();
                var target = new RenderTexture(480, 270, 24);
                var readback = new Texture2D(480, 270, TextureFormat.RGB24, false);
                var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                var pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
                var previous = RenderTexture.active;
                try
                {
                    actor.UpdateShadow();
                    actor.Visual.sprite = pixel;
                    actor.Visual.transform.localScale = new Vector3(0.5f, 1f, 1f);
                    actor.Visual.color = Color.red;
                    actor.Visual.sharedMaterial = material;
                    actor.Visual.sortingLayerName = WorldRenderSorting.LayerHero;
                    SceneManager.MoveGameObjectToScene(actor.Root, scene);
                    SceneManager.MoveGameObjectToScene(actor.Shadow.gameObject, scene);
                    var floor = new GameObject("Opaque rendered floor").AddComponent<SpriteRenderer>();
                    SceneManager.MoveGameObjectToScene(floor.gameObject, scene);
                    floor.sprite = pixel; floor.sharedMaterial = material;
                    floor.sortingLayerName = terrainLayer; floor.sortingOrder = 3;
                    floor.transform.position = new Vector3(1000, -0.5f);
                    floor.transform.localScale = new Vector3(6, 1, 1);
                    floor.color = Color.white;
                    var camera = new GameObject("Contact pixel test camera").AddComponent<Camera>();
                    SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                    camera.scene = scene; camera.enabled = false; camera.orthographic = true;
                    camera.orthographicSize = 5.625f; camera.aspect = 480f / 270f;
                    camera.transform.position = new Vector3(1000, 0, -10);
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
                    target.Create();
                    Color32[] Render(bool shadow)
                    {
                        actor.Shadow.enabled = shadow;
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target;
                        readback.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); readback.Apply();
                        return readback.GetPixels32();
                    }
                    var baseline = Render(false); var shaded = Render(true);
                    int visibleFloorPixels = 0;
                    for (int y = 131; y < 135; y++)
                        for (int x = 222; x < 258; x++)
                        {
                            int index = y * 480 + x;
                            if (baseline[index].r - shaded[index].r >= 15) visibleFloorPixels++;
                        }
                    Assert.That(visibleFloorPixels, Is.GreaterThanOrEqualTo(6), "The actual draw must darken terrain pixels, not merely enable a hidden renderer.");
                }
                finally
                {
                    RenderTexture.active = previous;
                    // Return fixture-owned objects before closing the temporary camera scene.
                    SceneManager.MoveGameObjectToScene(actor.Root, actor.Floor.scene);
                    SceneManager.MoveGameObjectToScene(actor.Shadow.gameObject, actor.Floor.scene);
                    EditorSceneManager.ClosePreviewScene(scene);
                    Object.DestroyImmediate(target); Object.DestroyImmediate(readback);
                    Object.DestroyImmediate(material); Object.DestroyImmediate(pixel);
                }
            }
        }

        [Test]
        public void ShadowHidesWhenThereIsNoGroundBelow()
        {
            using (var actor = new ShadowActor())
            {
                actor.Root.transform.position = new Vector3(1000, 20f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.False);
            }
        }

        [Test]
        public void JumpHeightWeakensAndShrinksContact_AndDistantFloorsHaveNoShadow()
        {
            using (var actor = new ShadowActor())
            {
                actor.UpdateShadow();
                float groundedAlpha = actor.Shadow.color.a;
                float groundedWidth = actor.Shadow.bounds.size.x;
                actor.Root.transform.position = new Vector3(1000, 2f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                Assert.That(actor.Shadow.color.a, Is.LessThan(groundedAlpha * 0.5f));
                Assert.That(actor.Shadow.bounds.size.x, Is.LessThan(groundedWidth));
                actor.Root.transform.position = new Vector3(1000, 5f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.False, "A floor several units below must not carry a detached contact shadow.");
            }
        }

        [Test]
        public void PlatformEdgeClipsTheOvalInsteadOfProjectingOntoTheLowerFloor()
        {
            using (var actor = new ShadowActor())
            {
                actor.Root.transform.position = new Vector3(1002.9f, 0.55f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                Assert.That(actor.Shadow.bounds.max.x, Is.LessThanOrEqualTo(1003.01f));
                Assert.That(actor.Shadow.bounds.center.x, Is.LessThan(actor.Root.transform.position.x));
            }
        }

        [Test]
        public void ContactFollowsTheSupportingSlope()
        {
            using (var actor = new ShadowActor())
            {
                actor.Floor.transform.rotation = Quaternion.Euler(0, 0, 15);
                actor.Root.transform.position = new Vector3(1000, 0.7f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                Assert.That(Quaternion.Angle(actor.Shadow.transform.rotation, actor.Floor.transform.rotation), Is.LessThan(0.1f));
            }
        }

        [Test]
        public void PartiallySupportedFeetKeepContactEvenWithTheBodyCentrePastTheLedge()
        {
            using (var actor = new ShadowActor())
            {
                actor.Root.transform.position = new Vector3(1003.1f, 0.55f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                Assert.That(actor.Shadow.bounds.max.x, Is.LessThanOrEqualTo(1003.01f));
                Assert.That(actor.Anchor().y, Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void TriggerVolumesDoNotActAsShadowSurfaces()
        {
            using (var actor = new ShadowActor())
            {
                var trigger = new GameObject("Ground trigger");
                try
                {
                    trigger.layer = LayerMask.NameToLayer("Ground");
                    trigger.transform.position = new Vector3(1000, 1f);
                    var collider = trigger.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(4, 0.2f); collider.isTrigger = true;
                    actor.Root.transform.position = new Vector3(1000, 2f);
                    Assert.That(actor.Anchor().y, Is.EqualTo(0f).Within(0.001f));
                }
                finally { Object.DestroyImmediate(trigger); }
            }
        }

        [Test]
        public void OneWayPlatformSupportsContact()
        {
            using (var actor = new ShadowActor())
            {
                actor.Floor.layer = LayerMask.NameToLayer("OneWayPlatform");
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                Assert.That(actor.Anchor().y, Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void DisablingAnActorHidesItsWorldSpaceShadow()
        {
            using (var actor = new ShadowActor())
            {
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                actor.Root.SetActive(false);
                actor.NotifyDisabled(); // Gameplay callbacks are not driven automatically in EditMode.
                Assert.That(actor.Shadow.enabled, Is.False);
            }
        }

        private sealed class ShadowActor : IDisposable
        {
            private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            public readonly GameObject Root;
            public readonly SpriteRenderer Visual;
            public readonly SpriteRenderer Shadow;
            private readonly GameObject _floor;
            public GameObject Floor => _floor;
            private readonly GroundingVisualController _controller;
            private readonly Texture2D _texture;
            private readonly Sprite _sprite;

            public ShadowActor()
            {
                _floor = new GameObject("Shadow test floor");
                _floor.layer = LayerMask.NameToLayer("Ground");
                _floor.transform.position = new Vector3(1000, -0.5f);
                _floor.AddComponent<BoxCollider2D>().size = new Vector2(6, 1);
                Root = new GameObject("Shadow test actor");
                Root.transform.position = new Vector3(1000, 0.55f);
                Root.AddComponent<BoxCollider2D>().size = Vector2.one;
                var visualObject = new GameObject("Visual");
                visualObject.transform.SetParent(Root.transform, false);
                Visual = visualObject.AddComponent<SpriteRenderer>();
                _texture = new Texture2D(24, 24);
                _sprite = Sprite.Create(_texture, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 24, 0, SpriteMeshType.FullRect);
                Visual.sprite = _sprite;
                _controller = Root.AddComponent<GroundingVisualController>();
                _controller.enabled = false;
                // EditMode does not invoke gameplay Awake automatically.
                Call("Awake");
                Shadow = (SpriteRenderer)typeof(GroundingVisualController).GetField("_shadowRenderer", Private).GetValue(_controller);
            }

            private object Call(string name, params object[] args) => typeof(GroundingVisualController).GetMethod(name, Private).Invoke(_controller, args);
            public Vector2 Anchor()
            {
                Physics2D.SyncTransforms();
                object[] args = { Vector2.zero, 0f };
                Assert.That((bool)Call("TryResolveGroundAnchor", args), Is.True);
                return (Vector2)args[0];
            }
            public void UpdateShadow() { Physics2D.SyncTransforms(); Call("UpdateShadowVisual"); }
            public void NotifyDisabled() => Call("OnDisable");
            public void Dispose()
            {
                Object.DestroyImmediate(Shadow.gameObject);
                typeof(GroundingVisualController).GetField("_shadowObject", Private).SetValue(_controller, null);
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(_floor);
                Object.DestroyImmediate(_sprite);
                Object.DestroyImmediate(_texture);
                Physics2D.SyncTransforms();
            }
        }
    }
}
