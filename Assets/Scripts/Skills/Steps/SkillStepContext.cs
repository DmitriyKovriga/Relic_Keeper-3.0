using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;

namespace Scripts.Skills.Steps
{
    /// <summary>
    /// Контекст выполнения рецепта скилла: статы, длительность, кэш результатов степов, флаг отмены.
    /// </summary>
    public class SkillStepContext
    {
        public PlayerStats OwnerStats;
        public float TotalDuration;
        public float FacingDirection => OwnerStats != null && OwnerStats.transform != null && OwnerStats.transform.localScale.x > 0 ? 1f : -1f;
        public float AoeScale = 1f;
        public bool Cancelled;

        /// <summary> Результаты степов по индексу: позиция/масштаб VFX для привязки хитбокса. </summary>
        public Dictionary<int, StepResult> StepResults = new Dictionary<int, StepResult>();

        public struct StepResult
        {
            public Vector3 Position;
            public float Scale;
            public float Duration;
            /// <summary> Время спавна (Time.time), для отложенного урона по % жизни VFX. </summary>
            public float SpawnTime;
        }

        public void SetStepResult(int stepIndex, Vector3 position, float scale, float duration = 0f, float spawnTime = 0f)
        {
            StepResults[stepIndex] = new StepResult { Position = position, Scale = scale, Duration = duration, SpawnTime = spawnTime };
        }

        public bool TryGetStepResult(int stepIndex, out StepResult result)
        {
            return StepResults.TryGetValue(stepIndex, out result);
        }
    }
}
