using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Skills.Steps;
using Scripts.Skills.Modules;
using Scripts.Skills.Projectiles;
using Scripts.Combat;
using Scripts.StatusEffects;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Skills.Visuals;
using Scripts.Visuals;

namespace Scripts.Skills
{
    /// <summary>
    /// Выполняет скилл по рецепту степов. Поддерживает отложенные триггеры (несколько действий в один момент % VFX) и ParallelGroup.
    /// </summary>
    [RequireComponent(typeof(SkillMovementControl))]
    [RequireComponent(typeof(SkillHandAnimation))]
    public class SkillStepRunner : SkillBehaviour
    {
        private enum SpawnVfxGrowthMode
        {
            Centered = 0,
            LockedAwayFromCaster = 1
        }

        private enum CooldownStepTarget
        {
            CurrentSkill = 0,
            SpecificSlot = 1,
            OtherSlots = 2,
            AllSlots = 3
        }

        [Header("Damage/Hitbox (for DealDamage steps)")]
        [SerializeField] private LayerMask _targetLayer = ~0;

        private SkillMovementControl _moveCtrl;
        private SkillHandAnimation _animCtrl;
        private SkillStepContext _ctx;
        private Coroutine _runCoroutine;
        private bool _cancelled;
        private List<(int stepIndex, StepEntry step, int sourceIdx, float pct)> _pendingDamageByVfxLife;
        private readonly Dictionary<int, HashSet<int>> _persistentHitTargetsByStep = new Dictionary<int, HashSet<int>>();

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
            _persistentHitTargetsByStep.Clear();
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
                case "PlayerImpulse":
                    ExecutePlayerImpulse(step);
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
                case "SpawnProjectile":
                    ExecuteSpawnProjectile(step);
                    break;
                case "SpawnGroundProjectile":
                    ExecuteSpawnProjectile(step);
                    break;
                case "BuildChainTargets":
                    ExecuteBuildChainTargets(stepIndex, step);
                    break;
                case "SpawnChainVFX":
                    ExecuteSpawnChainVFX(step);
                    break;
                case "ChainDamage":
                    ExecuteChainDamage(stepIndex, step);
                    break;
                case "ModifyCooldown":
                    ExecuteModifyCooldown(step);
                    break;
                case "DealDamageCircle":
                    ExecuteDealDamageCircle(stepIndex, step);
                    break;
                case "DealDamageRectangle":
                    ExecuteDealDamageRectangle(stepIndex, step);
                    break;
                case "PersistentDamageCircle":
                    ExecutePersistentDamageCircle(stepIndex, step);
                    break;
                case "PersistentDamageRectangle":
                    ExecutePersistentDamageRectangle(stepIndex, step);
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

