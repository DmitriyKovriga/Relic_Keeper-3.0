using NUnit.Framework;
using RelicKeeper.Editor.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class EnvironmentContactShadingTests
    {
        private Scene _scene;
        private GameObject _root;
        private Tilemap _map;
        private Tile _tile;
        private Sprite _pixel;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _root = new GameObject("Contact test room", typeof(Grid));
            SceneManager.MoveGameObjectToScene(_root, _scene);
            var go = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            SceneManager.MoveGameObjectToScene(go, _scene); go.transform.SetParent(_root.transform, false);
            _map = go.GetComponent<Tilemap>();
            _pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            _tile = ScriptableObject.CreateInstance<Tile>(); _tile.sprite = _pixel;
            go.GetComponent<TilemapRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_tile); Object.DestroyImmediate(_pixel);
        }

        private void Tile(int x, int y) => _map.SetTile(new Vector3Int(x, y), _tile);
        private Transform Group => _root.transform.Find(EnvironmentContactShadingBuilder.GroupName);

        [Test]
        public void HubRebuildKeepsTwoContactsAndAddsNoPhysics()
        {
            EnvironmentContactShadingBuilder.ConfigureHub(_root);
            EnvironmentContactShadingBuilder.ConfigureHub(_root);
            Assert.That(Group.childCount, Is.EqualTo(2));
            Assert.That(_root.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
            Assert.That(_root.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(2));
        }

        [Test]
        public void OpenPlatformEdgesAreNotOutlined()
        {
            Tile(0, 0); Tile(1, 0); Tile(2, 0);
            EnvironmentContactShadingBuilder.ConfigureRoom(_root);
            Assert.That(Group.childCount, Is.Zero);
        }

        [Test]
        public void WallFloorJointIsBakedOnceInsideSolidFloor()
        {
            Tile(0, 0); Tile(1, 0); Tile(2, 0); Tile(0, 1); Tile(0, 2);
            EnvironmentContactShadingBuilder.ConfigureRoom(_root);
            EnvironmentContactShadingBuilder.ConfigureRoom(_root);
            Assert.That(Group.childCount, Is.EqualTo(1));
            var bounds = Group.GetComponentInChildren<SpriteRenderer>().bounds;
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(1f - 1f / 24f + 0.001f));
            Assert.That(bounds.min.y, Is.GreaterThan(0f));
            Assert.That(_map.GetUsedTilesCount(), Is.EqualTo(1));
            Assert.That(_root.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
        }

        [Test]
        public void RecessDarkeningStaysInsideCeilingAndBelowItsWalkableTop()
        {
            Tile(0, 0); Tile(0, 1); Tile(1, 2);
            EnvironmentContactShadingBuilder.ConfigureRoom(_root);
            Assert.That(Group.childCount, Is.EqualTo(1));
            var bounds = Group.GetComponentInChildren<SpriteRenderer>().bounds;
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(2f + 1f / 24f - 0.001f));
            Assert.That(bounds.max.y, Is.LessThan(3f));
        }

        [Test]
        public void RenderedJointDarkensFloorButPreservesTheTopPixelRow()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            Tile(0, 0); Tile(1, 0); Tile(2, 0); Tile(0, 1); Tile(0, 2);
            EnvironmentContactShadingBuilder.ConfigureRoom(_root);
            var camera = new GameObject("Contact camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, _scene);
            camera.scene = _scene; camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 5.625f; camera.aspect = 480f / 270f;
            camera.transform.position = new Vector3(1.5f, 0.5f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(480, 270, 24);
            var pixels = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                Color32[] Render(bool active)
                {
                    Group.gameObject.SetActive(active);
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); pixels.Apply();
                    return pixels.GetPixels32();
                }
                var before = Render(false); var after = Render(true);
                int changed = 0;
                for (int x = 229; x < 244; x++)
                {
                    int edge = 146 * 480 + x;
                    Assert.That(after[edge], Is.EqualTo(before[edge]), "The walkable top pixel must remain readable.");
                    for (int y = 142; y < 146; y++) if (before[y * 480 + x].r - after[y * 480 + x].r > 8) changed++;
                }
                Assert.That(changed, Is.GreaterThan(10), "The floor contact must visibly darken actual rendered pixels.");
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
        }
    }
}
