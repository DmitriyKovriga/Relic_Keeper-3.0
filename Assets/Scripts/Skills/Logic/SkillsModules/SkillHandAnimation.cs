using UnityEngine;
using Scripts.Stats;
using Scripts.Skills;
using Scripts.Visuals;

namespace Scripts.Skills.Modules
{
    /// <summary>
    /// HandPivot windup / strike / recovery for weapon skills.
    /// Hold pose stays on WeaponHolder; this only moves HandPivot.
    /// Style is set once per cast via SetActiveStyle.
    /// Keyframes come from WeaponSwingStyleTableSO (Resources) with code-constant fallback.
    /// </summary>
    public class SkillHandAnimation : MonoBehaviour
    {
        // Fallback constants (pre-SO SkillHandAnimation values). Do not retune silently —
        // editor exposes these via WeaponSwingStyleTableSO so designers can fix visually.
        private const float SlashWindupZ = 110f;
        private static readonly Vector2 SlashWindupPos = new Vector2(0.05f, 0.08f);
        private const float SlashImpactZ = -30f;
        private static readonly Vector2 SlashImpactPos = Vector2.zero;

        private const float LowArcWindupZ = 0f;
        private static readonly Vector2 LowArcWindupPos = new Vector2(-0.50f, 0.315f);
        private const float LowArcImpactZ = -150f; // tip ABOVE path with LowGuard hold (~+100)
        private static readonly Vector2 LowArcImpactPos = new Vector2(0.35f, -0.056f);

        private const float StabWindupZ = 45f;
        private static readonly Vector2 StabWindupPos = new Vector2(0.06f, 0.48f);
        private const float StabImpactZ = -125f;
        private static readonly Vector2 StabImpactPos = new Vector2(0.10f, -0.40f);

        [Tooltip("Optional override. If empty, loads Resources/Visuals/WeaponSwingStyleTable.")]
        [SerializeField] private WeaponSwingStyleTableSO _styleTable;

        private Transform _handPivot;
        private PlayerStats _ownerStats;
        private WeaponHandAnimationAnchor _anchor;
        private SpriteRenderer _weaponRenderer;
        private SpriteRenderer _nockedProjectile;
        private Vector3 _nockPosition;
        private Vector2 _nockOffset;
        private float _nockPullback;
        public bool KeepsWeaponVisibleAtImpact => _activeStyle == WeaponSwingStyle.BowDraw;

        private Vector3 _defaultPos = Vector3.zero;
        private Quaternion _defaultRot = Quaternion.identity;

        private WeaponSwingStyle _activeStyle = WeaponSwingStyle.Slash;
        private string _activeStyleId = "slash";

        private Quaternion _windupRot;
        private Vector3 _windupPos;
        private Quaternion _impactRot;
        private Vector3 _impactPos;

        public void Initialize(PlayerStats stats)
        {
            _ownerStats = stats;
            BindHand();

            EnsureStyleTable();
            ApplyStyleKeyframes(WeaponSwingStyle.Slash);
        }

        private void BindHand()
        {
            if (_ownerStats == null) return;
            var skill = GetComponent<SkillBehaviour>();
            var visuals = _ownerStats.GetComponent<WeaponVisualController>();
            if (visuals == null || !visuals.TryGetAnimationHand(skill != null ? skill.SlotIndex : -1, out _handPivot, out _weaponRenderer))
            {
                _handPivot = _ownerStats.transform.Find("Visuals/HandPivot");
                // Child order changes when equipping an offhand; never choose the first renderer.
                _weaponRenderer = _handPivot != null ? _handPivot.Find("WeaponHolder")?.GetComponent<SpriteRenderer>() : null;
            }

            if (_handPivot != null)
            {
                _anchor = WeaponVisualController.GetAnimationAnchor(_handPivot);
                _defaultPos = _anchor.RestPosition;
                _defaultRot = _anchor.RestRotation;
            }
            else
            {
                Debug.LogError($"[SkillHandAnimation] HandPivot not found under {_ownerStats.name}!");
            }
        }

        /// <summary>
        /// Select swing keyframes for the upcoming cast. Call before windup.
        /// </summary>
        public void SetActiveStyle(WeaponSwingStyle style)
        {
            if (style == WeaponSwingStyle.FromStance)
                style = WeaponSwingStyle.Slash;
            ReleaseDrawnProjectile();
            BindHand();
            if (_anchor != null) _anchor.Acquire(this);
            _activeStyle = style;
            _activeStyleId = WeaponSwingStyleTableSO.CanonicalId(style);
            ApplyStyleKeyframes(style);
        }

