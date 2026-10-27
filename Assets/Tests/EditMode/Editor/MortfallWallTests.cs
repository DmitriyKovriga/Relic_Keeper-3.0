using System.Collections;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Tiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class MortfallWallTests
    {
        private GameObject _root;
        private Tilemap _wall, _ground, _platforms, _details;
        private MortfallWallRuleTile _rule;
        private Tile _floor;
        private MortfallWallSupport _support;

        [SetUp]
        public void SetUp()
        {
            _rule = AssetDatabase.LoadAssetAtPath<MortfallWallRuleTile>(MortfallWallSetup.RulePath);
            Assert.That(_rule, Is.Not.Null);
            _root = new GameObject("Wall tests", typeof(Grid));
            _wall = Map("Background"); _ground = Map("Ground"); _platforms = Map("Platforms"); _details = Map("WallDetails");
            _floor = ScriptableObject.CreateInstance<Tile>(); _floor.colliderType = Tile.ColliderType.None;
            _support = _wall.gameObject.AddComponent<MortfallWallSupport>(); _support.Configure(_ground, _platforms);
        }
        private Tilemap Map(string name)
        {
            var go = new GameObject(name, typeof(Tilemap)); go.transform.SetParent(_root.transform, false); return go.GetComponent<Tilemap>();
        }
        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_floor != null) Object.DestroyImmediate(_floor);
        }
        private void BottomLeftCorner()
        {
            _wall.SetTile(Vector3Int.zero, _rule); _wall.SetTile(Vector3Int.up, _rule);
            _wall.SetTile(Vector3Int.right, _rule); _wall.SetTile(Vector3Int.up + Vector3Int.right, _rule);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GroundAndPlatformSupportCloseTheBottom_WithoutHiddenWallCells(bool platform)
        {
            BottomLeftCorner(); Assert.That(_rule.ResolveMask(_wall, Vector3Int.zero), Is.EqualTo(22));
            var airborne = _rule.SpriteForMask(22);
            var map = platform ? _platforms : _ground; map.SetTile(Vector3Int.down, _floor);
            Assert.That(_rule.ResolveMask(_wall, Vector3Int.zero), Is.EqualTo(86));
            Assert.That(_rule.SpriteForMask(86), Is.Not.SameAs(airborne));
            Assert.That(_wall.HasTile(Vector3Int.down), Is.False);
            map.SetTile(Vector3Int.down, null);
            Assert.That(_rule.ResolveMask(_wall, Vector3Int.zero), Is.EqualTo(22));
        }

        [UnityTest]
        public IEnumerator PaintingAndErasingSupportRefreshesTheWallSpriteAutomatically()
        {
            BottomLeftCorner(); _wall.RefreshAllTiles(); yield return null;
            Assert.That(_wall.GetSprite(Vector3Int.zero), Is.SameAs(_rule.SpriteForMask(22)));
            _platforms.SetTile(Vector3Int.down, _floor); yield return null;
            Assert.That(_wall.GetSprite(Vector3Int.zero), Is.SameAs(_rule.SpriteForMask(86)));
            _platforms.SetTile(Vector3Int.down, null); yield return null;
            Assert.That(_wall.GetSprite(Vector3Int.zero), Is.SameAs(_rule.SpriteForMask(22)));
        }

        [Test]
        public void DistantDiagonalsAndOtherLayersDoNotInventWallConnections()
        {
            _wall.SetTile(Vector3Int.zero, _rule); _wall.SetTile(new Vector3Int(-1, -1), _rule);
            _ground.SetTile(Vector3Int.left, _floor); _platforms.SetTile(Vector3Int.up, _floor); _details.SetTile(Vector3Int.down, _floor);
            Assert.That(_rule.ResolveMask(_wall, Vector3Int.zero), Is.Zero);
            // Even at a platform corner, a diagonal cannot close an absent side.
            _platforms.SetTile(new Vector3Int(-1, -1), _floor);
            Assert.That(_rule.ResolveMask(_wall, Vector3Int.zero), Is.Zero);
        }

        [Test]
        public void SupportUsesWorldCoordinates_AndCanBeDisabled()
        {
            BottomLeftCorner(); _platforms.transform.localPosition = Vector3.right * 3;
            _platforms.SetTile(new Vector3Int(-3, -1), _floor);
            Assert.That(_rule.ResolveMask(_wall, Vector3Int.zero), Is.EqualTo(86));
            _support.enabled = false;
            Assert.That(_rule.ResolveMask(_wall, Vector3Int.zero), Is.EqualTo(22));
        }

        [Test]
        public void WindowsAndHangingWallsRemainEmpty_AndOriginalColliderDataIsPreserved()
        {
            foreach (var offset in MortfallWallRuleTile.Neighbors) _wall.SetTile(offset, _rule);
            _platforms.SetTile(Vector3Int.down, _floor); _wall.RefreshAllTiles();
            Assert.That(_wall.HasTile(Vector3Int.zero), Is.False);
            Assert.That(_wall.GetComponent<Collider2D>(), Is.Null);
            foreach (var offset in MortfallWallRuleTile.Neighbors)
                Assert.That(_wall.GetColliderType(offset), Is.EqualTo(_rule.m_DefaultColliderType));
        }

        [Test]
        public void All47CanonicalShapesHaveSprites_AndEveryExposedBottomUsesCorrectedArt()
        {
            var masks = Enumerable.Range(0, 256).Select(MortfallWallRuleTile.NormalizeMask).Distinct().ToArray();
            Assert.That(masks.Length, Is.EqualTo(47));
            foreach (int mask in masks)
            {
                var sprite = _rule.SpriteForMask(mask); Assert.That(sprite, Is.Not.Null, "Mask " + mask);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(24, 24)));
                if ((mask & 64) == 0) Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(MortfallWallSetup.AtlasPath));
            }
            Assert.That(AssetDatabase.AssetPathToGUID(MortfallWallSetup.RulePath), Is.EqualTo("5455ec6f4b935434fb78a56caffc43c4"));
            Assert.That(_rule.m_TilingRules.All(x => x.m_RuleTransform == RuleTile.TilingRuleOutput.Transform.Fixed), Is.True);
            var importer = (TextureImporter)AssetImporter.GetAtPath(MortfallWallSetup.AtlasPath);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(24)); Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void EveryRoomAndAppearancePreviewUseTheirOwnSupportLayers()
        {
            foreach (string path in WorldColorPreviewStage.GetMortfallRooms())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var support = prefab.GetComponentInChildren<MortfallWallSupport>(true);
                Assert.That(support, Is.Not.Null, path);
                Assert.That(support.Ground.transform.IsChildOf(prefab.transform), Is.True);
                Assert.That(support.Platforms.transform.IsChildOf(prefab.transform), Is.True);
                Assert.That(support.GetComponent<Tilemap>().name, Is.EqualTo("Background"));
            }
            using (var stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null))
            {
                var support = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MortfallWallSupport>(true)).Single();
                Assert.That(support.Ground.gameObject.scene, Is.EqualTo(stage.Scene));
                Assert.That(support.Platforms.gameObject.scene, Is.EqualTo(stage.Scene));
            }
        }
    }
}
