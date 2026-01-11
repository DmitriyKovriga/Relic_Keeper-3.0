using UnityEngine;
using Scripts.Combat;

public interface IDamageable
{
    void TakeDamage(DamageSnapshot damage);
}