        public void SetActiveStyleId(string styleId)
        {
            if (string.IsNullOrEmpty(styleId))
                styleId = "slash";
            ReleaseDrawnProjectile();
            BindHand();
            if (_anchor != null) _anchor.Acquire(this);
            _activeStyleId = styleId;
            _activeStyle = WeaponSwingStyleResolver.StyleIdToEnum(styleId);
            ApplyStyleKeyframesById(styleId);
        }

        public WeaponSwingStyle ActiveStyle => _activeStyle;
        public string ActiveStyleId => _activeStyleId;

        // --- Generic API ---

        /// <summary>Lerp HandPivot from idle toward windup pose. t: 0..1</summary>
        public void LerpWindup(float t)
        {
            if (_handPivot == null || _anchor == null || !_anchor.IsOwnedBy(this)) return;
            t = Mathf.Clamp01(t);
            _handPivot.localRotation = Quaternion.Slerp(_defaultRot, _windupRot, t);
            _handPivot.localPosition = Vector3.Lerp(_defaultPos, _windupPos, t);
            if (_nockedProjectile != null && _nockedProjectile.enabled)
            {
                _nockedProjectile.transform.localPosition = _nockPosition + Vector3.left * (_nockPullback * t);
                _nockedProjectile.sortingLayerID = _weaponRenderer.sortingLayerID;
                _nockedProjectile.sortingOrder = _weaponRenderer.sortingOrder + 1;
            }
        }

        /// <summary>Snap HandPivot to impact pose. Does not hide weapon (callers do).</summary>
        public void SnapImpact()
        {
            if (_handPivot == null || _anchor == null || !_anchor.IsOwnedBy(this)) return;
            _handPivot.localRotation = _impactRot;
            _handPivot.localPosition = _impactPos;
        }

        /// <summary>Lerp HandPivot from impact back to idle. t: 0..1</summary>
        public void LerpRecovery(float t)
        {
            if (_handPivot == null || _anchor == null || !_anchor.IsOwnedBy(this)) return;
            t = Mathf.Clamp01(t);
            _handPivot.localRotation = Quaternion.Slerp(_impactRot, _defaultRot, t);
            _handPivot.localPosition = Vector3.Lerp(_impactPos, _defaultPos, t);
        }

        // --- Legacy slash wrappers (force Slash style) ---

        public void LerpSlashWindup(float t)
        {
            SetActiveStyle(WeaponSwingStyle.Slash);
            LerpWindup(t);
        }

        public void SnapToSlashImpact()
        {
            SetActiveStyle(WeaponSwingStyle.Slash);
            SnapImpact();
        }

        public void LerpSlashRecovery(float t)
        {
            SetActiveStyle(WeaponSwingStyle.Slash);
            LerpRecovery(t);
        }

        // A visual-only arrow is held during draw; the damage projectile starts at release.
        public void PrepareDrawnProjectile(GameObject projectilePrefab)
        {
            ReleaseDrawnProjectile();
            if (_activeStyle != WeaponSwingStyle.BowDraw || _handPivot == null || _weaponRenderer == null || projectilePrefab == null)
                return;
            var source = projectilePrefab.GetComponentInChildren<SpriteRenderer>();
            if (source == null || source.sprite == null) return;
            if (_nockedProjectile == null)
            {
                var arrow = new GameObject("Nocked projectile");
                arrow.transform.SetParent(_handPivot, false);
                _nockedProjectile = arrow.AddComponent<SpriteRenderer>();
            }
            _nockedProjectile.sprite = source.sprite;
            _nockedProjectile.sharedMaterial = source.sharedMaterial;
            _nockedProjectile.color = source.color;
            _nockedProjectile.sortingLayerID = _weaponRenderer.sortingLayerID;
            _nockedProjectile.sortingOrder = _weaponRenderer.sortingOrder + 1;
            _nockPosition = _handPivot.InverseTransformPoint(_weaponRenderer.transform.position) + (Vector3)_nockOffset;
            _nockedProjectile.transform.localPosition = _nockPosition;
            _nockedProjectile.transform.localRotation = Quaternion.identity;
            _nockedProjectile.transform.localScale = Vector3.one;
            _nockedProjectile.enabled = true;
        }

        public bool TryGetDrawnProjectilePosition(out Vector2 position)
        {
            position = Vector2.zero;
            if (_nockedProjectile == null || !_nockedProjectile.enabled || _activeStyle != WeaponSwingStyle.BowDraw)
                return false;
            position = _nockedProjectile.transform.position;
            return true;
        }

