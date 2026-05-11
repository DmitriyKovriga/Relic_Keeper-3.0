using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Skills.Steps;
using Scripts.Skills.Modules;
using Scripts.Combat;
using Scripts.StatusEffects;

namespace Scripts.Skills
{
    /// <summary>
    /// Выполняет скилл по рецепту степов. Поддерживает отложенные триггеры (несколько действий в один момент % VFX) и ParallelGroup.
    /// </summary>
    [RequireComponent(typeof(SkillMovementControl))]
    [RequireComponent(typeof(SkillHandAnimation))]
    public class SkillStepRunner : SkillBehaviour
    {
        private const float DefaultActionSpeed = 1f;
        private const float MinActionSpeed = 0.05f;
        private const float MaxActionSpeed = 12f;

        private enum SpawnVfxGrowthMode
        {
            Centered = 0,
            LockedAwayFromCaster = 1
        }

        [Header("Damage/Hitbox (for DealDamage steps)")]
        [SerializeField] private LayerMask _targetLayer = ~0;

        private SkillMovementControl _moveCtrl;
        private SkillHandAnimation _animCtrl;
        private SkillStepContext _ctx;
        private Coroutine _runCoroutine;
        private bool _cancelled;
        private List<(int stepIndex, StepEntry step, int sourceIdx, float pct)> _pendingDamageByVfxLife;

        public override void Cancel()
        {
            _cancelled = true;

            if (_runCoroutine != null)
            {
                StopCoroutine(_runCoroutine);
                _runCoroutine = null;
            }

            Cleanup();
        }

        private void Awake()
        {
            _moveCtrl = GetComponent<SkillMovementControl>();
            _animCtrl = GetComponent<SkillHandAnimation>();
        }

        public override void Initialize(PlayerStats stats, SkillDataSO data)
        {
            base.Initialize(stats, data);
            _moveCtrl.Initialize(stats);
            _animCtrl.Initialize(stats);
        }

        protected override void Execute()
        {
            if (_data?.Recipe == null || _data.Recipe.Steps == null || _data.Recipe.Steps.Count == 0)
            {
                Debug.LogWarning("[SkillStepRunner] No recipe or empty steps.");
                _isCasting = false;
                return;
            }

            _cancelled = false;
            _ctx = new SkillStepContext
            {
                OwnerStats = _ownerStats,
                TotalDuration = 1f / ResolveActionSpeed(),
                AoeScale = 1f + _ownerStats.GetValue(StatType.AreaOfEffect) / 100f,
                Cancelled = false
            };
            _runCoroutine = StartCoroutine(RunRecipe());
        }

        private float ResolveActionSpeed()
        {
            float speed = _ownerStats != null ? _ownerStats.GetValue(StatType.AttackSpeed) : 0f;
            if (speed <= 0f)
                speed = DefaultActionSpeed;

            return Mathf.Clamp(speed, MinActionSpeed, MaxActionSpeed);
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_ctx != null)
                _ctx.Cleanup();
            _runCoroutine = null;
            if (_animCtrl != null) _animCtrl.ForceReset();
            if (_moveCtrl != null) _moveCtrl.SetLock(false);
            _isCasting = false;
        }

