using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class MysticShieldTestCast : SkillBehaviour
    {
        public Action OnExecute;
        protected override float CurrentTime => 100f;
        protected override void Execute() => OnExecute?.Invoke();
    }

    public class MysticShieldRepriseTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new();
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private GameObject Inactive(string name)
        {
            var go = Keep(new GameObject(name)); go.SetActive(false); return go;
        }
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(RelicKeeper.Editor.VFX.MysticShieldRepriseBuilder.SkillPath);

        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test] public void GeneratedMysticShieldRollsDefensiveAffixesAndReprise()
        {
            var shield = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(RelicKeeper.Editor.VFX.MysticShieldRepriseBuilder.ItemPath);
            var source = AssetDatabase.LoadAssetAtPath<AffixPoolSO>("Assets/Resources/Affixes/Pools/Helmets/Armore/Pool/HelmetsMysticPool.asset");
            Assert.That(shield.AffixPool, Is.Not.SameAs(source));
            Assert.That(shield.AffixPool.Slot, Is.EqualTo(EquipmentSlot.OffHand));
            Assert.That(shield.AffixPool.DefenseType, Is.EqualTo(ArmorDefenseType.MysticShield));
            Assert.That(shield.AffixPool.Affixes, Is.EquivalentTo(source.Affixes));
            Assert.That(shield.AffixPool.Affixes.Count, Is.GreaterThan(0));
            var item = ItemGenerator.GenerateRuntime(shield, 1, 1);
            Assert.That(item.GrantedSkills, Is.EqualTo(new[] { Skill }));
            Assert.That(item.Affixes.Count, Is.GreaterThan(0));
        }

        [Test] public void HeldSpriteIsSeparateNativePixelArtAndFeedbackIsConfigured()
        {
            var shield = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(RelicKeeper.Editor.VFX.MysticShieldRepriseBuilder.ItemPath);
            Assert.That(shield.InHandSprite, Is.Not.Null);
            Assert.That(shield.InHandSprite, Is.Not.SameAs(shield.Icon));
            Assert.That(shield.InHandSprite.rect.size, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(shield.InHandSprite.pixelsPerUnit, Is.EqualTo(24));
            Assert.That(shield.InHandSprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(Skill.Recipe.Steps.Find(s => s.StepDefinition.Id == "SpawnVFX").GetObject<GameObject>("VfxPrefab"), Is.Not.Null);
        }

        [Test] public void TooltipExplainsOtherSkillsAndRecipeAppliesOnUse()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(10));
            Assert.That(Skill.ManaCost, Is.EqualTo(10));
            var step = Skill.Recipe.Steps.Find(s => s.StepDefinition.Id == "ModifyCooldown");
            Assert.That(step.StartPercentPipeline, Is.Zero);
            Assert.That(step.EndPercentPipeline, Is.Zero);
            Assert.That(SkillDescriptionGenerator.Build(Skill, "en"), Does.Contain("other skills"));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "ru"), Does.Contain("остальных навыков"));
        }

        [TestCase(1)] [TestCase(0)]
        public void CastReducesOtherCooldownsClampsToZeroAndKeepsOwnCooldown(int shieldSlot)
        {
            var stats = Inactive("Reprise owner").AddComponent<PlayerStats>();
            stats.GetStat(StatType.MaxMana).BaseValue = 100;
            typeof(PlayerStats).GetMethod("Awake", Private).Invoke(stats, null);
            var manager = stats.gameObject.AddComponent<PlayerSkillManager>();
            var active = (Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", Private).GetValue(manager);
            var runner = Inactive("Reprise recipe").AddComponent<SkillStepRunner>();
            runner.SetRuntimeSlot(manager, shieldSlot);
            var step = Skill.Recipe.Steps.Find(s => s.StepDefinition.Id == "ModifyCooldown");
            var execute = typeof(SkillStepRunner).GetMethod("ExecuteModifyCooldown", Private);
            for (int i = 0; i < 6; i++)
            {
                var skill = Inactive("Equipped skill " + i).AddComponent<MysticShieldTestCast>();
                var data = i == shieldSlot ? Skill : Keep(ScriptableObject.CreateInstance<SkillDataSO>());
                if (i != shieldSlot) data.Cooldown = 20;
                skill.Initialize(stats, data); skill.SetRuntimeSlot(manager, i);
                active[i] = skill;
                if (i != shieldSlot) skill.AddCooldownRemaining(i == 2 ? 3 : i == 5 ? 0 : 8);
            }
            var reprise = (MysticShieldTestCast)active[shieldSlot];
            reprise.OnExecute = () => execute.Invoke(runner, new object[] { step });
            float manaBefore = stats.Mana.Current;
            reprise.TryCast();
            Assert.That(stats.Mana.Current, Is.EqualTo(manaBefore - 10));
            Assert.That(reprise.CooldownRemaining, Is.EqualTo(10).Within(0.001));
            for (int i = 0; i < 6; i++)
                if (i != shieldSlot) Assert.That(active[i].CooldownRemaining, Is.EqualTo(i == 2 || i == 5 ? 0 : 3).Within(0.001));
            reprise.TryCast();
            Assert.That(stats.Mana.Current, Is.EqualTo(manaBefore - 10), "Cannot cast again during its own cooldown.");
        }
    }
}
