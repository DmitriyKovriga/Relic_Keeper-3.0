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

        private PlayerMovement _movement;
        private PlayerAttackInput _attack;
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
            // Keep the feet anchored while the upper body compresses/extends.
            float footY = _source.sprite != null ? _source.sprite.bounds.min.y : 0f;
            _display.transform.localPosition = new Vector3(0f, footY * (1f - scaleY), 0f);
            _pulse *= Mathf.Exp(-_returnSpeed * Time.deltaTime);
        }
    }
}