        private IEnumerator RunRecipe()
        {
            _isCasting = true;
            try
            {
                var recipe = _data.Recipe;
                var steps = recipe.Steps;
                int n = steps.Count;
                var started = new bool[n];
                var ended = new bool[n];
                var executed = new bool[n];
                _pendingDamageByVfxLife = new List<(int, StepEntry, int, float)>();

                var channelIndices = recipe.IsChanneling && recipe.ChannelLoopStepIndices != null
                    ? new HashSet<int>(recipe.ChannelLoopStepIndices)
                    : new HashSet<int>();

                float elapsed = 0f;
                while (elapsed < _ctx.TotalDuration && !_cancelled)
                {
                    elapsed += Time.deltaTime;
                    float T = Mathf.Clamp01(elapsed / _ctx.TotalDuration);

                for (int i = 0; i < n; i++)
                {
                    if (recipe.IsChanneling && channelIndices.Contains(i)) continue;
                    var step = steps[i];
                    if (step.StepDefinition == null) continue;

                    if (step.IsParallelGroup)
                    {
                        if (T >= step.StartPercentPipeline - 0.0001f && !executed[i])
                        {
                            executed[i] = true;
                            if (step.SubSteps != null && step.SubSteps.Count > 0)
                            {
                                foreach (var sub in step.SubSteps)
                                {
                                    if (sub.StepDefinition != null)
                                        ExecuteStepLogic(-1, sub, 1f, 0f);
                                }
                            }
                        }
                        continue;
                    }

                    float startP = step.StartPercentPipeline;
                    float endP = step.EndPercentPipeline;
                    bool isSpawnVfxWindow = step.StepDefinition.Id == "SpawnVFX" && endP > startP + 0.0001f;
                    bool isDuration = step.StepDefinition.IsDurationStep || isSpawnVfxWindow;

                    if (isDuration && endP <= startP) endP = Mathf.Min(1f, startP + 0.001f);

                    if (isDuration)
                    {
                        if (T >= startP - 0.0001f && !started[i])
                        {
                            started[i] = true;
                            if (step.StepDefinition.Id == "MovementLock")
                                _moveCtrl.SetLock(true);
                            else if (step.StepDefinition.Id == "SpawnVFX")
                                ExecuteStepLogic(i, step, 0f, (endP - startP) * _ctx.TotalDuration);
                        }
                        if (started[i] && T < endP + 0.0001f && step.StepDefinition.Id != "MovementLock" && step.StepDefinition.Id != "SpawnVFX")
                        {
                            float stepDuration = (endP - startP) * _ctx.TotalDuration;
                            float phaseT = stepDuration > 0 ? Mathf.Clamp01((T - startP) / (endP - startP)) : 1f;
                            ExecuteStepLogic(i, step, phaseT, stepDuration);
                        }
                        if (T >= endP - 0.0001f && started[i] && !ended[i])
                        {
                            ended[i] = true;
                            if (step.StepDefinition.Id == "MovementLock")
                                _moveCtrl.SetLock(false);
                        }
                    }
                    else
                    {
                        if (T >= startP - 0.0001f && !executed[i])
                        {
                            executed[i] = true;
                            int srcIdx = step.GetInt("SourceStepIndex", -1);
                            float vfxLifePct = step.GetFloat("VfxLifetimePercent", 0f);
                            bool deferByVfxLife = (
                                step.StepDefinition.Id == "DealDamageCircle" ||
                                step.StepDefinition.Id == "DealDamageRectangle" ||
                                step.StepDefinition.Id == "ApplyStatusSelf" ||
                                step.StepDefinition.Id == "ApplyStatusCircle" ||
                                step.StepDefinition.Id == "ApplyStatusRectangle" ||
                                step.StepDefinition.Id == "ApplyQuickStatusSelf" ||
                                step.StepDefinition.Id == "ApplyQuickStatusCircle" ||
                                step.StepDefinition.Id == "ApplyQuickStatusRectangle")
                                && srcIdx >= 0 && vfxLifePct > 0f;
                            if (deferByVfxLife)
                                _pendingDamageByVfxLife.Add((i, step, srcIdx, vfxLifePct));
                            else
                                ExecuteStepLogic(i, step, 1f, 0f);
                        }
                    }
                }
                for (int j = _pendingDamageByVfxLife.Count - 1; j >= 0; j--)
                {
                    var (stepIndex, step, sourceIdx, pct) = _pendingDamageByVfxLife[j];
                    if (_ctx.TryGetStepResult(sourceIdx, out var res) && res.Duration > 0f && (Time.time - res.SpawnTime) >= pct * res.Duration)
                    {
                        ExecuteStepLogic(stepIndex, step, 1f, 0f);
                        _pendingDamageByVfxLife.RemoveAt(j);
                    }
                }
                    yield return null;
                }

                for (int j = _pendingDamageByVfxLife.Count - 1; j >= 0; j--)
                {
                    var (stepIndex, step, sourceIdx, pct) = _pendingDamageByVfxLife[j];
                    if (_ctx.TryGetStepResult(sourceIdx, out var res) && res.Duration > 0f && (Time.time - res.SpawnTime) >= pct * res.Duration)
                    {
                        ExecuteStepLogic(stepIndex, step, 1f, 0f);
                        _pendingDamageByVfxLife.RemoveAt(j);
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    if (recipe.IsChanneling && channelIndices.Contains(i)) continue;
                    var step = steps[i];
                    if (step.StepDefinition == null) continue;
                    if (step.IsParallelGroup && !executed[i] && step.StartPercentPipeline >= 1f - 0.0001f)
                    {
                        executed[i] = true;
                        if (step.SubSteps != null && step.SubSteps.Count > 0)
                        {
                            foreach (var sub in step.SubSteps)
                            {
                                if (sub.StepDefinition != null)
                                    ExecuteStepLogic(-1, sub, 1f, 0f);
                            }
                        }
                        continue;
                    }
                    if (!step.IsParallelGroup && step.StepDefinition.IsDurationStep && started[i] && !ended[i])
                    {
                        ended[i] = true;
                        if (step.StepDefinition.Id == "MovementLock")
                            _moveCtrl.SetLock(false);
                    }
                    if (!step.IsParallelGroup && !step.StepDefinition.IsDurationStep && !executed[i] && step.StartPercentPipeline >= 1f - 0.0001f)
                    {
                        executed[i] = true;
                        ExecuteStepLogic(i, step, 1f, 0f);
                    }
                }

                if (recipe.IsChanneling && recipe.ChannelLoopStepIndices != null && recipe.ChannelLoopStepIndices.Count > 0 && !_cancelled)
                {
                    float channelStart = Time.time;
                    float tickDuration = recipe.ChannelTickDuration > 0 ? recipe.ChannelTickDuration : _ctx.TotalDuration;
                    while (Time.time - channelStart < recipe.ChannelMaxDuration && !_cancelled)
                    {
                        foreach (int idx in recipe.ChannelLoopStepIndices)
                        {
                            if (idx < 0 || idx >= steps.Count) continue;
                            var chStep = steps[idx];
                            if (chStep.StepDefinition == null) continue;
                            float startP = chStep.StartPercentPipeline;
                            float endP = chStep.EndPercentPipeline;
                            bool isSpawnVfxWindow = chStep.StepDefinition.Id == "SpawnVFX" && endP > startP + 0.0001f;
                            if ((chStep.StepDefinition.IsDurationStep || isSpawnVfxWindow) && endP > startP)
                            {
                                float sd = (endP - startP) * _ctx.TotalDuration;
                                if (chStep.StepDefinition.Id == "SpawnVFX")
                                {
                                    ExecuteStepLogic(idx, chStep, 0f, sd);
                                    yield return new WaitForSeconds(sd);
                                }
                                else
                                {
                                    for (float el = 0f; el < sd && !_cancelled; el += Time.deltaTime)
                                    {
                                        ExecuteStepLogic(idx, chStep, el / sd, sd);
                                        yield return null;
                                    }
                                }
                            }
                            else
                                ExecuteStepLogic(idx, chStep, 1f, 0f);
                        }
                        yield return new WaitForSeconds(Mathf.Max(0.01f, tickDuration));
                    }
                }
            }
            finally
            {
                Cleanup();
            }
        }

        private void ExecuteStepLogic(int stepIndex, StepEntry step, float phaseT, float stepDuration)
        {
            string id = step.StepDefinition != null ? step.StepDefinition.Id : "";
            switch (id)
            {
                case "MovementLock":
                    _moveCtrl.SetLock(true);
                    break;
                case "MovementUnlock":
                    _moveCtrl.SetLock(false);
                    break;
                case "WeaponWindup":
                    _animCtrl.LerpSlashWindup(phaseT);
                    break;
                case "WeaponStrike":
                    _animCtrl.SetWeaponVisible(false);
                    _animCtrl.SnapToSlashImpact();
                    break;
                case "WeaponRecovery":
                    _animCtrl.LerpSlashRecovery(phaseT);
                    break;
                case "Wait":
                    break;
                case "SpawnVFX":
                    ExecuteSpawnVFX(stepIndex, step, stepDuration);
                    break;
                case "DealDamageCircle":
                    ExecuteDealDamageCircle(stepIndex, step);
                    break;
                case "DealDamageRectangle":
                    ExecuteDealDamageRectangle(stepIndex, step);
                    break;
                case "GenerateMysticShield":
                    ExecuteGenerateMysticShield(step);
                    break;
                case "ConsumeMysticShield":
                    ExecuteConsumeMysticShield(step);
                    break;
                case "MysticShieldDamageBoost":
                    ExecuteMysticShieldDamageBoost(step);
                    break;
                case "ApplyStatusSelfIfMysticShieldConsumed":
                    ExecuteApplyStatusSelfIfMysticShieldConsumed(step);
                    break;
                case "ApplyStatusSelfPerConsumedMysticShield":
                    ExecuteApplyStatusSelfPerConsumedMysticShield(step);
                    break;
                case "ApplyStatusSelf":
                    ExecuteApplyStatusSelf(stepIndex, step);
                    break;
                case "ApplyStatusCircle":
                    ExecuteApplyStatusCircle(stepIndex, step);
                    break;
                case "ApplyStatusRectangle":
                    ExecuteApplyStatusRectangle(stepIndex, step);
                    break;
                case "ApplyQuickStatusSelf":
                    ExecuteApplyQuickStatusSelf(stepIndex, step);
                    break;
                case "ApplyQuickStatusSelfPerConsumedMysticShield":
                    ExecuteApplyQuickStatusSelfPerConsumedMysticShield(step);
                    break;
                case "ApplyQuickStatusCircle":
                    ExecuteApplyQuickStatusCircle(stepIndex, step);
                    break;
                case "ApplyQuickStatusRectangle":
                    ExecuteApplyQuickStatusRectangle(stepIndex, step);
                    break;
                case "ApplyStatBasedEffectSelf":
                    ExecuteApplyStatBasedEffectSelf(step);
                    break;
                default:
                    if (!string.IsNullOrEmpty(id)) Debug.Log($"[SkillStepRunner] Step '{id}' not implemented yet.");
                    break;
            }
        }

        private void ExecuteSpawnVFX(int stepIndex, StepEntry step, float requestedLifetime)
        {
            GameObject prefab = step.GetObject<GameObject>("VfxPrefab");
            bool fadeOutEnabled = step.GetBool("FadeOutEnabled", true);
            float fadeOutStartLifePercent = Mathf.Clamp01(step.GetFloat("FadeOutStartLifePercent", 0.5f));
            float fadeStartAlphaMultiplier = Mathf.Clamp01(step.GetFloat("FadeStartAlphaMultiplier", 0.5f));
            float lifetime = ResolveSpawnVfxLifetime(step, requestedLifetime);
            float offsetX = step.GetFloat("OffsetX", 0f);
            float offsetY = step.GetFloat("OffsetY", 0f);
            float scaleMultiplier = step.GetFloat("ScaleMultiplier", 1f);
            float effectiveScale = _ctx.AoeScale * scaleMultiplier;
            SpawnVfxGrowthMode growthMode = ResolveSpawnVfxGrowthMode(step);
            Vector2 baseOffset = new Vector2(offsetX * _ctx.FacingDirection, offsetY);
            if (prefab == null)
            {
                var vfxModule = GetComponent<SkillVFX>();
                if (vfxModule != null)
                {
                    GameObject moduleVfx = vfxModule.PlayForLifetime(
                        _ownerStats.transform,
                        _ctx.FacingDirection,
                        effectiveScale,
                        lifetime,
                        fadeOutEnabled,
                        fadeOutStartLifePercent,
                        fadeStartAlphaMultiplier,
                        out var moduleSpawnPos);
                    if (moduleVfx != null)
                    {
                        moduleSpawnPos = ApplySpawnVfxGrowthAnchor(moduleVfx, moduleSpawnPos, effectiveScale, baseOffset, growthMode);
                        CacheSpawnVfxStepResult(stepIndex, moduleSpawnPos, effectiveScale, lifetime, moduleVfx);
                    }
                }
                return;
            }
            bool attachToParent = step.GetBool("AttachToParent", false);
            bool invertFacing = step.GetBool("InvertFacing", false);
            Vector3 spawnPos = _ownerStats.transform.position + new Vector3(baseOffset.x, baseOffset.y, 0f);
            GameObject vfx = Instantiate(prefab, spawnPos, Quaternion.identity);
            float finalDir = _ctx.FacingDirection * (invertFacing ? -1f : 1f);
            Vector3 scale = vfx.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * finalDir * effectiveScale;
            scale.y = Mathf.Abs(scale.y) * effectiveScale;
            vfx.transform.localScale = scale;
            var anim = vfx.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.speed = SkillVFX.GetAnimatorPlaybackDurationAtSpeedOne(anim, lifetime) / lifetime;
            spawnPos = ApplySpawnVfxGrowthAnchor(vfx, spawnPos, effectiveScale, baseOffset, growthMode);
            var autoDestroy = AutoDestroyVFX.Ensure(vfx);
            if (autoDestroy != null)
                autoDestroy.Initialize(lifetime, fadeOutEnabled, fadeOutStartLifePercent, fadeStartAlphaMultiplier);
            if (attachToParent) vfx.transform.SetParent(_ownerStats.transform);

            CacheSpawnVfxStepResult(stepIndex, spawnPos, effectiveScale, lifetime, vfx);
        }

        private float ResolveSpawnVfxLifetime(StepEntry step, float requestedLifetime)
        {
            if (requestedLifetime > 0.0001f)
                return requestedLifetime;

            float legacyBaseDuration = Mathf.Max(0.0001f, step.GetFloat("BaseDuration", 0.5f));
            float attackSpeed = _ctx.TotalDuration > 0f ? 1f / _ctx.TotalDuration : 1f;
            return legacyBaseDuration / Mathf.Max(0.0001f, attackSpeed);
        }

        private void CacheSpawnVfxStepResult(int stepIndex, Vector3 spawnPos, float scale, float lifetime, GameObject vfx)
        {
            Vector3 visualCenter = spawnPos;
            float visualRadius = 0f;
            Transform visualTransform = vfx != null ? vfx.transform : null;
            var sr = vfx != null ? vfx.GetComponentInChildren<SpriteRenderer>() : null;
            if (TryGetCurrentVfxMetrics(sr, out var currentCenter, out var currentSize))
            {
                visualCenter = currentCenter;
                visualRadius = Mathf.Max(currentSize.x, currentSize.y) * 0.5f;
            }

            _ctx.SetStepResult(
                stepIndex,
                spawnPos,
                scale,
                lifetime,
                Time.time,
                visualCenter,
                visualRadius,
                visualTransform,
                sr);
        }

        private void ExecuteDealDamageCircle(int stepIndex, StepEntry step)
        {
            ResolveCircleArea(step, out Vector2 center, out float radius);
            var targets = GetTargetsInCircle(center, radius);
            float mult = ResolveDamageMultiplier(step);
            var snapshot = DamageCalculator.CreateDamageSnapshot(_ownerStats, mult, ResolveDamageContext(), step.DamageConversions);
            foreach (var t in targets) t.TakeDamage(snapshot);
        }

        private void ExecuteDealDamageRectangle(int stepIndex, StepEntry step)
        {
            ResolveRectangleArea(step, out Vector2 center, out Vector2 size, out float angle);
            var targets = GetTargetsInBox(center, size, angle);
            float mult = ResolveDamageMultiplier(step);
            var snapshot = DamageCalculator.CreateDamageSnapshot(_ownerStats, mult, ResolveDamageContext(), step.DamageConversions);
            foreach (var t in targets) t.TakeDamage(snapshot);
        }

        private float ResolveDamageMultiplier(StepEntry step)
        {
            float baseMultiplier = step.GetFloat("DamageMultiplier", 1f);
            float shieldMultiplier = _ctx != null ? _ctx.MysticShieldDamageMultiplier : 1f;
            return Mathf.Max(0f, baseMultiplier * Mathf.Max(0f, shieldMultiplier));
        }

        private void ExecuteConsumeMysticShield(StepEntry step)
        {
            if (_ctx == null || _ownerStats == null)
                return;

            int amount = Mathf.Max(1, step.GetInt("Amount", 1));
            bool consumeAll = step.GetBool("ConsumeAll", false);
            bool requireFullAmount = step.GetBool("RequireFullAmount", false);
            if (!MysticShieldController.TryResolve(_ownerStats.transform, out MysticShieldController shield) || shield == null)
                return;

            if (consumeAll)
            {
                if (shield.TryConsumeAllCharges(out int allConsumed))
                    _ctx.RegisterMysticShieldConsumption(allConsumed);
                return;
            }

            if (requireFullAmount && shield.CurrentCharges < amount)
                return;

            if (shield.TryConsumeCharges(amount, out int consumed))
                _ctx.RegisterMysticShieldConsumption(consumed);
        }

        private void ExecuteGenerateMysticShield(StepEntry step)
        {
            if (_ctx == null || _ownerStats == null)
                return;

            if (!MysticShieldController.TryResolve(_ownerStats.transform, out MysticShieldController shield) || shield == null)
                return;

            bool fillToMax = step.GetBool("FillToMax", false);
            int generated = fillToMax
                ? shield.FillCharges()
                : shield.AddCharges(Mathf.Max(1, step.GetInt("Amount", 1)));

            _ctx.RegisterMysticShieldGeneration(generated);
        }

        private void ExecuteMysticShieldDamageBoost(StepEntry step)
        {
            if (_ctx == null)
                return;

            int minConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1));
            if (_ctx.MysticShieldsConsumed < minConsumed)
                return;