        private void ExecutePlayerImpulse(StepEntry step)
        {
            float angleDegrees = step.GetFloat("AngleDegrees", 0f);
            float force = Mathf.Max(0f, step.GetFloat("Force", 4f));
            bool relativeToFacing = step.GetBool("RelativeToFacing", true);
            bool clearCurrentVelocity = step.GetBool("ClearCurrentVelocity", false);
            _moveCtrl.ApplyImpulse(angleDegrees, force, relativeToFacing, clearCurrentVelocity);
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
            WorldRenderSorting.ConfigureSorter(
                vfx,
                RenderDepthCategory.HeroAttackVfx,
                spawnPos.y,
                localOffset: 0,
                staticAnchor: !attachToParent);

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

        private void ExecuteSpawnProjectile(StepEntry step)
        {
            if (_ownerStats == null || step == null)
                return;

            bool groundMotion = step.StepDefinition != null && step.StepDefinition.Id == "SpawnGroundProjectile";
            if (groundMotion && !step.GetBool("AllowInAir", false) && !IsOwnerGrounded())
                return;

            bool useProjectileCountStat = !groundMotion && step.GetBool("UseProjectileCountStat", true);
            int baseCount = groundMotion ? 1 : Mathf.Max(1, step.GetInt("BaseProjectileCount", 1));
            int additionalCount = useProjectileCountStat ? Mathf.Max(0, Mathf.FloorToInt(_ownerStats.GetValue(StatType.ProjectileCount))) : 0;
            int totalCount = Mathf.Max(1, baseCount + additionalCount);
            float baseSpeed = Mathf.Max(0.01f, step.GetFloat("BaseSpeed", 8f));
            float speedMultiplier = Mathf.Max(0f, 1f + _ownerStats.GetValue(StatType.ProjectileSpeed) / 100f);
            float offsetX = step.GetFloat("OffsetX", 0.45f);
            float offsetY = step.GetFloat("OffsetY", 0.35f);
            float damageMultiplier = ResolveDamageMultiplier(step);
            bool useWeaponSprite = step.GetBool("UseCurrentWeaponSprite", false);
            Sprite projectileSprite = useWeaponSprite ? ResolveCurrentWeaponSprite() : null;
            GameObject projectilePrefab = step.GetObject<GameObject>("ProjectilePrefab");

            if (projectilePrefab == null && projectileSprite == null)
            {
                Debug.LogWarning("[SkillStepRunner] SpawnProjectile needs Projectile Prefab or Use Current Weapon Sprite with equipped weapon sprite.");
                return;
            }

            LayerMask groundSurfaceLayer = groundMotion ? ResolveGroundProjectileSurfaceLayer(step) : step.GetInt("GroundLayerMask", 1 << 6);
            var data = new SkillProjectileLaunchData
            {
                OwnerStats = _ownerStats,
                OwnerTransform = _ownerStats.transform,
                Step = step,
                DamageContext = ResolveProjectileDamageContext(),
                DamageMultiplier = damageMultiplier,
                TargetLayer = _targetLayer.value == 0 ? ~0 : _targetLayer,
                WorldLayer = step.GetInt("WorldLayerMask", 1 << 6),
                StopOnWorld = step.GetBool("StopOnWorld", true),
                ProjectilePrefab = projectilePrefab,
                OverrideSprite = projectileSprite,
                Speed = baseSpeed * speedMultiplier,
                Lifetime = Mathf.Max(0.05f, step.GetFloat("Lifetime", 4f)),
                HitRadius = Mathf.Max(0.02f, step.GetFloat("HitRadius", 1f)),
                RotationDegreesPerSecond = step.GetFloat("RotationDegreesPerSecond", useWeaponSprite ? 720f : 0f),
                RemainingForks = groundMotion ? 0 : Mathf.Max(0, Mathf.FloorToInt(_ownerStats.GetValue(StatType.ProjectileFork))),
                RemainingChains = groundMotion ? 0 : Mathf.Max(0, Mathf.FloorToInt(_ownerStats.GetValue(StatType.ProjectileChain))),
                RemainingPierces = Mathf.Max(0, Mathf.FloorToInt(_ownerStats.GetValue(StatType.ProjectilePierce))),
                InfinitePierce = step.GetBool("InfinitePierce", false),
                IgnoreFork = groundMotion || step.GetBool("IgnoreFork", false),
                IgnoreChain = groundMotion || step.GetBool("IgnoreChain", false),
                ForkAngle = Mathf.Max(0f, step.GetFloat("ForkAngle", 18f)),
                ChainSearchRadius = Mathf.Max(0.1f, step.GetFloat("ChainSearchRadius", 12f)),
                GroundMotion = groundMotion || step.GetBool("GroundMotion", false),
                GroundLayer = groundSurfaceLayer,
                BreakOnGroundObstacles = groundMotion && step.GetBool("BreakOnGroundObstacles", true),
                GroundSnapUp = Mathf.Max(0.01f, step.GetFloat("GroundSnapUp", 0.7f)),
                GroundSnapDown = Mathf.Max(0.01f, step.GetFloat("GroundSnapDown", 2.5f)),
                GroundYOffset = step.GetFloat("GroundYOffset", 0.06f),
                Homing = step.GetBool("Homing", false),
                HomingSearchRadius = Mathf.Max(0.1f, step.GetFloat("HomingSearchRadius", 14f)),
                HomingTurnSpeedDegreesPerSecond = Mathf.Max(0f, step.GetFloat("HomingTurnSpeedDegreesPerSecond", 0f)),
                RemainingReversals = Mathf.Max(0, step.GetInt("ReversalCount", 0)),
                ReverseInterval = Mathf.Max(0.01f, step.GetFloat("ReverseInterval", 1f)),
                FirstReverseAtSeconds = Mathf.Max(0.01f, step.GetFloat("FirstReverseAtSeconds", 1f)),
                ReturnToOwnerOnReverse = step.GetBool("ReturnToOwnerOnReverse", true),
                ClearHitHistoryOnReverse = step.GetBool("ClearHitHistoryOnReverse", true)
            };

            Vector2 origin = (Vector2)_ownerStats.transform.position + new Vector2(offsetX * _ctx.FacingDirection, offsetY);
            if (data.GroundMotion && TryProjectToGround(origin, data.GroundLayer, data.GroundSnapUp, data.GroundSnapDown, data.GroundYOffset, out Vector2 groundedOrigin))
                origin = groundedOrigin;
            else if (data.GroundMotion && !step.GetBool("AllowWithoutGround", false))
                return;

            Vector2 forward = new Vector2(_ctx.FacingDirection, 0f);
            var spreadMode = (SkillProjectileSpreadMode)step.GetInt("SpreadMode", (int)SkillProjectileSpreadMode.Cone);
            for (int i = 0; i < totalCount; i++)
            {
                Vector2 spawnPos = origin;
                Vector2 direction = forward;
                if (spreadMode == SkillProjectileSpreadMode.ParallelRows)
                {
                    spawnPos += Vector2.up * ResolveParallelProjectileOffset(i, step.GetFloat("ParallelSpacingY", 0.25f));
                }
                else
                {
                    float angleStep = step.GetFloat("ConeAnglePerProjectile", 8f);
                    float centeredIndex = i - (totalCount - 1) * 0.5f;
                    direction = RotateVector(forward, centeredIndex * angleStep);
                }

                SkillProjectile.Spawn(data, spawnPos, direction, null);
            }
        }

        private void ExecuteBuildChainTargets(int stepIndex, StepEntry step)
        {
            if (_ctx == null || _ownerStats == null || stepIndex < 0)
                return;

            float offsetX = step.GetFloat("OffsetX", 0.65f);
            float offsetY = step.GetFloat("OffsetY", 0.35f);
            float firstBoxLength = Mathf.Max(0.1f, step.GetFloat("FirstBoxLength", 6f));
            float firstBoxHeight = Mathf.Max(0.1f, step.GetFloat("FirstBoxHeight", 2f));
            float chainRadius = Mathf.Max(0.1f, step.GetFloat("ChainSearchRadius", 7f));
            int baseExtraChains = Mathf.Max(0, step.GetInt("BaseExtraChains", 3));
            bool useProjectileChain = step.GetBool("UseProjectileChainStat", true);
            int statExtraChains = useProjectileChain ? Mathf.Max(0, Mathf.FloorToInt(_ownerStats.GetValue(StatType.ProjectileChain))) : 0;
            int maxTargets = 1 + baseExtraChains + statExtraChains;
            bool allowRepeatTargets = step.GetBool("AllowRepeatTargets", true);
            bool preventImmediateBacktracking = step.GetBool("PreventImmediateBacktracking", false);
            bool requireLineOfSight = step.GetBool("RequireLineOfSight", true);
            bool limitToScreen = step.GetBool("LimitToScreen", true);
            int worldLayerMask = step.GetInt("WorldLayerMask", 1 << 6);
            float fizzleLength = Mathf.Max(0.1f, step.GetFloat("FizzleLength", 0.9f));

            Vector3 ownerPosition = _ownerStats.transform.position;
            Vector3 start = ownerPosition + new Vector3(offsetX * _ctx.FacingDirection, offsetY, 0f);
            var result = new SkillStepContext.ChainResult
            {
                StartPosition = start,
                FizzleEndPosition = start + new Vector3(fizzleLength * _ctx.FacingDirection, 0f, 0f),
                IsFizzle = true
            };

            if (!TryFindFirstChainTarget(start, firstBoxLength, firstBoxHeight, requireLineOfSight, worldLayerMask, out var firstTarget))
            {
                _ctx.RegisterChainResult(stepIndex, result);
                return;
            }

            result.IsFizzle = false;
            result.Targets.Add(firstTarget);

            SkillStepContext.ChainTarget previousTarget = default;
            SkillStepContext.ChainTarget currentTarget = firstTarget;
            var usedTargets = new HashSet<IDamageable> { firstTarget.Target };

            while (result.Targets.Count < maxTargets &&
                   TryFindNextChainTarget(
                       currentTarget,
                       previousTarget,
                       chainRadius,
                       allowRepeatTargets,
                       preventImmediateBacktracking,
                       limitToScreen,
                       usedTargets,
                       out var nextTarget))
            {
                result.Targets.Add(nextTarget);
                if (!allowRepeatTargets)
                    usedTargets.Add(nextTarget.Target);

                previousTarget = currentTarget;
                currentTarget = nextTarget;
            }

            _ctx.RegisterChainResult(stepIndex, result);
        }

        private bool TryFindFirstChainTarget(
            Vector3 start,
            float length,
            float height,
            bool requireLineOfSight,
            LayerMask worldLayer,
            out SkillStepContext.ChainTarget target)
        {
            target = default;
            Vector2 center = (Vector2)start + new Vector2(_ctx.FacingDirection * length * 0.5f, 0f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(length, height), 0f, _targetLayer);
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (!TryResolveValidDamageTarget(hit, out IDamageable damageable))
                    continue;

                Transform targetTransform = ResolveDamageableTransform(damageable);
                if (targetTransform == null)
                    continue;

                Vector3 targetPosition = ResolveChainTargetPoint(targetTransform);
                if (!IsStrictlyInFront(start, targetPosition))
                    continue;

                if (requireLineOfSight && IsLineBlocked(start, targetPosition, worldLayer))
                    continue;

                float distance = Mathf.Abs(targetPosition.x - start.x);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                target = new SkillStepContext.ChainTarget
                {
                    Target = damageable,
                    TargetTransform = targetTransform,
                    Position = targetPosition
                };
            }

            return target.Target != null;
        }

