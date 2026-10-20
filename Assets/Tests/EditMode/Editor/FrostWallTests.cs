using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class FrostWallTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(6400, 6400);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _stats;
        private PlayerFrostWallController _wall;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(FrostWallBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single();
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

        [SetUp]
        public void Setup()
        {
            var go = Keep(new GameObject("Frost Wall owner")); go.transform.position = Origin;
            _stats = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_stats, null);
            _stats.GetStat(StatType.MaxHealth).BaseValue = 100; _stats.Health.RestoreFull();
            _stats.GetStat(StatType.CritChance).BaseValue = 0;
            foreach (var type in new[] { StatType.DamagePhysical, StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning })
                _stats.GetStat(type).BaseValue = 10;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _wall = go.AddComponent<PlayerFrostWallController>(); Physics2D.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_wall != null) _wall.End();
            foreach (var projectile in Resources.FindObjectsOfTypeAll<SkillProjectile>())
            {
                var data = typeof(SkillProjectile).GetField("_data", Flags).GetValue(projectile) as SkillProjectileLaunchData;
                if (data != null && data.OwnerStats == _stats) Object.DestroyImmediate(projectile.gameObject);
            }
            foreach (var projectile in Resources.FindObjectsOfTypeAll<EnemyProjectile>())
                if (!EditorUtility.IsPersistent(projectile) && (Vector2.Distance(projectile.transform.position, Origin) < 20 || projectile.gameObject.hideFlags != HideFlags.None))
                    Object.DestroyImmediate(projectile.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private void Begin() => Assert.That(_wall.Begin(Skill, Step), Is.True);
        private MysticShieldController Shield()
        {
            _stats.GetStat(StatType.MaxMysticShield).BaseValue = 3;
            var shield = _stats.GetComponent<MysticShieldController>();
            typeof(MysticShieldController).GetMethod("Awake", Flags).Invoke(shield, null); shield.FillCharges(); return shield;
        }
        private LuckyShotHitRecorder Enemy(Vector2 offset)
        {
            var go = Keep(new GameObject("Wall attacker") { layer = 7 }); go.transform.position = Origin + offset;
            var target = go.AddComponent<LuckyShotHitRecorder>(); go.AddComponent<BoxCollider2D>().size = Vector2.one * .3f;
            Physics2D.SyncTransforms(); return target;
        }
        private SkillProjectile Shard() => Resources.FindObjectsOfTypeAll<SkillProjectile>().Single(projectile =>
            (typeof(SkillProjectile).GetField("_data", Flags).GetValue(projectile) as SkillProjectileLaunchData)?.OwnerStats == _stats);

        [Test]
        public void BaseWallDoesNotRequireChargesAndDoesNotScaleFromUnusedShieldEffectiveness()
        {
            _stats.GetStat(StatType.MysticShieldSkillEffectiveness).BaseValue = 4; Begin();
            Assert.That(_wall.IsEmpowered, Is.False); Assert.That(_wall.DamageMultiplier, Is.EqualTo(.5f));
            Assert.That(_wall.RemainingDuration, Is.EqualTo(5));
            _wall.Tick(4.9f); Assert.That(_wall.IsActive, Is.True);
            _wall.Tick(.1f); Assert.That(_wall.IsActive, Is.False);
        }

        [Test]
        public void EmpoweredWallConsumesExactlyOneAndSnapshotsEffectivenessAndEffectDuration()
        {
            var shield = Shield(); int before = shield.CurrentCharges;
            _stats.GetStat(StatType.MysticShieldSkillEffectiveness).BaseValue = 2;
            _stats.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(50, StatModType.PercentAdd, this));
            Begin(); Assert.That(shield.CurrentCharges, Is.EqualTo(before - 1)); Assert.That(_wall.IsEmpowered, Is.True);
            Assert.That(_wall.DamageMultiplier, Is.EqualTo(2)); Assert.That(_wall.RemainingDuration, Is.EqualTo(30));
            _stats.GetStat(StatType.MysticShieldSkillEffectiveness).BaseValue = 10;
            Assert.That(_wall.DamageMultiplier, Is.EqualTo(2));
            var enemy = Enemy(new Vector2(3, .5f));
            PlayerFrostWallController.TryIntercept(Origin + new Vector2(3, .5f), Origin + new Vector2(0, .5f), 0, enemy.gameObject, out _);
            Assert.That(shield.CurrentCharges, Is.EqualTo(before - 1), "Blocking does not consume another charge.");
        }

        [Test]
        public void BarrierIsFixedDirectionalAndExpiresWithoutProtectingThroughItsBack()
        {
            Begin(); Bounds bounds = _wall.BarrierBounds;
            Assert.That(PlayerFrostWallController.TryIntercept(Origin + new Vector2(-3, 0), Origin, 0, null, out _), Is.False);
            Assert.That(PlayerFrostWallController.TryIntercept(Origin + new Vector2(3, 4), Origin + new Vector2(0, 4), 0, null, out _), Is.False);
            Assert.That(PlayerFrostWallController.TryIntercept(Origin + new Vector2(3, .5f), Origin + new Vector2(0, .5f), 0, null, out _), Is.True);
            _stats.transform.position += Vector3.right * 10; _wall.Tick(.1f); Assert.That(_wall.BarrierBounds, Is.EqualTo(bounds));
            _wall.Tick(10);
            Assert.That(PlayerFrostWallController.TryIntercept(Origin + new Vector2(3, .5f), Origin, 0, null, out _), Is.False);
        }

        [Test]
        public void RetaliationUsesMainHandSlotHalfWeaponDamageAndConvertsAllChannelsToCold()
        {
            Begin(); var enemy = Enemy(new Vector2(3, .5f));
            Assert.That(PlayerFrostWallController.TryInterceptAttack(enemy.gameObject, _stats.transform), Is.True);
            var shard = Shard();
            var data = (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(shard);
            Assert.That(data.SkillSlotIndex, Is.Zero); Assert.That(data.DamageMultiplier, Is.EqualTo(.5f));
            Assert.That(data.RemainingChains + data.RemainingForks, Is.Zero);
            typeof(SkillProjectile).GetMethod("DealDamage", Flags).Invoke(shard, new object[] { enemy, false });
            Assert.That(enemy.Hits.Count, Is.EqualTo(1));
            Assert.That(enemy.Hits[0].Cold, Is.EqualTo(20).Within(.001f));
            Assert.That(enemy.Hits[0].Physical + enemy.Hits[0].Fire + enemy.Hits[0].Lightning, Is.Zero);
        }

        [Test]
        public void FastEnemyProjectileIsBlockedBeforePlayerAndCreatesOnlyOneShard()
        {
            Begin(); var enemy = Enemy(new Vector2(4, .5f));
            var data = Keep(Object.Instantiate(Resources.Load<EnemyDataSO>("Enemy/SO_Mold")));
            data.Attack.ProjectileSpeed = 100; data.Attack.ProjectileLifetime = 10;
            EnemyProjectile.Spawn(data, new DamageSnapshot(_stats) { Physical = 25 }, Origin + new Vector2(3, .5f), Vector2.left, null, enemy.gameObject);
            var projectile = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Single();
            Assert.That(projectile.gameObject.activeSelf, Is.True); Assert.That(projectile.gameObject.hideFlags, Is.EqualTo(HideFlags.None));
            foreach (var component in projectile.GetComponents<Component>()) Assert.That(component.hideFlags, Is.EqualTo(HideFlags.None));
            projectile.Tick(.05f);
            Assert.That(projectile == null, Is.True); Assert.That(_stats.Health.Current, Is.EqualTo(100));
            Assert.That(_wall.Retaliations, Is.EqualTo(1)); Assert.That(Shard(), Is.Not.Null);
        }

        [Test]
        public void ProjectileHitsPlayerBeforeABarrierBehindThem()
        {
            Begin(); var enemy = Enemy(new Vector2(-4, 0));
            var data = Keep(Object.Instantiate(Resources.Load<EnemyDataSO>("Enemy/SO_Mold")));
            data.Attack.ProjectileSpeed = 100; data.Attack.ProjectileLifetime = 10;
            EnemyProjectile.Spawn(data, new DamageSnapshot(_stats) { Physical = 25 }, Origin + Vector2.left * 3, Vector2.right, null, enemy.gameObject);
            var projectile = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Single(); projectile.Tick(.05f);
            Assert.That(_wall.Retaliations, Is.Zero); Assert.That(_stats.Health.Current, Is.LessThan(100));
        }

        [Test]
        public void NoPoolProjectileStillActivatesAndExpires()
        {
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
            EnemyProjectile.Spawn(data, new DamageSnapshot(_stats), Origin + Vector2.up * 10, Vector2.right, null, null);
            var projectile = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Single();
            Assert.That(projectile.gameObject.activeSelf, Is.True); projectile.Tick(100);
            Assert.That(projectile == null, Is.True);
        }

        [Test]
        public void PooledEnemyProjectileCanBeReusedAfterExpiryWithoutDuplicateReturns()
        {
            var pool = Keep(new GameObject("Projectile test pool")).AddComponent<PoolManager>();
            typeof(PoolManager).GetMethod("Awake", Flags).Invoke(pool, null);
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
            EnemyProjectile.Spawn(data, new DamageSnapshot(_stats), Origin + Vector2.up * 10, Vector2.right, null, null);
            var projectile = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Single();
            projectile.Tick(100); projectile.Tick(100); Assert.That(projectile.gameObject.activeSelf, Is.False);
            EnemyProjectile.Spawn(data, new DamageSnapshot(_stats), Origin + Vector2.up * 11, Vector2.left, null, null);
            Assert.That(Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Single(), Is.SameAs(projectile));
            Assert.That(projectile.gameObject.hideFlags, Is.EqualTo(HideFlags.None));
            projectile.Tick(.01f); Assert.That(projectile.gameObject.activeSelf, Is.True);
            Assert.That(projectile.transform.position.x, Is.LessThan(Origin.x));
        }

        [TestCase("PerformMeleeAttack")]
        [TestCase("PerformDirectTargetAttack")]
        [TestCase("TryApplyChargeContactHit")]
        public void EnemyAttackDeliveryBlocksAcrossWallButStillHitsFromBehind(string delivery)
        {
            Begin(); var receiver = _stats.gameObject.AddComponent<LuckyShotHitRecorder>();
            var enemy = Enemy(new Vector2(3, 0)); var controller = enemy.gameObject.AddComponent<EnemyAttackController>();
            var data = Keep(Object.Instantiate(Resources.Load<EnemyDataSO>("Enemy/SO_Mold")));
            data.Attack.HitboxSize = new Vector2(4, 1); data.Attack.HitboxOffset = new Vector2(-1.5f, 0);
            controller.Initialize(null, data);
            typeof(EnemyAttackController).GetField("_currentTarget", Flags).SetValue(controller, _stats.transform);
            var config = typeof(EnemyAttackController).GetMethod("GetCurrentAttackConfig", Flags).Invoke(controller, null);
            typeof(EnemyAttackController).GetMethod(delivery, Flags).Invoke(controller, new[] { config });
            Assert.That(receiver.Hits, Is.Empty); Assert.That(_wall.Retaliations, Is.EqualTo(1));
            enemy.transform.position = Origin + Vector2.left * 3; Physics2D.SyncTransforms();
            data.Attack.HitboxOffset = new Vector2(1.5f, 0);
            config = typeof(EnemyAttackController).GetMethod("GetCurrentAttackConfig", Flags).Invoke(controller, null);
            typeof(EnemyAttackController).GetField("_hasAppliedHit", Flags).SetValue(controller, false);
            typeof(EnemyAttackController).GetMethod(delivery, Flags).Invoke(controller, new[] { config });
            Assert.That(receiver.Hits.Count, Is.EqualTo(1)); Assert.That(_wall.Retaliations, Is.EqualTo(1));
        }

        [Test]
        public void BlockedPlacementDoesNotSpendChargeOrReplaceExistingWall()
        {
            var shield = Shield(); Begin(); var bounds = _wall.BarrierBounds; int charges = shield.CurrentCharges;
            var obstacle = Keep(new GameObject("Wall obstruction") { layer = 6 }); obstacle.transform.position = Origin + Vector2.right * .6f;
            obstacle.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 3); Physics2D.SyncTransforms();
            Assert.That(_wall.Begin(Skill, Step), Is.False); Assert.That(shield.CurrentCharges, Is.EqualTo(charges));
            Assert.That(_wall.BarrierBounds, Is.EqualTo(bounds)); Assert.That(_wall.IsActive, Is.True);
        }

        [Test]
        public void FacingLeftRecastDeathAndCharacterChangeHaveCorrectLifetime()
        {
            Begin(); _stats.transform.localScale = new Vector3(-1, 1, 1); Begin();
            Assert.That(_wall.BarrierBounds.center.x, Is.LessThan(Origin.x));
            var manager = _stats.gameObject.AddComponent<PlayerSkillManager>();
            typeof(PlayerSkillManager).GetField("_playerStats", Flags).SetValue(manager, _stats);
            typeof(PlayerSkillManager).GetMethod("HandleActiveCharacterChanged", Flags).Invoke(manager, new object[] { "another-character" });
            Assert.That(_wall.IsActive, Is.False);
            Begin(); _stats.Health.SetCurrent(0); _wall.Tick(0); Assert.That(_wall.IsActive, Is.False);
            Assert.That(_wall.Begin(Skill, Step), Is.False);
        }

        [TestCase("en", "Frost Wall", "main-hand", "exactly one")]
        [TestCase("ru", "Ледяная стена", "основной руке", "ровно один")]
        public void SkillAssetsPoolAndLocalesMatchTheMechanic(string code, string name, string hand, string charge)
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(5)); Assert.That(Skill.ManaCost, Is.EqualTo(35));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.Icon, Is.Not.Null);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(FrostWallBuilder.PoolPath);
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == Skill && entry.Weight > 0), Is.EqualTo(1));
            Assert.That(Step.GetObject<Sprite>("ShardSprite"), Is.Not.Null);
            Assert.That(Step.GetObject<GameObject>("FieldVfxPrefab"), Is.Not.Null);
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey).LocalizedValue, Is.EqualTo(name));
            string description = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            Assert.That(description, Does.Contain(hand)); Assert.That(description, Does.Contain(charge));
            Assert.That(description, Does.Contain("50%"));
            Assert.That(SkillDpsPreview.Build(Skill, _stats, 1).HasContent, Is.False, "Do not invent DPS from an unknown incoming attack rate.");
        }
    }
}
