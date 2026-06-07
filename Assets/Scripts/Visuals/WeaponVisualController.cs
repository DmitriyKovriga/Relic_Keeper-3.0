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

        private WorldDepthSort _playerDepthSort;
        private WorldDepthSort _weaponDepthSort;

        private void Awake()
        {
            if (_playerRenderer == null)
                _playerRenderer = GetComponent<SpriteRenderer>();

            EnsureDepthSorters();
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

            EnsureDepthSorters();
        }

        private void EnsureDepthSorters()
        {
            if (_playerRenderer != null)
            {
                _playerDepthSort = _playerRenderer.GetComponent<WorldDepthSort>();
                if (_playerDepthSort == null)
                    _playerDepthSort = _playerRenderer.gameObject.AddComponent<WorldDepthSort>();
                _playerDepthSort.Configure(RenderDepthCategory.Player, localOffset: 0, staticAnchor: false, anchorY: _playerRenderer.transform.position.y);
            }

            if (_weaponRenderer != null)
            {
                _weaponDepthSort = _weaponRenderer.GetComponent<WorldDepthSort>();
                if (_weaponDepthSort == null)
                    _weaponDepthSort = _weaponRenderer.gameObject.AddComponent<WorldDepthSort>();
                if (_weaponRenderer.enabled)
                    _weaponDepthSort.Configure(RenderDepthCategory.PlayerOverlay, localOffset: 2, staticAnchor: false, anchorY: _weaponRenderer.transform.position.y);
            }
        }
    }
}
