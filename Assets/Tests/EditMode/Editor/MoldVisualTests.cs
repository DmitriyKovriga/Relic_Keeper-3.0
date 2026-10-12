using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class MoldVisualTests
    {
        [TestCase("MoldIdle", 5)]
        [TestCase("MoldWalk", 9)]
        [TestCase("MoldAttack", 13)]
        [TestCase("MoldHit", 5)]
        public void SheetsRetainNativeScaleAndConsistentGroundAnchor(string name, int count)
        {
            string path = "Assets/Resources/Enemy/MoldResources/Refined/" + name + ".png";
            var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            Assert.That(frames.Length, Is.EqualTo(count));
            Assert.That(frames.All(x => x.rect.size == new Vector2(36, 36) && x.pixelsPerUnit == 24 && x.pivot == new Vector2(18, 18)), Is.True);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void SlowerLightningCastKeepsItsReleasePoseAndProjectile()
        {
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
            Assert.That(data.ID, Is.EqualTo("Mold"));
            Assert.That(data.Movement.MoveSpeed, Is.Zero);
            Assert.That(data.Attack.AttackCooldown, Is.EqualTo(1.8f));
            Assert.That(data.Attack.ProjectileVisualResourcePath, Is.EqualTo("VFX/Projectiles/Lightning_ball/lighting_ball-Sheet"));
            Assert.That(data.Animation.AttackFps, Is.EqualTo(14f));
            Assert.That(data.Animation.AttackImpactFrame, Is.EqualTo(8));
            Assert.That(data.Animation.AttackImpactFrame / data.Animation.AttackFps, Is.EqualTo(8f / 14f).Within(0.001f));
            Assert.That(data.Animation.AttackImpactFrame, Is.LessThan(Resources.LoadAll<Sprite>(data.Animation.AttackSpritesResourcePath).Length));
        }

        [Test]
        public void RuntimeUsesRefinedIdleAndSignalsOnlyAtReleasePose()
        {
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
            var root = Object.Instantiate(data.Prefab);
            try
            {
                var entity = root.GetComponent<EnemyEntity>();
                entity.Setup(data, 1);
                var bridge = root.GetComponent<EnemyAnimationBridge>();
                var idle = Resources.LoadAll<Sprite>(data.Animation.IdleSpritesResourcePath).OrderBy(x => x.name).ToArray();
                Assert.That(entity.VisualRenderer.sprite, Is.SameAs(idle[0]));
                Assert.That(bridge.SupportsHitReaction, Is.True);
                bridge.PlayAttack();
                var time = typeof(EnemyAnimationBridge).GetField("_stateTime", BindingFlags.Instance | BindingFlags.NonPublic);
                var apply = typeof(EnemyAnimationBridge).GetMethod("ApplySpriteFrame", BindingFlags.Instance | BindingFlags.NonPublic);
                time.SetValue(bridge, 8f / 14f - 0.01f);
                apply.Invoke(bridge, new object[] { true });
                Assert.That(bridge.ConsumeAttackImpactSignal(), Is.False);
                time.SetValue(bridge, 8f / 14f + 0.001f);
                apply.Invoke(bridge, new object[] { true });
                Assert.That(bridge.ConsumeAttackImpactSignal(), Is.True);
                Assert.That(bridge.ConsumeAttackImpactSignal(), Is.False);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
    }
}
