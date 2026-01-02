public enum StatModType { Flat = 100, PercentAdd = 200, PercentMult = 300 }

[System.Serializable]
public class StatModifier
{
    public float Value;
    public StatModType Type;
    public object Source;

    public StatModifier(float value, StatModType type, object source)
    {
        Value = value;
        Type = type;
        Source = source;
    }
}