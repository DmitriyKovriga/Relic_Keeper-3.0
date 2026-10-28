#if UNITY_EDITOR
using System.Linq;
using Scripts.Combat;
using Scripts.Configuration;
using Scripts.Enemies;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private bool _hurtReview, _hurtImmortal;
        private PlayerDamageReceiver _hurtReceiver;
        private static readonly string[] HurtCases = { "warrior", "rogue-left", "mage-air", "rapid", "zero", "dot", "suppressed" };
        public static void BeginPlayerHurtReview() => BeginCapture(true, true, hurtReview: true);
        private void ConfigureHurtCase()
        {
            _hurtImmortal = PlaytestConfiguration.PlayerImmortal; PlaytestConfiguration.SetEditorPlayerImmortal(false);
            _hurtReceiver = _owner.GetComponent<PlayerDamageReceiver>() ?? _owner.gameObject.AddComponent<PlayerDamageReceiver>();
            _owner.GetStat(StatType.Evasion).BaseValue = 0; _owner.GetStat(StatType.Armor).BaseValue = 0;
            _owner.GetStat(StatType.PhysicalResist).BaseValue = 0; _owner.GetStat(StatType.MaxMysticShield).BaseValue = 0;
            _owner.GetStat(StatType.DamageTaken).BaseValue = _case.name == "zero" ? -100 : 0; _owner.NotifyChanged();
            _comparisonProfile.EnablePlayerHurt = _case.name != "suppressed";
            foreach (var art in _owner.GetComponentsInChildren<SpriteRenderer>()) art.sharedMaterial = _material;
            _frameLimit = 110;
            if (_defenseReview) ConfigureDefenseCase();
            if (_deathReview) ConfigureDeathCase();
        }
        private void UpdateHurtCase()
        {
            if (_defenseReview) { UpdateDefenseCase(); return; }
            if (_deathReview) { UpdateDeathCase(); return; }
            if (_frame == CastFrame) StartCaseCast();
            if (_frame != 30 && !(_case.name == "rapid" && (_frame == 32 || _frame == 34 || _frame == 50))) return;
            if (_case.name == "dot") _hurtReceiver.ApplyPureDamage(10, _enemy.gameObject);
            else _hurtReceiver.TakeDamage(new DamageSnapshot(_enemy.gameObject) { Physical = 10, IsDirectHit = true });
        }
        private void ValidateHurtCase()
        {
            if (_defenseReview) { ValidateDefenseCase(); return; }
            if (_deathReview) { ValidateDeathCase(); return; }
            bool expected = _case.name != "zero" && _case.name != "dot" && _case.name != "suppressed";
            bool visible = _case.frames.Any(f => f.hitFlash > 0);
            if (visible != expected) _case.error = "Player hurt eligibility mismatch.";
            else if (_case.frames.Last().hitFlash != 0) _case.error = "Player hurt did not clear.";
        }
        private void DisposeHurtCase()
        {
            _defensePause?.Dispose(); _defensePause = null;
            PlaytestConfiguration.SetEditorPlayerImmortal(_hurtImmortal);
        }
    }
}
#endif
