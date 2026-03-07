using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;

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

        /// <summary>Per-step cached results used by dependent steps.</summary>
        public Dictionary<int, StepResult> StepResults = new Dictionary<int, StepResult>();

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
        }

        public void SetStepResult(
            int stepIndex,
            Vector3 position,
            float scale,
            float duration = 0f,
            float spawnTime = 0f,
            Vector3 visualCenter = default,
            float visualRadius = 0f)
        {
            StepResults[stepIndex] = new StepResult
            {
                Position = position,
                Scale = scale,
                Duration = duration,
                SpawnTime = spawnTime,
                VisualCenter = visualCenter == default ? position : visualCenter,
                VisualRadius = visualRadius
            };
        }

        public bool TryGetStepResult(int stepIndex, out StepResult result)
        {
            return StepResults.TryGetValue(stepIndex, out result);
        }
    }
}
