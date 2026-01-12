using System.Text;

namespace Scripts.Combat
{
    /// <summary>
    /// Контейнер данных об уроне. Передается от Атакующего к Жертве.
    /// </summary>
    [System.Serializable]
    public class DamageSnapshot
    {
        // Источник урона (кто ударил)
        public object Source; 

        // Типы урона (Raw values before mitigation)
        public float Physical;
        public float Fire;
        public float Cold;
        public float Lightning;
        // Можно добавить Chaos, if needed

        // Мета-данные
        public bool IsCrit;
        public float CritMultiplier; // 1.5 = 150%

        // Хелпер для получения суммы
        public float TotalDamage => Physical + Fire + Cold + Lightning;

        public DamageSnapshot(object source)
        {
            Source = source;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (IsCrit) sb.Append("[CRIT!] ");
            sb.Append($"Total: {TotalDamage:F1} (");
            if (Physical > 0) sb.Append($"Phys: {Physical:F1} ");
            if (Fire > 0) sb.Append($"Fire: {Fire:F1} ");
            if (Cold > 0) sb.Append($"Cold: {Cold:F1} ");
            if (Lightning > 0) sb.Append($"Light: {Lightning:F1} ");
            sb.Append(")");
            return sb.ToString();
        }
    }
}