        private bool TryFindNextChainTarget(
            SkillStepContext.ChainTarget current,
            SkillStepContext.ChainTarget previous,
            float radius,
            bool allowRepeatTargets,
            bool preventImmediateBacktracking,
            bool limitToScreen,
            HashSet<IDamageable> usedTargets,
            out SkillStepContext.ChainTarget target)
        {
            target = default;
            Collider2D[] hits = Physics2D.OverlapCircleAll(current.Position, radius, _targetLayer);
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (!TryResolveValidDamageTarget(hit, out IDamageable damageable))
                    continue;

                if (ReferenceEquals(damageable, current.Target))
                    continue;
                if (!allowRepeatTargets && usedTargets != null && usedTargets.Contains(damageable))
                    continue;
                if (preventImmediateBacktracking && previous.Target != null && ReferenceEquals(damageable, previous.Target))
                    continue;

                Transform targetTransform = ResolveDamageableTransform(damageable);
                if (targetTransform == null)
                    continue;

                Vector3 targetPosition = ResolveChainTargetPoint(targetTransform);
                if (limitToScreen && !IsPointVisibleOnScreen(targetPosition))
                    continue;

                float distanceSqr = ((Vector2)targetPosition - (Vector2)current.Position).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                target = new SkillStepContext.ChainTarget
                {
                    Target = damageable,
                    TargetTransform = targetTransform,
                    Position = targetPosition
                };
            }

