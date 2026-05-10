using System.Collections.Generic;
using Scripts.GameplayEvents;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.StatusEffects
{
    public enum StatusEffectKind
    {
        Buff,
        Debuff
    }

    public enum DerivedStatEffectOperation
    {
        AddStatModifier,
        RestoreHealth,
        RestoreMana
    }

    public enum StatusEventSubject
    {
        CarrierAsTarget,
        CarrierAsSource,
        CarrierAsSourceOrTarget,
        Any
    }

    public enum StatusEventReactionAction
    {
        ApplyStatusEffect,
        ApplyQuickEffect,
        EndCurrentEffect,
        ExtendCurrentEffect
    }

    [System.Serializable]
    public struct DerivedStatModifier
    {
        public StatType SourceStat;
        [Min(0f)] public float SourcePercent;
        public StatType TargetStat;
        public StatModType TargetModifierType;
    }

    [System.Serializable]
    public sealed class StatusEventReaction
    {
        public GameplayEventType EventType;
        public StatusEventSubject Subject = StatusEventSubject.CarrierAsTarget;
        public StatusEventReactionAction Action = StatusEventReactionAction.EndCurrentEffect;

        public StatusEffectSO StatusEffectToApply;
        public StatusEffectKind QuickEffectKind = StatusEffectKind.Buff;
        [Min(0f)] public float QuickEffectDurationSeconds = 3f;
        public List<SerializableStatModifier> QuickModifiers = new List<SerializableStatModifier>();
        public List<DerivedStatModifier> QuickDerivedModifiers = new List<DerivedStatModifier>();
        [Min(0f)] public float ExtendSeconds = 1f;
    }

    [CreateAssetMenu(menuName = "RPG/Status Effect", fileName = "SE_")]
    public sealed class StatusEffectSO : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public StatusEffectKind Kind = StatusEffectKind.Buff;

        [Header("Presentation")]
        public string NameEn;
        public string NameRu;
        [TextArea(2, 4)] public string DescriptionEn;
        [TextArea(2, 4)] public string DescriptionRu;
        public Sprite Icon;
        public bool ShowInHud = true;

        [Header("Runtime")]
        [Min(0.05f)] public float BaseDurationSeconds = 5f;
        public List<SerializableStatModifier> Modifiers = new List<SerializableStatModifier>();
        public List<DerivedStatModifier> DerivedModifiers = new List<DerivedStatModifier>();
        public List<StatusEventReaction> EventReactions = new List<StatusEventReaction>();

        public float DurationSeconds => Mathf.Max(0.05f, BaseDurationSeconds);

        public string GetDisplayName(bool preferRu = true)
        {
            if (preferRu && !string.IsNullOrWhiteSpace(NameRu))
                return NameRu;

            if (!string.IsNullOrWhiteSpace(NameEn))
                return NameEn;

            if (!string.IsNullOrWhiteSpace(Id))
                return Id;

            return name;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
#endif
    }
}
