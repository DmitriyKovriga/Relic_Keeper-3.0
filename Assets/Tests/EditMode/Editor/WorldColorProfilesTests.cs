using NUnit.Framework;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using Scripts.Visuals.SpriteFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using RelicKeeper.Editor.Visuals;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class WorldColorProfilesTests
    {
        private Scene _scene;
        private Camera _camera;
        private AtmosphereController _controller;
        private WorldColorProfileSO _profile, _other;
        private Material _source;
        private Texture2D _texture, _readback;
        private Sprite _sprite;
        private RenderTexture _target;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _camera = new GameObject("Color profile test camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(_camera.gameObject, _scene);
            _camera.scene = _scene; _camera.enabled = false;
            _camera.transform.position = new Vector3(0, 0, -10);
            _camera.orthographic = true; _camera.orthographicSize = 2f; _camera.aspect = 1f;
            _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.black;
            _camera.allowMSAA = false;
            _profile = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            _other = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            _controller = _camera.gameObject.AddComponent<AtmosphereController>();
            var fields = new SerializedObject(_controller);
            fields.FindProperty("_hubColorProfile").objectReferenceValue = _profile;
            fields.FindProperty("_colorUnlitShader").objectReferenceValue = Shader.Find("RelicKeeper/Sprites/World Color Unlit");
            fields.FindProperty("_colorLitShader").objectReferenceValue = Shader.Find("RelicKeeper/Sprites/World Color Lit");
            fields.ApplyModifiedPropertiesWithoutUndo();
            _source = new Material(Shader.Find("RelicKeeper/Sprites/Relic Sprite FX"));
            _texture = new Texture2D(8, 8, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            var pixels = new Color[64]; for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.25f, 0.25f, 0.25f, 1f);
            _texture.SetPixels(pixels); _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 8f);
            _target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32); _target.Create();
            _readback = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
        }

        [TearDown]
        public void TearDown()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_source); Object.DestroyImmediate(_sprite); Object.DestroyImmediate(_texture);
            Object.DestroyImmediate(_target); Object.DestroyImmediate(_readback);
            Object.DestroyImmediate(_profile); Object.DestroyImmediate(_other);
        }

        private SpriteRenderer AddSprite(string name = "Test sprite")
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            SceneManager.MoveGameObjectToScene(renderer.gameObject, _scene);
            renderer.sprite = _sprite; renderer.sharedMaterial = _source;
            renderer.gameObject.AddComponent<SpriteFxController>();
            return renderer;
        }

        private Color Render(Camera camera = null)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "GPU rendering is required.");
            RenderPipeline.SubmitRenderRequest(camera != null ? camera : _camera, new RenderPipeline.StandardRequest { destination = _target });
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target; _readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); _readback.Apply();
            }
            finally { RenderTexture.active = previous; }
            return _readback.GetPixel(64, 64);
        }

        [Test]
        public void DungeonOverrideFallbackAndReturnDoNotLeakHubColors()
        {
            Assert.That(_controller.ActiveColorProfile, Is.SameAs(_profile));
            _controller.ApplyDungeonColors(_other);
            Assert.That(_controller.ActiveColorProfile, Is.SameAs(_other));
            _controller.ApplyDungeonColors(null);
            Assert.That(_controller.ActiveColorProfile, Is.Null, "An unconfigured dungeon must not inherit the hub profile.");
            var fields = new SerializedObject(_controller);
            fields.FindProperty("_defaultDungeonColorProfile").objectReferenceValue = _other;
            fields.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(_controller.ActiveColorProfile, Is.SameAs(_other));
            _controller.RestoreHubColors();
            Assert.That(_controller.ActiveColorProfile, Is.SameAs(_profile));
        }

        [Test]
        public void ExplicitRolesDoNotChangeSortingAndUnaffectedRestoresOriginalMaterial()
        {
            var renderer = AddSprite(); renderer.sortingLayerName = "Hero"; renderer.sortingOrder = 117;
            var group = renderer.gameObject.AddComponent<WorldColorGroup>();
            group.Configure(WorldColorRole.Background);
            Assert.That(renderer.sharedMaterial.GetFloat("_RKGradeGroup"), Is.EqualTo(3f));
            Assert.That(renderer.sortingLayerName, Is.EqualTo("Hero")); Assert.That(renderer.sortingOrder, Is.EqualTo(117));
            group.Configure(WorldColorRole.Unaffected);
            Assert.That(renderer.sharedMaterial, Is.SameAs(_source));
        }

        [Test]
        public void SharedVariantsPreservePropertyBlocksAndRestoreOnDisable()
        {
            var a = AddSprite(); var b = AddSprite();
            var block = new MaterialPropertyBlock(); block.SetFloat("_CustomOwnerProperty", 0.72f); a.SetPropertyBlock(block);
            WorldColorGrading.Bind(a, RenderDepthCategory.Player); WorldColorGrading.Bind(b, RenderDepthCategory.Player);
            Assert.That(a.sharedMaterial, Is.SameAs(b.sharedMaterial)); Assert.That(a.sharedMaterial, Is.Not.SameAs(_source));
            a.GetPropertyBlock(block); Assert.That(block.GetFloat("_CustomOwnerProperty"), Is.EqualTo(0.72f));
            _controller.enabled = false;
            Assert.That(a.sharedMaterial, Is.SameAs(_source)); Assert.That(b.sharedMaterial, Is.SameAs(_source));
            Assert.That(_source.GetFloat("_RKGradeGroup"), Is.Zero);
        }

        [Test]
        public void WeaponCopyOfGradedMaterialStillRestoresPersistentSource()
        {
            var main = AddSprite(); WorldColorGrading.Bind(main, RenderDepthCategory.Player);
            var offhand = AddSprite(); offhand.sharedMaterial = main.sharedMaterial;
            WorldColorGrading.Bind(offhand, RenderDepthCategory.Player);
            Assert.That(offhand.sharedMaterial, Is.SameAs(main.sharedMaterial));
            _controller.enabled = false;
            Assert.That(offhand.sharedMaterial, Is.SameAs(_source));
        }

        [Test]
        public void SaveSuspensionRestoresSourcesAndCanResumeWithoutPropertyLoss()
        {
            var renderer = AddSprite(); var fx = renderer.GetComponent<SpriteFxController>();
            WorldColorGrading.Bind(renderer, RenderDepthCategory.Player);
            fx.SetFade(0.4f);
            WorldColorGrading.SuspendBindings();
            Assert.That(renderer.sharedMaterial, Is.SameAs(_source));
            WorldColorGrading.RefreshRegisteredScenes();
            Assert.That(renderer.sharedMaterial, Is.Not.SameAs(_source));
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FxRuntimeFade"), Is.EqualTo(0.4f));
        }

        [Test]
        public void PlayTransitionRestoresMaterialsAndBlocksAllPreviewRebindingUntilEnteredMode()
        {
            var renderer = AddSprite(); WorldColorGrading.Bind(renderer, RenderDepthCategory.Player);
            var duplicate = AddSprite(); duplicate.sharedMaterial = renderer.sharedMaterial;
            // Deliberately do not register the duplicate: Unity can snapshot it before a sorting tick.
            WorldColorSaveGuard.OnPlayModeChanged(PlayModeStateChange.ExitingEditMode);
            try
            {
                Assert.That(renderer.sharedMaterial, Is.SameAs(_source));
                Assert.That(duplicate.sharedMaterial, Is.SameAs(_source));
                WorldColorGrading.Register(_controller);
                _controller.RefreshColorGroups();
                WorldColorGrading.Bind(renderer, RenderDepthCategory.Player);
                Assert.That(renderer.sharedMaterial, Is.SameAs(_source), "An open preview must not rebind materials during the snapshot.");
                WorldColorSaveGuard.OnPlayModeChanged(PlayModeStateChange.EnteredPlayMode);
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                Assert.That(Render().r, Is.GreaterThan(0.1f));
                WorldColorSaveGuard.OnPlayModeChanged(PlayModeStateChange.ExitingPlayMode);
                Assert.That(renderer.sharedMaterial, Is.SameAs(_source));
            }
            finally { WorldColorSaveGuard.OnPlayModeChanged(PlayModeStateChange.EnteredEditMode); }
        }

        [Test]
        public void CanvasSpritesAndContactShadowsRemainUngraded()
        {
            var shadow = AddSprite("GroundShadow"); WorldColorGrading.Bind(shadow, RenderDepthCategory.Player);
            Assert.That(shadow.sharedMaterial, Is.SameAs(_source));
            var ui = AddSprite(); ui.gameObject.AddComponent<Canvas>();
            WorldColorGrading.Bind(ui, RenderDepthCategory.Player);
            Assert.That(ui.sharedMaterial, Is.SameAs(_source));
        }

        [Test]
        public void SavingSceneSerializesOriginalMaterialRatherThanTemporaryVariant()
        {
            const string path = "Assets/Tests/EditMode/ColorProfileSaveGuardTemporary.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Null, "Do not overwrite an existing asset.");
            // Save a disposable copy of Test Runner's scene; never save over the open scene.
            Scene scene = SceneManager.GetActiveScene();
            GameObject cameraObject = null, spriteObject = null;
            try
            {
                cameraObject = new GameObject("Saved profile camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var controller = cameraObject.AddComponent<AtmosphereController>();
                spriteObject = new GameObject("Saved graded sprite");
                var renderer = spriteObject.AddComponent<SpriteRenderer>();
                SceneManager.MoveGameObjectToScene(renderer.gameObject, scene);
                Material persistent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/RelicFX/RelicSpriteFX.mat");
                renderer.sharedMaterial = persistent; WorldColorGrading.Bind(renderer, RenderDepthCategory.Player);
                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(persistent));
                Assert.That(EditorSceneManager.SaveScene(scene, path, true), Is.True);
                string serialized = System.IO.File.ReadAllText(path);
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(persistent));
                Assert.That(serialized, Does.Contain("guid: " + guid));
                Assert.That(serialized, Does.Not.Contain("(Characters)"));
            }
            finally
            {
                if (spriteObject != null) Object.DestroyImmediate(spriteObject);
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void SavingPrefabSerializesOriginalMaterialRatherThanTemporaryVariant()
        {
            const string path = "Assets/Tests/EditMode/ColorProfilePrefabTemporary.prefab";
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Null);
            var renderer = AddSprite();
            Material persistent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/RelicFX/RelicSpriteFX.mat");
            renderer.sharedMaterial = persistent; WorldColorGrading.Bind(renderer, RenderDepthCategory.Player);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(renderer.gameObject, path);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.GetComponent<SpriteRenderer>().sharedMaterial, Is.SameAs(persistent));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void NeutralMatchesOriginalAndOnlyAssignedGroupRespondsToLiveChanges()
        {
            var renderer = AddSprite(); Color original = Render();
            WorldColorGrading.Bind(renderer, RenderDepthCategory.Player);
            Assert.That(Render().r, Is.EqualTo(original.r).Within(0.012f));
            _profile.background.exposure = 1f;
            Assert.That(Render().r, Is.EqualTo(original.r).Within(0.012f));
            _profile.characters.exposure = 1f;
            Assert.That(Render().r, Is.GreaterThan(original.r + 0.09f));
            _controller.PreviewNeutralColors = true;
            Assert.That(Render().r, Is.EqualTo(original.r).Within(0.012f));
        }

        [Test]
        public void CombatEffectOptOutAndHitFlashRemainReadable()
        {
            var renderer = AddSprite(); WorldColorGrading.Bind(renderer, RenderDepthCategory.HeroAttackVfx);
            Color original = Render(); _profile.characters.exposure = -2f; _profile.effectsInfluence = 0f;
            Assert.That(Render().r, Is.EqualTo(original.r).Within(0.012f));
            _profile.effectsInfluence = 1f;
            Assert.That(Render().r, Is.LessThan(original.r - 0.09f));
            renderer.GetComponent<SpriteFxController>().Flash(Color.red, 1f);
            Color flash = Render(); Assert.That(flash.r, Is.GreaterThan(0.95f)); Assert.That(flash.g, Is.LessThan(0.02f));
            renderer.GetComponent<SpriteFxController>().SetDissolve(1f);
            Assert.That(Render().maxColorComponent, Is.LessThan(0.02f));
        }

        [Test]
        public void OtherCameraDoesNotInheritProfile()
        {
            var renderer = AddSprite(); WorldColorGrading.Bind(renderer, RenderDepthCategory.Player);
            Color neutral = Render(); _profile.characters.exposure = 1f;
            Assert.That(Render().r, Is.GreaterThan(neutral.r + 0.09f));
            var other = new GameObject("Other camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(other.gameObject, _scene);
            other.CopyFrom(_camera); other.scene = _scene;
            Assert.That(Render(other).r, Is.EqualTo(neutral.r).Within(0.012f));
        }

        [TestCase("Universal Render Pipeline/2D/Sprite-Unlit-Default")]
        [TestCase("Universal Render Pipeline/2D/Sprite-Lit-Default")]
        [TestCase("Sprites/Default")]
        public void StockMaterialsKeepTheirLightingFamilyAndOriginalAsset(string shaderName)
        {
            Object.DestroyImmediate(_source); _source = new Material(Shader.Find(shaderName));
            var lighting = new GameObject("Stock material test lighting"); SceneManager.MoveGameObjectToScene(lighting, _scene);
            var light = lighting.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global; light.intensity = 1f;
            var renderer = AddSprite(); Color original = Render();
            WorldColorGrading.Bind(renderer, RenderDepthCategory.Environment);
            Assert.That(renderer.sharedMaterial.shader.name, Does.Contain(shaderName.Contains("Unlit") || shaderName == "Sprites/Default" ? "World Color Unlit" : "World Color Lit"));
            Assert.That(_source.shader.name, Is.EqualTo(shaderName));
            Color neutral = Render(); Assert.That(neutral.r, Is.GreaterThan(0.1f));
            Assert.That(neutral.r, Is.EqualTo(original.r).Within(0.012f));
            _profile.environment.exposure = 1f;
            Assert.That(Render().r, Is.GreaterThan(neutral.r + 0.09f));
        }
    }
}
