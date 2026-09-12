using UnityEngine;
using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Stats;
using Scripts.StatusEffects;

namespace Scripts.Skills.Steps
{
    /// <summary>
    /// Один шаг в рецепте скилла: ссылка на тип степа + оверрайды. Опционально — отложенный триггер или вложенные степы (ParallelGroup).
    /// </summary>
    [System.Serializable]
    public class StepEntry
    {
        [Tooltip("Тип степа (один asset на тип)")]
        public StepDefinitionSO StepDefinition;
        [Tooltip("Переопределения параметров для этого скилла")]
        public List<StepParamValue> Overrides = new List<StepParamValue>();
        [Tooltip("Outgoing damage conversion rules applied by this step before character stat conversions.")]
        public List<DamageConversionRule> DamageConversions = new List<DamageConversionRule>();
        [Tooltip("Private stat modifiers that exist only while this step builds its hit snapshot.")]
        public List<SerializableStatModifier> ScopedStatModifiers = new List<SerializableStatModifier>();
        [Tooltip("Private stat modifiers scaled by ailment stacks on each individual target.")]
        public List<TargetAilmentStackStatModifierRule> TargetAilmentStackModifiers = new List<TargetAilmentStackStatModifierRule>();
        [Tooltip("Extra actions triggered once for every target hit by this damage/projectile step.")]
        public List<SkillOnHitEffectRule> OnHitEffects = new List<SkillOnHitEffectRule>();

        [Header("Timing (percent of pipeline 0..1)")]
        [Range(0f, 1f)] [Tooltip("Момент начала степа (0 = старт пайплайна, 0.35 = 35%)")]
        public float StartPercentPipeline = 0f;
        [Range(0f, 1f)] [Tooltip("Момент конца степа. Для мгновенного степа = Start (один момент)")]
        public float EndPercentPipeline = 0f;

        [Header("Parallel group (only for ParallelGroup step type)")]
        [Tooltip("Подстепы, выполняемые одновременно")]
        public List<StepEntry> SubSteps = new List<StepEntry>();

        /// <summary> Мгновенный степ: срабатывает в один момент (Start == End). </summary>
        public bool IsInstant => Mathf.Approximately(StartPercentPipeline, EndPercentPipeline);
        public bool IsParallelGroup => StepDefinition != null && StepDefinition.Id == "ParallelGroup";

        /// <summary> Значение параметра: сначала оверрайд в этом степе, иначе дефолт из определения. </summary>
        public float GetFloat(string key, float defaultVal = 0f)
        {
            var o = Overrides.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Float);
            if (o != null) return o.FloatVal;
            if (StepDefinition != null && StepDefinition.TryGetDefault(key, out float v)) return v;
            return defaultVal;
        }
        public int GetInt(string key, int defaultVal = 0)
        {
            var o = Overrides.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Int);
            if (o != null) return o.IntVal;
            if (StepDefinition != null && StepDefinition.TryGetDefault(key, out int v)) return v;
            return defaultVal;
        }
        public bool GetBool(string key, bool defaultVal = false)
        {
            var o = Overrides.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Bool);
            if (o != null) return o.BoolVal;
            if (StepDefinition != null && StepDefinition.TryGetDefault(key, out bool v)) return v;
            return defaultVal;
        }
        public string GetString(string key, string defaultVal = null)
        {
            var o = Overrides.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.String);
            if (o != null) return o.StringVal ?? defaultVal;
            if (StepDefinition != null && StepDefinition.TryGetDefault(key, out string v)) return v ?? defaultVal;
            return defaultVal;
        }
        public T GetObject<T>(string key) where T : Object
        {
            var o = Overrides.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Object);
            if (o != null && o.ObjectVal is T t) return t;
            if (StepDefinition != null && StepDefinition.TryGetDefault(key, out Object obj) && obj is T t2) return t2;
            return null;
        }

        public void SetOverrideFloat(string key, float value)
        {
            var o = Overrides.Find(x => x.Key == key);
            if (o != null) { o.Type = StepParamValue.ParamKind.Float; o.FloatVal = value; }
            else Overrides.Add(new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value });
        }
        public void SetOverrideInt(string key, int value)
        {
            var o = Overrides.Find(x => x.Key == key);
            if (o != null) { o.Type = StepParamValue.ParamKind.Int; o.IntVal = value; }
            else Overrides.Add(new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Int, IntVal = value });
        }
        public void SetOverrideBool(string key, bool value)
        {
            var o = Overrides.Find(x => x.Key == key);
            if (o != null) { o.Type = StepParamValue.ParamKind.Bool; o.BoolVal = value; }
            else Overrides.Add(new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Bool, BoolVal = value });
        }
        public void SetOverrideObject(string key, Object value)
        {
            var o = Overrides.Find(x => x.Key == key);
            if (o != null) { o.Type = StepParamValue.ParamKind.Object; o.ObjectVal = value; }
            else Overrides.Add(new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Object, ObjectVal = value });
        }
    }

    [System.Serializable]
    public class TargetAilmentStackStatModifierRule
    {
        public AilmentType Ailment = AilmentType.Poison;
        public StatType Stat = StatType.DamagePhysical;
        public StatModType Type = StatModType.Flat;
        public float ValuePerStack = 10f;
        public int MaxStacksCounted;
    }

    public enum SkillOnHitEffectType
    {
        SpawnVfxDamageCircle = 0
    }

    [System.Serializable]
    public class SkillOnHitEffectRule
    {
        public SkillOnHitEffectType Type = SkillOnHitEffectType.SpawnVfxDamageCircle;
        public GameObject VfxPrefab;
        [Min(0.01f)] public float Lifetime = 0.35f;
        [Min(0.01f)] public float ScaleMultiplier = 1f;
        [Range(0f, 1f)] public float HitAtLifePercent = 0.5f;
        [Min(0.01f)] public float Radius = 1.2f;
        [Min(0f)] public float DamageMultiplier = 0.5f;
        public bool ExcludePrimaryTarget = true;
        public bool FadeOutEnabled = true;
        [Range(0f, 1f)] public float FadeOutStartLifePercent = 0.6f;
        [Range(0f, 1f)] public float FadeStartAlphaMultiplier = 0.5f;
    }
}
