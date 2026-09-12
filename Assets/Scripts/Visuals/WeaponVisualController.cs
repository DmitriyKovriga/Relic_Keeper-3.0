using UnityEngine;
using Scripts.Inventory;
using Scripts.Items;

namespace Scripts.Visuals
{
    /// <summary>
    /// Weapon overlay depth only. Body sorting is owned by PlayerMovement (root WorldDepthSort).
    /// </summary>
    public class WeaponVisualController : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("Weapon renderer located in the character hand")]
        [SerializeField] private SpriteRenderer _weaponRenderer;

        private WorldDepthSort _weaponDepthSort;

        private void Awake()
        {
            EnsureWeaponDepthSort();
        }

        private void Start()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped += UpdateVisuals;
                InventoryManager.Instance.OnItemUnequipped += UpdateVisuals;
                InventoryManager.Instance.OnInventoryChanged += RefreshVisuals;
                RefreshVisuals();
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

        private void UpdateVisuals(InventoryItem _)
        {
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (InventoryManager.Instance == null || _weaponRenderer == null)
                return;

            InventoryItem mainHandItem = InventoryManager.Instance.EquipmentItems[2];
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

            EnsureWeaponDepthSort();
        }

        private void EnsureWeaponDepthSort()
        {
            if (_weaponRenderer == null || !_weaponRenderer.enabled)
                return;

            _weaponDepthSort = _weaponRenderer.GetComponent<WorldDepthSort>();
            if (_weaponDepthSort == null)
                _weaponDepthSort = _weaponRenderer.gameObject.AddComponent<WorldDepthSort>();

            _weaponDepthSort.Configure(
                RenderDepthCategory.PlayerOverlay,
                localOffset: 2,
                staticAnchor: false,
                anchorY: _weaponRenderer.transform.position.y);
        }
    }
}
