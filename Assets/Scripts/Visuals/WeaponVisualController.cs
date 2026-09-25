using System;
using UnityEngine;
using Scripts.Combat;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Skills;

namespace Scripts.Visuals
{
    /// <summary>
    /// Weapon overlay sprites + idle hold stances.
    /// Body sorting is owned by PlayerMovement (root WorldDepthSort).
    /// Stance is authored on the weapon's active skill #1 (<see cref="SkillDataSO.HoldStance"/>).
    /// Swing / attack windup stays on HandPivot via SkillHandAnimation вЂ” do not couple to HoldStance.
    /// </summary>
    public class WeaponVisualController : MonoBehaviour
    {
        private const string OffhandHolderName = "OffhandWeaponHolder";

        [Serializable]
        public struct StancePose
        {
            public WeaponHoldStance Stance;
            public Vector2 LocalPosition;
            public float LocalEulerZ;
            [Tooltip("When true, sort with Player (behind body) instead of PlayerOverlay.")]
            public bool SortBehindCharacter;
            public bool FlipX;
            public bool FlipY;
        }

        [Header("Components")]
        [Tooltip("Main-hand weapon renderer (WeaponHolder under Visuals/HandPivot)")]
        [SerializeField] private SpriteRenderer _weaponRenderer;
        [Tooltip("Optional. Created at runtime under HandPivot when dual 1H is equipped.")]
        [SerializeField] private SpriteRenderer _offhandWeaponRenderer;

        [Header("Hold stance poses (tunable)")]
        [Tooltip("Local pose of WeaponHolder relative to HandPivot. Approximates the author sketch; tune in Play Mode.")]
        [SerializeField] private StancePose[] _stancePoses = CreateDefaultPoseTable();

        [SerializeField, HideInInspector] private int _poseTableVersion;

        [Header("Dual wield")]
        [Tooltip("Added to offhand local position when two combat 1H weapons are equipped (sideways offset).")]
        [SerializeField] private Vector2 _dualWieldOffhandOffset = new Vector2(-0.28f, -0.06f);

        [Header("Sorting")]
        [SerializeField] private int _frontLocalOffset = 2;
        [SerializeField] private int _behindLocalOffset = -2;

        private WorldDepthSort _weaponDepthSort;
        private WorldDepthSort _offhandDepthSort;
        private Transform _handPivot;
        private PlayerSkillManager _skillManager;

        private void Awake()
        {
            EnsureDefaultPoseTable();
            CacheHandPivot();
            EnsureWeaponDepthSort(_weaponRenderer, ref _weaponDepthSort, behind: false);
        }

        private const int CurrentPoseTableVersion = 7;

        private void EnsureDefaultPoseTable()
        {
            if (_stancePoses == null || _stancePoses.Length == 0 || _poseTableVersion < CurrentPoseTableVersion)
            {
                _stancePoses = CreateDefaultPoseTable();
                _poseTableVersion = CurrentPoseTableVersion;
            }
        }

        private void Start()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped += UpdateVisuals;
                InventoryManager.Instance.OnItemUnequipped += UpdateVisuals;
                InventoryManager.Instance.OnInventoryChanged += RefreshVisuals;
            }

            _skillManager = GetComponent<PlayerSkillManager>();
            if (_skillManager != null)
                _skillManager.OnSkillSlotUpdated += HandleSkillSlotUpdated;