            return target.Target != null;
        }

        private void ExecuteSpawnChainVFX(StepEntry step)
        {
            if (_ctx == null || step == null)
                return;

            int sourceStepIndex = step.GetInt("SourceChainStepIndex", -1);
            if (!_ctx.TryGetChainResult(sourceStepIndex, out var chainResult))
                return;

            List<Vector3> points = chainResult.BuildVisualPoints();
            if (points == null || points.Count < 2)
                return;

            GameObject prefab = step.GetObject<GameObject>("VfxPrefab");
            GameObject instance = prefab != null
                ? Instantiate(prefab, points[0], Quaternion.identity)
                : new GameObject("RuntimeChainVFX");

            var visual = instance.GetComponent<SkillChainVisualEffects>();
            if (visual == null)
                visual = instance.AddComponent<SkillChainVisualEffects>();

            float segmentDelay = Mathf.Max(0f, step.GetFloat("SegmentDelay", 0.06f));
            float segmentLifetime = Mathf.Max(0.01f, step.GetFloat("SegmentLifetime", 0.18f));
            visual.Play(points, segmentDelay, segmentLifetime);

            float destroyAfter = segmentLifetime + segmentDelay * Mathf.Max(1, points.Count - 1) + 0.5f;
            Destroy(instance, destroyAfter);
        }

        private void ExecuteChainDamage(int stepIndex, StepEntry step)
        {
            if (_ctx == null || step == null)
                return;

            int sourceStepIndex = step.GetInt("SourceChainStepIndex", -1);
            if (!_ctx.TryGetChainResult(sourceStepIndex, out var chainResult) || chainResult.IsFizzle || chainResult.Targets.Count == 0)
                return;

            float delayPerSegment = Mathf.Max(0f, step.GetFloat("DamageDelayPerSegment", 0f));
            if (delayPerSegment > 0f)
            {
                StartCoroutine(ExecuteChainDamageDelayed(stepIndex, step, chainResult, delayPerSegment));
                return;
            }

            var hitResults = new List<SkillStepContext.HitResult>(chainResult.Targets.Count);
            for (int i = 0; i < chainResult.Targets.Count; i++)
                DealDamageToChainTarget(step, chainResult.Targets[i], hitResults);

            if (hitResults.Count > 0)
                _ctx.RegisterHitResults(stepIndex, hitResults);
        }

