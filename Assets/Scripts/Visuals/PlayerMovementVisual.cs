using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Deforms only a presentation sprite, leaving the root collider and sockets intact.</summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(PlayerMovement))]
    public sealed class PlayerMovementVisual : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.15f)] private float _jumpStretch = 0.075f;
        [SerializeField, Range(0f, 0.15f)] private float _landingSquash = 0.055f;
        [SerializeField, Min(1f)] private float _returnSpeed = 16f;
        [SerializeField] private bool _alignFeetToCollider = true;
        [SerializeField] private float _feetOffset;
        public const float DefaultAirborneLeanDegrees = 6f;
        public const float DefaultAirborneLeanSpeed = 5f;
        public const float DefaultAirborneLeanResponse = 12f;
        [SerializeField, Range(0f, 12f)] private float _airborneLeanDegrees = DefaultAirborneLeanDegrees;
        [SerializeField, Min(0.1f)] private float _airborneLeanAtSpeed = DefaultAirborneLeanSpeed;
        [SerializeField, Min(1f)] private float _airborneLeanResponse = DefaultAirborneLeanResponse;
        private float _airborneLean;

        private PlayerMovement _movement;
        private PlayerAttackInput _attack;
        private Collider2D _bodyCollider;
        private SpriteRenderer _source;
        private SpriteRenderer _display;
        private MaterialPropertyBlock _properties;
        private bool _originalForceRenderingOff;
        private float _pulse;
        private PlayerRenderAnchor _renderAnchor;
        private PlayerCharacterAnimationController _characterAnimation;
        private Scripts.Skills.PlayerSkillManager _skillManager;
        public SpriteRenderer DisplayRenderer => _display;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _attack = GetComponent<PlayerAttackInput>();
            _bodyCollider = GetComponent<Collider2D>();
            _source = GetComponent<SpriteRenderer>();
            _originalForceRenderingOff = _source.forceRenderingOff;
            var visual = new GameObject("MovementSprite");
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            _display = visual.AddComponent<SpriteRenderer>();
            _properties = new MaterialPropertyBlock();
            _renderAnchor = GetComponent<PlayerRenderAnchor>() ?? gameObject.AddComponent<PlayerRenderAnchor>();
            _renderAnchor.Initialize(visual.transform, transform.Find("Visuals"));
            _characterAnimation = GetComponent<PlayerCharacterAnimationController>();
            _skillManager = GetComponent<Scripts.Skills.PlayerSkillManager>();
        }

        private void OnEnable()
        {
            _movement.OnJumpStarted += Jump;
            _movement.OnLanded += Land;
            _source.forceRenderingOff = true;
        }

        private void OnDisable()
        {
            _movement.OnJumpStarted -= Jump;
            _movement.OnLanded -= Land;
            _source.forceRenderingOff = _originalForceRenderingOff;
            if (_display != null)
                _display.enabled = false;
            _pulse = 0f;
            _airborneLean = 0f;
            if (_renderAnchor != null)
            {
                _renderAnchor.WeaponPresentationOffset = Vector3.zero;
                _renderAnchor.PresentationLeanDegrees = 0f;
                _renderAnchor.Restore();
            }
        }

        private void OnDestroy()
        {
            if (_display != null)
                Destroy(_display.gameObject);
        }

        private void Jump() => _pulse = _jumpStretch;
        private void Land() => _pulse = -_landingSquash;

        private void LateUpdate()
        {
            if (_source == null || _display == null)
                return;
            if (_properties == null)
                _properties = new MaterialPropertyBlock();

            _display.enabled = _source.enabled && !_originalForceRenderingOff;
            _display.sprite = _source.sprite;
            _display.color = _source.color;
            _display.flipX = _source.flipX;
            _display.flipY = _source.flipY;
            _display.sharedMaterial = _source.sharedMaterial;
            _display.sortingLayerID = _source.sortingLayerID;
            _display.sortingOrder = _source.sortingOrder;
            _display.maskInteraction = _source.maskInteraction;
            _display.spriteSortPoint = _source.spriteSortPoint;
            _source.GetPropertyBlock(_properties);
            _display.SetPropertyBlock(_properties);

            bool authored = _characterAnimation != null && _characterAnimation.HasAuthoredPresentation;
            float stretch = authored ? 0f : _pulse;
            if (_attack != null && _attack.IsDashing)
                stretch -= 0.035f;
            float scaleY = (authored ? _characterAnimation.PresentationScale.y : 1f) + stretch;
            float width = _characterAnimation != null ? _characterAnimation.PresentationHorizontalScale : 1f;
            _display.transform.localScale = new Vector3(authored ? width : width / scaleY, scaleY, 1f);
            _display.transform.localPosition = new Vector3(0f, ResolveDisplayY(scaleY), 0f);
            if (_renderAnchor != null)
            {
                bool casting = _skillManager != null && _skillManager.IsAnySkillCasting;
                bool dashing = _attack != null && _attack.IsDashing;
                _renderAnchor.WeaponPresentationOffset = !casting && !dashing && _characterAnimation != null
                    ? _characterAnimation.PresentationWeaponOffset : Vector3.zero;
                bool airborne = _movement != null && !_movement.IsGrounded && !casting && !dashing;
                float targetLean = CalculateAirborneLean(_movement != null ? _movement.CurrentVelocity.x : 0f,
                    Mathf.Sign(transform.lossyScale.x), airborne, _airborneLeanDegrees, _airborneLeanAtSpeed);
                _airborneLean = airborne ? Mathf.Lerp(_airborneLean, targetLean, 1f - Mathf.Exp(-_airborneLeanResponse * Time.deltaTime)) : 0f;
                _renderAnchor.PresentationLeanDegrees = _airborneLean;
            }
            _pulse *= Mathf.Exp(-_returnSpeed * Time.deltaTime);
        }

        public static float CalculateAirborneLean(float horizontalVelocity, float facing, bool airborne,
            float maximumDegrees = DefaultAirborneLeanDegrees, float fullLeanSpeed = DefaultAirborneLeanSpeed)
        {
            if (!airborne) return 0f;
            return -Mathf.Clamp(horizontalVelocity * facing / Mathf.Max(0.1f, fullLeanSpeed), -1f, 1f) * maximumDegrees;
        }

        private float ResolveDisplayY(float scaleY)
        {
            if (_source.sprite == null)
                return 0f;

            if (_alignFeetToCollider && _bodyCollider == null)
                _bodyCollider = GetComponent<Collider2D>();

            // A flipped sprite has its visual bottom on the opposite side of its pivot.
            float spriteFootY = _source.flipY
                ? -_source.sprite.bounds.max.y
                : _source.sprite.bounds.min.y;

            // Keep the feet anchored while the upper body compresses/extends. Animation
            // frames can have a different height/pivot, so align every frame to the
            // physical body's bottom instead of assuming that both origins already match.
            float targetFootY = _alignFeetToCollider && _bodyCollider != null
                ? ResolveColliderBottomLocalY(_bodyCollider) + _feetOffset
                : spriteFootY;

            return targetFootY - spriteFootY * scaleY;
        }

        private static float ResolveColliderBottomLocalY(Collider2D bodyCollider)
        {
            float worldScaleY = Mathf.Abs(bodyCollider.transform.lossyScale.y);
            if (worldScaleY <= 0.0001f)
                return bodyCollider.offset.y;

            return bodyCollider.offset.y - bodyCollider.bounds.extents.y / worldScaleY;
        }
    }
}
