using UnityEngine;
using Scripts.Inventory;
using Scripts.Items;

namespace Scripts.Visuals
{
    public class WeaponVisualController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("Weapon renderer located in the character hand")]
        [SerializeField] private SpriteRenderer _weaponRenderer;
        [SerializeField] private SpriteRenderer _playerRenderer;

        [Header("Sorting")]
        [Tooltip("Keeps player above world sprites. UI is not affected.")]
        [SerializeField] private bool _enforceTopCharacterSorting = true;
        [SerializeField] private int _playerSortingOrder = 20000;
        [SerializeField] private int _weaponOrderOffset = 10;

        private void Awake()
        {
            if (_playerRenderer == null)
                _playerRenderer = GetComponent<SpriteRenderer>();

            ApplySortingOrder();
        }

        private void Start()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped += UpdateVisuals;
                InventoryManager.Instance.OnItemUnequipped += UpdateVisuals;
                InventoryManager.Instance.OnInventoryChanged += RefreshVisuals;
                CheckCurrentWeapon();
            }
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped -= UpdateVisuals;
                InventoryManager.Instance.OnItemUnequipped -= UpdateVisuals;
                InventoryManager.Instance.OnInventoryChanged -= RefreshVisuals;
            }
        }

        private void CheckCurrentWeapon()
        {
            RefreshVisuals();
        }

        private void UpdateVisuals(InventoryItem _)
        {
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (InventoryManager.Instance == null || _weaponRenderer == null)
                return;

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

            ApplySortingOrder();
        }

        private void ApplySortingOrder()
        {
            if (!_enforceTopCharacterSorting)
                return;

            if (_playerRenderer != null)
                _playerRenderer.sortingOrder = _playerSortingOrder;

            if (_weaponRenderer != null)
                _weaponRenderer.sortingOrder = _playerSortingOrder + _weaponOrderOffset;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_playerRenderer == null)
                _playerRenderer = GetComponent<SpriteRenderer>();

            ApplySortingOrder();
        }
#endif
    }
}
