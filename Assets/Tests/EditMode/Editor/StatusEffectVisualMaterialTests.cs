using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class StatusEffectVisualMaterialTests
    {
        [Test]
        public void HaloMaterial_IsIncludedThroughResources_WithDirectShaderDependency()
        {
            Material material = Resources.Load<Material>("Visuals/StatusAuraHalo");
            Assert.That(material, Is.Not.Null, "Runtime-created effects need a build-included material.");
            Assert.That(material.shader.name, Is.EqualTo("RelicKeeper/Sprites/Aura Halo"));
            Assert.That(material.shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(material)),
                Does.Contain("Assets/Shaders/RelicFX/AuraHalo.shader"));
        }

        [Test]
        public void Ignite_ReusesBuildIncludedMaterial_AcrossEnemiesAndReactivation()
        {
            var first = new GameObject("First burning enemy");
            var second = new GameObject("Second burning enemy");
            try
            {
                first.AddComponent<SpriteRenderer>(); second.AddComponent<SpriteRenderer>();
                var ignite = first.AddComponent<IgniteVisualController>();
                var otherIgnite = second.AddComponent<IgniteVisualController>();
                ignite.SetBurning(true); otherIgnite.SetBurning(true);
                Material material = Resources.Load<Material>("Visuals/StatusAuraHalo");
                var flames = first.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "Flame").ToArray();
                Assert.That(flames, Has.Length.EqualTo(3));
                Assert.That(flames.All(s => s.sharedMaterial == material), Is.True);
                Assert.That(second.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "Flame")
                    .All(s => s.sharedMaterial == material), Is.True);
                ignite.SetBurning(false);
                Assert.That(first.transform.Find("IgniteFlames").gameObject.activeSelf, Is.False);
                ignite.SetBurning(true);
                Assert.That(first.GetComponentsInChildren<SpriteRenderer>().Count(s => s.name == "Flame"), Is.EqualTo(3));
                Assert.That(flames.All(s => s.sharedMaterial == material), Is.True);
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        [Test]
        public void Aura_UsesOwnedCopy_WithoutChangingTheSharedFlameMaterial()
        {
            var root = new GameObject("Aura material ownership");
            Material owned = null;
            try
            {
                var aura = root.AddComponent<StatusAuraPresenter>();
                owned = (Material)typeof(StatusAuraPresenter).GetMethod("EnsureHaloMaterial",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(aura, null);
                var source = Resources.Load<Material>("Visuals/StatusAuraHalo");
                Assert.That(owned, Is.Not.Null);
                Assert.That(owned, Is.Not.SameAs(source));
                Assert.That(owned.shader, Is.EqualTo(source.shader));
                owned.SetColor("_RendererColor", Color.red);
                Assert.That(source.GetColor("_RendererColor"), Is.EqualTo(Color.white));
            }
            finally
            {
                // Do not run the presenter's Play Mode Destroy path in an EditMode test.
                if (root != null)
                {
                    var aura = root.GetComponent<StatusAuraPresenter>();
                    typeof(StatusAuraPresenter).GetField("_haloMaterial", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(aura, null);
                    Object.DestroyImmediate(root);
                }
                if (owned != null) Object.DestroyImmediate(owned);
            }
        }
    }
}
