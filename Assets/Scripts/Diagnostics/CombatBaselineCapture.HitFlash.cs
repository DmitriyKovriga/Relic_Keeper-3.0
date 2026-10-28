#if UNITY_EDITOR
using System;
using System.Linq;
using Scripts.Enemies;
using Scripts.Stats;
using Scripts.Visuals;
using Scripts.Visuals.SpriteFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private bool _flashComparison;
        private MaterialPropertyBlock _flashBlock;
        private Material _flashStatusMaterial;
        private static readonly string[] FlashCases = { "normal-off", "normal-on", "heavy-off", "heavy-on", "bow-off", "bow-on",
            "critical-on", "frozen-on", "status-on", "lit-on", "zombie-on", "rapid-on" };

        public static void BeginHitFlashComparison() => BeginCapture(true, true, false, true);

        private void ConfigureHitFlashCase()
        {
            _flashBlock ??= new MaterialPropertyBlock();
            _comparisonProfile.EnableEnemyHitFlash = !_case.name.EndsWith("-off", StringComparison.Ordinal);
            var flash = _enemy.GetComponent<EnemyHitFlash>();
            typeof(EnemyHitFlash).GetField("_profile", Private).SetValue(flash, _comparisonProfile);
            flash.Initialize(_enemy.VisualRenderer);
            if (_case.name == "critical-on") _owner.GetStat(StatType.CritChance).BaseValue = 100;
            if (_case.name == "rapid-on") _owner.GetStat(StatType.AttackSpeed).BaseValue = 10;
            _owner.NotifyChanged();
            if (_case.name == "frozen-on") _enemy.GetComponent<EnemyFreezeController>().TryApplyFreeze(5);
            if (_case.name == "status-on")
            {
                _flashStatusMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/RelicFX/RelicSpriteFX.mat"));
                // Match StatusAuraPresenter's material setup; the shared demonstration asset has a static green flash.
                _flashStatusMaterial.SetFloat("_FxFlash", 0); _flashStatusMaterial.SetFloat("_FxEmission", 0);
                _flashStatusMaterial.SetFloat("_FxOutline", 0); _flashStatusMaterial.SetFloat("_FxInnerShadow", 0);
                _enemy.VisualRenderer.sharedMaterial = _flashStatusMaterial;
                var fx = _enemy.VisualRenderer.GetComponent<SpriteFxController>();
                fx.TemporaryTint(new Color(.65f, 1, .7f), 5); fx.SetEmission(Color.green, .03f);
            }
            if (_case.name == "lit-on")
            {
                var lamp = new GameObject("Temporary warm light"); lamp.transform.SetParent(_fixture.transform);
                lamp.transform.position = _enemy.transform.position + new Vector3(-.6f, 1, 0);
                var light = lamp.AddComponent<Light2D>(); light.lightType = Light2D.LightType.Point;
                light.blendStyleIndex = 1; light.color = new Color(1, .55f, .2f); light.intensity = .75f;
                light.pointLightOuterRadius = 3; light.pointLightInnerRadius = .3f;
                light.targetSortingLayers = SortingLayer.layers.Select(x => x.id).ToArray();
            }
        }

        private void RecordHitFlashFrame(FrameRecord frame)
        {
            var source = _enemy.VisualRenderer;
            frame.hitFlash = _enemy.GetComponent<EnemyHitFlash>().Amount;
            var display = _enemy.GetComponent<EnemyHitRecoil>().DisplayRenderer;
            var visible = display != null && display.enabled ? display : source;
            visible.GetPropertyBlock(_flashBlock); frame.visibleHitFlash = _flashBlock.GetFloat("_FxHitFlash");
            frame.statusEmission = _flashBlock.GetFloat("_FxRuntimeEmission");
            frame.targetColor = visible.color;
            frame.targetMaterial = visible.sharedMaterial != null ? visible.sharedMaterial.shader.name : "None";
        }

        private void DisposeHitFlashCase()
        {
            if (_flashStatusMaterial != null) UnityEngine.Object.DestroyImmediate(_flashStatusMaterial);
            _flashStatusMaterial = null;
        }

        private void ValidateHitFlashCase()
        {
            if (_case.name == "rapid-on")
            {
                // CompleteCase's ordinary one-cast assertion is inapplicable to this deliberately repeated case.
                if (_case.acceptedCasts > 1 && _case.confirmedHits > 1) _case.error = null;
                else { _case.error = "Rapid review did not reproduce multiple successful casts."; return; }
            }
            if (_case.error != null) return;
            bool enabled = _comparisonProfile.EnableEnemyHitFlash;
            if (_case.confirmedHits < 1) _case.error = "No confirmed damaging hit.";
            else if (_case.frames.Any(x => x.visibleHitFlash > .001f) != enabled) _case.error = "Flash enable/disable mismatch on the visible renderer.";
            else if (_case.frames.Last().visibleHitFlash != 0) _case.error = "Hit flash failed to clear.";
            else if (_case.frames.Any(x => x.visibleHitFlash > .50001f)) _case.error = "Flash exceeded strength cap.";
            else if (_case.frames.Any(x => x.targetMaterial != "RelicKeeper/Sprites/World Color Lit" && x.targetMaterial != "RelicKeeper/Sprites/Relic Sprite FX")) _case.error = "Flash changed the target shader.";
            else if (_case.name == "status-on" && _case.frames.Any(x => Mathf.Abs(x.statusEmission - .03f) > .0001f)) _case.error = "Combat flash replaced status emission.";
            else if (_case.name == "frozen-on" && !_enemy.GetComponent<EnemyFreezeController>().IsFrozen) _case.error = "Combat flash changed freeze state.";
            else if (_case.name == "rapid-on" && _case.frames.Count(x => x.frame > CastFrame && x.frame < 80 && x.visibleHitFlash == 0) < 5) _case.error = "Rapid hits had no quiet frames.";
        }
    }
}
#endif
