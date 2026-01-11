using UnityEngine;
using Scripts.Combat;
using TMPro; // Если хочешь выводить текст над головой

public class DummyTarget : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float _health = 1000f;
    [SerializeField] private float _armor = 0f;
    [SerializeField] private float _fireResist = 0f;

    public void TakeDamage(DamageSnapshot damage)
    {
        // --- ЭТАП MITIGATION (Снижение урона) ---
        
        // 1. Броня (Простая формула: Armor вычитает физ урон, но не больше 90%)
        // В PoE формула сложнее: Damage * (10 / (10 + Armor / Damage)), но для старта пойдет Flat или %
        float physDamage = damage.Physical;
        if (_armor > 0)
        {
            // Упрощенно: каждая единица брони блокирует 0.1 урона (для примера)
            // Реализуй тут свою формулу защиты
            physDamage -= _armor * 0.1f; 
            if (physDamage < 0) physDamage = 0;
        }

        // 2. Резисты (75% кап)
        float fireDmg = damage.Fire * (1f - Mathf.Clamp(_fireResist, -100, 75) / 100f);
        float coldDmg = damage.Cold; // Добавь резисты
        float lightDmg = damage.Lightning; // Добавь резисты

        // 3. Итоговый урон
        float finalDamage = physDamage + fireDmg + coldDmg + lightDmg;
        
        _health -= finalDamage;

        if (FloatingTextManager.Instance != null)
        {
            FloatingTextManager.Instance.Show(damage, transform.position);
        }

        // --- ВИЗУАЛИЗАЦИЯ (Floating Text) ---
        Debug.Log($"[Dummy] Took {finalDamage:F1} damage. (HP: {_health}) | {damage}");
        
        // Тут можно вызвать систему Floating Text (мы ее сделаем позже)
        // FloatingTextManager.Show(finalDamage, transform.position, damage.IsCrit);

        if (_health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("[Dummy] Destroyed!");
        Destroy(gameObject);
    }
}