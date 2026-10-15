using UnityEngine;
using Scripts.Stats;
using Scripts.Skills.Visuals;

namespace Scripts.Skills
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerStasisController : MonoBehaviour
    {
        private PlayerStats _stats;
        private Rigidbody2D _body;
        private RigidbodyConstraints2D _savedConstraints;
        private float _savedGravity;
        private float _healPercentPerSecond;
        private float _duration;
        private FrostAnabiosisVisual _visual;

        public bool IsActive { get; private set; }
        public float Remaining { get; private set; }
        public static bool IsActiveFor(Component owner) => owner != null &&
            owner.TryGetComponent<PlayerStasisController>(out var stasis) && stasis.IsActive;

        public bool Begin(float duration, float healPercentPerSecond, SkillBehaviour source = null)
        {
            _stats = GetComponent<PlayerStats>();
            if (!isActiveAndEnabled || IsActive || duration <= 0 || _stats.Health == null || _stats.Health.Current <= 0) return false;
            IsActive = true;
            Remaining = _duration = duration;
            _healPercentPerSecond = Mathf.Max(0, healPercentPerSecond);
            // Cancel windups/charges in other slots; the crystal lives on the player, not the equipped shield.
            GetComponent<PlayerSkillManager>()?.CancelAllSkillsExcept(source);
            _body = GetComponent<Rigidbody2D>();
            if (_body != null)
            {
                _savedConstraints = _body.constraints; _savedGravity = _body.gravityScale;
                _body.linearVelocity = Vector2.zero; _body.angularVelocity = 0;
                _body.constraints = RigidbodyConstraints2D.FreezeAll; _body.gravityScale = 0;
            }
            _visual = FrostAnabiosisVisual.Create(transform, GetComponent<Collider2D>());
            return true;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            if (_stats == null || _stats.Health == null || _stats.Health.Current <= 0) { End(); return; }
            float elapsed = Mathf.Min(Mathf.Max(0, deltaTime), Remaining);
            Remaining = Mathf.Max(0, Remaining - elapsed);
            _stats.Health.Increase(_stats.Health.Max * _healPercentPerSecond * .01f * elapsed);
            if (_visual != null) _visual.Sample(1 - Remaining / _duration);
            if (Remaining <= 0) End();
        }

        public void End()
        {
            if (!IsActive) return;
            IsActive = false; Remaining = 0;
            if (_body != null)
            {
                _body.constraints = _savedConstraints; _body.gravityScale = _savedGravity;
                _body.linearVelocity = Vector2.zero;
            }
            if (_visual != null)
            {
                if (Application.isPlaying) _visual.Release();
                else DestroyImmediate(_visual.gameObject);
                _visual = null;
            }
        }

        private void OnDisable() => End();
    }
}
