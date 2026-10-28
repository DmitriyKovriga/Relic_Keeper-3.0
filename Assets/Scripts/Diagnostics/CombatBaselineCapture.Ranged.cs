#if UNITY_EDITOR
using System.Linq;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private static bool _rangedReview;
        private static SkillDataSO[] _rangedExpansion;
        private static readonly string[] RangedPilotCases = { "bow-off", "bow-on", "wand-off", "wand-on", "staff-off", "staff-on", "throw-off", "throw-on", "bow-run", "wand-air", "staff-left", "throw-air", "wand-fast", "cancel" };
        private string[] RangedCases => _rangedExpansion != null ? _rangedExpansion.Select(x => x.ID).ToArray() : RangedPilotCases;
        private string RangedSkillPath => _rangedExpansion != null ? AssetDatabase.GetAssetPath(_rangedExpansion[_index]) : "Assets/Resources/Skills/" + (_case.name.StartsWith("bow") ? "2HWeapon/Bow/SimpleBowShotSkill"
            : _case.name.StartsWith("staff") ? "2HWeapon/Staff/Fire/FlamingSpear/FlamingSpearSkill"
            : _case.name.StartsWith("throw") ? "2HWeapon/Chakram/lb/FanOfSickles/FanOfSicklesSkill" : "1HWeapon/Wand/ManaSpear/ManaSpearSkill") + ".asset";
        private string RangedWeaponKind => RangedSkillPath.Contains("/Bow/") ? "Bow" : RangedSkillPath.Contains("/Staff/") ? "Staff" : RangedSkillPath.Contains("/Chakram/") ? "Chakram" : RangedSkillPath.Contains("/Sword/") ? "Sword" : RangedSkillPath.Contains("/Axe/") ? "Axe" : "Wand";
        private string RangedHero => RangedWeaponKind == "Bow" ? "Rogue" : RangedWeaponKind == "Chakram" || RangedWeaponKind == "Sword" || RangedWeaponKind == "Axe" ? "Warrior" : "Mage";
        public static void BeginRangedReview() { _rangedExpansion = null; _rangedReview = true; BeginCapture(true, true, hurtReview: true); }
        public static void BeginRangedExpansionReview()
        {
            _rangedExpansion = Resources.LoadAll<SkillDataSO>("Skills").Where(x => x.BodyFollowStyle != RangedBodyStyle.None).OrderBy(x => x.ID).ToArray();
            _rangedReview = true; BeginCapture(true, true, hurtReview: true);
        }
        public static void BeginWeaponThrowBodyReview()
        {
            _rangedExpansion = new[] { Resources.Load<SkillDataSO>("Skills/1HWeapon/Axe/WeaponThrowSkill") };
            _rangedReview = true; BeginCapture(true, true, hurtReview: true);
        }
        private void ConfigureRangedCase()
        {
            _frameLimit = 120; _enemy.gameObject.SetActive(false);
            _comparisonProfile.EnableBodyFollow = !_case.name.EndsWith("off");
            if (_case.name.EndsWith("left")) _owner.transform.localScale = new Vector3(-1, 1, 1);
            if (_case.name.EndsWith("air"))
            {
                _owner.transform.position += Vector3.up * 1.5f;
                typeof(PlayerCharacterAnimationController).GetMethod("HandleJumpStarted", Private).Invoke(_owner.GetComponent<PlayerCharacterAnimationController>(), null);
            }
            if (_case.name.EndsWith("fast")) { _owner.GetStat(StatType.CastSpeed).AddModifier(new StatModifier(500, StatModType.PercentAdd, this)); _owner.NotifyChanged(); }
        }
        private void UpdateRangedCase()
        {
            if (_case.name.EndsWith("run")) _owner.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(5, 0);
            if (_frame == CastFrame) { StartCaseCast(); _frameLimit = 120; }
            if (_case.name == "cancel" && _frame == CastFrame + 3) _runner.Cancel();
        }
        private void ValidateRangedCase()
        {
            bool moved = _case.frames.Any(f => Mathf.Abs(f.attackBodyDegrees) > .01f);
            if (_case.name.EndsWith("fast") && (_case.recipeDuration >= .5f || _case.frames.Any(f => Mathf.Abs(f.attackBodyDegrees) >= 1.5f)))
            { _case.error = "Fast cast did not exercise a reduced body response."; return; }
            if (moved == _case.name.EndsWith("off")) _case.error = "Ranged body toggle mismatch.";
            else if (_case.frames.Any(f => Mathf.Abs(f.attackBodyDegrees) > 3.001f)) _case.error = "Ranged body exceeded its lean bound.";
            else if (_case.frames.Last().bodyOwned || Mathf.Abs(_case.frames.Last().attackBodyDegrees) > .001f) _case.error = "Ranged body did not clean up.";
        }
    }
}
#endif
