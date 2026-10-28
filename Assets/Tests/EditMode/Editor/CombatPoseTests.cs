using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CombatPoseTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new List<Object>();
        private CombatPoseProfile _profile;
        private static SkillDataSO Slash => Resources.Load<SkillDataSO>("Skills/2HWeapon/Axe/LeftButton/CleaveLB");
        private static SkillDataSO Heavy => Resources.Load<SkillDataSO>("Skills/2HWeapon/Hammer/Adventurer/WeightOfAGiant/WeightOfAGiantSkill");
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);

        [SetUp]
        public void Setup()
        {
            Assert.That(CombatPoseProfile.LoadDefault(), Is.Not.Null);
            _profile = Keep(Object.Instantiate(CombatPoseProfile.LoadDefault()));
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] is GameObject root)
                    foreach (var visual in root.GetComponentsInChildren<PlayerMovementVisual>(true))
                        if (visual.DisplayRenderer != null) { Object.DestroyImmediate(visual.DisplayRenderer.gameObject); Set(visual, "_display", null); }
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualMeleeRecipesSamplePreparationStrikeAndCompleteRecovery(bool heavy)
        {
            var state = new AttackBodyFollow(); var owner = new object();
            Assert.That(state.TryBegin(owner, heavy ? Heavy : Slash, _profile), Is.True);
            state.Windup(owner, 0); Assert.That(state.Pose.Degrees, Is.Zero);
            state.Windup(owner, 1); Assert.That(state.Pose.Degrees, Is.GreaterThan(0));
            Assert.That(state.Pose.Height, Is.InRange(.94f, 1f));
            state.Strike(owner); Assert.That(state.Pose.Degrees, Is.LessThan(0));
            float strike = state.Pose.Degrees;
            state.Recovery(owner, .5f); Assert.That(state.Pose.Degrees, Is.InRange(strike, 0f));
            state.Recovery(owner, 1); Assert.That(state.Pose.Degrees, Is.Zero); Assert.That(state.Pose.Height, Is.EqualTo(1));
            state.Release(owner); Assert.That(state.Owner, Is.Null);
        }

        [Test]
        public void OlderCastCannotSampleOrClearTheNewCastBodyOwner()
        {
            var state = new AttackBodyFollow(); var older = new object(); var newer = new object();
            state.TryBegin(older, Slash, _profile); state.Strike(older);
            state.TryBegin(newer, Heavy, _profile); state.Windup(newer, 1); float pose = state.Pose.Degrees;
            state.Recovery(older, 1); state.Release(older);
            Assert.That(state.Owner, Is.SameAs(newer)); Assert.That(state.Pose.Degrees, Is.EqualTo(pose));
            state.Release(newer); Assert.That(state.Pose.Degrees, Is.Zero);
        }

        [TestCase("2HWeapon/Axe/CrimsonHook/CrimsonHookSkill")]
        [TestCase("2HWeapon/Sword/Adventurer/RetreatingThrust/RetreatingThrustSkill")]
        [TestCase("2HWeapon/Hammer/Adventurer/FallingAnvil/FallingAnvilSkill")]
        [TestCase("2HWeapon/Hammer/Adventurer/GroundShake/GroundShakeSkill")]
        [TestCase("2HWeapon/Sword/Adventurer/LungeSlash/LungeSlashSkill")]
        [TestCase("1HWeapon/Sword/DexterousLungeSkill")]
        [TestCase("1HWeapon/Hammer/StunningBlowSkill")]
        [TestCase("1HWeapon/Dagger/NinjaStubSkill")]
        [TestCase("Shields/TowerCharge")]
        [TestCase("Shields/SpectacularParry")]
        public void CustomMeleeRecipesAcquireBodyFollowWithoutEditingTheirData(string path)
        {
            var skill = Resources.Load<SkillDataSO>("Skills/" + path);
            Assert.That(skill, Is.Not.Null, path);
            string recipeBefore = JsonUtility.ToJson(skill.Recipe);
            var state = new AttackBodyFollow(); var owner = new object();
            Assert.That(state.TryBegin(owner, skill, _profile), Is.True, path);
            state.Windup(owner, 1); Assert.That(state.Pose.Degrees, Is.GreaterThan(0));
            state.Strike(owner); Assert.That(state.Pose.Degrees, Is.LessThan(0));
            state.Release(owner); Assert.That(state.Owner, Is.Null);
            Assert.That(JsonUtility.ToJson(skill.Recipe), Is.EqualTo(recipeBefore));
        }

        [TestCase("1HWeapon/Sword/DexterousLungeSkill", .6f)]
        [TestCase("1HWeapon/Hammer/StunningBlowSkill", .1f)]
        [TestCase("2HWeapon/Sword/Adventurer/LungeSlash/LungeSlashSkill", .4f)]
        public void MissingOrZeroLengthRecoveryHoldsContactThenEasesOutOnRecipeClock(string path, float start)
        {
            var state = new AttackBodyFollow(); var owner = new object();
            Assert.That(state.TryBegin(owner, Resources.Load<SkillDataSO>("Skills/" + path), _profile), Is.True);
            state.Windup(owner, 1); float preparation = state.Pose.Degrees;
            state.SamplePipeline(owner, 1); Assert.That(state.Pose.Degrees, Is.EqualTo(preparation), "No strike has occurred.");
            state.Strike(owner); float strike = state.Pose.Degrees;
            state.SamplePipeline(owner, start - .001f); Assert.That(state.Pose.Degrees, Is.EqualTo(strike));
            state.Recovery(owner, 1); Assert.That(state.Pose.Degrees, Is.EqualTo(strike), "Degenerate weapon recovery must not snap the body.");
            state.SamplePipeline(owner, start); Assert.That(state.Pose.Degrees, Is.EqualTo(strike));
            state.SamplePipeline(owner, (start + 1) * .5f); Assert.That(state.Pose.Degrees, Is.EqualTo(strike * .5f).Within(.0001f));
            state.SamplePipeline(owner, 1); Assert.That(state.Pose.Degrees, Is.Zero); Assert.That(state.Pose.Height, Is.EqualTo(1));
        }

        [Test]
        public void PipelineFallbackCannotOverwriteAuthoredRecoveryOrANewerOwner()
        {
            var state = new AttackBodyFollow(); var older = new object(); var newer = new object();
            var lunge = Resources.Load<SkillDataSO>("Skills/1HWeapon/Sword/DexterousLungeSkill");
            state.TryBegin(older, lunge, _profile); state.Strike(older);
            state.TryBegin(newer, Heavy, _profile); state.Strike(newer); state.Recovery(newer, .5f);
            float pose = state.Pose.Degrees;
            state.SamplePipeline(older, 1); state.SamplePipeline(newer, 1); state.Release(older);
            Assert.That(state.Owner, Is.SameAs(newer)); Assert.That(state.Pose.Degrees, Is.EqualTo(pose));
        }

        [Test]
        public void FailedBlinkEasesPreparationAwayWithoutInventingAHit()
        {
            var state = new AttackBodyFollow(); var owner = new object();
            state.TryBegin(owner, Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/NinjaStubSkill"), _profile);
            state.Windup(owner, 1); float preparation = state.Pose.Degrees;
            state.Recovery(owner, 0); Assert.That(state.Pose.Degrees, Is.EqualTo(preparation));
            state.Recovery(owner, .5f); Assert.That(state.Pose.Degrees, Is.EqualTo(preparation * .5f).Within(.0001f));
            state.Recovery(owner, 1); Assert.That(state.Pose.Degrees, Is.Zero);
        }

        [Test]
        public void LandingAttackIgnoresRecipeProgressUntilLandingAndCanBeCancelledInAir()
        {
            var state = new AttackBodyFollow(); var owner = new object();
            var skill = Resources.Load<SkillDataSO>("Skills/2HWeapon/Hammer/Adventurer/FallingAnvil/FallingAnvilSkill");
            state.TryBegin(owner, skill, _profile); state.Windup(owner, 1);
            float preparation = state.Pose.Degrees;
            state.SamplePipeline(owner, 1); Assert.That(state.Pose.Degrees, Is.EqualTo(preparation));
            state.Strike(owner); Assert.That(state.Pose.Degrees, Is.LessThan(0));
            state.Recovery(owner, .5f); Assert.That(state.Pose.Degrees, Is.LessThan(0));
            state.Recovery(owner, 1); Assert.That(state.Pose.Degrees, Is.Zero);
            state.TryBegin(owner, skill, _profile); state.Windup(owner, 1); state.Release(owner);
            Assert.That(state.Owner, Is.Null); Assert.That(state.Pose.Degrees, Is.Zero);
        }

        [Test]
        public void UnsupportedUtilityRangedAndSpinRecipesNeverAcquireAnOrdinaryBodyPose()
        {
            var state = new AttackBodyFollow(); var skill = Keep(Object.Instantiate(Slash));
            skill.WeaponRole = SkillWeaponRole.Equipment;
            Assert.That(state.TryBegin(new object(), skill, _profile), Is.False);
            foreach (string path in new[] { "Skills/2HWeapon/Axe/RightButton/SweepRB", "Skills/2HWeapon/Bow/LuckyShot/LuckyShotSkill",
                "Skills/2HWeapon/Sword/HorizonCrossSection/HorizonCrossSectionSkill",
                "Skills/2HWeapon/Axe/ConningTower/ConningTowerSkill", "Skills/1HWeapon/Wand/ManaSpear/ManaSpearSkill",
                "Skills/2HWeapon/Chakram/rb/OrbitalWeaponTrowRBSkill" })
            {
                var candidate = Resources.Load<SkillDataSO>(path);
                Assert.That(candidate, Is.Not.Null, path);
                Assert.That(state.TryBegin(new object(), candidate, _profile), Is.False, path);
            }
            _profile.EnableBodyFollow = false;
            Assert.That(state.TryBegin(new object(), Slash, _profile), Is.False);
            Assert.That(state.Owner, Is.Null);
        }

        private PlayerMovementVisual Player(string hero, int facing, bool grounded, out PlayerStats stats, out Transform hands)
        {
            var root = Keep(new GameObject("Combat body test")); root.SetActive(false);
            var attack = root.AddComponent<PlayerAttackInput>();
            stats = root.GetComponent<PlayerStats>() ?? root.AddComponent<PlayerStats>();
            if (stats.Health == null) Call(stats, "Awake");
            var data = Resources.Load<CharacterDataSO>("Heroes/" + hero + "Resources/" + hero);
            stats.Initialize(data); stats.Health.RestoreFull();
            var animation = root.GetComponent<PlayerCharacterAnimationController>() ?? root.AddComponent<PlayerCharacterAnimationController>();
            Call(animation, "Awake"); Call(animation, "ApplyCharacterData", data);
            var visuals = new GameObject("Visuals"); visuals.transform.SetParent(root.transform, false);
            hands = new GameObject("HandPivot").transform; hands.SetParent(visuals.transform, false);
            hands.localPosition = new Vector3(.2f, .3f, 0);
            var weapon = new GameObject("WeaponHolder"); weapon.transform.SetParent(hands, false);
            weapon.AddComponent<SpriteRenderer>().sprite = data.Portrait;
            var movement = root.GetComponent<PlayerMovement>(); Set(movement, "_rb", root.GetComponent<Rigidbody2D>());
            Set(movement, "_isGrounded", grounded);
            var visual = root.GetComponent<PlayerMovementVisual>() ?? root.AddComponent<PlayerMovementVisual>();
            if (visual.DisplayRenderer == null) Call(visual, "Awake");
            Set(visual, "_combatPoseProfile", _profile);
            root.transform.localScale = new Vector3(facing, 1, 1); root.SetActive(true);
            return visual;
        }

        [TestCase("Warrior", 1, true)] [TestCase("Warrior", -1, false)]
        [TestCase("Rogue", -1, true)] [TestCase("Rogue", 1, false)]
        [TestCase("Mage", 1, true)] [TestCase("Mage", -1, false)]
        public void BodyPosePreservesFeetPhysicsAndWeaponRenderPathForEveryClass(string hero, int facing, bool grounded)
        {
            var visual = Player(hero, facing, grounded, out var stats, out var hand);
            var root = stats.transform; var collider = root.GetComponent<BoxCollider2D>();
            Physics2D.SyncTransforms(); Bounds bounds = collider.bounds;
            var sourceSprite = root.GetComponent<SpriteRenderer>().sprite;
            var handAnimation = root.gameObject.AddComponent<SkillHandAnimation>(); handAnimation.Initialize(stats);
            handAnimation.SetActiveStyleId("overhead");
            object owner = new object(); Assert.That(visual.BeginAttackBody(owner, Heavy), Is.True);
            var camera = Keep(new GameObject("Combat render check")).AddComponent<Camera>(); camera.orthographic = true;
            var anchor = root.GetComponent<PlayerRenderAnchor>();
            foreach (float phase in new[] { .25f, .7f, 1f })
            {
                handAnimation.LerpWindup(phase); visual.SampleAttackWindup(owner, phase);
                _profile.EnableBodyFollow = false; Call(visual, "LateUpdate");
                var display = visual.DisplayRenderer;
                Vector3 foot = display.transform.TransformPoint(new Vector3(0, sourceSprite.bounds.min.y));
                anchor.BeginRender(camera); Vector3 weaponPosition = hand.position; Quaternion weaponRotation = hand.rotation; anchor.EndRender(camera);
                _profile.EnableBodyFollow = true; Call(visual, "LateUpdate");
                Assert.That(display.sprite, Is.SameAs(sourceSprite));
                Assert.That(Vector3.Distance(display.transform.TransformPoint(new Vector3(0, sourceSprite.bounds.min.y)), foot), Is.LessThan(.00001f));
                Assert.That(visual.VisibleAttackPose.Degrees, Is.GreaterThan(0));
                anchor.BeginRender(camera);
                Assert.That(Vector3.Distance(hand.position, weaponPosition), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(hand.rotation, weaponRotation), Is.LessThan(.001f)); anchor.EndRender(camera);
                Assert.That(root.position, Is.EqualTo(Vector3.zero)); Assert.That(root.rotation, Is.EqualTo(Quaternion.identity));
                Physics2D.SyncTransforms(); Assert.That(collider.bounds, Is.EqualTo(bounds));
            }
            visual.EndAttackBody(owner); Call(visual, "LateUpdate"); Assert.That(visual.VisibleAttackPose.Degrees, Is.Zero);
        }

        [TestCase("Warrior", 1)] [TestCase("Warrior", -1)]
        [TestCase("Rogue", 1)] [TestCase("Rogue", -1)]
        [TestCase("Mage", 1)] [TestCase("Mage", -1)]
        public void MovementWeightKeepsFeetPhysicsAndHandsAnchored(string hero, int facing)
        {
            var visual = Player(hero, facing, true, out var stats, out var hands);
            Call(visual, "LateUpdate"); var display = visual.DisplayRenderer; var sprite = display.sprite;
            Vector3 foot = display.transform.TransformPoint(new Vector3(0, sprite.bounds.min.y));
            Vector3 handPosition = hands.position, rootPosition = stats.transform.position;
            var body = stats.GetComponent<Rigidbody2D>(); body.linearVelocity = Vector2.right * 3;
            var state = (MovementWeightShift)typeof(PlayerMovementVisual).GetField("_movementWeight", Private).GetValue(visual);
            state.Sample(0, 0, 5, facing, .016f, true, _profile); state.Sample(3, 1, 5, facing, .016f, true, _profile);
            Set(visual, "_movementCharacter", stats.CurrentCharacterData);
            Call(visual, "LateUpdate");
            Assert.That(Mathf.Abs(visual.VisibleMovementPose.Degrees), Is.GreaterThan(0));
            Assert.That(Vector3.Distance(display.transform.TransformPoint(new Vector3(0, sprite.bounds.min.y)), foot), Is.LessThan(.00001f));
            Assert.That(hands.position, Is.EqualTo(handPosition)); Assert.That(stats.transform.position, Is.EqualTo(rootPosition));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 3));
            var token = new object(); visual.BeginAttackBody(token, Slash); visual.SampleAttackStrike(token); Call(visual, "LateUpdate");
            Assert.That(visual.VisibleMovementPose.Degrees, Is.Zero); Assert.That(visual.VisibleMovementPose.Height, Is.EqualTo(1));
        }

        [TestCase("spin")] [TestCase("dodge")] [TestCase("dash")] [TestCase("stasis")]
        public void SpecialPresentationTakesPriorityAndCleanupRestoresNeutral(string mode)
        {
            var visual = Player("Warrior", 1, true, out var stats, out _); object owner = new object();
            visual.BeginAttackBody(owner, Slash); visual.SampleAttackStrike(owner); Call(visual, "LateUpdate");
            Assert.That(visual.VisibleAttackPose.Degrees, Is.Not.Zero);
            var attack = stats.GetComponent<PlayerAttackInput>();
            if (mode == "spin") stats.GetComponent<PlayerCharacterAnimationController>().SampleSkillSpin(new object(), .5f);
            if (mode == "dodge" || mode == "dash")
            {
                Set(attack, "_isDodging", true); Set(attack, mode == "dodge" ? "_isStationaryDodge" : "_isGroundDash", true);
            }
            if (mode == "stasis") Set(stats.gameObject.AddComponent<PlayerStasisController>(), "<IsActive>k__BackingField", true);
            Call(visual, "LateUpdate"); Assert.That(visual.VisibleAttackPose.Degrees, Is.Zero);
            visual.EndAttackBody(owner); Assert.That(visual.AttackBodyOwner, Is.Null);
        }

        [TestCase("Warrior", "run")] [TestCase("Rogue", "jump")] [TestCase("Mage", "fall")]
        public void LocomotionKeepsItsOwnClipAndFootBaselineUnderAttackFollow(string hero, string mode)
        {
            var visual = Player(hero, 1, mode == "run", out var stats, out _);
            var controller = stats.GetComponent<PlayerCharacterAnimationController>();
            var rb = stats.GetComponent<Rigidbody2D>();
            if (mode == "run")
            {
                rb.linearVelocity = Vector2.right * 4;
                Set(controller, "_wasRawGrounded", true); Set(controller, "_groundContactStartedAt", Mathf.Max(0, Time.time - 1));
                // Test runner time can start at zero; the controller's normal confirmation window remains authoritative.
                if (Time.time < .06f) Call(controller, "Play", System.Enum.Parse(controller.GetType().GetNestedType("AnimationState", BindingFlags.NonPublic), "Run"),
                    controller.GetType().GetField("_run", Private).GetValue(controller));
                else Call(controller, "Update");
            }
            else if (mode == "jump") { rb.linearVelocity = Vector2.up * 4; Call(controller, "HandleJumpStarted"); }
            else { rb.linearVelocity = Vector2.down * 4; Call(controller, "Update"); }
            var source = stats.GetComponent<SpriteRenderer>(); Sprite sprite = source.sprite;
            Call(visual, "LateUpdate"); Vector3 foot = visual.DisplayRenderer.transform.TransformPoint(new Vector3(0, sprite.bounds.min.y));
            object token = new object(); visual.BeginAttackBody(token, Slash); visual.SampleAttackStrike(token); Call(visual, "LateUpdate");
            Assert.That(visual.DisplayRenderer.sprite, Is.SameAs(sprite));
            Assert.That(visual.VisibleAttackPose.Degrees, Is.Not.Zero);
            Assert.That(Vector3.Distance(visual.DisplayRenderer.transform.TransformPoint(new Vector3(0, sprite.bounds.min.y)), foot), Is.LessThan(.00001f));
            Call(visual, "OnDisable"); Assert.That(visual.AttackBodyOwner, Is.Null);
        }

        [Test]
        public void RecoilPulseRetriggersContinuouslyAndNeverAccumulatesBeyondItsCap()
        {
            var pulse = new RecoilPulse(); Assert.That(pulse.Trigger(0, 1, 1, _profile), Is.True);
            pulse.Sample(.05f, out var before, out _);
            Assert.That(pulse.Trigger(.05f, -1, 1, _profile), Is.True);
            pulse.Sample(.05f, out var after, out _); Assert.That(after, Is.EqualTo(before).Within(.000001f));
            Assert.That(pulse.Trigger(.051f, 1, 1, _profile), Is.False);
            _profile.RecoilPixels = 100; // Runtime safety also covers values outside the Inspector range.
            for (int i = 1; i < 60; i++)
            {
                float now = i * .06f; pulse.Trigger(now, 1, 100, _profile); pulse.Sample(now + .01f, out var offset, out _);
                Assert.That(Mathf.Abs(offset), Is.LessThanOrEqualTo(2.5f / 24f));
            }
            Assert.That(pulse.Sample(10, out _, out _), Is.False);
        }

        [Test]
        public void CancellingAnOlderRunnerCannotResetTheNewerBodyFollow()
        {
            var visual = Player("Warrior", 1, true, out var stats, out _);
            object older = new object(), newer = new object();
            var a = Keep(new GameObject("Old cast")).AddComponent<SkillStepRunner>();
            var b = Keep(new GameObject("New cast")).AddComponent<SkillStepRunner>();
            Set(a, "_bodyVisual", visual); Set(a, "_bodyOwner", older);
            Set(b, "_bodyVisual", visual); Set(b, "_bodyOwner", newer);
            visual.BeginAttackBody(older, Slash); visual.BeginAttackBody(newer, Heavy); visual.SampleAttackStrike(newer);
            a.Cancel(); Assert.That(visual.AttackBodyOwner, Is.SameAs(newer));
            b.Cancel(); Assert.That(visual.AttackBodyOwner, Is.Null); Call(visual, "LateUpdate");
            Assert.That(visual.VisibleAttackPose.Degrees, Is.Zero);
        }

        private EnemyEntity Enemy(string type, int facing)
        {
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_" + type);
            var enemy = Keep(Object.Instantiate(data.Prefab)); enemy.Setup(data, 1);
            enemy.GetComponent<EnemyBrain>().enabled = false;
            var stats = enemy.GetComponent<EnemyStats>();
            foreach (var stat in new[] { StatType.MaxHealth, StatType.StunThreshold })
            { stats.TryGetStat(stat, out var value); value.ClearAllModifiers(); value.BaseValue = 10000; }
            stats.TryGetStat(StatType.Armor, out var armor); armor.BaseValue = 0;
            enemy.GetComponent<EnemyHealth>().Initialize(); enemy.GetComponent<EnemyStunController>().RecalculateMaxMeter(true);
            var recoil = enemy.GetComponent<EnemyHitRecoil>(); Set(recoil, "_profile", _profile); recoil.Initialize(enemy.VisualRenderer);
            enemy.transform.localScale = new Vector3(facing, 1, 1);
            return enemy;
        }

        private void Hit(EnemyEntity enemy)
        {
            var source = Keep(new GameObject("Recoil test attacker")); source.transform.position = Vector3.left * 3;
            enemy.GetComponent<EnemyHealth>().TakeDamage(new DamageSnapshot(source)
            { Physical = 1, Impact = new AttackImpactContact(new AttackImpactContext(Slash, 0), source.transform.position, Vector2.right) });
        }

        [TestCase("Mold", 1)] [TestCase("Mold", -1)] [TestCase("Knight", 1)] [TestCase("Zombie", -1)]
        public void SheetAndAnimatorEnemiesRecoilWithoutMovingPhysicsOrChangingSourcePose(string type, int facing)
        {
            var enemy = Enemy(type, facing); var recoil = enemy.GetComponent<EnemyHitRecoil>();
            var source = enemy.VisualRenderer; var collider = enemy.GetComponent<Collider2D>();
            Physics2D.SyncTransforms(); Bounds body = collider.bounds;
            Vector3 root = enemy.transform.position; Quaternion rootRotation = enemy.transform.rotation;
            var block = new MaterialPropertyBlock(); int property = Shader.PropertyToID("_CombatPoseTest");
            block.SetFloat(property, .37f); source.SetPropertyBlock(block); source.color = new Color(.4f, .6f, .8f, .7f);
            Hit(enemy); Vector3 sourcePosition = source.transform.localPosition; var sprite = source.sprite;
            recoil.Sample(Time.time);
            Assert.That(recoil.DisplayRenderer, Is.Not.Null); Assert.That(recoil.DisplayRenderer.enabled, Is.True);
            Assert.That(source.forceRenderingOff, Is.True); Assert.That(source.sprite, Is.SameAs(sprite));
            Assert.That(recoil.DisplayRenderer.sprite, Is.SameAs(sprite)); Assert.That(recoil.DisplayRenderer.color, Is.EqualTo(source.color));
            recoil.DisplayRenderer.GetPropertyBlock(block); Assert.That(block.GetFloat(property), Is.EqualTo(.37f));
            Assert.That(source.transform.localPosition, Is.EqualTo(sourcePosition));
            Assert.That(enemy.transform.position, Is.EqualTo(root)); Assert.That(enemy.transform.rotation, Is.EqualTo(rootRotation));
            Physics2D.SyncTransforms(); Assert.That(collider.bounds, Is.EqualTo(body));
            Assert.That(recoil.DisplayRenderer.transform.position.x - source.transform.position.x, Is.GreaterThan(0), "World recoil must not reverse when the enemy faces left.");
            recoil.Sample(Time.time + 1); Assert.That(source.forceRenderingOff, Is.False); Assert.That(recoil.DisplayRenderer.enabled, Is.False);
        }

        [Test]
        public void BusyEnemyKeepsItsAttackAndRecoilResetsOnDeathHiddenOrReuse()
        {
            var enemy = Enemy("Mold", 1); var recoil = enemy.GetComponent<EnemyHitRecoil>();
            var target = Keep(new GameObject("Enemy attack target")); target.transform.position = enemy.transform.position + Vector3.left;
            var attack = enemy.GetComponent<EnemyAttackController>(); Assert.That(attack.TryStartAttack(target.transform), Is.True);
            Hit(enemy); recoil.Sample(Time.time); Assert.That(attack.IsBusy, Is.True);
            enemy.VisualRenderer.enabled = false; recoil.Sample(Time.time); Assert.That(recoil.DisplayRenderer.enabled, Is.False);
            Assert.That(enemy.VisualRenderer.forceRenderingOff, Is.False);
            enemy.VisualRenderer.enabled = true; recoil.Initialize(enemy.VisualRenderer); Hit(enemy); recoil.Sample(Time.time);
            Assert.That(recoil.DisplayRenderer.enabled, Is.True);
            CombatHitFeedback.Confirm(enemy.GetComponent<EnemyHealth>(), new DamageSnapshot(null) { Physical = 1 }, 1, true);
            Assert.That(recoil.DisplayRenderer.enabled, Is.False); Assert.That(enemy.VisualRenderer.forceRenderingOff, Is.False);
            enemy.VisualRenderer.forceRenderingOff = true; Hit(enemy); recoil.Sample(Time.time);
            Assert.That(recoil.DisplayRenderer.enabled, Is.False, "A hidden source must never be revealed by the overlay.");
        }

        [Test]
        public void FreezeAndPresentationDisableClearTheRecoilWithoutChangingItsSource()
        {
            var enemy = Enemy("Mold", 1); var recoil = enemy.GetComponent<EnemyHitRecoil>();
            Hit(enemy); recoil.Sample(Time.time); Assert.That(recoil.DisplayRenderer.enabled, Is.True);
            Set(enemy.GetComponent<EnemyFreezeController>(), "_isFrozen", true);
            recoil.Sample(Time.time); Assert.That(recoil.DisplayRenderer.enabled, Is.False);
            Assert.That(enemy.VisualRenderer.forceRenderingOff, Is.False);
            Set(enemy.GetComponent<EnemyFreezeController>(), "_isFrozen", false);
            Hit(enemy); recoil.Sample(Time.time); Assert.That(recoil.DisplayRenderer.enabled, Is.True);
            _profile.EnableEnemyRecoil = false; recoil.Sample(Time.time);
            Assert.That(recoil.DisplayRenderer.enabled, Is.False); Assert.That(enemy.VisualRenderer.forceRenderingOff, Is.False);
        }

        [Test]
        public void BossRecoilIsReducedAndCompressionDoesNotLiftItsFeet()
        {
            var enemy = Enemy("Mold", 1); var recoil = enemy.GetComponent<EnemyHitRecoil>();
            Set(enemy.GetComponent<EnemyStats>(), "<Rank>k__BackingField", EnemyRank.Boss);
            Hit(enemy); recoil.Sample(Time.time + _profile.RecoilDuration * .2f);
            var source = enemy.VisualRenderer; float foot = source.sprite.bounds.min.y;
            var sourceFoot = source.transform.TransformPoint(new Vector3(0, foot));
            var shownFoot = recoil.DisplayRenderer.transform.TransformPoint(new Vector3(0, foot));
            Assert.That(shownFoot.y, Is.EqualTo(sourceFoot.y).Within(.00001f));
            Assert.That(shownFoot.x - sourceFoot.x, Is.EqualTo(_profile.RecoilPixels * _profile.BossRecoilMultiplier / 24).Within(.00001f));
        }
    }
}
