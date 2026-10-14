using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class WeaponSwapTestSkill : SkillBehaviour
    {
        public static float? TestTime;
        protected override float CurrentTime => TestTime ?? Time.time;
        protected override void Execute() { }
        public void BeginCasting() => _isCasting = true;
        public override void Cancel() => _isCasting = false;
    }

    public class WeaponSwapSkillLifetimeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new();
        private InventoryManager _previousInventory;
        private InventoryManager _inventory;
        private PlayerSkillManager _manager;

        [SetUp]
        public void SetUp()
        {
            _previousInventory = InventoryManager.Instance;
            SetInventorySingleton(null);
            WeaponSwapTestSkill.TestTime = null;
            _inventory = NewObject("Inventory").AddComponent<InventoryManager>();
            Invoke(_inventory, "Awake");
            _manager = NewObject("SkillOwner").AddComponent<PlayerSkillManager>();
            Invoke(_manager, "Awake");
            Invoke(_manager, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            SetInventorySingleton(_previousInventory);
            WeaponSwapTestSkill.TestTime = null;
        }

        [Test]
        public void SwapToEmptyPresetClearsSkillAndSwapBackPreservesCooldown()
        {
            var weapon = Item(EquipmentSlot.MainHand);
            _inventory.EquipmentItems[(int)EquipmentSlot.MainHand] = weapon;
            _manager.RefreshAllSkills();
            _manager.AddSkillCooldown(0, 8f);

            _inventory.SwapWeaponPreset();
            Assert.That(_manager.GetSkillData(0), Is.Null);
            _inventory.SwapWeaponPreset();

            Assert.That(_manager.GetSkillData(0), Is.SameAs(weapon.GrantedSkills[0]));
            Assert.That(_manager.GetSkillCooldownRemaining(0), Is.InRange(7.8f, 8f));
        }

        [Test]
        public void TwoHandedPresetKeepsBothCooldownsAndUnchangedUtilityCast()
        {
            var first = Item(EquipmentSlot.MainHand, true, 2);
            var second = Item(EquipmentSlot.MainHand, true, 2);
            _inventory.EquipmentItems[(int)EquipmentSlot.MainHand] = first;
            _inventory.EquipmentItems[(int)EquipmentSlot.Helmet] = Item(EquipmentSlot.Helmet);
            SetField(_inventory, "_storedMainHand", second);
            _manager.RefreshAllSkills();
            _manager.AddSkillCooldown(0, 8f);
            _manager.AddSkillCooldown(1, 6f);
            _manager.AddSkillCooldown(2, 4f);
            var utility = Active(2);

            _inventory.SwapWeaponPreset();
            Assert.That(_manager.GetSkillCooldownRemaining(0), Is.Zero);
            Assert.That(_manager.GetSkillCooldownRemaining(1), Is.Zero);
            Assert.That(Active(2), Is.SameAs(utility));
            utility.BeginCasting();
            _manager.RefreshAllSkills();
            Assert.That(utility.IsCasting, Is.True, "An unchanged equipment refresh must not cancel its skill.");
            _manager.AddSkillCooldown(0, 3f);
            _inventory.SwapWeaponPreset();

            Assert.That(_manager.GetSkillCooldownRemaining(0), Is.InRange(7.8f, 8f));
            Assert.That(_manager.GetSkillCooldownRemaining(1), Is.InRange(5.8f, 6f));
            Assert.That(_manager.GetSkillCooldownRemaining(2), Is.InRange(3.8f, 4f));
            _inventory.SwapWeaponPreset();
            Assert.That(_manager.GetSkillCooldownRemaining(0), Is.InRange(2.8f, 3f));
        }

        [Test]
        public void ExchangingHandsCarriesEachItemsCooldown()
        {
            var main = Item(EquipmentSlot.MainHand);
            var off = Item(EquipmentSlot.OffHand);
            _inventory.EquipmentItems[(int)EquipmentSlot.MainHand] = main;
            _inventory.EquipmentItems[(int)EquipmentSlot.OffHand] = off;
            _manager.RefreshAllSkills();
            _manager.AddSkillCooldown(0, 8f);
            _manager.AddSkillCooldown(1, 3f);

            _inventory.EquipmentItems[(int)EquipmentSlot.MainHand] = off;
            _inventory.EquipmentItems[(int)EquipmentSlot.OffHand] = main;
            _manager.RefreshAllSkills();

            Assert.That(_manager.GetSkillCooldownRemaining(0), Is.InRange(2.8f, 3f));
            Assert.That(_manager.GetSkillCooldownRemaining(1), Is.InRange(7.8f, 8f));
        }

        [Test]
        public void EquipmentRefreshAndPresetSwapDoNotDespawnOwnedProjectiles()
        {
            var owner = _manager.gameObject.AddComponent<PlayerStats>();
            SetField(_manager, "_playerStats", owner);
            var projectile = NewObject("LaunchedProjectile").AddComponent<SkillProjectile>();
            SetField(projectile, "_data", new SkillProjectileLaunchData { OwnerStats = owner });
            Invoke(projectile, "RegisterActive");
            _inventory.EquipmentItems[(int)EquipmentSlot.MainHand] = Item(EquipmentSlot.MainHand);
            _manager.RefreshAllSkills();
            _inventory.SwapWeaponPreset();

            Assert.That(projectile.gameObject.activeSelf, Is.True);
            Assert.That(typeof(SkillProjectile).GetField("_despawning", PrivateInstance).GetValue(projectile), Is.False);
            Assert.That(typeof(SkillProjectile).GetField("_registeredActive", PrivateInstance).GetValue(projectile), Is.True);
        }

        [Test]
        public void UnequippedCooldownAdvancesAndExpiresWithoutRestarting()
        {
            var data = Skill();
            var original = NewObject("OriginalSkill").AddComponent<WeaponSwapTestSkill>();
            WeaponSwapTestSkill.TestTime = 10f;
            original.Initialize(null, data);
            original.AddCooldownRemaining(8f);
            var state = original.CaptureCooldownState();
            var restored = NewObject("RestoredSkill").AddComponent<WeaponSwapTestSkill>();
            restored.Initialize(null, data);

            WeaponSwapTestSkill.TestTime = 12f;
            restored.RestoreCooldownState(state);
            Assert.That(restored.CooldownRemaining, Is.EqualTo(6f).Within(0.001f));
            WeaponSwapTestSkill.TestTime = 20f;
            restored.RestoreCooldownState(state);
            Assert.That(restored.CooldownRemaining, Is.Zero);
            Assert.That(state.IsRecoveringAt(20f), Is.False);
        }

        [Test]
        public void RestoredCooldownKeepsProgressWhenNewSlotHasDifferentDuration()
        {
            var data = Skill();
            var original = NewObject("OriginalSkill").AddComponent<WeaponSwapTestSkill>();
            WeaponSwapTestSkill.TestTime = 10f;
            original.Initialize(null, data);
            original.AddCooldownRemaining(8f);
            var state = original.CaptureCooldownState();
            data.Cooldown = 5f;
            var restored = NewObject("RestoredSkill").AddComponent<WeaponSwapTestSkill>();
            restored.Initialize(null, data);
            WeaponSwapTestSkill.TestTime = 12f;
            restored.RestoreCooldownState(state);
            Assert.That(restored.CooldownRemaining, Is.EqualTo(3f).Within(0.001f));
        }

        private GameObject NewObject(string name)
        {
            var obj = new GameObject(name);
            _objects.Add(obj);
            return obj;
        }

        private SkillDataSO Skill()
        {
            var data = ScriptableObject.CreateInstance<SkillDataSO>();
            _objects.Add(data);
            data.SkillName = "Swap regression";
            data.Cooldown = 10f;
            data.SkillPrefab = NewObject("TestSkillPrefab");
            data.SkillPrefab.AddComponent<WeaponSwapTestSkill>();
            return data;
        }

        private InventoryItem Item(EquipmentSlot slot, bool twoHanded = false, int skillCount = 1)
        {
            var data = ScriptableObject.CreateInstance<WeaponItemSO>();
            _objects.Add(data);
            data.Slot = slot;
            data.IsTwoHanded = twoHanded;
            var item = new InventoryItem(data);
            for (int i = 0; i < skillCount; i++) item.GrantedSkills.Add(Skill());
            return item;
        }

        private WeaponSwapTestSkill Active(int slot) => (WeaponSwapTestSkill)
            ((Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", PrivateInstance).GetValue(_manager))[slot];
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
        private static void SetField(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static void SetInventorySingleton(InventoryManager value) => typeof(InventoryManager)
            .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
}
