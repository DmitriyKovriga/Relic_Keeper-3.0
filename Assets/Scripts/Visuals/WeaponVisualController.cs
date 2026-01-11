using UnityEngine;
using Scripts.Inventory;
using Scripts.Items;

namespace Scripts.Visuals
{
    public class WeaponVisualController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("SpriteRenderer, который находится в руке персонажа")]
        [SerializeField] private SpriteRenderer _weaponRenderer;

        private void Start()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped += UpdateVisuals;
                InventoryManager.Instance.OnItemUnequipped += UpdateVisuals;
                
                // Инициализация при старте (если загрузка прошла раньше)
                CheckCurrentWeapon();
            }
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped -= UpdateVisuals;
                InventoryManager.Instance.OnItemUnequipped -= UpdateVisuals;
            }
        }

        private void CheckCurrentWeapon()
        {
            // Ищем оружие в MainHand (индекс 0 в EquipmentItems, смещение 100)
            // В твоем InventoryManager: 0=Head, 1=Body, 2=MainHand
            // Значит индекс в массиве EquipmentItems = 2.
            
            var weaponItem = InventoryManager.Instance.EquipmentItems[2];
            UpdateVisuals(weaponItem);
        }

        private void UpdateVisuals(InventoryItem item)
        {
            // Если событие не про оружие (например, надели шлем), нам все равно нужно проверить текущее оружие.
            // Но для оптимизации: если item == null или это не оружие MainHand, проверяем массив.
            
            var mainHandItem = InventoryManager.Instance.EquipmentItems[2];

            if (mainHandItem != null && mainHandItem.Data is WeaponItemSO weaponData)
            {
                _weaponRenderer.sprite = weaponData.InHandSprite;
                _weaponRenderer.enabled = true;
            }
            else
            {
                _weaponRenderer.sprite = null;
                _weaponRenderer.enabled = false;
            }
        }
    }
}