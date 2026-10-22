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
    public class IcyPalisadeTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner; SkillStepRunner _runner; Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(IcyPalisadeBuilder.SkillPath);
        static StepEntry[] Hits => Skill.Recipe.Steps.Where(step => step.StepDefinition.Id == "DealDamageRectangle").ToArray();
        T Keep<T>(T item) where T : Object { _objects.Add(item); return item; }
        [SetUp] public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Frost Jolt owner")); go.transform.position = new Vector2(9450, 7400);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100; _owner.GetStat(StatType.DamageLightning).BaseValue = 0;
            _owner.GetStat(StatType.MaxMysticShield).BaseValue = 0; _owner.GetStat(StatType.ColdResist).BaseValue = 0; _owner.GetStat(StatType.Pushback).BaseValue = 0;
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
        void Strike(int group = 0)
        {
            Physics2D.SyncTransforms(); var context=(SkillStepContext)typeof(SkillStepRunner).GetField("_ctx",F).GetValue(_runner); context.AoeScale=1+_owner.GetValue(StatType.AreaOfEffect)/100;
            typeof(SkillStepRunner).GetMethod("ExecuteDealDamageRectangle",F).Invoke(_runner,new object[]{Skill.Recipe.Steps.IndexOf(Hits[group]),Hits[group]});
        }
        GameObject Ground(float x=0, float top=-.5f, float width=12, int layer=6)
        {
            var go=Keep(new GameObject("Palisade terrain")); go.layer=layer;
            go.transform.position=_owner.transform.position+new Vector3(x,top-.1f,0); go.AddComponent<BoxCollider2D>().size=new Vector2(width,.2f); return go;
        }
        [TestCase(0,250)] [TestCase(1,300)] [TestCase(3,400)] [TestCase(5,500)]
        public void CapacityAddsIndependentDamageMultiplier(int capacity,float expected)
        {
            Ground(); _owner.GetStat(StatType.MaxMysticShield).BaseValue=capacity; var target=Target(1.1f); Strike();
            var hit=target.Hits.Single(); Assert.That(hit.Cold,Is.EqualTo(expected).Within(.001)); Assert.That(hit.Physical+hit.Fire+hit.Lightning,Is.Zero.Within(.001));
            Assert.That(_owner.GetValue(StatType.SpellDamage),Is.Zero); Assert.That(_owner.GetValue(StatType.MaxMysticShield),Is.EqualTo(capacity));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(3)] public void RemainingChargesAreNeitherUsedNorConsumed(int current)
        {
            Ground(); _owner.GetStat(StatType.MaxMysticShield).BaseValue=3;
            var shield=_owner.GetComponent<MysticShieldController>(); typeof(MysticShieldController).GetMethod("Awake",F).Invoke(shield,null); shield.FillCharges();
            if(current<3)shield.TryConsumeCharges(3-current,out _); var target=Target(1.1f); Strike();
            Assert.That(target.Hits.Single().Cold,Is.EqualTo(400).Within(.001)); Assert.That(shield.CurrentCharges,Is.EqualTo(current));
        }
        [Test] public void SpellModifiersMultiplyCapacityBonusAndPreviewContainsAllThreeGroups()
        {
            Ground(); _owner.GetStat(StatType.MaxMysticShield).BaseValue=3;
            _owner.GetStat(StatType.SpellDamage).AddModifier(new StatModifier(50,StatModType.PercentAdd));
            var target=Target(1.1f); Strike(); Assert.That(target.Hits.Single().Cold,Is.EqualTo(600).Within(.001));
            Assert.That(SkillDpsPreview.Build(Skill,_owner,0).Hit.ColdPerHit,Is.EqualTo(1800).Within(.001));
        }
        [TestCase(1f)] [TestCase(-1f)] public void ThreeGroupsHitForwardOnceEachWithCompoundHurtboxes(float facing)
        {
            Ground(); _owner.transform.localScale=new Vector3(facing,1,1);
            var targets=new[]{Target(1.1f*facing,true),Target(2.4f*facing,true),Target(3.7f*facing,true)}; var behind=Target(-1.1f*facing);
            for(int group=0;group<3;group++) { Strike(group); for(int i=0;i<3;i++)Assert.That(targets[i].Hits.Count,Is.EqualTo(i<=group?1:0)); }
            Assert.That(behind.Hits,Is.Empty);
        }
        [Test] public void OverlappingSpikesDoNotMultiplyAGroupHitButLaterGroupsCanHitAgain()
        {
            Ground(); var target=Target(2.4f,true); target.GetComponent<BoxCollider2D>().size=new Vector2(6,1);
            for(int i=0;i<3;i++)Strike(i); Assert.That(target.Hits.Count,Is.EqualTo(3));
        }
        [Test] public void EverySpikeSamplesItsOwnSteppedGround()
        {
            float[] xs={.55f,1.1f,1.65f}; float[] tops={-.5f,0,.2f}; var targets=new List<LuckyShotHitRecorder>();
            for(int i=0;i<3;i++){Ground(xs[i],tops[i],.35f);var target=Target(xs[i]);target.transform.position+=Vector3.up*(tops[i]+.8f);targets.Add(target);}
            Strike(); foreach(var target in targets)Assert.That(target.Hits.Count,Is.EqualTo(1));
        }
        [Test] public void GapsAndTallWallsDoNotCreateFloatingOrEmbeddedSpikes()
        {
            Ground(.55f,-.5f,.35f); var supported=Target(.55f); var unsupported=Target(1.65f); Strike();
            Assert.That(supported.Hits.Count,Is.EqualTo(1)); Assert.That(unsupported.Hits,Is.Empty);
            Ground(); var wall=Ground(1.1f,3,.4f); wall.GetComponent<BoxCollider2D>().size=new Vector2(.4f,7);
            var insideWall=Target(1.1f); Strike(); Assert.That(insideWall.Hits,Is.Empty);
        }
        [Test] public void SlopesAndOneWayPlatformsSupportSpikes()
        {
            int platform=LayerMask.NameToLayer("OneWayPlatform"); Assert.That(platform,Is.GreaterThanOrEqualTo(0));
            Ground(1.1f,-.5f,2,platform); var target=Target(1.1f); Strike(); Assert.That(target.Hits.Count,Is.EqualTo(1));
            var slope=Keep(new GameObject("Palisade slope")); slope.layer=6; slope.transform.position=_owner.transform.position;
            slope.AddComponent<PolygonCollider2D>().points=new[]{new Vector2(2,-.5f),new Vector2(4,.3f),new Vector2(4,-1),new Vector2(2,-1)};
            var upper=Target(3.7f); upper.transform.position+=Vector3.up*.6f; Strike(2); Assert.That(upper.Hits.Count,Is.EqualTo(1));
        }
        [Test] public void AreaEnlargesSpikesWithoutChangingTheirSamplePositions()
        {
            Ground(); var target=Target(2.1f); Strike(); Assert.That(target.Hits,Is.Empty);
            _owner.GetStat(StatType.AreaOfEffect).BaseValue=100; Strike(); Assert.That(target.Hits.Count,Is.EqualTo(1));
        }
        [Test] public void SkillDataHasSequentialGroupsAndNoSolidVfxColliders()
        {
            Assert.That(Skill.Cooldown,Is.EqualTo(10)); Assert.That(Skill.ManaCost,Is.EqualTo(25)); Assert.That(Skill.WeaponRole,Is.EqualTo(SkillWeaponRole.Special));
            Assert.That(Skill.ActionSpeedMode,Is.EqualTo(SkillActionSpeedMode.Spell)); Assert.That(Hits.Length,Is.EqualTo(3));
            Assert.That(Hits.Select(h=>h.StartPercentPipeline),Is.EqualTo(new[]{.3f,.45f,.6f}).Within(.001));
            Assert.That(Hits.All(h=>h.GetInt("SpikesPerGroup")==3),Is.True);
            var prefab=Hits[0].GetObject<GameObject>("VfxPrefab"); Assert.That(prefab.GetComponentsInChildren<Collider2D>(true),Is.Empty);
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills,Does.Contain(Skill));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(IcyPalisadeBuilder.PoolPath).PossibleSkills.Count(s=>s.Skill==Skill),Is.EqualTo(1));
            var icon=SkillIconBackgrounds.Read(Skill.Icon);try{Assert.That(icon.GetPixels32().All(p=>p.a==255),Is.True);}finally{Object.DestroyImmediate(icon);}
        }
        [TestCase("en","Icy Palisade","charges are not consumed")]
        [TestCase("ru","Ледяной частокол","заряды не расходуются")]
        public void LocalesExplainCapacityAndChargePreservation(string code,string name,string text)
        {
            var table=AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value,Is.EqualTo(name));
            var description=SkillDescriptionGenerator.BuildAutomatic(Skill,code); Assert.That(description,Does.Contain(text)); Assert.That(description,Does.Contain("+20"));
        }
    }
}
