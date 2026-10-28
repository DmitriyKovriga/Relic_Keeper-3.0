#if UNITY_EDITOR
using System.Linq;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private static bool _deathReview;
        private static readonly string[] DeathCases = { "right", "left", "heavy", "dot", "frozen", "repeated", "fan" };
        public sealed partial class FrameRecord { public int deathFragments; public float fragmentVelocityX; }
        public sealed partial class CaseResult { public int deathCallbacks, rejectedDeathHits; public Vector2 fatalDirection; public bool livingFeedbackCleared; }
        public static void BeginDeathReview() { _deathReview = true; BeginCapture(true, true, hurtReview: true); }
        private void ConfigureDeathCase()
        {
            _health.DestroyOnDeath = true; _enemyData.DeathEffect = new EnemyDeathEffectConfig();
            if (_case.name == "fan") { Set(_enemy.GetComponent<EnemyStats>(), StatType.MaxHealth, 1); _health.Initialize(); }
            _health.OnDeath += value =>
            {
                _case.deathCallbacks++; _case.fatalDirection = value.DeathImpact.Direction;
                var recoil = value.GetComponent<EnemyHitRecoil>(); var flash = value.GetComponent<EnemyHitFlash>();
                _case.livingFeedbackCleared = (recoil.DisplayRenderer == null || !recoil.DisplayRenderer.enabled) && flash.Amount == 0;
            };
            _frameLimit = 120;
        }
        private DamageSnapshot DeathHit(float amount) => new DamageSnapshot(_owner.gameObject)
        {
            Physical = amount,
            Impact = new AttackImpactContact(new AttackImpactContext(null, 0, enableHitStop: false), _owner.transform.position,
                _case.name == "left" ? Vector2.left : Vector2.right, _case.name == "heavy" ? 2.5f : 1)
        };
        private void UpdateDeathCase()
        {
            if (_case.name == "fan") { if (_frame == CastFrame) StartCaseCast(); return; }
            if (_frame == 24) _health.TakeDamage(DeathHit(10));
            if (_frame == 28 && _case.name == "frozen") _enemy.GetComponent<EnemyFreezeController>().TryApplyFreeze(5);
            if (_frame != 30) return;
            Random.InitState(1137);
            if (_case.name == "dot") _health.ApplyPureDamage(100000, _owner.gameObject);
            else _health.TakeDamage(DeathHit(100000));
            if (_case.name == "repeated")
                for (int i = 0; i < 20; i++) if (!_health.TakeDamage(DeathHit(100000))) _case.rejectedDeathHits++;
        }
        private void RecordDeathFrame(FrameRecord frame)
        {
            var fragments = _fixture.GetComponentsInChildren<EnemyDeathFragment>();
            frame.deathFragments = fragments.Count(f => f.gameObject.activeInHierarchy);
            frame.fragmentVelocityX = fragments.Where(f => f.gameObject.activeInHierarchy).Sum(f => f.GetComponent<Rigidbody2D>().linearVelocity.x);
        }
        private void ValidateDeathCase()
        {
            if (_case.deathCallbacks != 1 || _case.kills != 1) _case.error = "Death was missing or duplicated.";
            else if (!_case.livingFeedbackCleared) _case.error = "Living-hit feedback survived death.";
            else if (!_case.frames.Any(f => f.deathFragments == 6)) _case.error = "Expected existing six-fragment death effect.";
            else if (_case.name == "repeated" && _case.rejectedDeathHits != 20) _case.error = "Late contacts were accepted.";
            else if (_case.name == "left" && _case.fatalDirection.x >= 0 || _case.name == "right" && _case.fatalDirection.x <= 0) _case.error = "Fatal direction mismatch.";
            else if (_case.name == "dot" && _case.fatalDirection != Vector2.zero) _case.error = "DoT inherited a previous hit direction.";
        }
    }
}
#endif
