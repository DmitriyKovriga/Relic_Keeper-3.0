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
    public class ThunderHammerTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner; SkillStepRunner _runner; Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(ThunderHammerBuilder.SkillPath);
        static StepEntry Hit => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "DealDamageRectangle");
        T Keep<T>(T item) where T : Object { _objects.Add(item); return item; }
        [SetUp] public void Setup()
        {
            _random = Random.state; var go = Keep(new GameObject("Thunder Hammer owner")); go.transform.position = new Vector2(9450, 7400);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100; _owner.GetStat(StatType.DamageLightning).BaseValue = 10;
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
            typeof(SkillStepRunner).GetMethod("ExecuteDealDamageRectangle",F).Invoke(_runner,new object[]{Skill.Recipe.Steps.IndexOf(Hit),Hit});
        }
        [Test] public void FreePrimaryIsRegisteredWithHeavyAnimationAndPixelLabArt()
        {
            Assert.That(Skill.WeaponRole,Is.EqualTo(SkillWeaponRole.MainHand)); Assert.That(Skill.Cooldown,Is.Zero); Assert.That(Skill.ManaCost,Is.Zero);
            Assert.That(Hit.GetFloat("DamageMultiplier"),Is.EqualTo(1)); Assert.That(Skill.SkillSpeedMultiplier,Is.EqualTo(1));
            Assert.That(Skill.SwingStyleId,Is.EqualTo("overhead")); Assert.That(Skill.Recipe.Steps.Single(s=>s.StepDefinition.Id=="WeaponWindup").EndPercentPipeline,Is.EqualTo(Hit.StartPercentPipeline));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(ThunderHammerBuilder.PoolPath).PossibleSkills.Any(entry=>entry.Skill==Skill),Is.True);
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills,Does.Contain(Skill));
            var icon=SkillIconBackgrounds.Read(Skill.Icon); try{Assert.That(icon.GetPixels32().All(pixel=>pixel.a==255),Is.True);}finally{Object.DestroyImmediate(icon);}
            var vfx=Skill.Recipe.Steps.Single(step=>step.StepDefinition.Id=="SpawnVFX").GetObject<GameObject>("VfxPrefab");
            Assert.That(vfx.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single().length,Is.InRange(.3f,.35f));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(ThunderHammerBuilder.Root+"Strike.png").OfType<Sprite>().Count(),Is.EqualTo(9));
        }
        [TestCase(0,0)] [TestCase(149,0)] [TestCase(150,5)] [TestCase(299,5)] [TestCase(300,10)] [TestCase(900,30)]
        public void EveryComplete150ArmorAddsFiveLightningWithoutConvertingPhysical(float armor,float bonus)
        {
            _owner.GetStat(StatType.Armor).BaseValue=armor; var target=Target(1); Strike(); var hit=target.Hits.Single();
            Assert.That(hit.Physical,Is.EqualTo(100).Within(.001)); Assert.That(hit.Lightning,Is.EqualTo(10+bonus).Within(.001));
            Assert.That(_owner.GetValue(StatType.DamageLightning),Is.EqualTo(10));
            var preview=SkillDpsPreview.Build(Skill,_owner,0); Assert.That(preview.Hit.LightningPerHit,Is.EqualTo(hit.Lightning).Within(.001)); Assert.That(preview.Hit.PhysicalPerHit,Is.EqualTo(hit.Physical).Within(.001));
        }
        [Test] public void ModifiedArmorCountsAndBothComponentsShareTheCriticalHit()
        {
            _owner.GetStat(StatType.Armor).BaseValue=100; _owner.GetStat(StatType.Armor).AddModifier(new StatModifier(50,StatModType.PercentAdd));
            _owner.GetStat(StatType.CritChance).BaseValue=100; _owner.GetStat(StatType.CritMultiplier).BaseValue=200;
            var target=Target(1); Strike(); var hit=target.Hits.Single(); Assert.That(hit.IsCrit,Is.True);
            Assert.That(hit.Physical,Is.EqualTo(200).Within(.001)); Assert.That(hit.Lightning,Is.EqualTo(30).Within(.001));
        }
        [TestCase(1f)] [TestCase(-1f)] public void BothComponentsUseTheSameFrontAreaAndCompoundHurtboxesHitOnce(float facing)
        {
            _owner.transform.localScale=new Vector3(facing,1,1); var first=Target(facing*.6f,true); var second=Target(facing*1.7f); var behind=Target(-facing); var far=Target(facing*3);
            Strike(); foreach(var target in new[]{first,second}) {Assert.That(target.Hits.Count,Is.EqualTo(1));Assert.That(target.Hits[0].Physical,Is.EqualTo(100));Assert.That(target.Hits[0].Lightning,Is.EqualTo(10));}
            Assert.That(behind.Hits,Is.Empty); Assert.That(far.Hits,Is.Empty);
        }
        [Test] public void AreaOfEffectExpandsBothComponentsTogether()
        {
            var target=Target(2.6f); Strike(); Assert.That(target.Hits,Is.Empty); _owner.GetStat(StatType.AreaOfEffect).BaseValue=100; Strike();
            Assert.That(target.Hits.Count,Is.EqualTo(1)); Assert.That(target.Hits[0].Lightning,Is.EqualTo(10));
        }
        [Test] public void PhysicalBuildsStunWhileArmorLightningBuildsNormalShockThreshold()
        {
            _owner.GetStat(StatType.DamageLightning).BaseValue=0; _owner.GetStat(StatType.Armor).BaseValue=3000;
            _owner.GetStat(StatType.ShockChance).BaseValue=100; _owner.GetStat(StatType.ShockDuration).BaseValue=2; _owner.GetStat(StatType.StunBuildUp).BaseValue=100;
            var go=Keep(new GameObject("Thunder Hammer enemy")); go.layer=7; go.transform.position=_owner.transform.position+Vector3.right;
            var stats=go.AddComponent<EnemyStats>(); stats.Initialize(null,1); stats.TryGetStat(StatType.MaxHealth,out var hp); hp.BaseValue=1000; stats.TryGetStat(StatType.StunThreshold,out var threshold); threshold.BaseValue=700; stats.TryGetStat(StatType.Armor,out var defenseArmor); defenseArmor.BaseValue=0; stats.TryGetStat(StatType.PhysicalResist,out var physicalResist); physicalResist.BaseValue=10;
            stats.TryGetStat(StatType.Evasion,out var evasion); if(evasion!=null)evasion.BaseValue=0;
            var health=go.AddComponent<EnemyHealth>(); health.Initialize(); var ailments=go.AddComponent<AilmentController>(); var stun=go.AddComponent<EnemyStunController>(); stun.Initialize(null);
            go.AddComponent<BoxCollider2D>().size=Vector2.one*.15f; float initial=stun.CurrentMeter;
            Strike(); Assert.That(stun.CurrentMeter,Is.LessThan(initial)); Assert.That(ailments.IsShocked,Is.False);
            Strike(); Assert.That(ailments.IsShocked,Is.False); Strike(); Assert.That(ailments.IsShocked,Is.True);
            Assert.That(stun.CurrentMeter,Is.EqualTo(initial-270).Within(.001));
        }
        [Test] public void CancellingBeforeImpactDoesNotDealEitherComponent()
        {
            var skill=Keep(Object.Instantiate(Skill)); var recipe=Keep(ScriptableObject.CreateInstance<SkillRecipeSO>()); recipe.Steps=new List<StepEntry>{Hit}; skill.Recipe=recipe;
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter",F).Invoke(_runner,new object[]{_owner,skill,0});
            typeof(SkillStepRunner).GetField("_ctx",F).SetValue(_runner,new SkillStepContext{OwnerStats=_owner,TotalDuration=100000,AoeScale=1}); var target=Target(1);
            var sequence=(IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe",F).Invoke(_runner,null);
            try {Assert.That(sequence.MoveNext(),Is.True);_runner.Cancel();Assert.That(sequence.MoveNext(),Is.False);Assert.That(target.Hits,Is.Empty);}finally{(sequence as System.IDisposable)?.Dispose();}
        }
        [TestCase("en","Thunder Hammer","Every 150","Physical damage builds stun","lightning damage helps inflict shock")]
        [TestCase("ru","Громовой молот","Каждые 150","Физический урон накапливает оглушение","урон молнией помогает наложить шок")]
        public void LocalesDescribeArmorBonusAndBothAilments(string code,string name,string armor,string stun,string shock)
        {
            var table=AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset"); Assert.That(table.GetEntry(Skill.NameKey)?.Value,Is.EqualTo(name));
            var text=SkillDescriptionGenerator.BuildAutomatic(Skill,code); foreach(string part in new[]{armor,stun,shock,"+5","100%"})Assert.That(text,Does.Contain(part));
        }
    }
}