            RefreshVisuals();
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped -= UpdateVisuals;
                InventoryManager.Instance.OnItemUnequipped -= UpdateVisuals;
                InventoryManager.Instance.OnInventoryChanged -= RefreshVisuals;
            }

            if (_skillManager != null)
                _skillManager.OnSkillSlotUpdated -= HandleSkillSlotUpdated;
        }

        private void HandleSkillSlotUpdated(int slotIndex, SkillDataSO _)
        {
            // Active skill #1 lives in main-hand slot 0; refresh poses when skill data changes.
            if (slotIndex == WeaponHandStatScope.MainHandSkillSlot
                || slotIndex == WeaponHandStatScope.OffHandSkillSlot)
                RefreshVisuals();
        }

        private void UpdateVisuals(InventoryItem _)
        {
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            EnsureDefaultPoseTable();
            if (InventoryManager.Instance == null || _weaponRenderer == null)
                return;

            CacheHandPivot();

            InventoryItem[] equipment = InventoryManager.Instance.EquipmentItems;
            InventoryItem mainHandItem = GetSlot(equipment, EquipmentSlot.MainHand);
            InventoryItem offHandItem = GetSlot(equipment, EquipmentSlot.OffHand);

            bool mainIsWeapon = TryGetCombatWeapon(mainHandItem, out WeaponItemSO mainWeapon);
            bool offIsCombatOneHand = TryGetCombatOneHandedWeapon(offHandItem, out WeaponItemSO offWeapon);
            bool dualOneHand = mainIsWeapon
                && !mainWeapon.IsTwoHanded
                && offIsCombatOneHand;

            WeaponHoldStance mainStance = ResolveHoldStance(mainHandItem);
            ApplyHandVisual(
                _weaponRenderer,
                ref _weaponDepthSort,
                mainIsWeapon ? mainWeapon.InHandSprite : null,
                mainStance,
                lateralOffset: Vector2.zero,
                spriteTiltZ: mainIsWeapon ? mainWeapon.InHandSpriteTiltZ : 0f);

            if (dualOneHand)
            {
                EnsureOffhandRenderer();
                WeaponHoldStance offStance = ResolveHoldStance(offHandItem);
                // Same stance family as main when offhand skill has no explicit stance change вЂ”
                // still read offhand skill #1; offset keeps dual-wield readable.
                if (offStance == WeaponHoldStance.Default && mainStance != WeaponHoldStance.Default)
                    offStance = mainStance;

                ApplyHandVisual(
                    _offhandWeaponRenderer,
                    ref _offhandDepthSort,
                    offWeapon.InHandSprite,
                    offStance,
                    lateralOffset: _dualWieldOffhandOffset,
                    spriteTiltZ: offWeapon.InHandSpriteTiltZ);
            }
            else
            {
                ClearRenderer(_offhandWeaponRenderer);
            }
        }

        private void ApplyHandVisual(
            SpriteRenderer renderer,
            ref WorldDepthSort depthSort,
            Sprite sprite,
            WeaponHoldStance stance,
            Vector2 lateralOffset,
            float spriteTiltZ)
        {
            if (renderer == null)
                return;

            if (sprite != null)
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
            else
            {
                renderer.sprite = null;
                renderer.enabled = false;
                return;
            }

            StancePose pose = GetPose(stance);
            Transform t = renderer.transform;
            t.localPosition = new Vector3(
                pose.LocalPosition.x + lateralOffset.x,
                pose.LocalPosition.y + lateralOffset.y,
                t.localPosition.z);
            t.localRotation = Quaternion.Euler(0f, 0f, pose.LocalEulerZ - spriteTiltZ);
            renderer.flipX = pose.FlipX;
            renderer.flipY = pose.FlipY;

            EnsureWeaponDepthSort(renderer, ref depthSort, behind: pose.SortBehindCharacter);
        }

        private static void ClearRenderer(SpriteRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.sprite = null;
            renderer.enabled = false;
        }

        private StancePose GetPose(WeaponHoldStance stance)
        {
            if (_stancePoses != null)
            {
                for (int i = 0; i < _stancePoses.Length; i++)
                {
                    if (_stancePoses[i].Stance == stance)
                        return _stancePoses[i];
                }
            }

            StancePose[] defaults = CreateDefaultPoseTable();
            for (int i = 0; i < defaults.Length; i++)
            {
                if (defaults[i].Stance == stance)
                    return defaults[i];
            }

            return defaults[0];
        }

        private static WeaponHoldStance ResolveHoldStance(InventoryItem item)
        {
            if (item?.GrantedSkills == null || item.GrantedSkills.Count == 0)
                return WeaponHoldStance.Default;

            // Active skill #1 = GrantedSkills[0] (not the 2H secondary special).
            SkillDataSO primary = item.GrantedSkills[0];
            return primary != null ? primary.HoldStance : WeaponHoldStance.Default;
        }

        private void EnsureOffhandRenderer()
        {
            if (_offhandWeaponRenderer != null)
                return;

            CacheHandPivot();
            if (_handPivot == null)
                return;

            Transform existing = _handPivot.Find(OffhandHolderName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                _offhandWeaponRenderer = go.GetComponent<SpriteRenderer>();
            }
            else
            {
                go = new GameObject(OffhandHolderName);
                go.layer = _handPivot.gameObject.layer;
                go.transform.SetParent(_handPivot, false);
                go.transform.SetSiblingIndex(0); // draw under mainhand child order when same sorting
                _offhandWeaponRenderer = go.AddComponent<SpriteRenderer>();
            }

            if (_weaponRenderer != null && _offhandWeaponRenderer != null)
            {
                _offhandWeaponRenderer.sharedMaterial = _weaponRenderer.sharedMaterial;
                _offhandWeaponRenderer.sortingLayerID = _weaponRenderer.sortingLayerID;
            }
        }

        private void CacheHandPivot()
        {
            if (_handPivot != null)
                return;

            if (_weaponRenderer != null)
            {
                Transform parent = _weaponRenderer.transform.parent;
                if (parent != null && parent.name == "HandPivot")
                {
                    _handPivot = parent;
                    return;
                }
            }

            Transform visuals = transform.Find("Visuals/HandPivot");
            if (visuals != null)
                _handPivot = visuals;
        }

        private void EnsureWeaponDepthSort(SpriteRenderer renderer, ref WorldDepthSort depthSort, bool behind)
        {
            if (renderer == null || !renderer.enabled)
                return;

            depthSort = renderer.GetComponent<WorldDepthSort>();
            if (depthSort == null)
                depthSort = renderer.gameObject.AddComponent<WorldDepthSort>();

            // Shoulder (and any SortBehindCharacter pose): Player band below body (localOffset 0).
            // Default: PlayerOverlay in front of body.
            if (behind)
            {
                depthSort.Configure(
                    RenderDepthCategory.Player,
                    localOffset: _behindLocalOffset,
                    staticAnchor: false,
                    anchorY: renderer.transform.position.y);
            }
            else
            {
                depthSort.Configure(
                    RenderDepthCategory.PlayerOverlay,
                    localOffset: _frontLocalOffset,
                    staticAnchor: false,
                    anchorY: renderer.transform.position.y);
            }
        }

        private static InventoryItem GetSlot(InventoryItem[] equipment, EquipmentSlot slot)
        {
            int index = (int)slot;
            if (equipment == null || index < 0 || index >= equipment.Length)
                return null;

            InventoryItem item = equipment[index];
            return item != null && item.Data != null ? item : null;
        }

        private static bool TryGetCombatWeapon(InventoryItem item, out WeaponItemSO weapon)
        {
            weapon = item?.Data as WeaponItemSO;
            return weapon != null && !weapon.IsDefensiveOffHand;
        }

        private static bool TryGetCombatOneHandedWeapon(InventoryItem item, out WeaponItemSO weapon)
        {
            if (!TryGetCombatWeapon(item, out weapon))
                return false;
            return !weapon.IsTwoHanded;
        }

        /// <summary>
        /// Default table approximating the author sketch (units relative to HandPivot).
        /// Scene baseline Default was WeaponHolder (0.5, 0.083) / 0В°.
        /// </summary>
        public static StancePose[] CreateDefaultPoseTable()
        {
            return new[]
            {
                new StancePose
                {
                    Stance = WeaponHoldStance.Default,
                    LocalPosition = new Vector2(0.5f, 0.083f),
                    LocalEulerZ = 0f,
                    SortBehindCharacter = false,
                    FlipX = false,
                    FlipY = false
                },
                new StancePose
                {
                    Stance = WeaponHoldStance.Aggressive,
                    // FlipX; tip-forward horizontal is -90; ~10 deg below => -100. (NOT +90 вЂ” that is tip-back.)
                    LocalPosition = new Vector2(0.82f, 0.02f),
                    LocalEulerZ = -100f,
                    SortBehindCharacter = false,
                    FlipX = true,
                    FlipY = false
                },
                new StancePose
                {
                    Stance = WeaponHoldStance.LowGuard,
                    LocalPosition = new Vector2(-0.18f, -0.22f),
                    LocalEulerZ = 100f,
                    SortBehindCharacter = false,
                    FlipX = false,
                    FlipY = false
                },
                new StancePose
                {
                    // Tip-down reverse-grip (tip-up art). EulerZ 180 = tip straight down.
                    // Not tip-up like Default (0). Clear flips. Bump pose table version on change.
                    Stance = WeaponHoldStance.Dagger,
                    LocalPosition = new Vector2(0.28f, -0.12f),
                    LocalEulerZ = 180f,
                    SortBehindCharacter = false,
                    FlipX = false,
                    FlipY = false
                },

                new StancePose
                {
                    Stance = WeaponHoldStance.Shoulder,
                    // FlipX+Y; handle further down (-130); lowered 5px (PPU 24 => -0.208 Y).
                    LocalPosition = new Vector2(-0.18f, 0.312f),
                    LocalEulerZ = -120f,
                    SortBehindCharacter = true,
                    FlipX = true,
                    FlipY = true
                },
                new StancePose
                {
                    Stance = WeaponHoldStance.Staff,
                    // Flat horizontal across hands; tip-forward axis -90 (reverse of prior +90 mistake).
                    LocalPosition = new Vector2(0.1f, -0.05f),
                    LocalEulerZ = -90f,
                    SortBehindCharacter = false,
                    FlipX = false,
                    FlipY = false
                }
            };
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _stancePoses = CreateDefaultPoseTable();
        }

        private void OnValidate()
        {
            if (_stancePoses == null || _stancePoses.Length == 0 || _poseTableVersion < CurrentPoseTableVersion)
            {
                _stancePoses = CreateDefaultPoseTable();
                _poseTableVersion = CurrentPoseTableVersion;
            }
        }
#endif
    }
}
