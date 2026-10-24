using NUnit.Framework;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class DepthMistTests
    {
        private Scene _scene;
        private Camera _camera;
        private AtmosphereController _controller;
        private AtmosphereProfileSO _profile;
        private Sprite _pixel;
        private Material _material;
        private RenderTexture _target;
        private Texture2D _readback;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            WorldColorGrading.SetBindingsSuspended(false);
            Assert.That(Shader.Find("RelicKeeper/Atmosphere/Depth Mist").isSupported, Is.True, "Depth mist shader must compile on the active graphics device.");
            _scene = EditorSceneManager.NewPreviewScene();
            _camera = NewCamera();
            _controller = _camera.gameObject.AddComponent<AtmosphereController>();
            _profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            _profile.backgroundMist = 0.6f; _profile.backgroundTint = Color.white;
            _profile.wispStrength = 0; _profile.depthMistCenterY = 0;
            _profile.depthMistHeight = 20; _profile.depthMistFeather = 1; _profile.driftSpeed = Vector2.zero;
            _controller.Profile = _profile;
            _pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            _material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AddSprite("Background", -1000, Vector2.zero, new Vector2(50, 50), Color.black);
            _target = new RenderTexture(480, 270, 24); _target.Create();
            _readback = new Texture2D(480, 270, TextureFormat.RGB24, false);
        }

        private Camera NewCamera()
        {
            var camera = new GameObject("Mist test camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, _scene);
            camera.scene = _scene; camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 5.625f; camera.aspect = 480f / 270f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            return camera;
        }

        private void AddSprite(string layer, int order, Vector2 position, Vector2 size, Color color)
        {
            var renderer = new GameObject(layer + " sample").AddComponent<SpriteRenderer>();
            SceneManager.MoveGameObjectToScene(renderer.gameObject, _scene);
            renderer.sprite = _pixel; renderer.sharedMaterial = _material; renderer.color = color;
            renderer.transform.position = position; renderer.transform.localScale = new Vector3(size.x, size.y, 1);
            renderer.sortingLayerName = layer; renderer.sortingOrder = order;
        }

        private Color32[] Render(Camera camera = null)
        {
            RenderPipeline.SubmitRenderRequest(camera != null ? camera : _camera, new RenderPipeline.StandardRequest { destination = _target });
            var previous = RenderTexture.active;
            try { RenderTexture.active = _target; _readback.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); _readback.Apply(); return _readback.GetPixels32(); }
            finally { RenderTexture.active = previous; }
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_profile); Object.DestroyImmediate(_pixel); Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_target); Object.DestroyImmediate(_readback);
        }

        [Test]
        public void MistChangesBackgroundButNotOpaqueTerrainOrHero()
        {
            AddSprite("Background", 6, new Vector2(-3, 0), Vector2.one, Color.black);
            AddSprite("Default", 0, Vector2.zero, Vector2.one * 2, Color.red);
            AddSprite("Hero", 0, new Vector2(3, 0), Vector2.one, Color.green);
            _profile.backgroundMist = 0; var before = Render();
            Assert.That(_camera.transform.childCount, Is.Zero, "Neutral settings should not allocate a plane.");
            _profile.backgroundMist = 0.6f; var after = Render();
            Assert.That(after[135 * 480 + 100].r, Is.GreaterThan(before[135 * 480 + 100].r + 20));
            Assert.That(after[135 * 480 + 168].r, Is.GreaterThan(before[135 * 480 + 168].r + 20), "Mist must separate the dense forest band from the playable plane.");
            Assert.That(after[135 * 480 + 240], Is.EqualTo(before[135 * 480 + 240]), "Terrain must draw over depth mist.");
            Assert.That(after[135 * 480 + 312], Is.EqualTo(before[135 * 480 + 312]), "The hero must remain unchanged.");
        }

        [Test]
        public void MistBeltFadesOutAboveAndBelowItsWorldBand()
        {
            _profile.limitDepthMistHeight = true;
            _profile.depthMistHeight = 1; _profile.depthMistFeather = 0.2f;
            var image = Render();
            Assert.That(image[135 * 480 + 240].r, Is.GreaterThan(20));
            Assert.That(image[183 * 480 + 240].r, Is.LessThan(2));
            Assert.That(image[87 * 480 + 240].r, Is.LessThan(2));
        }

        [TestCase(-25f, -20f)]
        [TestCase(0f, 0f)]
        [TestCase(25f, 20f)]
        public void DefaultCoverageIncludesTheWholeViewFarFromTheRoomEntrance(float x, float y)
        {
            // A dungeon can descend well below its spawn; a hub can span many screens.
            // Keep the actual authored narrow belt values to catch unintended clipping.
            _profile.depthMistHeight = 7; _profile.depthMistCenterY = 3.4f;
            _controller.ResetDepthMistAnchor(new Vector2(-1.19f, 5.58f));
            _camera.transform.position = new Vector3(x, y, -10);
            _profile.backgroundMist = 0; var before = Render();
            _profile.backgroundMist = 0.6f; var after = Render();
            for (int py = 10; py < 270; py += 40)
            for (int px = 10; px < 480; px += 40)
                Assert.That(after[py * 480 + px].r, Is.GreaterThan(before[py * 480 + px].r + 20),
                    $"Mist disappeared at ({x}, {y}), pixel ({px}, {py}), away from the entrance.");
        }

        [Test]
        public void PatternStaysAtTheSameWorldPointWhileCameraPans()
        {
            _profile.wispStrength = 1; _profile.noiseScale = 0.4f;
            var before = Render();
            _camera.transform.position += Vector3.right;
            var after = Render();
            Assert.That((int)after[135 * 480 + 240].r, Is.EqualTo((int)before[135 * 480 + 264].r).Within(1));
        }

        [Test]
        public void AdvancingTheMistClockMovesWispsWithoutMovingTheCamera()
        {
            _profile.wispStrength = 1; _profile.driftSpeed = new Vector2(0.5f, 0.1f);
            _controller.ResetDepthMistAnchor(Vector2.zero);
            var before = Render();
            typeof(AtmosphereController).GetField("_depthMistStartTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_controller, Time.realtimeSinceStartup - 10f);
            var after = Render();
            int changed = 0;
            for (int i = 0; i < after.Length; i++) if (Mathf.Abs(after[i].r - before[i].r) > 2) changed++;
            Assert.That(changed, Is.GreaterThan(100), "The bound drift clock must move actual rendered wisps.");
        }

        [Test]
        public void OtherCameraAndDisabledControllerDoNotInheritMist_AndReenableCreatesOnlyOnePlane()
        {
            Render();
            Assert.That(_camera.transform.childCount, Is.EqualTo(1));
            var other = NewCamera();
            Assert.That(Render(other)[135 * 480 + 240].r, Is.LessThan(2));
            _controller.enabled = false;
            Assert.That(_camera.transform.childCount, Is.Zero);
            Assert.That(Render()[135 * 480 + 240].r, Is.LessThan(2));
            _controller.enabled = true; Render(); Render();
            Assert.That(_camera.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void DungeonOverrideFallbackAndHubReturnRestorePresetAndAnchorWithoutChangingScreenFog()
        {
            var hub = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            var fallback = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            var dungeon = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            try
            {
                var fields = new SerializedObject(_controller);
                fields.FindProperty("_hubDepthMistProfile").objectReferenceValue = hub;
                fields.FindProperty("_defaultDungeonDepthMistProfile").objectReferenceValue = fallback;
                fields.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(_controller.ActiveDepthMistProfile, Is.SameAs(hub));
                _controller.ApplyDungeonPresentation(null, dungeon);
                _controller.ResetDepthMistAnchor(new Vector2(13, 7));
                Assert.That(_controller.ActiveDepthMistProfile, Is.SameAs(dungeon));
                Assert.That(_controller.DepthMistAnchor, Is.EqualTo(new Vector2(13, 7)));
                _controller.ApplyDungeonPresentation(null, null);
                Assert.That(_controller.ActiveDepthMistProfile, Is.SameAs(fallback));
                Assert.That(_controller.DepthMistAnchor, Is.EqualTo(Vector2.zero));
                _controller.RestoreHubColors();
                Assert.That(_controller.ActiveDepthMistProfile, Is.SameAs(hub));
                Assert.That(_controller.Profile, Is.SameAs(_profile));
            }
            finally { Object.DestroyImmediate(hub); Object.DestroyImmediate(fallback); Object.DestroyImmediate(dungeon); }
        }
    }
}
