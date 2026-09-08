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

        private PlayerMovement _movement;
        private PlayerAttackInput _attack;
        private CapsuleCollider2D _bodyCollider;
        private SpriteRenderer _source;
        private SpriteRenderer _display;
        private MaterialPropertyBlock _properties;
        private bool _originalForceRenderingOff;
        private float _pulse;
        public SpriteRenderer DisplayRenderer => _display;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _attack = GetComponent<PlayerAttackInput>();
            _bodyCollider = GetComponent<CapsuleCollider2D>();
            _source = GetComponent<SpriteRenderer>();
            _originalForceRenderingOff = _source.forceRenderingOff;
            var visual = new GameObject("MovementSprite");
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            _display = visual.AddComponent<SpriteRenderer>();
            _properties = new MaterialPropertyBlock();
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

            float stretch = _pulse;
            if (_attack != null && _attack.IsDashing)
                stretch -= 0.035f;
            float scaleY = 1f + stretch;
            _display.transform.localScale = new Vector3(1f / scaleY, scaleY, 1f);
            _display.transform.localPosition = new Vector3(0f, ResolveDisplayY(scaleY), 0f);
            _pulse *= Mathf.Exp(-_returnSpeed * Time.deltaTime);
        }

        private float ResolveDisplayY(float scaleY)
        {
            if (_source.sprite == null)
                return 0f;

            if (_alignFeetToCollider && _bodyCollider == null)
                _bodyCollider = GetComponent<CapsuleCollider2D>();

            // A flipped sprite has its visual bottom on the opposite side of its pivot.
            float spriteFootY = _source.flipY
                ? -_source.sprite.bounds.max.y
                : _source.sprite.bounds.min.y;

            // Keep the feet anchored while the upper body compresses/extends. Animation
            // frames can have a different height/pivot, so align every frame to the
            // physical body's bottom instead of assuming that both origins already match.
            float targetFootY = _alignFeetToCollider && _bodyCollider != null
                ? _bodyCollider.offset.y - _bodyCollider.size.y * 0.5f + _feetOffset
                : spriteFootY;

            return targetFootY - spriteFootY * scaleY;
        }
    }
}
