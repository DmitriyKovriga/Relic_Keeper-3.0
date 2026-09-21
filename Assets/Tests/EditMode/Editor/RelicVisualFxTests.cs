using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using Scripts.Visuals.SpriteFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class RelicVisualFxTests
    {
        [Test]
        public void NeutralProfileSkipsPass_AndSanitizeClampsUnsafeValues()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            try
            {
                Assert.That(profile.HasVisibleEffect, Is.False);
                profile.revealRadius = -1; profile.revealSoftness = 0; profile.pixelsPerUnit = 0;
                profile.referenceResolution = Vector2Int.zero; profile.darkness = 2; profile.fogOpacity = -1;
                profile.Sanitize();
                Assert.That(profile.revealRadius, Is.Zero);
                Assert.That(profile.revealSoftness, Is.GreaterThan(0));
                Assert.That(profile.pixelsPerUnit, Is.EqualTo(1));
                Assert.That(profile.referenceResolution, Is.EqualTo(Vector2Int.one));
                Assert.That(profile.darkness, Is.EqualTo(1));
                Assert.That(profile.fogOpacity, Is.Zero);
                Assert.That(profile.HasVisibleEffect, Is.True);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void RevealHasClearCoreSmoothFalloff_AndTranslationInvariance()
        {
            Assert.That(AtmosphereProfileSO.EvaluateReveal(Vector2.zero, Vector2.zero, 2, 2, 1), Is.EqualTo(1));
            Assert.That(AtmosphereProfileSO.EvaluateReveal(new Vector2(2, 0), Vector2.zero, 2, 2, 1), Is.EqualTo(1));
            Assert.That(AtmosphereProfileSO.EvaluateReveal(new Vector2(3, 0), Vector2.zero, 2, 2, 1), Is.EqualTo(0.5f).Within(0.001));
            Assert.That(AtmosphereProfileSO.EvaluateReveal(new Vector2(4, 0), Vector2.zero, 2, 2, 1), Is.Zero);
            Assert.That(AtmosphereProfileSO.EvaluateReveal(new Vector2(103, -10), new Vector2(100, -10), 2, 2, 0.8f), Is.EqualTo(0.4f).Within(0.001));
        }

        [Test]
        public void PixelSnapIsStableWithinCellIncludingNegativeCoordinates()
        {
            Assert.That(AtmosphereProfileSO.SnapToPixel(new Vector2(-0.01f, 0.01f), 24),
                Is.EqualTo(AtmosphereProfileSO.SnapToPixel(new Vector2(-0.02f, 0.02f), 24)));
        }

        [Test]
        public void MissingOrDestroyedPlayerDisablesReveal_AndControllersAreCameraLocal()
        {
            var a = new GameObject("Camera A", typeof(Camera));
            var b = new GameObject("Camera B", typeof(Camera));
            var player = new GameObject("Player");
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            try
            {
                var first = a.AddComponent<AtmosphereController>(); first.Profile = profile;
                var second = b.AddComponent<AtmosphereController>(); second.Profile = profile;
                Assert.That(first.RevealParameters.w, Is.Zero);
                first.BindPlayer(player.transform);
                Assert.That(first.RevealParameters.w, Is.EqualTo(1));
                Assert.That(second.RevealParameters.w, Is.Zero);
                Object.DestroyImmediate(player);
                Assert.That(first.RevealParameters.w, Is.Zero);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(profile); if (player != null) Object.DestroyImmediate(player); }
        }

        [Test]
        public void SpriteControllerPreservesForeignMpb_FadeAlias_AndSharedMaterial()
        {
            var go = new GameObject("FX test", typeof(SpriteRenderer));
            var material = new Material(Shader.Find(RelicVisualTools.SpriteShaderName));
            try
            {
                var renderer = go.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_ForeignProperty", 42);
                block.SetColor("_RendererColor", new Color(1, 1, 1, 0.37f));
                renderer.SetPropertyBlock(block);
                var controller = go.AddComponent<SpriteFxController>();
                controller.Flash(Color.red); controller.SetEmission(Color.cyan, 2); controller.SetDissolve(2); controller.SetFade(-1);
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ForeignProperty"), Is.EqualTo(42));
                Assert.That(block.GetColor("_RendererColor").a, Is.EqualTo(0.37f));
                Assert.That(block.GetFloat("_FxRuntimeFlash"), Is.EqualTo(1));
                Assert.That(controller.DissolveAmount, Is.EqualTo(1));
                Assert.That(controller.Fade, Is.Zero);
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                controller.enabled = false;
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_FxRuntimeFlash"), Is.Zero);
                Assert.That(block.GetFloat("_FxRuntimeFade"), Is.EqualTo(1));
                Assert.That(block.GetFloat("_ForeignProperty"), Is.EqualTo(42));
                Assert.That(block.GetColor("_RendererColor").a, Is.EqualTo(0.37f));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(material); }
        }

        [Test]
        public void InstallerIsIdempotentOnIsolatedRendererAsset()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/RelicFxRendererTest.asset");
            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, path);
            try
            {
                var first = RelicVisualTools.InstallFeature(renderer);
                var second = RelicVisualTools.InstallFeature(renderer);
                Assert.That(second, Is.SameAs(first));
                Assert.That(renderer.rendererFeatures.Count, Is.EqualTo(1));
                var serialized = new SerializedObject(renderer);
                Assert.That(serialized.FindProperty("m_RendererFeatureMap").arraySize, Is.EqualTo(1));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void UiValidationAcceptsOverlay_AndRejectsCameraSpaceCanvas()
        {
            var cameraObject = new GameObject("UI test camera", typeof(Camera));
            var canvasObject = new GameObject("UI test canvas", typeof(Canvas));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                Assert.That(RelicVisualTools.FindUnsupportedCanvas(camera), Is.SameAs(canvas));
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Assert.That(RelicVisualTools.FindUnsupportedCanvas(camera), Is.Not.SameAs(canvas));
            }
            finally { Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void InstalledAssetsHaveNeutralDefaults_AndFeatureIsUniqueWithShaderReference()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(RelicVisualTools.ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.HasVisibleEffect, Is.False);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
            int count = 0;
            foreach (var feature in renderer.rendererFeatures)
                if (feature is RelicAtmosphereFeature)
                {
                    count++;
                    Assert.That(new SerializedObject(feature).FindProperty("_shader").objectReferenceValue, Is.Not.Null);
                }
            Assert.That(count, Is.EqualTo(1));
            Assert.That(ShaderUtil.ShaderHasError(Shader.Find(RelicVisualTools.SpriteShaderName)), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(Shader.Find("Hidden/RelicKeeper/Atmosphere")), Is.False);
        }
    }
}
