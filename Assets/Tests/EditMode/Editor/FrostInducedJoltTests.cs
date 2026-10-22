using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;
namespace RelicKeeper.Tests.EditMode
{
    public class FrostInducedJoltTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner; SkillStepRunner _runner; Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(FrostInducedJoltBuilder.SkillPath);
        static StepEntry Hit => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "DealDamageCircle");
        T Keep<T>(T item) where T : Object { _objects.Add(item); return item; }
        [SetUp] public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Frost Jolt owner")); go.transform.position = new Vector2(9450, 7400);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100; _owner.GetStat(StatType.DamageLightning).BaseValue = 0;
            _owner.GetStat(StatType.ColdResist).BaseValue = 0; _owner.GetStat(StatType.Pushback).BaseValue = 0;
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 0;
            _owner.GetStat(StatType.Armor).BaseValue = 0; _owner.GetStat(StatType.CritChance).BaseValue = 0; _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold }) _owner.GetStat(stat).BaseValue = 0;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f,1);
            _runner = go.AddComponent<SkillStepRunner>(); typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, Skill, 0 });
        }
        [TearDown] public void Cleanup()
        {
            for (int i=_objects.Count-1;i>=0;i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear(); Random.state = _random;
        }
        LuckyShotHitRecorder Target(float x, bool compound=false)
        {
            var go=Keep(new GameObject("Hammer target")); go.layer=7; go.transform.position=_owner.transform.position+Vector3.right*x; go.AddComponent<BoxCollider2D>().size=Vector2.one*.15f;
            if(compound){ var child=new GameObject("Extra hurtbox"); child.layer=7; child.transform.SetParent(go.transform,false); child.AddComponent<CircleCollider2D>().radius=.07f; }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        void Strike()
        {
            Physics2D.SyncTransforms(); var context=(SkillStepContext)typeof(SkillStepRunner).GetField("_ctx",F).GetValue(_runner); context.AoeScale=1+_owner.GetValue(StatType.AreaOfEffect)/100;
            typeof(SkillStepRunner).GetMethod("ExecuteDealDamageCircle",F).Invoke(_runner,new object[]{Skill.Recipe.Steps.IndexOf(Hit),Hit});
        }
        [Test] public void SpecialColdStaffCostsAndArtAreRegistered()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(15)); Assert.That(Skill.ManaCost, Is.EqualTo(25));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.ActionSpeedMode, Is.EqualTo(SkillActionSpeedMode.Spell));
            Assert.That(Skill.HoldStanceId, Is.EqualTo("staff")); Assert.That(Skill.EnablePushback, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(FrostInducedJoltBuilder.PoolPath).PossibleSkills.Count(entry=>entry.Skill==Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon=SkillIconBackgrounds.Read(Skill.Icon); try{Assert.That(icon.GetPixels32().All(p=>p.a==255),Is.True);}finally{Object.DestroyImmediate(icon);}
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(FrostInducedJoltBuilder.Root+"Strike.png").OfType<Sprite>().Count(),Is.EqualTo(10));
        }
        [TestCase(-25,750)] [TestCase(0,750)] [TestCase(1,760)] [TestCase(74.9f,1490)] [TestCase(75,1500)] [TestCase(100,1750)]
        public void ColdResistanceAddsSkillScopedKnockback(float resist,float expected)
        {
            _owner.GetStat(StatType.ColdResist).BaseValue=resist; var target=Target(1); Strike(); var hit=target.Hits.Single();
            Assert.That(hit.PushbackRating,Is.EqualTo(expected).Within(.001)); Assert.That(hit.Cold,Is.EqualTo(250).Within(.001));
            Assert.That(hit.Physical+hit.Fire+hit.Lightning,Is.Zero.Within(.001)); Assert.That(_owner.GetValue(StatType.Pushback),Is.Zero);
            Assert.That(_owner.GetValue(StatType.ColdResist),Is.EqualTo(resist));
        }
        [Test] public void ModifiedResistanceAndExistingPushbackBothContribute()
        {
            _owner.GetStat(StatType.ColdResist).BaseValue=50; _owner.GetStat(StatType.ColdResist).AddModifier(new StatModifier(25,StatModType.Flat));
            _owner.GetStat(StatType.Pushback).BaseValue=100; var target=Target(1); Strike();
            Assert.That(target.Hits.Single().PushbackRating,Is.EqualTo(1600)); Assert.That(_owner.GetValue(StatType.Pushback),Is.EqualTo(100));
        }
        [TestCase(1f)] [TestCase(-1f)] public void HitsBothSidesOnceAndPushOriginIsAlwaysTheCharacter(float facing)
        {
            _owner.transform.localScale=new Vector3(facing,1,1); var left=Target(-1,true); var right=Target(1,true); var far=Target(2);
            Strike(); foreach(var target in new[]{left,right}) {
                var hit=target.Hits.Single(); Assert.That(hit.HitOrigin,Is.EqualTo((Vector2)_owner.transform.position));
                int sign=PushbackResolver.ResolveHorizontalSign(hit.HitOrigin.x,target.transform.position.x,.15f);
                Assert.That(sign,Is.EqualTo(target==left?-1:1)); Assert.That(hit.PushbackRating,Is.EqualTo(750));
            } Assert.That(far.Hits,Is.Empty);
        }
        [TestCase(0f)] [TestCase(100f)] public void AreaOfEffectExpandsAroundTheSameCenterWithoutHittingBoxCorners(float aoe)
        {
            _owner.GetStat(StatType.AreaOfEffect).BaseValue=aoe;
            float radius=1.75f*(1+aoe/100); float vertical=1.75f*(1+aoe/200);
            var left=Target(-radius+.1f); var right=Target(radius-.1f); var outside=Target(radius+.2f);
            var corner=Target(radius-.1f); corner.transform.position+=Vector3.up*(vertical-.1f);
            Strike(); Assert.That(left.Hits.Count,Is.EqualTo(1)); Assert.That(right.Hits.Count,Is.EqualTo(1)); Assert.That(outside.Hits,Is.Empty); Assert.That(corner.Hits,Is.Empty);
        }
        [Test] public void SpellDamageAndCritScaleColdDamageAndMatchPreview()
        {
            _owner.GetStat(StatType.SpellDamage).AddModifier(new StatModifier(50,StatModType.PercentAdd));
            _owner.GetStat(StatType.CritChance).BaseValue=100; _owner.GetStat(StatType.CritMultiplier).BaseValue=200;
            var target=Target(1); Strike(); var hit=target.Hits.Single(); Assert.That(hit.IsCrit,Is.True); Assert.That(hit.Cold,Is.EqualTo(750).Within(.001));
            Assert.That(SkillDpsPreview.Build(Skill,_owner,0).Hit.ColdPerHit,Is.EqualTo(hit.Cold).Within(.001));
        }
        [TestCase(0f,false)] [TestCase(100f,true)] public void NormalFreezeChanceAndDurationStillApply(float chance,bool frozen)
        {
            _owner.GetStat(StatType.FreezeChance).BaseValue=chance; _owner.GetStat(StatType.FreezeDuration).BaseValue=2;
            var target=Target(1); var enemy=target.gameObject.AddComponent<EnemyStats>(); enemy.AddModifier(StatType.MaxHealth,new StatModifier(10000,StatModType.Flat));
            var health=target.gameObject.AddComponent<EnemyHealth>(); health.Initialize();
            var freeze=target.gameObject.AddComponent<EnemyFreezeController>(); target.gameObject.AddComponent<AilmentController>();
            Strike(); Assert.That(freeze.IsFrozen,Is.EqualTo(frozen));
            if(frozen)Assert.That((float)typeof(EnemyFreezeController).GetField("_frozenUntil",F).GetValue(freeze)-Time.time,Is.EqualTo(2).Within(.01));
        }
        [TestCase("en","Frost-induced Jolt","Cold Resistance","freeze")]
        [TestCase("ru","Морозный толчок","Сопротивление холоду","заморозки")]
        public void BothLocalesExplainWaveAndResistanceLink(string code,string title,string resistance,string freeze)
        {
            var table=AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value,Is.EqualTo(title));
            var text=SkillDescriptionGenerator.BuildAutomatic(Skill,code,stat=>stat==StatType.ColdResist?resistance:stat==StatType.Pushback?(code=="ru"?"Отталкивание":"Knockback"):SkillDescriptionGenerator.Humanize(stat.ToString()));
            Assert.That(text.ToLowerInvariant(),Does.Contain(resistance.ToLowerInvariant()));
            Assert.That(text,Does.Contain("+10")); Assert.That(text,Does.Contain(freeze));
        }
    }
}
