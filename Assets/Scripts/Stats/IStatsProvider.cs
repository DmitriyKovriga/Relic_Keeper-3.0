namespace Scripts.Stats
{
    /// <summary>
    /// Источник значений статов. Используется для расчёта урона, скиллов, баффов и т.д.
    /// Позволяет не завязываться на PlayerStats — можно передавать временный контекст (бафф, аура).
    /// </summary>
    public interface IStatsProvider
    {
        float GetValue(StatType type);
    }
}
