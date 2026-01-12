using UnityEngine;
using Scripts.Combat;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private GameObject _popupPrefab;
    [Tooltip("Насколько выше центра объекта спавнить текст")]
    [SerializeField] private float _spawnHeight = 1.5f; 
    [Tooltip("Разброс по X/Y для красоты")]
    [SerializeField] private Vector2 _randomOffset = new Vector2(0.5f, 0.5f);

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void Show(DamageSnapshot damage, Vector3 targetPosition)
    {
        if (_popupPrefab == null)
        {
            Debug.LogError("[FloatingTextManager] Не назначен Popup Prefab!");
            return;
        }

        // 1. Определяем доминирующий тип урона
        string dmgType = "Physical";
        float maxDmg = damage.Physical;

        if (damage.Fire > maxDmg) { maxDmg = damage.Fire; dmgType = "Fire"; }
        if (damage.Cold > maxDmg) { maxDmg = damage.Cold; dmgType = "Cold"; }
        if (damage.Lightning > maxDmg) { maxDmg = damage.Lightning; dmgType = "Lightning"; }

        // 2. Расчет позиции (Над головой + Рандом)
        Vector3 finalPos = targetPosition;
        finalPos.y += _spawnHeight; 
        finalPos.x += Random.Range(-_randomOffset.x, _randomOffset.x);
        finalPos.y += Random.Range(-_randomOffset.y, _randomOffset.y);

        // 3. Спавн
        GameObject popupObj = PoolManager.Instance.Spawn(_popupPrefab, finalPos, Quaternion.identity);
        
        // 4. Настройка
        DamagePopup popup = popupObj.GetComponent<DamagePopup>();
        if (popup != null)
        {
            popup.Setup(damage.TotalDamage, damage.IsCrit, dmgType);
        }
        else
        {
            Debug.LogError($"[FloatingTextManager] На префабе {_popupPrefab.name} нет скрипта DamagePopup!");
        }
    }
}