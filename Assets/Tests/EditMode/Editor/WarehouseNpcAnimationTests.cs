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
    public class WarehouseNpcAnimationTests
    {
        [Test]
        public void IdleUsesAllGeneratedFramesAndKeepsStashInteraction()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseNpcAnimationBuilder.PrefabPath);
            Assert.That(prefab.GetComponent<HubServiceNpc>().Service, Is.EqualTo(HubService.Stash));
            var clip = prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
            Assert.That(clip.isLooping, Is.True);
            Assert.That(clip.length, Is.EqualTo(1.8f).Within(0.001f));
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(bindings.Length, Is.EqualTo(1));
            Assert.That(bindings[0].path, Is.Empty);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            Assert.That(keys.Length, Is.EqualTo(9));
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(keys[i].time, Is.EqualTo(i * 0.2f).Within(0.001f));
                Assert.That(keys[i].value.name, Is.EqualTo("WarehouseNpcIdle_" + i.ToString("D2")));
            }
        }

        [Test]
        public void NativeArtworkKeepsFeetPixelsFixedThroughoutLoop()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(WarehouseNpcAnimationBuilder.ArtPath));
                Assert.That(texture.width, Is.EqualTo(252));
                Assert.That(texture.height, Is.EqualTo(28));
                for (int frame = 1; frame < 9; frame++)
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 28; x++)
                            Assert.That(texture.GetPixel(frame * 28 + x, y), Is.EqualTo(texture.GetPixel(x, y)), $"frame {frame}, pixel {x},{y}");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void SampledPoseChangesWithoutMovingNpcRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseNpcAnimationBuilder.PrefabPath);
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
