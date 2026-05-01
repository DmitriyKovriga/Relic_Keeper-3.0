using System.Collections.Generic;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.StatusEffects
{
    public enum StatusEffectKind
    {
        Buff,
        Debuff
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
