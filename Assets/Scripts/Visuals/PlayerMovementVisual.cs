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
        public static readonly Vector2 GroundDashScale = new Vector2(0.96f, 0.87f);
        [SerializeField] private Vector2 _groundDashScale = GroundDashScale;
        [SerializeField, Min(1f)] private float _dashShapeResponse = 18f;
        private float _dashShape;

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
        [SerializeField] private CombatPoseProfile _combatPoseProfile;
        private readonly AttackBodyFollow _attackBody = new AttackBodyFollow();
        private PlayerStats _stats;
        private CharacterDataSO _attackCharacter;
        private readonly PlayerHurtPulse _hurt = new PlayerHurtPulse();
        private SpriteFX.SpriteFxController _hurtFx;
        private CharacterDataSO _hurtCharacter;
        public float VisibleHurtFlash { get; private set; }

        private void OnDamage(Scripts.GameplayEvents.GameplayEventContext context)
        {
            if (_stats == null || _stats.Health == null || _stats.Health.Current <= 0 || GamePauseService.IsPaused) return;
            if (_combatPoseProfile == null) _combatPoseProfile = CombatPoseProfile.LoadDefault();
            if (_hurt.Trigger(context, gameObject, _combatPoseProfile, Time.time))
                _hurtCharacter = _stats.CurrentCharacterData;
        }

        private void ClearHurt()
        {
            _hurt.Clear(); VisibleHurtFlash = 0;
            if (_hurtFx != null) _hurtFx.SetHitFlash(Color.white, 0);
        }
        public SpriteRenderer DisplayRenderer => _display;
        public AttackBodyPose VisibleAttackPose { get; private set; } = AttackBodyPose.Neutral;
        public object AttackBodyOwner => _attackBody.Owner;

        public bool BeginAttackBody(object owner, Scripts.Skills.SkillDataSO skill)
        {
            if (!isActiveAndEnabled) return false;
            if (_combatPoseProfile == null) _combatPoseProfile = CombatPoseProfile.LoadDefault();
            if (!_attackBody.TryBegin(owner, skill, _combatPoseProfile)) return false;
            _attackCharacter = _stats != null ? _stats.CurrentCharacterData : null;
            return true;
        }
        public void SampleAttackWindup(object owner, float phase) => _attackBody.Windup(owner, phase);
        public void SampleAttackStrike(object owner) => _attackBody.Strike(owner);
        public void SampleAttackRecovery(object owner, float phase) => _attackBody.Recovery(owner, phase);
        public void SampleAttackPipeline(object owner, float phase) => _attackBody.SamplePipeline(owner, phase);
        public void EndAttackBody(object owner) => _attackBody.Release(owner);

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
            _stats = GetComponent<PlayerStats>();
            if (GetComponent<PlayerDefenseFeedback>() == null) gameObject.AddComponent<PlayerDefenseFeedback>();
        }

        private void OnEnable()
        {
            Scripts.GameplayEvents.GameplayEventBus.EventRaised += OnDamage;
            _movement.OnJumpStarted += Jump;
            _movement.OnLanded += Land;
            _source.forceRenderingOff = true;
        }

        private void OnDisable()
        {
            Scripts.GameplayEvents.GameplayEventBus.EventRaised -= OnDamage;
            ClearHurt();
            _movement.OnJumpStarted -= Jump;
            _movement.OnLanded -= Land;
            _source.forceRenderingOff = _originalForceRenderingOff;
            if (_display != null)
                _display.enabled = false;
            _pulse = 0f;
            _airborneLean = 0f;
            _dashShape = 0f;
            _attackBody.Clear(); VisibleAttackPose = AttackBodyPose.Neutral;
            if (_renderAnchor != null)
            {
                _renderAnchor.WeaponPresentationOffset = Vector3.zero;
                _renderAnchor.PresentationLeanDegrees = 0f;
                _renderAnchor.HideWeaponsForDodge = false;
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
            if (_stats == null || _stats.Health == null || _stats.Health.Current <= 0 ||
                _stats.CurrentCharacterData != _hurtCharacter || _combatPoseProfile == null || !_combatPoseProfile.EnablePlayerHurt)
                ClearHurt();
            VisibleHurtFlash = _hurt.Sample(Time.time, out float hurtDirection);
            if (_hurtFx == null && VisibleHurtFlash > 0) _hurtFx = _source.GetComponent<SpriteFX.SpriteFxController>() ?? _source.gameObject.AddComponent<SpriteFX.SpriteFxController>();
            if (_hurtFx != null) _hurtFx.SetHitFlash(_combatPoseProfile != null ? _combatPoseProfile.PlayerHurtColor : Color.white,
                _source.sharedMaterial != null && _source.sharedMaterial.HasProperty("_FxHitFlash") ? VisibleHurtFlash : 0);
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

            bool authored = _characterAnimation != null && (_characterAnimation.HasAuthoredPresentation || _characterAnimation.IsPlayingNeutralDodge || _characterAnimation.IsPlayingSkillSpin);
            float stretch = authored ? 0f : _pulse;
            bool casting = _skillManager != null && _skillManager.IsAnySkillCasting;
            bool groundDash = _attack != null && _attack.IsDashing && _movement != null && _movement.IsGrounded && !casting;
            bool canRecoverDash = _movement != null && _movement.IsGrounded && !casting;
            _dashShape = canRecoverDash ? Mathf.Lerp(_dashShape, groundDash ? 1f : 0f,
                1f - Mathf.Exp(-_dashShapeResponse * Time.deltaTime)) : 0f;
            float scaleY = (authored ? _characterAnimation.PresentationScale.y : 1f) + stretch;
            float width = _characterAnimation != null ? _characterAnimation.PresentationHorizontalScale : 1f;
            Vector2 dashScale = Vector2.Lerp(Vector2.one, _groundDashScale, _dashShape);
            _display.transform.localScale = new Vector3((authored ? width : width / scaleY) * dashScale.x, scaleY * dashScale.y, 1f);
            scaleY *= dashScale.y;
            if (_stats != null && (_stats.CurrentCharacterData != _attackCharacter || _stats.Health != null && _stats.Health.Current <= 0f))
                _attackBody.Clear();
            bool specialPose = _characterAnimation != null && (_characterAnimation.IsPlayingNeutralDodge || _characterAnimation.IsPlayingSkillSpin);
            bool dodgePose = _attack != null && (_attack.IsDashing || _attack.IsStationaryDodging);
            bool suppressAttack = specialPose || dodgePose || Scripts.Skills.PlayerStasisController.IsActiveFor(this);
            VisibleAttackPose = !suppressAttack && _combatPoseProfile != null && _combatPoseProfile.EnableBodyFollow
                ? _attackBody.Pose : AttackBodyPose.Neutral;
            float hurtAmount = !suppressAttack && _combatPoseProfile != null
                ? VisibleHurtFlash / Mathf.Max(.001f, _combatPoseProfile.PlayerHurtFlash) : 0;
            float hurtHeight = 1f - hurtAmount * (_combatPoseProfile != null ? Mathf.Clamp(_combatPoseProfile.PlayerHurtCompression, 0, .04f) : 0);
            Vector3 bodyScale = _display.transform.localScale;
            bodyScale.x /= VisibleAttackPose.Height; bodyScale.y *= VisibleAttackPose.Height;
            bodyScale.x /= hurtHeight; bodyScale.y *= hurtHeight;
            _display.transform.localScale = bodyScale;
            scaleY *= VisibleAttackPose.Height;
            scaleY *= hurtHeight;
            float hurtDegrees = hurtAmount * hurtDirection * Mathf.Sign(transform.lossyScale.x) * (_combatPoseProfile != null ? Mathf.Clamp(_combatPoseProfile.PlayerHurtDegrees, 0, 4) : 0);
            Quaternion bodyRotation = Quaternion.Euler(0f, 0f, VisibleAttackPose.Degrees - hurtDegrees);
            _display.transform.localRotation = bodyRotation;
            float footY = _source.sprite != null ? (_source.flipY ? -_source.sprite.bounds.max.y : _source.sprite.bounds.min.y) : 0f;
            _display.transform.localPosition = new Vector3(0f, ResolveDisplayY(scaleY), 0f) +
                SpriteFootPose.RotationCompensation(footY * scaleY, bodyRotation);
            if (_renderAnchor != null)
            {
                bool dashing = _attack != null && _attack.IsDashing;
                Vector3 grip = !casting && (!dashing || groundDash) && _characterAnimation != null
                    ? _characterAnimation.PresentationWeaponOffset : Vector3.zero;
                _renderAnchor.WeaponPresentationOffset = AdjustDashGrip(grip, dashScale);
                bool neutralDodge = _attack != null && _attack.IsStationaryDodging;
                bool airborne = _movement != null && !_movement.IsGrounded && !casting && !dashing && !neutralDodge;
                float targetLean = CalculateAirborneLean(_movement != null ? _movement.CurrentVelocity.x : 0f,
                    Mathf.Sign(transform.lossyScale.x), airborne, _airborneLeanDegrees, _airborneLeanAtSpeed);
                _airborneLean = airborne ? Mathf.Lerp(_airborneLean, targetLean, 1f - Mathf.Exp(-_airborneLeanResponse * Time.deltaTime)) : 0f;
                _renderAnchor.PresentationLeanDegrees = _airborneLean;
                _renderAnchor.HideWeaponsForDodge = neutralDodge;
            }
            _pulse *= Mathf.Exp(-_returnSpeed * Time.deltaTime);
        }

        public static float CalculateAirborneLean(float horizontalVelocity, float facing, bool airborne,
            float maximumDegrees = DefaultAirborneLeanDegrees, float fullLeanSpeed = DefaultAirborneLeanSpeed)
        {
            if (!airborne) return 0f;
            return -Mathf.Clamp(horizontalVelocity * facing / Mathf.Max(0.1f, fullLeanSpeed), -1f, 1f) * maximumDegrees;
        }

        public static Vector3 AdjustDashGrip(Vector3 gripOffset, Vector2 scale)
        {
            // Authored hand baseline: five pixels forward, four above the anchored feet.
            return gripOffset + new Vector3((5f / 24f + gripOffset.x) * (scale.x - 1f),
                (4f / 24f + gripOffset.y) * (scale.y - 1f), 0f);
        }

        private float ResolveDisplayY(float scaleY)
        {
            if (_source.sprite == null)
                return 0f;

            if (_alignFeetToCollider && _bodyCollider == null)
                _bodyCollider = GetComponent<Collider2D>();

            // A flipped sprite has its visual bottom on the opposite side of its pivot.
            Bounds reference = _characterAnimation != null && (_characterAnimation.IsPlayingNeutralDodge || _characterAnimation.IsPlayingSkillSpin)
                ? _characterAnimation.NeutralDodgeReferenceBounds : _source.sprite.bounds;
            float spriteFootY = _source.flipY ? -reference.max.y : reference.min.y;

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
