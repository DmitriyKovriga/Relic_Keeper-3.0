using System.IO;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using Scripts.Visuals.SpriteFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RelicKeeper.Tests.EditMode
{
    // Actual GPU renders at the project's logical resolution. No changes to open gameplay scenes.
    public sealed class RelicVisualFxRenderTests
    {
        private Scene _scene;
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _readback, _spriteTexture;
        private Sprite _sprite;
        private Material _material, _stock;
        private AtmosphereProfileSO _profile;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "GPU required; do not run with -nographics.");
            _scene = EditorSceneManager.NewPreviewScene();
            _camera = new GameObject("Relic render test").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(_camera.gameObject, _scene);
            _camera.scene = _scene;
            _camera.transform.position = new Vector3(0, 0, -10);
            _camera.orthographic = true; _camera.orthographicSize = 5.625f;
            _camera.aspect = 480f / 270f;
            _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.white;
            _camera.allowMSAA = false; _camera.enabled = false;
            _target = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _target.Create();
            _readback = new Texture2D(480, 270, TextureFormat.RGBA32, false, true);
            _material = new Material(Shader.Find(RelicVisualTools.SpriteShaderName));
            _stock = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            _profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_target); Object.DestroyImmediate(_readback);
            Object.DestroyImmediate(_material); Object.DestroyImmediate(_stock); Object.DestroyImmediate(_profile);
            if (_sprite != null) Object.DestroyImmediate(_sprite);
            if (_spriteTexture != null) Object.DestroyImmediate(_spriteTexture);
        }

        private void Render()
        {
            RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _target });
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                _readback.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); _readback.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        private SpriteRenderer AddSprite()
        {
            _spriteTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            var pixels = new Color[1024];
            for (int y = 8; y < 24; y++)
                for (int x = 8; x < 24; x++) pixels[y * 32 + x] = x < 16 ? Color.red : Color.green;
            _spriteTexture.SetPixels(pixels); _spriteTexture.Apply();
            _sprite = Sprite.Create(_spriteTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 24, 0, SpriteMeshType.FullRect);
            var renderer = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            SceneManager.MoveGameObjectToScene(renderer.gameObject, _scene);
            renderer.sprite = _sprite; renderer.sharedMaterial = _material;
            renderer.gameObject.AddComponent<SpriteFxController>();
            return renderer;
        }

        [Test]
        public void NeutralMatchesStockSprite_ColorFlipAndSingleLegacyFadeWork()
        {
            _camera.backgroundColor = Color.black;
            var renderer = AddSprite(); renderer.color = new Color(0.7f, 0.8f, 0.9f, 0.6f);
            renderer.sharedMaterial = _stock; Render(); Color expected = _readback.GetPixel(236, 135);
            renderer.sharedMaterial = _material; Render(); Color actual = _readback.GetPixel(236, 135);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.015f), "Neutral must match URP sprite including Renderer.color.");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.015f));
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            block.SetColor("_RendererColor", renderer.color); renderer.SetPropertyBlock(block);
            Render(); Assert.That(_readback.GetPixel(236, 135).r, Is.EqualTo(actual.r).Within(0.015f), "AutoDestroy writes the same alpha twice; shader must apply it once.");
            renderer.flipX = true; Render();
            Assert.That(_readback.GetPixel(236, 135).g, Is.GreaterThan(0.1f));
            renderer.GetComponent<SpriteFxController>().SetFade(0); Render();
            Assert.That(_readback.GetPixel(236, 135).maxColorComponent, Is.LessThan(0.01f));
        }

        [Test]
        public void OuterOutlineUsesTransparentPadding_AndDissolveOneRemovesEverything()
        {
            _camera.backgroundColor = Color.black;
            var renderer = AddSprite();
            Render(); Assert.That(_readback.GetPixel(231, 135).r, Is.LessThan(0.01f));
            _material.SetFloat("_FxOutline", 1); _material.SetColor("_FxOutlineColor", Color.blue);
            Render(); Assert.That(_readback.GetPixel(231, 135).b, Is.GreaterThan(0.9f));
            renderer.GetComponent<SpriteFxController>().SetDissolve(1); Render();
            Assert.That(_readback.GetPixel(231, 135).b, Is.LessThan(0.01f));
            Assert.That(_readback.GetPixel(236, 135).r, Is.LessThan(0.01f));
        }

        [Test]
        public void SpriteRectBoundsPreventNeighbourBleed_AfterAnimatedSpriteChange()
        {
            _camera.backgroundColor = Color.black;
            var renderer = AddSprite();
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++)
                for (int x = 16; x < 32; x++) pixels[y * 32 + x] = Color.white;
            _spriteTexture.SetPixels(pixels); _spriteTexture.Apply();
            Object.DestroyImmediate(_sprite);
            _sprite = Sprite.Create(_spriteTexture, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0.5f), 24, 0, SpriteMeshType.FullRect);
            renderer.sprite = _sprite;
            renderer.GetComponent<SpriteFxController>().SendMessage("LateUpdate");
            _material.SetFloat("_FxOutline", 1); _material.SetFloat("_FxOutlineWidth", 4); _material.SetFloat("_FxQuality", 2);
            Render();
            Assert.That(_readback.GetPixel(247, 135).r, Is.LessThan(0.01f), "Opaque adjacent sprite must not create outline in a transparent atlas subrect.");
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Assert.That(block.GetVector("_FxUvRect").z, Is.EqualTo(0.5f));
        }

        [Test]
        public void AtmosphereQualityModesRender_AndSecondCameraDoesNotInheritReveal()
        {
            var controller = _camera.gameObject.AddComponent<AtmosphereController>(); controller.Profile = _profile;
            _profile.fogOpacity = 0.7f; _profile.fogTint = Color.blue; _profile.noiseAmount = 1;
            _profile.driftSpeed = Vector2.zero;
            for (int quality = 0; quality <= 2; quality++)
            {
                _profile.quality = (AtmosphereQuality)quality; Render();
                Color color = _readback.GetPixel(120, 190);
                Assert.That(color.b, Is.GreaterThan(color.r + 0.05f));
                Assert.That(float.IsNaN(color.r), Is.False);
            }
            var otherObject = new GameObject("Unaffected camera");
            SceneManager.MoveGameObjectToScene(otherObject, _scene);
            var other = otherObject.AddComponent<Camera>(); other.CopyFrom(_camera); other.scene = _scene;
            var original = _camera; _camera = other;
            Render(); Assert.That(_readback.GetPixel(120, 190).r, Is.GreaterThan(0.95f));
            _camera = original;
        }

        [Test]
        public void AtmosphereRevealsOffCenterPlayer_AcrossCameraTranslationAndRotation()
        {
            var controller = _camera.gameObject.AddComponent<AtmosphereController>(); controller.Profile = _profile;
            Render(); Color neutral = _readback.GetPixel(120, 190);
            Assert.That(neutral.r, Is.GreaterThan(0.95f));
            var player = new GameObject("Reveal target"); player.transform.position = new Vector3(-5f, 2.3f, 0);
            SceneManager.MoveGameObjectToScene(player, _scene);
            controller.BindPlayer(player.transform);
            _profile.darkness = 0.8f; _profile.revealRadius = 0.8f; _profile.revealSoftness = 0.5f; _profile.noiseAmount = 0;
            Render();
            Assert.That(_readback.GetPixel(120, 190).r, Is.GreaterThan(0.9f), "Reveal should be at the player, including correct vertical orientation.");
            Assert.That(_readback.GetPixel(360, 80).r, Is.LessThan(0.65f), "Outside reveal must darken.");
            string directory = Path.GetFullPath("Logs/RelicFX"); Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "reveal-480x270.png"), _readback.EncodeToPNG());
            _camera.transform.position += new Vector3(12, -8, 0); player.transform.position += new Vector3(12, -8, 0);
            Render(); Assert.That(_readback.GetPixel(120, 190).r, Is.GreaterThan(0.9f), "Translated camera and player must preserve reveal.");
            _camera.transform.rotation = Quaternion.Euler(0, 0, 30);
            Vector3 viewport = _camera.WorldToViewportPoint(player.transform.position);
            Render(); Assert.That(_readback.GetPixel((int)(viewport.x * 480), (int)(viewport.y * 270)).r, Is.GreaterThan(0.9f), "Rotated orthographic camera must preserve reveal.");
            controller.BindPlayer(null); Render();
            Assert.That(_readback.GetPixel(120, 190).r, Is.LessThan(0.65f), "No player means no phantom reveal at origin.");
            controller.enabled = false; Render(); Assert.That(_readback.GetPixel(120, 190).r, Is.GreaterThan(0.95f));
        }
    }
}
