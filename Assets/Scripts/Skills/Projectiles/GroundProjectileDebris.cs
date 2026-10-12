using UnityEngine;
using Scripts.Skills.Visuals;

namespace Scripts.Skills.Projectiles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GroundProjectileDebris : MonoBehaviour
    {
        [SerializeField] private Material _particleMaterial;
        [SerializeField] private bool _stationaryImpact;
        [SerializeField] private LayerMask _groundLayer = 1 << 6;
        [SerializeField, Min(0.05f)] private float _spacing = 0.4f;
        [SerializeField, Range(1, 16)] private int _burstCount = 6;
        [SerializeField] private Vector2 _lifetime = new Vector2(0.45f, 0.8f);
        [SerializeField] private Vector2 _backwardSpeed = new Vector2(1.5f, 4f);
        [SerializeField] private Vector2 _upwardSpeed = new Vector2(2.5f, 5.5f);
        [SerializeField] private Vector2 _sizeInPixels = new Vector2(2, 4);
        [SerializeField, Min(0)] private float _gravity = 1.25f;
        [SerializeField] private Color _darkSoil = new Color(0.29f, 0.25f, 0.18f);
        [SerializeField] private Color _lightStone = new Color(0.62f, 0.61f, 0.44f);

        private SpriteRenderer _source;
        private ParticleSystem _particles;
        private Vector2 _lastContact;
        private float _remainingDistance;
        private bool _hasContact;
        private readonly System.Random _random = new System.Random();

        public ParticleSystem RuntimeParticles => _particles;

        private void OnEnable()
        {
            _source = GetComponent<SpriteRenderer>();
            _hasContact = false;
            _remainingDistance = 0;
        }

        private void LateUpdate() => EmitAfterMovement();

        // Called after the projectile has moved and snapped to the surface.
        public void EmitAfterMovement()
        {
            if (!isActiveAndEnabled || _source == null || !_source.enabled) return;
            int facing = _source.flipX ? -1 : 1;
            Bounds bounds = _source.bounds;
            if (_stationaryImpact)
            {
                if (_hasContact) return;
                float spacing = Mathf.Max(0.05f, _spacing);
                for (float x = bounds.min.x + spacing * 0.5f; x < bounds.max.x; x += spacing)
                {
                    Vector2 surface = new Vector2(x, bounds.min.y);
                    if (!TrySurface(surface, out _)) continue;
                    // Throw earth away from the centre of a stationary hammer impact.
                    EmitAt(surface, x < bounds.center.x ? 1 : -1);
                    _hasContact = true;
                }
                return;
            }
            Vector2 contact = new Vector2(bounds.center.x + facing * bounds.extents.x * 0.85f, bounds.min.y);
            if (!TrySurface(contact, out _))
            {
                _hasContact = false;
                _remainingDistance = 0;
                return;
            }
            if (!_hasContact)
            {
                EmitAt(contact, facing);
                _hasContact = true;
            }
            else
            {
                float distance = Vector2.Distance(_lastContact, contact);
                float spacing = Mathf.Max(0.05f, _spacing);
                // A pooled projectile or teleport must not draw a trail across the map.
                if (distance > 4f)
                {
                    _remainingDistance = 0;
                    EmitAt(contact, facing);
                }
                else if (distance > 0.0001f)
                {
                    float first = spacing - _remainingDistance;
                    for (float travelled = first; travelled <= distance; travelled += spacing)
                        EmitAt(Vector2.Lerp(_lastContact, contact, travelled / distance), facing);
                    _remainingDistance = (_remainingDistance + distance) % spacing;
                }
            }
            _lastContact = contact;
        }

        private bool TrySurface(Vector2 contact, out RaycastHit2D hit)
        {
            hit = Physics2D.Raycast(contact + Vector2.up * 0.2f, Vector2.down, 0.4f, _groundLayer);
            return hit.collider != null && !hit.collider.isTrigger && hit.normal.y >= 0.55f;
        }

        private void EmitAt(Vector2 contact, int facing)
        {
            if (!TrySurface(contact, out var hit)) return;
            EnsureParticles();
            var renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerID = _source.sortingLayerID;
            renderer.sortingOrder = _source.sortingOrder + 1;
            for (int i = 0; i < _burstCount; i++)
            {
                Color color = Color.Lerp(_darkSoil, _lightStone, Next01());
                color = PlayerAttackVfxOpacity.MultiplyAlpha(color);
                _particles.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(hit.point.x, hit.point.y + 1f / 24f, transform.position.z),
                    velocity = new Vector3(-facing * Between(_backwardSpeed), Between(_upwardSpeed), 0),
                    startLifetime = Between(_lifetime),
                    startSize = Mathf.Round(Between(_sizeInPixels)) / 24f,
                    startColor = color,
                    rotation = 90f * _random.Next(4),
                    angularVelocity = 90f * (_random.Next(3) - 1)
                }, 1);
            }
        }

        private void EnsureParticles()
        {
            if (_particles != null) return;
            // Independent world object: moving/pooling the projectile cannot drag its debris.
            var root = new GameObject("Ground impact world debris");
            _particles = root.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.gravityModifier = _gravity;
            main.startSpeed = 0;
            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;
            var collision = _particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = _groundLayer;
            collision.enableDynamicColliders = false;
            collision.bounce = 0.2f;
            collision.dampen = 0.65f;
            collision.lifetimeLoss = 0.2f;
            var color = _particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.65f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _particleMaterial != null ? _particleMaterial : _source.sharedMaterial;
            _particles.Play();
        }

        private float Next01() => (float)_random.NextDouble();
        private float Between(Vector2 range) => Mathf.Lerp(range.x, range.y, Next01());

        private void OnDisable()
        {
            _hasContact = false;
            if (_particles == null) return;
            if (Application.isPlaying)
            {
                var main = _particles.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else DestroyImmediate(_particles.gameObject);
            _particles = null;
        }
    }
}
