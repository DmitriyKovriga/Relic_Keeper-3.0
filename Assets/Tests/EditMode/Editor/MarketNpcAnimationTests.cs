using System.IO;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Hub;
using Scripts.Hub;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class MarketNpcAnimationTests
    {
        [Test]
        public void IdleUsesAllGeneratedFramesAndKeepsMarketInteraction()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarketNpcAnimationBuilder.PrefabPath);
            Assert.That(prefab.GetComponent<HubServiceNpc>().Service, Is.EqualTo(HubService.Market));
            Assert.That(prefab.transform.Find("WorldLabel").localPosition.y, Is.EqualTo(0.5f));
            var clip = prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
            Assert.That(clip.isLooping, Is.True);
            Assert.That(clip.length, Is.EqualTo(1.5f).Within(0.001f));
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(bindings.Length, Is.EqualTo(1));
            Assert.That(bindings[0].path, Is.Empty);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            Assert.That(keys.Length, Is.EqualTo(5));
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(keys[i].time, Is.EqualTo(i * 0.3f).Within(0.001f));
                Assert.That(keys[i].value.name, Is.EqualTo("MarketNpcIdle_" + i.ToString("D2")));
            }
        }

        [Test]
        public void NativeArtworkKeepsFeetPixelsFixedThroughoutLoop()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(MarketNpcAnimationBuilder.ArtPath));
                Assert.That(texture.width, Is.EqualTo(140));
                Assert.That(texture.height, Is.EqualTo(28));
                for (int frame = 1; frame < 5; frame++)
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 28; x++)
                            Assert.That(texture.GetPixel(frame * 28 + x, y), Is.EqualTo(texture.GetPixel(x, y)), $"frame {frame}, pixel {x},{y}");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MerchantFitsTheExistingSmallNpcScaleInEveryFrame()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(MarketNpcAnimationBuilder.ArtPath));
                for (int frame = 0; frame < MarketNpcAnimationBuilder.FrameCount; frame++)
                {
                    int bottom = 28, top = -1;
                    for (int y = 0; y < 28; y++)
                        for (int x = 0; x < 28; x++)
                            if (texture.GetPixel(frame * 28 + x, y).a > 0.5f)
                            {
                                bottom = Mathf.Min(bottom, y);
                                top = Mathf.Max(top, y);
                            }
                    Assert.That(bottom, Is.EqualTo(2), $"frame {frame}: feet must meet the ground");
                    Assert.That(top - bottom + 1, Is.InRange(20, 24), $"frame {frame}: match the 22px tavern and 24px warehouse NPCs");
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void SampledPoseChangesWithoutMovingNpcRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarketNpcAnimationBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.transform.position = new Vector3(-11, 0.5f, 0);
                var renderer = instance.GetComponent<SpriteRenderer>();
                var clip = instance.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
                clip.SampleAnimation(instance, 0);
                var first = renderer.sprite;
                clip.SampleAnimation(instance, 0.45f);
                Assert.That(renderer.sprite, Is.Not.SameAs(first));
                Assert.That(instance.transform.position, Is.EqualTo(new Vector3(-11, 0.5f, 0)));
                Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(24));
                Assert.That(renderer.sprite.pivot, Is.EqualTo(new Vector2(14, 14)));
                Assert.That(renderer.bounds.min.y + 2f / 24f, Is.EqualTo(0).Within(0.001f));
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
