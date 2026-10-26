using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CeilingBeamVisibilityTests
    {
        [Test]
        public void RoofOutsideTheActualRayCannotCutTheBeam_ButAnObstacleOnTheRayCan()
        {
            var root = new GameObject("Beam mask regression", typeof(Grid));
            var mapObject = new GameObject("Roof", typeof(Tilemap)); mapObject.transform.SetParent(root.transform, false);
            var map = mapObject.GetComponent<Tilemap>();
            var tile = ScriptableObject.CreateInstance<Tile>(); tile.colliderType = Tile.ColliderType.Grid;
            var beam = new GameObject("Wide right-side beam"); beam.transform.SetParent(root.transform, false);
            var source = beam.AddComponent<LocalLightShaftSource>();
            try
            {
                // Reproduce the saved right-hand Room 001 overhang and beam.
                for (int x = 21; x <= 24; x++) for (int y = 10; y <= 13; y++) map.SetTile(new Vector3Int(x, y), tile);
                source.transform.localPosition = new Vector3(18.752598f, 11.508198f);
                source.Configure(AssetDatabase.LoadAssetAtPath<Sprite>(LocalLightShaftSetup.ArtPath), new Vector2Int(10, 14), Enumerable.Repeat(160f, 64).ToArray());
                source.SetShape(new Vector2(3.05f, 1.11f), -10);
                var maps = new[] { map };
                Assert.That(LocalLightShaftSetup.ClipColumns(maps, source)[32], Is.LessThan(14), "The old parallel-column mask reproduces the false cut.");
                var visible = source.PixelToLocal(new Vector2(32.5f, 90.5f));
                var mask = CeilingBeamVisibility.Bake(maps, source); source.ConfigureVisibility(mask);
                Assert.That(source.Contains(visible), Is.True, "This pixel's source ray passes below the overhang.");
                root.transform.position = new Vector3(4, -2); root.transform.rotation = Quaternion.Euler(0, 0, 23); root.transform.localScale = new Vector3(1.4f, 0.8f, 1);
                Assert.That(CeilingBeamVisibility.Bake(maps, source), Is.EqualTo(mask), "The mask must follow transformed room grids.");
                Vector3 world = source.transform.TransformPoint(visible);
                map.SetTile(map.WorldToCell(Vector3.Lerp(source.SourcePosition, world, 0.5f)), tile);
                source.ConfigureVisibility(CeilingBeamVisibility.Bake(maps, source));
                Assert.That(source.Contains(visible), Is.False, "A real obstruction between source and pixel must block the beam.");
                Assert.That(source.ParticleIllumination(visible), Is.Zero, "Dust follows the exact same visibility mask.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void RadiusAndRotationPointAtTheVisibleTip_AndKeepTheExistingShapeRatio()
        {
            var root = new GameObject("Beam aim");
            var source = root.AddComponent<LocalLightShaftSource>();
            try
            {
                source.SetShape(new Vector2(3.05f, 1.11f), -10);
                root.transform.rotation = Quaternion.Euler(0, 0, 27); root.transform.localScale = new Vector3(1.2f, 0.8f, 1);
                float angle = 42, radius = 9;
                Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.down * radius;
                var value = CeilingBeamControls.AimAt(root.transform, source.ReadAppearance(), direction);
                source.ApplyAppearance(value);
                var actual = source.EndPosition - source.SourcePosition;
                Assert.That(Vector3.Distance(actual, direction), Is.LessThan(0.0001f));
                Assert.That(value.size.x / value.size.y, Is.EqualTo(3.05f / 1.11f).Within(0.0001f));
                Assert.That(value.strength, Is.EqualTo(1)); Assert.That(value.dustOpacity, Is.EqualTo(1)); Assert.That(value.dustSpeed, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
