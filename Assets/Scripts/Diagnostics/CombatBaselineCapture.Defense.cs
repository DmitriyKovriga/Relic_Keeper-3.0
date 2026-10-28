#if UNITY_EDITOR
using System.Linq;
using Scripts.Combat;
using Scripts.GameplayEvents;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private static bool _defenseReview;
        private PlayerDefenseFeedback _defense;
        private GamePauseService.PauseHandle _defensePause;
        private PlayerParryController _defenseParry;
        private static readonly string[] DefenseCases = { "parry", "rogue-left", "mage-air", "rapid-parry", "partial-shield", "depleted", "dot", "zero", "disabled", "pause", "class-change", "owner-zero-health" };
        public sealed partial class FrameRecord
        {
            public int defenseResponses, activeDefenseResponses;
            public float playerHealth;
            public bool defensePaused;
        }
        public static void BeginDefenseReview() { _defenseReview = true; BeginCapture(true, true, hurtReview: true); }

        private void ConfigureDefenseCase()
        {
            _defense = _owner.GetComponent<PlayerDefenseFeedback>();
            typeof(PlayerDefenseFeedback).GetField("_profile", Private).SetValue(_defense, _comparisonProfile);
            _comparisonProfile.EnableDefenseFeedback = _case.name != "disabled";
            bool shieldCase = _case.name == "mage-air" || _case.name == "partial-shield" || _case.name == "depleted";
            _owner.GetStat(StatType.MaxMysticShield).BaseValue = shieldCase ? 3 : 0;
            _owner.GetStat(StatType.MysticShieldMitigationPercent).BaseValue = _case.name == "partial-shield" ? 50 : 100;
            _owner.GetStat(StatType.MaxMysticShieldMitigationPercent).BaseValue = 100;
            _owner.GetStat(StatType.Evasion).BaseValue = _case.name == "rogue-left" ? 1000000 : 0;
            _owner.NotifyChanged();
            var shield = _owner.GetComponent<MysticShieldController>(); shield.FillCharges();
            if (_case.name == "depleted")
            {
                _defense.enabled = false; shield.TryConsumeAllCharges(out _); _defense.enabled = true;
            }
            if (!shieldCase && _case.name != "rogue-left" && _case.name != "dot" && _case.name != "zero")
            {
                _defenseParry = _owner.gameObject.AddComponent<PlayerParryController>();
                _defenseParry.OpenWindow(this, 10, null);
            }
        }

        private void UpdateDefenseCase()
        {
            if (_frame == CastFrame) StartCaseCast();
            if (_frame == 32)
            {
                if (_case.name == "pause") _defensePause = GamePauseService.Acquire(GamePauseReason.PauseMenu);
                if (_case.name == "class-change") _owner.Initialize(Resources.Load<CharacterDataSO>("Heroes/RogueResources/Rogue"));
                if (_case.name == "owner-zero-health") _owner.Health.SetCurrent(0);
            }
            if (_frame == 50 && _defensePause != null) { _defensePause.Dispose(); _defensePause = null; }
            if (_frame != 30 && !(_case.name == "rapid-parry" && (_frame == 31 || _frame == 32 || _frame == 52))) return;
            if (_case.name == "rogue-left")
            {
                Random.State state;
                do { state = Random.state; } while (!EvasionMitigation.RollDodge(EvasionMitigation.GetDodgeChancePercent(_owner)));
                Random.state = state;
            }
            if (_case.name == "dot") _hurtReceiver.ApplyPureDamage(10, _enemy.gameObject);
            else _hurtReceiver.TakeDamage(new DamageSnapshot(_enemy.gameObject) { Physical = _case.name == "zero" ? 0 : 10 });
        }

        private void RecordDefenseFrame(FrameRecord frame)
        {
            frame.defenseResponses = _defense.AcceptedResponses; frame.activeDefenseResponses = _defense.ActiveResponses;
            frame.playerHealth = _owner.Health.Current; frame.defensePaused = GamePauseService.IsPaused;
        }

        private void ValidateDefenseCase()
        {
            bool none = _case.name == "depleted" || _case.name == "dot" || _case.name == "zero" || _case.name == "disabled";
            int responses = _defense.AcceptedResponses;
            if (none ? responses != 0 : responses == 0) _case.error = "Defense eligibility mismatch.";
            else if (_case.frames.Last().activeDefenseResponses != 0) _case.error = "Defense accent did not clear.";
            else if (!none && !_case.frames.Any(f => f.activeDefenseResponses > 0)) _case.error = "Defense was never visible.";
            else if (_case.name == "rapid-parry" && responses != 2) _case.error = "Rapid parries did not obey cooldown.";
            else if ((_case.name == "parry" || _case.name == "rogue-left" || _case.name == "mage-air") && _case.frames.Any(f => f.hitFlash > 0)) _case.error = "Successful defense produced a hurt flash.";
            else if (_case.name == "partial-shield" && !_case.frames.Any(f => f.hitFlash > 0 && f.activeDefenseResponses > 0)) _case.error = "Partial shield lost its hurt or defense response.";
        }
    }
}
#endif
