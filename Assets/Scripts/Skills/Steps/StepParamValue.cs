using UnityEngine;

namespace Scripts.Skills.Steps
{
    /// <summary> Один параметр степа (дефолт или оверрайд). По полю Type читать соответствующее значение. </summary>
    [System.Serializable]
    public class StepParamValue
    {
        public string Key;
        public ParamKind Type = ParamKind.Float;
        public float FloatVal;
        public int IntVal;
        public bool BoolVal;
        public string StringVal;
        public Object ObjectVal;

        public enum ParamKind { Float, Int, Bool, String, Object }

        public float GetFloat() => FloatVal;
        public int GetInt() => IntVal;
        public bool GetBool() => BoolVal;
        public string GetString() => StringVal ?? "";
        public Object GetObject() => ObjectVal;
    }
}
