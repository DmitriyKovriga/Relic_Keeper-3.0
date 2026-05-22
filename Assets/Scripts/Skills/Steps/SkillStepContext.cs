using UnityEngine;
using System.Collections.Generic;
using System;
using Scripts.Stats;
using Scripts.Combat;

namespace Scripts.Skills.Steps
{
    /// <summary>
    /// Runtime context for step-based skill execution.
    /// </summary>
    public class SkillStepContext
    {
        public PlayerStats OwnerStats;
        public float TotalDuration;
        public float FacingDirection => OwnerStats != null && OwnerStats.transform != null && OwnerStats.transform.localScale.x > 0 ? 1f : -1f;
        public float AoeScale = 1f;
        public bool Cancelled;
        public int MysticShieldsConsumed;
        public int MysticShieldsGenerated;
        public float MysticShieldDamageMultiplier = 1f;
        private readonly List<Action> _cleanupActions = new List<Action>();
        private bool _cleanupRan;

        public bool HasConsumedMysticShield => MysticShieldsConsumed > 0;

        public void RegisterMysticShieldConsumption(int consumed)
        {
            if (consumed <= 0)
                return;

            MysticShieldsConsumed += consumed;
        }

        public void RegisterMysticShieldGeneration(int generated)
        {
            if (generated <= 0)
                return;

            MysticShieldsGenerated += generated;
        }

        public void MultiplyDamageFromMysticShield(float multiplier)
        {
            MysticShieldDamageMultiplier *= Mathf.Max(0f, multiplier);
        }

        public void RegisterCleanup(Action cleanup)
        {
            if (cleanup == null || _cleanupRan)
                return;

            _cleanupActions.Add(cleanup);
        }

        public void Cleanup()
        {
            if (_cleanupRan)
                return;

            _cleanupRan = true;
            for (int i = _cleanupActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _cleanupActions[i]?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _cleanupActions.Clear();
        }

        /// <summary>Per-step cached results used by dependent steps.</summary>
        public Dictionary<int, StepResult> StepResults = new Dictionary<int, StepResult>();
        private readonly Dictionary<int, ChainResult> _chainResultsByStep = new Dictionary<int, ChainResult>();
        private readonly Dictionary<int, List<HitResult>> _hitResultsByStep = new Dictionary<int, List<HitResult>>();
        private int _lastHitStepIndex = -1;

        public struct StepResult
        {
            public Vector3 Position;
            public float Scale;
            public float Duration;
            /// <summary>Spawn timestamp (Time.time), used for delayed triggers by VFX lifetime.</summary>
            public float SpawnTime;
            /// <summary>Visual center in world-space if known.</summary>
            public Vector3 VisualCenter;
            /// <summary>Visual radius in world-space if known.</summary>
            public float VisualRadius;
            /// <summary>Spawned VFX transform if the step created a visual.</summary>
            public Transform VisualTransform;
            /// <summary>Active sprite renderer of the spawned VFX if available.</summary>
            public SpriteRenderer VisualSpriteRenderer;
        }

        public void SetStepResult(
            int stepIndex,
            Vector3 position,
            float scale,
            float duration = 0f,
            float spawnTime = 0f,
            Vector3 visualCenter = default,
            float visualRadius = 0f,
            Transform visualTransform = null,
            SpriteRenderer visualSpriteRenderer = null)
        {
            StepResults[stepIndex] = new StepResult
            {
                Position = position,
                Scale = scale,
                Duration = duration,
                SpawnTime = spawnTime,
                VisualCenter = visualCenter == default ? position : visualCenter,
                VisualRadius = visualRadius,
                VisualTransform = visualTransform,
                VisualSpriteRenderer = visualSpriteRenderer
            };
        }

        public bool TryGetStepResult(int stepIndex, out StepResult result)
        {
            return StepResults.TryGetValue(stepIndex, out result);
        }

        public void RegisterChainResult(int stepIndex, ChainResult chainResult)
        {
            if (stepIndex < 0 || chainResult == null)
                return;

            _chainResultsByStep[stepIndex] = chainResult;
        }

        public bool TryGetChainResult(int stepIndex, out ChainResult chainResult)
        {
            chainResult = null;
            return stepIndex >= 0 && _chainResultsByStep.TryGetValue(stepIndex, out chainResult) && chainResult != null;
        }

        public void RegisterHitResults(int stepIndex, List<HitResult> hitResults)
        {
            if (stepIndex < 0 || hitResults == null)
                return;

            _hitResultsByStep[stepIndex] = hitResults;
            _lastHitStepIndex = stepIndex;
        }

        public int GetHitCount(int stepIndex)
        {
            if (stepIndex < 0)
                stepIndex = _lastHitStepIndex;

            return stepIndex >= 0 && _hitResultsByStep.TryGetValue(stepIndex, out var hits) && hits != null
                ? hits.Count
                : 0;
        }

        public bool TryGetHitResults(int stepIndex, out List<HitResult> hitResults)
        {
            hitResults = null;
            if (stepIndex < 0)
                stepIndex = _lastHitStepIndex;

            return stepIndex >= 0 && _hitResultsByStep.TryGetValue(stepIndex, out hitResults) && hitResults != null;
        }

        public struct HitResult
        {
            public IDamageable Target;
            public Transform TargetTransform;
            public Vector3 Position;
            public DamageSnapshot Snapshot;
        }

        public sealed class ChainResult
        {
            public Vector3 StartPosition;
            public Vector3 FizzleEndPosition;
            public bool IsFizzle;
            public readonly List<ChainTarget> Targets = new List<ChainTarget>();

            public int SegmentCount => IsFizzle ? 1 : Targets.Count;

            public List<Vector3> BuildVisualPoints()
            {
                var points = new List<Vector3>(Mathf.Max(2, Targets.Count + 1)) { StartPosition };
                if (IsFizzle || Targets.Count == 0)
                {
                    points.Add(FizzleEndPosition);
                    return points;
                }

                for (int i = 0; i < Targets.Count; i++)
                    points.Add(Targets[i].Position);

                return points;
            }
        }

        public struct ChainTarget
        {
            public IDamageable Target;
            public Transform TargetTransform;
            public Vector3 Position;
        }
    }
}
