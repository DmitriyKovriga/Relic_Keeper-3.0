using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Stats;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Projectiles
{
    public sealed class ArrowArtilleryBarrage : MonoBehaviour
    {
        private PlayerStats _owner;
        private SkillProjectileLaunchData _launch;
        private float _elapsed, _duration, _height, _radius, _delay;
        private int _count, _spawned;
        private SpriteRenderer _launchVisual;
        private Material _launchMaterial;
        public int SpawnedCount => _spawned;
        public int ArrowCount => _count;
        public float Radius => _radius;

        public static ArrowArtilleryBarrage Create(PlayerStats owner, SkillDataSO skill, StepEntry step, int slot)
        {
            var barrage = new GameObject("Arrow Artillery barrage").AddComponent<ArrowArtilleryBarrage>();
            var body = owner.GetComponent<Collider2D>();
            barrage.transform.position = body != null ? body.bounds.center : owner.transform.position;
            barrage._owner = owner;
            float aoe = Mathf.Max(.01f, 1 + owner.GetValue(StatType.AreaOfEffect) / 100);
            barrage._radius = Mathf.Max(.2f, step.GetFloat("Radius", 3) * aoe);
            barrage._height = Mathf.Max(1, step.GetFloat("SpawnHeight", 4));
            barrage._duration = Mathf.Max(.05f, step.GetFloat("RainDuration", .65f));
            barrage._delay = Mathf.Max(0, step.GetFloat("RainDelay", .15f));
            barrage._count = Mathf.Clamp(Mathf.CeilToInt(step.GetInt("ArrowCount", 17) * aoe), 3, 96);
            barrage._launch = new SkillProjectileLaunchData
            {
                OwnerStats = owner, OwnerTransform = owner.transform, SkillSlotIndex = slot,
                Skill = skill, Step = step, DamageContext = SkillDpsPreview.ResolveDamageContext(skill),
                DamageMultiplier = Mathf.Max(0, step.GetFloat("DamageMultiplier", 3)),
                ProjectilePrefab = step.GetObject<GameObject>("ProjectilePrefab"),
                Speed = ProjectileSpeedResolver.Resolve(step.GetFloat("ArrowSpeed", 16), owner),
                Lifetime = Mathf.Max(1, step.GetFloat("ArrowLifetime", 1.5f)),
                HitRadius = .28f, RotationDegreesPerSecond = 0,
                TargetLayer = 1 << 7, WorldLayer = (1 << 6) | (1 << 8), StopOnWorld = true,
                IgnoreFork = true, IgnoreChain = true,
                SharedHitHistory = new HashSet<IDamageable>()
            };
            var arrow = new GameObject("Volley launch arrow"); arrow.transform.SetParent(barrage.transform, false);
            barrage._launchVisual = arrow.AddComponent<SpriteRenderer>();
            barrage._launchVisual.sprite = Resources.Load<Sprite>("Skills/2HWeapon/Bow/BowArrow");
            barrage._launchMaterial = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam"));
            barrage._launchVisual.sharedMaterial = barrage._launchMaterial;
            arrow.transform.localRotation = Quaternion.Euler(0, 0, 90);
            barrage._launchVisual.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            barrage._launchVisual.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, barrage.transform.position.y, 2);
            barrage.Tick(0);
            return barrage;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (_owner == null || _owner.Health == null || _owner.Health.Current <= 0) { DisposeHost(); return; }
            _elapsed += Mathf.Max(0, deltaTime);
            if (_launchVisual != null)
            {
                _launchVisual.transform.localPosition = Vector3.up * (.35f + _elapsed * 14);
                _launchVisual.color = new Color(1, 1, 1, Mathf.Clamp01(1 - _elapsed / .25f) * PlayerAttackVfxOpacity.Multiplier);
            }
            while (_spawned < _count && _delay + _spawned * _duration / (_count - 1) <= _elapsed + .00001f)
            {
                // Sweep alternating sides toward the middle: the barrage is distributed, not one clump.
                int lane = _spawned % 2 == 0 ? _spawned / 2 : _count - 1 - _spawned / 2;
                float x = Mathf.Lerp(-_radius, _radius, lane / (float)(_count - 1));
                Vector2 column = (Vector2)transform.position + Vector2.right * x;
                float height = _height + (_spawned % 3) * .12f;
                foreach (var ceiling in Physics2D.RaycastAll(column, Vector2.up, height + .6f, _launch.WorldLayer))
                {
                    if (ceiling.collider.isTrigger) continue;
                    height = Mathf.Min(height, ceiling.distance - .6f);
                    break;
                }
                if (height >= .75f) SkillProjectile.Spawn(_launch, column + Vector2.up * height, Vector2.down);
                _spawned++;
            }
            if (_spawned >= _count) DisposeHost();
        }

        private void DisposeHost()
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            if (_launchMaterial == null) return;
            if (Application.isPlaying) Destroy(_launchMaterial); else DestroyImmediate(_launchMaterial);
        }
    }
}