        private IEnumerator ExecuteChainDamageDelayed(int stepIndex, StepEntry step, SkillStepContext.ChainResult chainResult, float delayPerSegment)
        {
            var hitResults = new List<SkillStepContext.HitResult>(chainResult.Targets.Count);
            for (int i = 0; i < chainResult.Targets.Count; i++)
            {
                if (i > 0)
                    yield return new WaitForSeconds(delayPerSegment);

                DealDamageToChainTarget(step, chainResult.Targets[i], hitResults);
                if (hitResults.Count > 0)
                    _ctx?.RegisterHitResults(stepIndex, hitResults);
            }
        }

        private void DealDamageToChainTarget(StepEntry step, SkillStepContext.ChainTarget chainTarget, List<SkillStepContext.HitResult> hitResults)
        {
            IDamageable target = chainTarget.Target;
            if (target == null)
                return;

            if (target is Object unityObject && unityObject == null)
                return;

            Transform targetTransform = chainTarget.TargetTransform != null
                ? chainTarget.TargetTransform
                : ResolveDamageableTransform(target);
            if (targetTransform == null)
                return;

            float mult = ResolveDamageMultiplier(step);
            DamageContext damageContext = ResolveDamageContext();
            IStatsProvider scopedStats = BuildScopedStatsProvider(step, target);
            DamageSnapshot snapshot = DamageCalculator.CreateDamageSnapshot(scopedStats, mult, damageContext, step.DamageConversions);
            snapshot.Source = _ownerStats;

            target.TakeDamage(snapshot);
            TryApplyAilmentsFromHit(scopedStats, target, snapshot);
            ExecuteOnHitEffects(step, target, chainTarget.TargetTransform, snapshot);

            hitResults?.Add(new SkillStepContext.HitResult
            {
                Target = target,
                TargetTransform = targetTransform,
                Position = targetTransform.position,
                Snapshot = snapshot
            });
        }

        private DamageContext ResolveProjectileDamageContext()
        {
            StatContextTagFlags tags = _data != null ? _data.DamageContextTags : StatContextTagFlags.None;
            if (tags == StatContextTagFlags.None)
                tags = StatContextTagFlags.Attack | StatContextTagFlags.Projectile;
            else
                tags |= StatContextTagFlags.Projectile;

            return new DamageContext(tags);
        }

        private Sprite ResolveCurrentWeaponSprite()
        {
            Transform handPivot = _ownerStats != null ? _ownerStats.transform.Find("Visuals/HandPivot") : null;
            if (handPivot != null)
            {
                SpriteRenderer weaponRenderer = handPivot.GetComponentInChildren<SpriteRenderer>(true);
                if (weaponRenderer != null && weaponRenderer.sprite != null)
                    return weaponRenderer.sprite;
            }

            InventoryItem mainHandItem = InventoryManager.Instance != null
                ? InventoryManager.Instance.EquipmentItems[(int)EquipmentSlot.MainHand]
                : null;
            if (mainHandItem?.Data is WeaponItemSO weaponData)
                return weaponData.InHandSprite;

            return null;
        }

        private static float ResolveParallelProjectileOffset(int index, float spacing)
        {
            if (index <= 0)
                return 0f;

            int lane = (index + 1) / 2;
            int sign = index % 2 == 1 ? 1 : -1;
            return sign * lane * Mathf.Max(0f, spacing);
        }

