using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Skills.Steps
{
    /// <summary>
    /// Определение типа степа: один asset на тип. Дефолтные параметры, отображаемые имена EN/RU только для редактора.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Step Definition", fileName = "Step_")]
    public class StepDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        [Tooltip("Display name in editor (EN)")]
        public string NameEn;
        [Tooltip("Display name in editor (RU)")]
        public string NameRu;
        [TextArea(1, 2)] public string DescriptionEn;
        [TextArea(1, 2)] public string DescriptionRu;
        public Sprite Icon;

        [Header("Defaults (merged with skill overrides at runtime)")]
        public List<StepParamValue> DefaultParams = new List<StepParamValue>();

        public string GetDisplayName(bool useRu)
        {
            if (useRu && !string.IsNullOrEmpty(NameRu)) return NameRu;
            return string.IsNullOrEmpty(NameEn) ? (Id ?? name) : NameEn;
        }

        /// <summary> Степ с диапазоном (Start % — End %): Lock, Windup, Recovery, Wait. Остальные — мгновенные в один момент. </summary>
        public bool IsDurationStep => Id == "MovementLock" || Id == "WeaponWindup" || Id == "WeaponRecovery" || Id == "Wait";

        /// <summary> Получить дефолтное значение по ключу. </summary>
        public bool TryGetDefault(string key, out float value)
        {
            value = 0f;
            var p = DefaultParams.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Float);
            if (p == null) return false;
            value = p.FloatVal;
            return true;
        }
        public bool TryGetDefault(string key, out int value)
        {
            value = 0;
            var p = DefaultParams.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Int);
            if (p == null) return false;
            value = p.IntVal;
            return true;
        }
        public bool TryGetDefault(string key, out bool value)
        {
            value = false;
            var p = DefaultParams.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Bool);
            if (p == null) return false;
            value = p.BoolVal;
            return true;
        }
        public bool TryGetDefault(string key, out string value)
        {
            value = null;
            var p = DefaultParams.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.String);
            if (p == null) return false;
            value = p.StringVal;
            return true;
        }
        public bool TryGetDefault(string key, out Object value)
        {
            value = null;
            var p = DefaultParams.Find(x => x.Key == key && x.Type == StepParamValue.ParamKind.Object);
            if (p == null) return false;
            value = p.ObjectVal;
            return true;
        }
    }
}