            float bonusPercentPerShield = step.GetFloat("BonusPercentPerConsumedShield", 50f);
            float multiplier = 1f + Mathf.Max(0f, bonusPercentPerShield) * _ctx.MysticShieldsConsumed / 100f;
            _ctx.MultiplyDamageFromMysticShield(multiplier);
        }

        private void ExecuteApplyStatusSelfIfMysticShieldConsumed(StepEntry step)
        {
            if (_ctx == null)
                return;

            int minConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1));
            if (_ctx.MysticShieldsConsumed < minConsumed)
                return;

            StatusEffectSO effect = step.GetObject<StatusEffectSO>("StatusEffect");
            if (effect == null)
                return;

            if (StatusEffectController.TryResolve(transform, out StatusEffectController controller))
                controller.ApplyStatusEffect(effect, this);
        }

        private void ExecuteApplyStatusSelfPerConsumedMysticShield(StepEntry step)
        {
            if (_ctx == null || _ctx.MysticShieldsConsumed <= 0)
                return;

            int minConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1));
            if (_ctx.MysticShieldsConsumed < minConsumed)
                return;

            StatusEffectSO effect = step.GetObject<StatusEffectSO>("StatusEffect");
            if (effect == null)
                return;

            if (StatusEffectController.TryResolve(transform, out StatusEffectController controller))
                controller.ApplyStatusEffectScaled(effect, _ctx.MysticShieldsConsumed, this);
        }

        private void ExecuteApplyStatusSelf(int stepIndex, StepEntry step)
        {
            StatusEffectSO effect = step.GetObject<StatusEffectSO>("StatusEffect");
            if (effect == null)
                return;

            if (StatusEffectController.TryResolve(transform, out StatusEffectController controller))
                controller.ApplyStatusEffect(effect, this);
        }

        private void ExecuteApplyStatusCircle(int stepIndex, StepEntry step)
        {
            StatusEffectSO effect = step.GetObject<StatusEffectSO>("StatusEffect");
            if (effect == null)
                return;

            ResolveCircleArea(step, out Vector2 center, out float radius);
            var targets = GetStatusTargetsInCircle(center, radius);
            for (int i = 0; i < targets.Count; i++)
                targets[i].ApplyStatusEffect(effect, this);
        }

        private void ExecuteApplyStatusRectangle(int stepIndex, StepEntry step)
        {
            StatusEffectSO effect = step.GetObject<StatusEffectSO>("StatusEffect");
            if (effect == null)
                return;

            ResolveRectangleArea(step, out Vector2 center, out Vector2 size, out float angle);
            var targets = GetStatusTargetsInBox(center, size, angle);
            for (int i = 0; i < targets.Count; i++)
                targets[i].ApplyStatusEffect(effect, this);
        }

        private void ExecuteApplyQuickStatusSelf(int stepIndex, StepEntry step)
        {
            if (StatusEffectController.TryResolve(transform, out StatusEffectController controller))
                ApplyQuickStatusToController(controller, step, 1, "SkillQuickSelf");
        }

        private void ExecuteApplyQuickStatusSelfPerConsumedMysticShield(StepEntry step)
        {
            if (_ctx == null || _ctx.MysticShieldsConsumed <= 0)
                return;

            int minConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1));
            if (_ctx.MysticShieldsConsumed < minConsumed)
                return;

            if (StatusEffectController.TryResolve(transform, out StatusEffectController controller))
                ApplyQuickStatusToController(controller, step, _ctx.MysticShieldsConsumed, "SkillQuickSelfPerShield");
        }

        private void ExecuteApplyQuickStatusCircle(int stepIndex, StepEntry step)
        {
            ResolveCircleArea(step, out Vector2 center, out float radius);
            var targets = GetStatusTargetsInCircle(center, radius);
            for (int i = 0; i < targets.Count; i++)
                ApplyQuickStatusToController(targets[i], step, 1, "SkillQuickCircle");
        }

        private void ExecuteApplyQuickStatusRectangle(int stepIndex, StepEntry step)
        {
            ResolveRectangleArea(step, out Vector2 center, out Vector2 size, out float angle);
            var targets = GetStatusTargetsInBox(center, size, angle);
            for (int i = 0; i < targets.Count; i++)
                ApplyQuickStatusToController(targets[i], step, 1, "SkillQuickRectangle");
        }

        private void ApplyQuickStatusToController(StatusEffectController controller, StepEntry step, int stackCount, string runtimeId)
        {
            if (controller == null || step == null)
                return;

            var modifiers = new List<SerializableStatModifier>
            {
                new SerializableStatModifier
                {
                    Stat = (StatType)Mathf.Clamp(step.GetInt("QuickStatusStat", (int)StatType.MoveSpeed), 0, System.Enum.GetValues(typeof(StatType)).Length - 1),
                    Value = step.GetFloat("QuickStatusValue", 0f) * Mathf.Max(1, stackCount),
                    Type = (StatModType)step.GetInt("QuickStatusModType", (int)StatModType.PercentAdd)
                }
            };

            float duration = Mathf.Max(0f, step.GetFloat("QuickStatusDuration", 0f));
            StatusEffectKind kind = step.GetInt("QuickStatusKind", 0) == (int)StatusEffectKind.Debuff
                ? StatusEffectKind.Debuff
                : StatusEffectKind.Buff;

            StatusEffectController.RuntimeStatusHandle handle = controller.ApplyRuntimeStatusEffect(
                modifiers,
                duration,
                kind,
                this,
                runtimeId);

            if (duration <= 0f && handle != null)
                _ctx?.RegisterCleanup(handle.Dispose);
        }

        private void ExecuteApplyStatBasedEffectSelf(StepEntry step)
        {
            if (_ownerStats == null)
                return;

            StatType sourceStat = ResolveStatType(step.GetInt("SourceStat", (int)StatType.Armor), StatType.Armor);
            float sourcePercent = step.GetFloat("SourcePercent", 25f);
            float value = _ownerStats.GetValue(sourceStat) * (sourcePercent / 100f);
            if (Mathf.Approximately(value, 0f))
                return;

            var operation = (DerivedStatEffectOperation)step.GetInt("Operation", (int)DerivedStatEffectOperation.AddStatModifier);
            switch (operation)
            {
                case DerivedStatEffectOperation.RestoreHealth:
                    _ownerStats.Health?.Increase(value);
                    break;
                case DerivedStatEffectOperation.RestoreMana:
                    _ownerStats.Mana?.Increase(value);
                    break;
                default:
                    ApplyStatBasedModifier(step, value);
                    break;
            }
        }

        private void ApplyStatBasedModifier(StepEntry step, float value)
        {
            if (!StatusEffectController.TryResolve(transform, out StatusEffectController controller))
                return;

            StatType targetStat = ResolveStatType(step.GetInt("TargetStat", (int)StatType.HealthRegen), StatType.HealthRegen);
            StatModType modifierType = (StatModType)step.GetInt("TargetModifierType", (int)StatModType.Flat);
            float duration = Mathf.Max(0f, step.GetFloat("Duration", 0f));
            StatusEffectKind kind = step.GetInt("StatusKind", 0) == (int)StatusEffectKind.Debuff
                ? StatusEffectKind.Debuff
                : StatusEffectKind.Buff;

            var modifiers = new List<SerializableStatModifier>
            {
                new SerializableStatModifier
                {
                    Stat = targetStat,
                    Value = value,
                    Type = modifierType
                }
            };

            StatusEffectController.RuntimeStatusHandle handle = controller.ApplyRuntimeStatusEffect(
                modifiers,
                duration,
                kind,
                this,
                "SkillStatBasedEffect");

            if (duration <= 0f && handle != null)
                _ctx?.RegisterCleanup(handle.Dispose);
        }

        private static StatType ResolveStatType(int rawValue, StatType fallback)
        {
            if (System.Enum.IsDefined(typeof(StatType), rawValue))
                return (StatType)rawValue;

            return fallback;
        }

        private Vector2 GetVisualCenterOrFallback(SkillStepContext.StepResult result)
        {
            if (TryGetCurrentVfxMetrics(result.VisualSpriteRenderer, out var center, out _))
                return center;

            if (result.VisualTransform != null)
                return result.VisualTransform.position;

            return result.VisualCenter;
        }

        private Vector2 GetVisualSizeOrFallback(SkillStepContext.StepResult result)
        {
            if (TryGetCurrentVfxMetrics(result.VisualSpriteRenderer, out _, out var size))
                return size;

            if (result.VisualRadius > 0f)
            {
                float diameter = result.VisualRadius * 2f;
                return new Vector2(diameter, diameter);
            }

            float fallback = Mathf.Max(0.1f, result.Scale);
            return new Vector2(fallback, fallback);
        }

        private bool TryGetCurrentVfxMetrics(SpriteRenderer spriteRenderer, out Vector2 worldCenter, out Vector2 worldSize)
        {
            worldCenter = Vector2.zero;
            worldSize = Vector2.zero;
            if (spriteRenderer == null)
                return false;

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null)
            {
                Bounds bounds = spriteRenderer.bounds;
                worldCenter = bounds.center;
                worldSize = bounds.size;
                return worldSize.x > 0f && worldSize.y > 0f;
            }

            float ppu = sprite.pixelsPerUnit <= 0f ? 100f : sprite.pixelsPerUnit;
            Vector2 localSize = new Vector2(sprite.rect.width / ppu, sprite.rect.height / ppu);
            Vector2 localCenter = new Vector2(
                (sprite.rect.width * 0.5f - sprite.pivot.x) / ppu,
                (sprite.rect.height * 0.5f - sprite.pivot.y) / ppu);

            Vector3 lossyScale = spriteRenderer.transform.lossyScale;
            worldSize = new Vector2(localSize.x * Mathf.Abs(lossyScale.x), localSize.y * Mathf.Abs(lossyScale.y));
            worldCenter = spriteRenderer.transform.TransformPoint(localCenter);
            return worldSize.x > 0f && worldSize.y > 0f;
        }

        private SpawnVfxGrowthMode ResolveSpawnVfxGrowthMode(StepEntry step)
        {
            int rawMode = step.GetInt("GrowthMode", 0);
            return rawMode == (int)SpawnVfxGrowthMode.LockedAwayFromCaster
                ? SpawnVfxGrowthMode.LockedAwayFromCaster
                : SpawnVfxGrowthMode.Centered;
        }

        private Vector3 ApplySpawnVfxGrowthAnchor(GameObject vfx, Vector3 spawnPos, float effectiveScale, Vector2 baseOffset, SpawnVfxGrowthMode growthMode)
        {
            if (vfx == null || growthMode == SpawnVfxGrowthMode.Centered || effectiveScale <= 1.0001f)
                return spawnPos;

            var spriteRenderer = vfx.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
                return spawnPos;

            Vector2 finalSize = spriteRenderer.bounds.size;
            if (finalSize.x <= 0f || finalSize.y <= 0f)
                return spawnPos;

            float baseWidth = finalSize.x / effectiveScale;
            float baseHeight = finalSize.y / effectiveScale;
            float extraWidth = Mathf.Max(0f, finalSize.x - baseWidth);
            float extraHeight = Mathf.Max(0f, finalSize.y - baseHeight);

            Vector3 shift = Vector3.zero;
            if (Mathf.Abs(baseOffset.x) > 0.0001f)
                shift.x = Mathf.Sign(baseOffset.x) * extraWidth * 0.5f;
            if (Mathf.Abs(baseOffset.y) > 0.0001f)
                shift.y = Mathf.Sign(baseOffset.y) * extraHeight * 0.5f;

            if (shift.sqrMagnitude <= 0.0000001f)
                return spawnPos;

            vfx.transform.position += shift;
            return spawnPos + shift;
        }

        private List<IDamageable> GetTargetsInCircle(Vector2 center, float radius)
        {
            var list = new List<IDamageable>();
            var uniqueTargets = new HashSet<IDamageable>();
            var hits = Physics2D.OverlapCircleAll(center, radius, _targetLayer);
            foreach (var h in hits)
            {
                if (TryResolveValidDamageTarget(h, out IDamageable target) && uniqueTargets.Add(target))
                    list.Add(target);
            }
            return list;
        }

        private List<IDamageable> GetTargetsInBox(Vector2 center, Vector2 size, float angleDeg)
        {
            var list = new List<IDamageable>();
            var uniqueTargets = new HashSet<IDamageable>();
            var hits = Physics2D.OverlapBoxAll(center, size, angleDeg, _targetLayer);
            foreach (var h in hits)
            {
                if (TryResolveValidDamageTarget(h, out IDamageable target) && uniqueTargets.Add(target))
                    list.Add(target);
            }
            return list;
        }

        private bool TryResolveValidDamageTarget(Collider2D hit, out IDamageable target)
        {
            target = null;
            if (hit == null)
                return false;

            if (!hit.TryGetComponent(out target))
                return false;

            if (IsOwnerCollider(hit))
                return false;

            if (target is Component component && IsOwnerTransform(component.transform))
                return false;

            return true;
        }

        private bool IsOwnerCollider(Collider2D hit)
        {
            return _ownerStats != null && hit.transform != null && IsOwnerTransform(hit.transform);
        }

        private bool IsOwnerTransform(Transform target)
        {
            if (_ownerStats == null || target == null)
                return false;

            Transform owner = _ownerStats.transform;
            return target == owner || target.IsChildOf(owner) || owner.IsChildOf(target);
        }

        private List<StatusEffectController> GetStatusTargetsInCircle(Vector2 center, float radius)
        {
            var uniqueTargets = new HashSet<StatusEffectController>();
            var hits = Physics2D.OverlapCircleAll(center, radius, _targetLayer);
            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                if (StatusEffectController.TryResolve(hit.transform, out StatusEffectController controller))
                    uniqueTargets.Add(controller);
            }

            return new List<StatusEffectController>(uniqueTargets);
        }

        private List<StatusEffectController> GetStatusTargetsInBox(Vector2 center, Vector2 size, float angleDeg)
        {
            var uniqueTargets = new HashSet<StatusEffectController>();
            var hits = Physics2D.OverlapBoxAll(center, size, angleDeg, _targetLayer);
            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                if (StatusEffectController.TryResolve(hit.transform, out StatusEffectController controller))
                    uniqueTargets.Add(controller);
            }

            return new List<StatusEffectController>(uniqueTargets);
        }

        private void ResolveCircleArea(StepEntry step, out Vector2 center, out float radius)
        {
            int sourceIdx = step.GetInt("SourceStepIndex", -1);
            if (sourceIdx >= 0 && _ctx.TryGetStepResult(sourceIdx, out var res))
            {
                Vector2 visualSize = GetVisualSizeOrFallback(res);
                center = GetVisualCenterOrFallback(res);
                Vector2 sizeMultipliers = new Vector2(
                    Mathf.Max(0.01f, step.GetFloat("SizeX", 1f)),
                    Mathf.Max(0.01f, step.GetFloat("SizeY", 1f)));
                Vector2 scaledSize = Vector2.Scale(visualSize, sizeMultipliers);
                Vector2 offset = new Vector2(
                    step.GetFloat("OffsetX", 0f) * res.Scale * _ctx.FacingDirection,
                    step.GetFloat("OffsetY", 0f) * res.Scale);
                center += offset;
                radius = Mathf.Max(scaledSize.x, scaledSize.y) * 0.5f;
                return;
            }

            float offsetX = step.GetFloat("OffsetX", 0f);
            float offsetY = step.GetFloat("OffsetY", 0f);
            float baseRadius = step.GetFloat("Radius", 1.5f);
            radius = baseRadius * _ctx.AoeScale;
            float shiftForward = radius - baseRadius;
            float finalOffsetX = offsetX + shiftForward;
            center = (Vector2)_ownerStats.transform.position + new Vector2(finalOffsetX * _ctx.FacingDirection, offsetY);
        }

        private void ResolveRectangleArea(StepEntry step, out Vector2 center, out Vector2 size, out float angle)
        {
            int sourceIdx = step.GetInt("SourceStepIndex", -1);
            if (sourceIdx >= 0 && _ctx.TryGetStepResult(sourceIdx, out var res))
            {
                Vector2 visualSize = GetVisualSizeOrFallback(res);
                center = GetVisualCenterOrFallback(res);
                Vector2 sizeMultipliers = new Vector2(
                    Mathf.Max(0.01f, step.GetFloat("SizeX", 1f)),
                    Mathf.Max(0.01f, step.GetFloat("SizeY", 1f)));
                size = Vector2.Scale(visualSize, sizeMultipliers);
                center += new Vector2(
                    step.GetFloat("OffsetX", 0f) * res.Scale * _ctx.FacingDirection,
                    step.GetFloat("OffsetY", 0f) * res.Scale);
            }
            else
            {
                center = (Vector2)_ownerStats.transform.position + new Vector2(
                    step.GetFloat("OffsetX", 0f) * _ctx.FacingDirection,
                    step.GetFloat("OffsetY", 0f));
                size = new Vector2(step.GetFloat("SizeX", 2f), step.GetFloat("SizeY", 1f)) * _ctx.AoeScale;
            }

            angle = step.GetFloat("Angle", 0f);
        }
    }
}
