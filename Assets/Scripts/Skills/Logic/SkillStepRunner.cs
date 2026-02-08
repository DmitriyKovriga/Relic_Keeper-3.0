using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Skills.Steps;
using Scripts.Skills.Modules;
using Scripts.Combat;

namespace Scripts.Skills
{
    /// <summary>
    /// Выполняет скилл по рецепту степов. Поддерживает отложенные триггеры (несколько действий в один момент % VFX) и ParallelGroup.
    /// </summary>
    [RequireComponent(typeof(SkillMovementControl))]
    [RequireComponent(typeof(SkillHandAnimation))]
    public class SkillStepRunner : SkillBehaviour
    {
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
                TotalDuration = 1f / Mathf.Max(0.01f, _ownerStats.GetValue(StatType.AttackSpeed)),
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
            if (_animCtrl != null) _animCtrl.ForceReset();
            if (_moveCtrl != null) _moveCtrl.SetLock(false);
            _isCasting = false;
        }

        private IEnumerator RunRecipe()
        {
            _isCasting = true;
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

                    bool isDuration = step.StepDefinition.IsDurationStep;
                    float startP = step.StartPercentPipeline;
                    float endP = step.EndPercentPipeline;

                    if (isDuration && endP <= startP) endP = Mathf.Min(1f, startP + 0.001f);

                    if (isDuration)
                    {
                        if (T >= startP - 0.0001f && !started[i])
                        {
                            started[i] = true;
                            if (step.StepDefinition.Id == "MovementLock")
                                _moveCtrl.SetLock(true);
                        }
                        if (started[i] && T < endP + 0.0001f && step.StepDefinition.Id != "MovementLock")
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
                            bool deferByVfxLife = (step.StepDefinition.Id == "DealDamageCircle" || step.StepDefinition.Id == "DealDamageRectangle")
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
                        if (chStep.StepDefinition.IsDurationStep && endP > startP)
                        {
                            float sd = (endP - startP) * _ctx.TotalDuration;
                            for (float el = 0f; el < sd && !_cancelled; el += Time.deltaTime)
                            {
                                ExecuteStepLogic(idx, chStep, el / sd, sd);
                                yield return null;
                            }
                        }
                        else
                            ExecuteStepLogic(idx, chStep, 1f, 0f);
                    }
                    yield return new WaitForSeconds(Mathf.Max(0.01f, tickDuration));
                }
            }

            Cleanup();
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
                    ExecuteSpawnVFX(stepIndex, step);
                    break;
                case "DealDamageCircle":
                    ExecuteDealDamageCircle(stepIndex, step);
                    break;
                case "DealDamageRectangle":
                    ExecuteDealDamageRectangle(stepIndex, step);
                    break;
                default:
                    if (!string.IsNullOrEmpty(id)) Debug.Log($"[SkillStepRunner] Step '{id}' not implemented yet.");
                    break;
            }
        }

        private void ExecuteSpawnVFX(int stepIndex, StepEntry step)
        {
            GameObject prefab = step.GetObject<GameObject>("VfxPrefab");
            if (prefab == null)
            {
                var vfxModule = GetComponent<SkillVFX>();
                if (vfxModule != null)
                {
                    float scaleMult = step.GetFloat("ScaleMultiplier", 1f);
                    float scaleForVfx = _ctx.AoeScale * scaleMult;
                    vfxModule.Play(_ownerStats.transform, _ctx.FacingDirection, scaleForVfx, _ctx.TotalDuration > 0 ? 1f / _ctx.TotalDuration : 1f);
                    Vector3 pos = _ownerStats.transform.position + new Vector3(step.GetFloat("OffsetX", 0f) * _ctx.FacingDirection, step.GetFloat("OffsetY", 0f), 0f);
                    _ctx.SetStepResult(stepIndex, pos, scaleForVfx, step.GetFloat("BaseDuration", 0.5f), Time.time);
                }
                return;
            }
            float offsetX = step.GetFloat("OffsetX", 0f);
            float offsetY = step.GetFloat("OffsetY", 0f);
            float baseDuration = step.GetFloat("BaseDuration", 0.5f);
            float scaleMultiplier = step.GetFloat("ScaleMultiplier", 1f);
            bool attachToParent = step.GetBool("AttachToParent", false);
            bool invertFacing = step.GetBool("InvertFacing", false);
            Vector3 spawnPos = _ownerStats.transform.position + new Vector3(offsetX * _ctx.FacingDirection, offsetY, 0f);
            GameObject vfx = Instantiate(prefab, spawnPos, Quaternion.identity);
            float finalDir = _ctx.FacingDirection * (invertFacing ? -1f : 1f);
            float effectiveScale = _ctx.AoeScale * scaleMultiplier;
            Vector3 scale = vfx.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * finalDir * effectiveScale;
            scale.y = Mathf.Abs(scale.y) * effectiveScale;
            vfx.transform.localScale = scale;
            float aps = _ctx.TotalDuration > 0 ? 1f / _ctx.TotalDuration : 1f;
            var anim = vfx.GetComponent<Animator>();
            if (anim != null) anim.speed = aps;
            float lifetime = baseDuration / aps;
            var autoDestroy = vfx.GetComponent<AutoDestroyVFX>();
            if (autoDestroy != null) autoDestroy.Initialize(lifetime);
            else Destroy(vfx, lifetime);
            if (attachToParent) vfx.transform.SetParent(_ownerStats.transform);
            _ctx.SetStepResult(stepIndex, spawnPos, effectiveScale, lifetime, Time.time);
        }

        private void ExecuteDealDamageCircle(int stepIndex, StepEntry step)
        {
            Vector2 center;
            float radius;
            int sourceIdx = step.GetInt("SourceStepIndex", -1);
            if (sourceIdx >= 0 && _ctx.TryGetStepResult(sourceIdx, out var res))
            {
                center = res.Position;
                radius = step.GetFloat("Radius", 1.5f) * res.Scale;
            }
            else
            {
                float offsetX = step.GetFloat("OffsetX", 0f);
                float offsetY = step.GetFloat("OffsetY", 0f);
                float r = step.GetFloat("Radius", 1.5f);
                radius = r * _ctx.AoeScale;
                float shiftForward = radius - r;
                float finalOffsetX = offsetX + shiftForward;
                center = (Vector2)_ownerStats.transform.position + new Vector2(finalOffsetX * _ctx.FacingDirection, offsetY);
            }
            var targets = GetTargetsInCircle(center, radius);
            float mult = step.GetFloat("DamageMultiplier", 1f);
            var snapshot = DamageCalculator.CreateDamageSnapshot(_ownerStats, mult);
            foreach (var t in targets) t.TakeDamage(snapshot);
        }

        private void ExecuteDealDamageRectangle(int stepIndex, StepEntry step)
        {
            Vector2 center;
            Vector2 size;
            float scaleMult;
            int sourceIdx = step.GetInt("SourceStepIndex", -1);
            if (sourceIdx >= 0 && _ctx.TryGetStepResult(sourceIdx, out var res))
            {
                center = res.Position;
                scaleMult = res.Scale;
                size = new Vector2(step.GetFloat("SizeX", 2f), step.GetFloat("SizeY", 1f)) * scaleMult;
            }
            else
            {
                scaleMult = _ctx.AoeScale;
                center = (Vector2)_ownerStats.transform.position + new Vector2(step.GetFloat("OffsetX", 0f) * _ctx.FacingDirection, step.GetFloat("OffsetY", 0f));
                size = new Vector2(step.GetFloat("SizeX", 2f), step.GetFloat("SizeY", 1f)) * scaleMult;
            }
            float angle = step.GetFloat("Angle", 0f);
            var targets = GetTargetsInBox(center, size, angle);
            float mult = step.GetFloat("DamageMultiplier", 1f);
            var snapshot = DamageCalculator.CreateDamageSnapshot(_ownerStats, mult);
            foreach (var t in targets) t.TakeDamage(snapshot);
        }

        private List<IDamageable> GetTargetsInCircle(Vector2 center, float radius)
        {
            var list = new List<IDamageable>();
            var hits = Physics2D.OverlapCircleAll(center, radius, _targetLayer);
            foreach (var h in hits)
            {
                if (h.TryGetComponent(out IDamageable target)) list.Add(target);
            }
            return list;
        }

        private List<IDamageable> GetTargetsInBox(Vector2 center, Vector2 size, float angleDeg)
        {
            var list = new List<IDamageable>();
            var hits = Physics2D.OverlapBoxAll(center, size, angleDeg, _targetLayer);
            foreach (var h in hits)
            {
                if (h.TryGetComponent(out IDamageable target)) list.Add(target);
            }
            return list;
        }
    }
}