        public void ReleaseDrawnProjectile()
        {
            if (_nockedProjectile != null) _nockedProjectile.enabled = false;
        }

        // --- Visibility / reset ---

        public void SetWeaponVisible(bool isVisible)
        {
            if (_weaponRenderer != null && _anchor != null && _anchor.IsOwnedBy(this)) _weaponRenderer.enabled = isVisible;
        }

        public void ForceReset()
        {
            ReleaseDrawnProjectile();
            if (_anchor != null && _anchor.Release(this))
            {
                if (_weaponRenderer != null) _weaponRenderer.enabled = _weaponRenderer.sprite != null;
            }
        }

        private void OnDisable()
        {
            ForceReset();
        }

        private void EnsureStyleTable()
        {
            if (_styleTable == null)
                _styleTable = WeaponSwingStyleTableSO.LoadDefault();
        }

        private void ApplyStyleKeyframes(WeaponSwingStyle style)
        {
            ApplyStyleKeyframesById(WeaponSwingStyleTableSO.CanonicalId(style));
        }

        private void ApplyStyleKeyframesById(string styleId)
        {
            EnsureStyleTable();

            var bowDefaults = WeaponSwingStyleTableSO.CreateDefaultStyleTable();
            _nockOffset = Vector2.zero;
            _nockPullback = 0;
            foreach (var row in bowDefaults)
                if (row.Style == WeaponSwingStyle.BowDraw) { _nockOffset = row.NockedProjectileLocalPosition; _nockPullback = row.NockedProjectilePullback; }
            float windupZ;
            float impactZ;
            Vector2 windupOff;
            Vector2 impactOff;

            if (_styleTable != null && _styleTable.TryGetById(styleId, out WeaponSwingStyleTableSO.SwingKeyframes kf))
            {
                _nockOffset = kf.NockedProjectileLocalPosition;
                _nockPullback = Mathf.Max(0, kf.NockedProjectilePullback);
                windupZ = kf.WindupZ;
                impactZ = kf.ImpactZ;
                windupOff = kf.WindupLocalPos;
                impactOff = kf.ImpactLocalPos;
            }
            else if (_styleTable != null)
            {
                WeaponSwingStyleTableSO.SwingKeyframes byEnum = _styleTable.Get(WeaponSwingStyleResolver.StyleIdToEnum(styleId));
                windupZ = byEnum.WindupZ;
                impactZ = byEnum.ImpactZ;
                windupOff = byEnum.WindupLocalPos;
                impactOff = byEnum.ImpactLocalPos;
            }
            else
            {
                ResolveFallbackKeyframes(WeaponSwingStyleResolver.StyleIdToEnum(styleId), out windupZ, out windupOff, out impactZ, out impactOff);
            }

            _windupRot = _defaultRot * Quaternion.Euler(0f, 0f, windupZ);
            _impactRot = _defaultRot * Quaternion.Euler(0f, 0f, impactZ);
            _windupPos = _defaultPos + new Vector3(windupOff.x, windupOff.y, 0f);
            _impactPos = _defaultPos + new Vector3(impactOff.x, impactOff.y, 0f);
        }

        private static void ResolveFallbackKeyframes(
            WeaponSwingStyle style,
            out float windupZ,
            out Vector2 windupOff,
            out float impactZ,
            out Vector2 impactOff)
        {
            switch (style)
            {
                case WeaponSwingStyle.BowDraw:
                    var bow = System.Array.Find(WeaponSwingStyleTableSO.CreateDefaultStyleTable(), row => row.Style == WeaponSwingStyle.BowDraw);
                    windupZ = bow.WindupZ;
                    impactZ = bow.ImpactZ;
                    windupOff = bow.WindupLocalPos;
                    impactOff = bow.ImpactLocalPos;
                    break;
                case WeaponSwingStyle.LowArc:
                    windupZ = LowArcWindupZ;
                    impactZ = LowArcImpactZ;
                    windupOff = LowArcWindupPos;
                    impactOff = LowArcImpactPos;
                    break;
                case WeaponSwingStyle.OverheadStab:
                    windupZ = StabWindupZ;
                    impactZ = StabImpactZ;
                    windupOff = StabWindupPos;
                    impactOff = StabImpactPos;
                    break;
                case WeaponSwingStyle.Slash:
                default:
                    windupZ = SlashWindupZ;
                    impactZ = SlashImpactZ;
                    windupOff = SlashWindupPos;
                    impactOff = SlashImpactPos;
                    break;
            }
        }
    }
}
