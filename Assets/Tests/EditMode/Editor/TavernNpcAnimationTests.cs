using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Hub;
using Scripts.Hub;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class TavernNpcAnimationTests
    {
        [Test]
        public void PrefabUsesCompleteNativeLoopAndRetainsTavernService()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TavernNpcAnimationBuilder.PrefabPath);
            Assert.That(prefab.GetComponent<HubServiceNpc>().Service, Is.EqualTo(HubService.Tavern));
            var clip = prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
            Assert.That(clip.isLooping, Is.True);
            Assert.That(clip.length, Is.EqualTo(3.77f).Within(0.001f));
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(bindings.Length, Is.EqualTo(1));
            Assert.That(bindings[0].path, Is.Empty);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            Assert.That(keys.Length, Is.EqualTo(29));
            Assert.That(keys.Take(29).Select(k => k.value).Distinct().Count(), Is.EqualTo(29));
            for (int i = 0; i < keys.Length; i++) Assert.That(keys[i].time, Is.EqualTo(i * 0.13f).Within(0.001f));
        }

        [Test]
        public void SamplingChangesPoseWithoutMovingFeetOrRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TavernNpcAnimationBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.transform.position = new Vector3(0, 0.5f, 0);
                var renderer = instance.GetComponent<SpriteRenderer>();
                var clip = instance.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
                clip.SampleAnimation(instance, 0);
                var first = renderer.sprite;
                clip.SampleAnimation(instance, 0.55f);
                Assert.That(renderer.sprite, Is.Not.SameAs(first));
                Assert.That(instance.transform.position, Is.EqualTo(new Vector3(0, 0.5f, 0)));
                Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(24));
                Assert.That(renderer.sprite.pivot, Is.EqualTo(new Vector2(14, 14)));
                Assert.That(renderer.bounds.min.y + 2f / 24f, Is.EqualTo(0).Within(0.001f));
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
