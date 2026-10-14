using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Configuration;
using Scripts.Enemies;
using Scripts.GameplayEvents;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class ShieldParryTests
    {
        private readonly List<Object> _created = new List<Object>();
        private PlayerStats _player;
        private PlayerDamageReceiver _receiver;
        private PlayerParryController _parry;
        private EnemyHealth _attacker;
        private EnemyStunController _stun;
        private PassiveEffectRuntime _runtime;
        private bool _immortal;
        private float _time;
        private object _cast;

        private T Keep<T>(T value) where T : Object { _created.Add(value); return value; }
        private static void Awake(object target) => target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

        [SetUp]
        public void SetUp()
        {
            _immortal = PlaytestConfiguration.PlayerImmortal;
            PlaytestConfiguration.SetEditorPlayerImmortal(false);
            var player = Keep(new GameObject("Parry player"));
            _player = player.AddComponent<PlayerStats>(); Awake(_player);
            _player.GetStat(StatType.MaxHealth).BaseValue = 100;
            _player.Health.RestoreFull();
            _receiver = player.AddComponent<PlayerDamageReceiver>(); Awake(_receiver);
            _parry = player.AddComponent<PlayerParryController>();
            _time = 10; _parry.Clock = () => _time; _cast = new object();
            _attacker = Enemy("Attacker");
            _stun = _attacker.GetComponent<EnemyStunController>();
        }

        private EnemyHealth Enemy(string name)
        {
            var go = Keep(new GameObject(name));
            var stats = go.AddComponent<EnemyStats>();
            stats.Initialize(null, 1);
            var health = go.AddComponent<EnemyHealth>(); Awake(health); health.Initialize();
            health.DestroyOnDeath = false;
            var stun = go.AddComponent<EnemyStunController>(); stun.Initialize(null);
            return health;
        }

        [TearDown]
        public void TearDown()
        {
            _runtime?.Dispose(); _runtime = null;
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            PlaytestConfiguration.SetEditorPlayerImmortal(_immortal);
        }

        private void Open() => _parry.OpenWindow(_cast, 0.4f, new CombatTriggerDamage { StunPercentOfMax = 25 });
        private DamageSnapshot Hit(EnemyHealth enemy = null) => new DamageSnapshot(enemy != null ? enemy : _attacker) { Physical = 10 };

        [Test]
        public void EveryHitIsAbsorbedAndRetaliationTargetsEachActualAttacker()
        {
            var other = Enemy("Other attacker");
            int count = 0;
            void Observe(GameplayEventContext e)
            {
                if (e.Type != GameplayEventType.Parried) return;
                count++; Assert.That(e.Target, Is.EqualTo(_player.gameObject));
                Assert.That(e.Source == _attacker.gameObject || e.Source == other.gameObject, Is.True);
            }
            GameplayEventBus.EventRaised += Observe;
            try
            {
                Open();
                Assert.That(_receiver.TakeDamageDetailed(Hit()).WasParried, Is.True);
                Assert.That(_receiver.TakeDamageDetailed(Hit(other)).WasParried, Is.True);
                Assert.That(_player.Health.Current, Is.EqualTo(100));
                Assert.That(_attacker.CurrentHealth, Is.EqualTo(_attacker.MaxHealth));
                Assert.That(_stun.Normalized, Is.EqualTo(0.75f).Within(0.0001));
                Assert.That(other.GetComponent<EnemyStunController>().Normalized, Is.EqualTo(0.75f).Within(0.0001));
                Assert.That(count, Is.EqualTo(2));
                Assert.That(_parry.IsActive, Is.True);
            }
            finally { GameplayEventBus.EventRaised -= Observe; }
        }

        [Test]
        public void FourParriesBreakTheStunMeterWithoutDamagingHP()
        {
            Open();
            for (int i = 0; i < 4; i++) Assert.That(_receiver.TakeDamage(Hit()), Is.False);
            Assert.That(_stun.IsStunned, Is.True);
            Assert.That(_attacker.CurrentHealth, Is.EqualTo(_attacker.MaxHealth));
        }

        [Test]
        public void WindowExpiresAtBoundaryAndCorrectOwnerCanCancel()
        {
            Open(); _time = 10.4f;
            Assert.That(_parry.TryParry(Hit()), Is.False);
            _time = 20; Open();
            _parry.CloseWindow(new object());
            Assert.That(_parry.IsActive, Is.True);
            _parry.CloseWindow(_cast);
            Assert.That(_parry.TryParry(Hit()), Is.False);
        }

        [Test]
        public void DamageOverTimeAndEnvironmentalDamageAreNotParried()
        {
            Open(); var dot = Hit(); dot.IsDirectHit = false;
            Assert.That(_parry.TryParry(dot), Is.False);
            Assert.That(_parry.TryParry(new DamageSnapshot(null) { Physical = 10 }), Is.False);
            Assert.That(_parry.TryParry(new DamageSnapshot(_attacker) { Physical = 0 }), Is.False);
            Assert.That(_stun.Normalized, Is.EqualTo(1));
        }

        [Test]
        public void EditorImmortalityStillAllowsTestingParry()
        {
            PlaytestConfiguration.SetEditorPlayerImmortal(true); Open();
            Assert.That(_receiver.TakeDamageDetailed(Hit()).WasParried, Is.True);
        }

        [Test]
        public void IncomingHitIsStillParriedIfItsAttackerDiedAfterLaunchingIt()
        {
            var incoming = Hit();
            _attacker.ApplyPureDamage(_attacker.MaxHealth, _player);
            Assert.That(_attacker.IsDead, Is.True);
            Open();
            Assert.That(_receiver.TakeDamageDetailed(incoming).WasParried, Is.True);
            Assert.That(_player.Health.Current, Is.EqualTo(100));
        }

        private void Passives(params PassiveTriggeredEffect[] effects)
        {
            var template = Keep(ScriptableObject.CreateInstance<PassiveNodeTemplateSO>());
            template.TriggeredEffects = new List<PassiveTriggeredEffect>(effects);
            _runtime = new PassiveEffectRuntime(_player) { Clock = () => _time, Random01 = () => 0 };
            _runtime.Rebuild(new[] { new PassiveNodeDefinition { ID = "ParryTest", Template = template } });
        }

        private static PassiveTriggeredEffect Trigger(PassiveEffectTarget target) => new PassiveTriggeredEffect
        {
            Trigger = new PassiveEventFilter { Event = GameplayEventType.Parried, Subject = StatusEventSubject.CarrierAsTarget },
            EffectTarget = target
        };

        [Test]
        public void PassiveParryCanBuffPlayerAndDebuffTheAttacker()
        {
            var self = Trigger(PassiveEffectTarget.Owner);
            self.Action = PassiveTriggerAction.ApplyBuff;
            self.BuffModifiers.Add(new SerializableStatModifier { Stat = StatType.MoveSpeed, Type = StatModType.Flat, Value = 3 });
            var enemy = Trigger(PassiveEffectTarget.OtherParticipant);
            enemy.Action = PassiveTriggerAction.ApplyBuff; enemy.InlineEffectKind = StatusEffectKind.Debuff;
            enemy.BuffModifiers.Add(new SerializableStatModifier { Stat = StatType.MoveSpeed, Type = StatModType.Flat, Value = -2 });
            float old = _player.GetValue(StatType.MoveSpeed);
            Passives(self, enemy); Open(); _receiver.TakeDamage(Hit());
            Assert.That(_player.GetValue(StatType.MoveSpeed), Is.EqualTo(old + 3));
            var controller = _attacker.GetComponent<StatusEffectController>();
            Assert.That(controller.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(controller.ActiveEffects[0].RuntimeKind, Is.EqualTo(StatusEffectKind.Debuff));
        }

        [Test]
        public void PassiveDamageUsesSeparateHpAndStunAmountsOnTheParriedAttacker()
        {
            var effect = Trigger(PassiveEffectTarget.OtherParticipant);
            effect.Action = PassiveTriggerAction.DealDamage;
            effect.Damage = new CombatTriggerDamage { HealthDamage = 7, StunPercentOfMax = 10 };
            var bystander = Enemy("Bystander");
            Passives(effect); Open(); _receiver.TakeDamage(Hit());
            Assert.That(_attacker.CurrentHealth, Is.EqualTo(_attacker.MaxHealth - 7));
            Assert.That(_stun.Normalized, Is.EqualTo(0.65f).Within(0.0001));
            Assert.That(bystander.CurrentHealth, Is.EqualTo(bystander.MaxHealth));
            Assert.That(_player.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void DamageTriggerCannotRecursivelyTriggerItself()
        {
            var effect = Trigger(PassiveEffectTarget.EventTarget);
            effect.Trigger.Event = GameplayEventType.DamageDealt;
            effect.Trigger.Subject = StatusEventSubject.CarrierAsSource;
            effect.Action = PassiveTriggerAction.DealDamage; effect.Damage.HealthDamage = 2;
            Passives(effect);
            GameplayEventBus.Raise(GameplayEventType.DamageDealt, _player.gameObject, _attacker.gameObject, amount: 1);
            Assert.That(_attacker.CurrentHealth, Is.EqualTo(_attacker.MaxHealth - 2));
        }

        [Test]
        public void AttackSpeedIsOneUnarmedAndUsesEachWeaponsOwnBase()
        {
            Assert.That(_player.GetValue(StatType.AttackSpeed), Is.EqualTo(1));
            var dagger = Keep(ScriptableObject.CreateInstance<WeaponItemSO>());
            dagger.Slot = EquipmentSlot.MainHand; dagger.AttacksPerSecond = 1.8f;
            var shield = Keep(ScriptableObject.CreateInstance<WeaponItemSO>());
            shield.Slot = EquipmentSlot.OffHand; shield.AttacksPerSecond = 0.9f;
            var main = new InventoryItem(dagger); var off = new InventoryItem(shield);
            _player.ApplyItemModifiers(main); _player.ApplyItemModifiers(off);
            _player.GetStat(StatType.AttackSpeed).AddModifier(new StatModifier(50, StatModType.PercentAdd, this));
            Assert.That(new WeaponHandStatsProvider(_player, off).GetValue(StatType.AttackSpeed), Is.EqualTo(2.7f).Within(0.0001));
            Assert.That(new WeaponHandStatsProvider(_player, main).GetValue(StatType.AttackSpeed), Is.EqualTo(1.35f).Within(0.0001));
            _player.ClearAllStatModifiers();
            Assert.That(_player.GetValue(StatType.AttackSpeed), Is.EqualTo(1));
        }

        [Test]
        public void BucklerHasDefensivePoolAndGuaranteedParrySkill()
        {
            var buckler = AssetDatabase.LoadAssetAtPath<WeaponItemSO>("Assets/Scripts/Items/Shields/LowTier/Evasion/Adventurer's Buckler/Adventurer's Buckler.asset");
            Assert.That(buckler.InHandSprite, Is.Not.Null);
            Assert.That(buckler.AffixPool.Affixes.Count, Is.GreaterThan(10));
            var item = ItemGenerator.GenerateRuntime(buckler, 1, 1);
            Assert.That(item.GrantedSkills[0].ID, Is.EqualTo("SpectacularParry"));
            Assert.That(item.Affixes.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LocalDefenseIncreaseOnlyScalesTheShieldsOwnDefense()
        {
            var shield = Keep(ScriptableObject.CreateInstance<WeaponItemSO>());
            shield.Slot = EquipmentSlot.OffHand;
            shield.ImplicitModifiers = new List<EquipmentItemSO.ItemStatModifier>
            {
                new EquipmentItemSO.ItemStatModifier { Stat = StatType.Evasion, Type = StatModType.Flat, Value = 40, Scope = StatScope.Global },
                new EquipmentItemSO.ItemStatModifier { Stat = StatType.Evasion, Type = StatModType.PercentAdd, Value = 50, Scope = StatScope.Local }
            };
            _player.GetStat(StatType.Evasion).BaseValue = 100;
            _player.ApplyItemModifiers(new InventoryItem(shield));
            Assert.That(_player.GetValue(StatType.Evasion), Is.EqualTo(160));
        }

        [Test]
        public void RepeatedWindowTicksDoNotExtendItAndCancelClosesIt()
        {
            var go = Keep(new GameObject("Parry recipe runner"));
            go.SetActive(false);
            var runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, _player);
            var definition = Keep(ScriptableObject.CreateInstance<StepDefinitionSO>()); definition.Id = "ParryWindow";
            var step = new StepEntry { StepDefinition = definition };
            var execute = typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", BindingFlags.Instance | BindingFlags.NonPublic);
            execute.Invoke(runner, new object[] { 2, step, 0f, 0.4f });
            _time += 0.3f;
            execute.Invoke(runner, new object[] { 2, step, 0.75f, 0.4f });
            _time += 0.2f;
            Assert.That(_parry.IsActive, Is.False);
            runner.Cancel();
            _time += 1;
            execute.Invoke(runner, new object[] { 2, step, 0f, 0.4f });
            Assert.That(_parry.IsActive, Is.True);
            runner.Cancel();
            Assert.That(_parry.IsActive, Is.False);
        }
    }
}