        private static Vector2 RotateVector(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos).normalized;
        }

        private bool IsOwnerGrounded()
        {
            if (_ownerStats == null)
                return false;

            PlayerMovement movement = _ownerStats.GetComponent<PlayerMovement>();
            return movement != null && movement.IsGrounded;
        }

        private static bool TryProjectToGround(Vector2 origin, LayerMask groundLayer, float upDistance, float downDistance, float yOffset, out Vector2 groundedPosition)
        {
            groundedPosition = origin;
            Vector2 rayOrigin = origin + Vector2.up * Mathf.Max(0.01f, upDistance);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, upDistance + downDistance, groundLayer);
            if (hit.collider == null)
                return false;

            groundedPosition = new Vector2(origin.x, hit.point.y + yOffset);
            return true;
        }

        private static LayerMask ResolveGroundProjectileSurfaceLayer(StepEntry step)
        {
            int mask = step != null ? step.GetInt("GroundLayerMask", 1 << 6) : 1 << 6;
            int platformLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (platformLayer >= 0)
                mask |= 1 << platformLayer;

            return mask;
        }

        private void ExecuteDealDamageCircle(int stepIndex, StepEntry step)
        {
            ResolveCircleArea(step, out Vector2 center, out float radius);
            var targets = GetTargetsInCircle(center, radius);
            DealDamageToTargets(stepIndex, step, targets);
        }

        private void ExecuteDealDamageRectangle(int stepIndex, StepEntry step)
        {
            ResolveRectangleArea(step, out Vector2 center, out Vector2 size, out float angle);
            var targets = GetTargetsInBox(center, size, angle);
            DealDamageToTargets(stepIndex, step, targets);
        }

        private void ExecutePersistentDamageCircle(int stepIndex, StepEntry step)
        {
            ResolveCircleArea(step, out Vector2 center, out float radius);
            var targets = GetTargetsInCircle(center, radius);
            DealDamageToTargets(stepIndex, step, targets, GetPersistentHitSet(stepIndex), appendHitResults: true);
        }

        private void ExecutePersistentDamageRectangle(int stepIndex, StepEntry step)
        {
            ResolveRectangleArea(step, out Vector2 center, out Vector2 size, out float angle);
            var targets = GetTargetsInBox(center, size, angle);
            DealDamageToTargets(stepIndex, step, targets, GetPersistentHitSet(stepIndex), appendHitResults: true);
        }

        private void DealDamageToTargets(
            int stepIndex,
            StepEntry step,
            List<IDamageable> targets,
            HashSet<int> hitOnceTargets = null,
            bool appendHitResults = false)
        {
            if (targets == null || targets.Count == 0)
                return;

            float mult = ResolveDamageMultiplier(step);
            DamageContext damageContext = ResolveDamageContext();
            var hitResults = stepIndex >= 0 ? new List<SkillStepContext.HitResult>(targets.Count) : null;
            for (int i = 0; i < targets.Count; i++)
            {
                IDamageable target = targets[i];
                if (target == null)
                    continue;

                if (hitOnceTargets != null && !hitOnceTargets.Add(GetDamageTargetKey(target)))
                    continue;

                IStatsProvider scopedStats = BuildScopedStatsProvider(step, target);
                var snapshot = DamageCalculator.CreateDamageSnapshot(scopedStats, mult, damageContext, step.DamageConversions);
                snapshot.Source = _ownerStats;
                target.TakeDamage(snapshot);
                TryApplyAilmentsFromHit(scopedStats, target, snapshot);
                Transform targetTransform = ResolveDamageableTransform(target);
                if (hitResults != null)
                {
                    hitResults.Add(new SkillStepContext.HitResult
                    {
                        Target = target,
                        TargetTransform = targetTransform,
                        Position = targetTransform != null ? targetTransform.position : _ownerStats.transform.position,
                        Snapshot = snapshot
                    });
                }
                ExecuteOnHitEffects(step, target, targetTransform, snapshot);
            }

            if (hitResults != null && hitResults.Count > 0)
            {
                if (appendHitResults && _ctx != null && _ctx.TryGetHitResults(stepIndex, out var existingHits) && existingHits != null && existingHits.Count > 0)
                {
                    var mergedHits = new List<SkillStepContext.HitResult>(existingHits.Count + hitResults.Count);
                    mergedHits.AddRange(existingHits);
                    mergedHits.AddRange(hitResults);
                    hitResults = mergedHits;
                }

                _ctx?.RegisterHitResults(stepIndex, hitResults);
            }
        }

        private HashSet<int> GetPersistentHitSet(int stepIndex)
        {
            if (stepIndex < 0)
                stepIndex = int.MinValue;

            if (!_persistentHitTargetsByStep.TryGetValue(stepIndex, out var hitSet))
            {
                hitSet = new HashSet<int>();
                _persistentHitTargetsByStep[stepIndex] = hitSet;
            }

            return hitSet;
        }

        private static int GetDamageTargetKey(IDamageable target)
        {
            if (target is Component component)
                return component.GetInstanceID();

            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target);
        }

        private IStatsProvider BuildScopedStatsProvider(StepEntry step, IDamageable target)
        {
            if ((step.ScopedStatModifiers == null || step.ScopedStatModifiers.Count == 0) &&
                (step.TargetAilmentStackModifiers == null || step.TargetAilmentStackModifiers.Count == 0))
                return _ownerStats;

            var modifiers = new List<SerializableStatModifier>();
            if (step.ScopedStatModifiers != null)
                modifiers.AddRange(step.ScopedStatModifiers);

            AppendTargetAilmentStackModifiers(step, target, modifiers);
            return modifiers.Count > 0 ? new ScopedStatsProvider(_ownerStats, modifiers) : _ownerStats;
        }

        private void AppendTargetAilmentStackModifiers(StepEntry step, IDamageable target, List<SerializableStatModifier> modifiers)
        {
            if (step.TargetAilmentStackModifiers == null || step.TargetAilmentStackModifiers.Count == 0 || modifiers == null)
                return;

            Transform targetTransform = ResolveDamageableTransform(target);
            if (!AilmentController.TryResolve(targetTransform, out AilmentController ailments) || ailments == null)
                return;

            for (int i = 0; i < step.TargetAilmentStackModifiers.Count; i++)
            {
                TargetAilmentStackStatModifierRule rule = step.TargetAilmentStackModifiers[i];
                if (rule == null || Mathf.Approximately(rule.ValuePerStack, 0f))
                    continue;

                int stackCount = ailments.GetStackCount(rule.Ailment);
                if (rule.MaxStacksCounted > 0)
                    stackCount = Mathf.Min(stackCount, rule.MaxStacksCounted);
                if (stackCount <= 0)
                    continue;

                modifiers.Add(new SerializableStatModifier
                {
                    Stat = rule.Stat,
                    Type = rule.Type,
                    Value = rule.ValuePerStack * stackCount
                });
            }
        }

        private void TryApplyAilmentsFromHit(IStatsProvider scopedStats, IDamageable target, DamageSnapshot hitSnapshot)
        {
            Transform targetTransform = ResolveDamageableTransform(target);
            AilmentController.TryApplyHitAilments(scopedStats, _ownerStats, targetTransform, hitSnapshot);
        }

        private void ExecuteOnHitEffects(StepEntry step, IDamageable primaryTarget, Transform primaryTransform, DamageSnapshot sourceSnapshot)
        {
            if (step?.OnHitEffects == null || step.OnHitEffects.Count == 0 || sourceSnapshot == null)
                return;

            Vector3 origin = primaryTransform != null ? primaryTransform.position : _ownerStats.transform.position;
            for (int i = 0; i < step.OnHitEffects.Count; i++)
            {
                SkillOnHitEffectRule rule = step.OnHitEffects[i];
                if (rule == null)
                    continue;

                switch (rule.Type)
                {
                    case SkillOnHitEffectType.SpawnVfxDamageCircle:
                        StartCoroutine(RunOnHitVfxDamageCircle(rule, primaryTarget, origin, sourceSnapshot));
                        break;
                }
            }
        }

        private IEnumerator RunOnHitVfxDamageCircle(
            SkillOnHitEffectRule rule,
            IDamageable primaryTarget,
            Vector3 origin,
            DamageSnapshot sourceSnapshot)
        {
            float lifetime = Mathf.Max(0.01f, rule.Lifetime);
            float scale = Mathf.Max(0.01f, rule.ScaleMultiplier) * (_ctx != null ? _ctx.AoeScale : 1f);
            GameObject vfx = null;
            if (rule.VfxPrefab != null)
            {
                vfx = Instantiate(rule.VfxPrefab, origin, Quaternion.identity);
                vfx.transform.localScale = new Vector3(
                    Mathf.Abs(vfx.transform.localScale.x) * scale,
                    Mathf.Abs(vfx.transform.localScale.y) * scale,
                    vfx.transform.localScale.z);

                var anim = vfx.GetComponentInChildren<Animator>();
                if (anim != null)
                    anim.speed = SkillVFX.GetAnimatorPlaybackDurationAtSpeedOne(anim, lifetime) / lifetime;

                var autoDestroy = AutoDestroyVFX.Ensure(vfx);
                if (autoDestroy != null)
                    autoDestroy.Initialize(lifetime, rule.FadeOutEnabled, rule.FadeOutStartLifePercent, rule.FadeStartAlphaMultiplier);
            }

            float delay = lifetime * Mathf.Clamp01(rule.HitAtLifePercent);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            Vector2 hitCenter = vfx != null ? (Vector2)vfx.transform.position : (Vector2)origin;
            float radius = Mathf.Max(0.01f, rule.Radius) * scale;
            var targets = GetTargetsInCircle(hitCenter, radius);
            var snapshot = CloneScaledSnapshot(sourceSnapshot, Mathf.Max(0f, rule.DamageMultiplier));
            for (int i = 0; i < targets.Count; i++)
            {
                IDamageable target = targets[i];
                if (target == null)
                    continue;

                if (rule.ExcludePrimaryTarget && ReferenceEquals(target, primaryTarget))
                    continue;

                target.TakeDamage(CloneScaledSnapshot(snapshot, 1f));
            }
        }

        private static DamageSnapshot CloneScaledSnapshot(DamageSnapshot source, float multiplier)
        {
            multiplier = Mathf.Max(0f, multiplier);
            return new DamageSnapshot(source.Source)
            {
                Physical = source.Physical * multiplier,
                Fire = source.Fire * multiplier,
                Cold = source.Cold * multiplier,
                Lightning = source.Lightning * multiplier,
                IsCrit = source.IsCrit,
                CritMultiplier = source.CritMultiplier
            };
        }

        private static Transform ResolveDamageableTransform(IDamageable target)
        {
            return target is Component component ? component.transform : null;
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

        private void ExecuteModifyCooldown(StepEntry step)
        {
            if (step == null)
                return;

            int sourceStepIndex = step.GetInt("SourceStepIndex", -1);
            int hitCount = _ctx != null ? _ctx.GetHitCount(sourceStepIndex) : 0;
            int minHitCount = Mathf.Max(0, step.GetInt("MinHitCount", 0));
            if (hitCount < minHitCount)
                return;

            bool scaleByHits = step.GetBool("ScaleByHitCount", false);
            int units = scaleByHits ? hitCount : 1;
            if (units <= 0)
                return;

            float seconds = Mathf.Max(0f, step.GetFloat("Seconds", 1f)) * units;
            if (seconds <= 0f)
                return;

            bool addCooldown = step.GetBool("AddInsteadOfReduce", false);
            CooldownStepTarget targetMode = (CooldownStepTarget)step.GetInt("TargetMode", (int)CooldownStepTarget.CurrentSkill);
            int slot = step.GetInt("TargetSlot", _slotIndex);

            switch (targetMode)
            {
                case CooldownStepTarget.SpecificSlot:
                    if (addCooldown)
                        _skillManager?.AddSkillCooldown(slot, seconds);
                    else
                        _skillManager?.ReduceSkillCooldown(slot, seconds);
                    break;
                case CooldownStepTarget.OtherSlots:
                    if (addCooldown)
                        _skillManager?.AddCooldownsExcept(_slotIndex, seconds);
                    else
                        _skillManager?.ReduceCooldownsExcept(_slotIndex, seconds);
                    break;
                case CooldownStepTarget.AllSlots:
                    if (addCooldown)
                        _skillManager?.AddAllCooldowns(seconds);
                    else
                        _skillManager?.ReduceAllCooldowns(seconds);
                    break;
                default:
                    if (addCooldown)
                        AddCooldownRemaining(seconds);
                    else
                        ReduceCooldownRemaining(seconds);
                    break;
            }
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

        private bool IsStrictlyInFront(Vector3 start, Vector3 targetPosition)
        {
            float dx = targetPosition.x - start.x;
            return Mathf.Sign(dx == 0f ? _ctx.FacingDirection : dx) == Mathf.Sign(_ctx.FacingDirection);
        }

        private bool IsLineBlocked(Vector3 start, Vector3 end, LayerMask worldLayer)
        {
            Vector2 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0.01f)
                return false;

            RaycastHit2D hit = Physics2D.Raycast(start, delta / distance, distance, worldLayer);
            return hit.collider != null;
        }

        private static Vector3 ResolveChainTargetPoint(Transform targetTransform)
        {
            if (targetTransform == null)
                return Vector3.zero;

            var collider = targetTransform.GetComponent<Collider2D>() ?? targetTransform.GetComponentInParent<Collider2D>();
            if (collider != null)
                return collider.bounds.center;

            var renderer = targetTransform.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
                return renderer.bounds.center;

            return targetTransform.position;
        }

        private static bool IsPointVisibleOnScreen(Vector3 point)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return true;

            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z >= 0f &&
                   viewport.x >= 0f && viewport.x <= 1f &&
                   viewport.y >= 0f && viewport.y <= 1f;
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
