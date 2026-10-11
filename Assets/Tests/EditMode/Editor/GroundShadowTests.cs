using System;
using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;
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
        public void OvalSitsOnSurfaceInsteadOfHangingBelowItsTopEdge()
        {
            using (var actor = new ShadowActor())
            {
                actor.Root.transform.position = new Vector3(1000, 0.55f);
                actor.UpdateShadow();
                Assert.That(actor.Shadow.enabled, Is.True);
                Assert.That(actor.Shadow.bounds.center.y, Is.GreaterThan(0f), "Most of the oval must stay on the walking surface, behind the feet.");
                Assert.That(actor.Shadow.bounds.min.y, Is.GreaterThan(-0.12f), "Only the configured contact overlap may extend below the surface.");
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

        private sealed class ShadowActor : IDisposable
        {
            private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            public readonly GameObject Root;
            public readonly SpriteRenderer Visual;
            public readonly SpriteRenderer Shadow;
            private readonly GameObject _floor;
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
