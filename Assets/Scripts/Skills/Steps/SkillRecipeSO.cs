using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Skills.Steps
{
    /// <summary>
    /// Рецепт скилла: последовательность степов. Опционально — ченнелинг (цикл степов пока зажата кнопка).
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Skill Recipe", fileName = "Recipe_")]
    public class SkillRecipeSO : ScriptableObject
    {
        [Header("Steps (order matters)")]
        public List<StepEntry> Steps = new List<StepEntry>();

        [Header("Combat Feel")]
        [Range(0, 5)]
        [Tooltip("Hit-stop duration in 60 Hz reference frames (3 = 50 ms, 0 = disabled). At most once per cast; the shared interval limits rapid/crowd repeats.")]
        public int HitStopFrames = 1;

        [Header("Channeling (optional)")]
        [Tooltip("Если true, после начальных степов выполняется цикл ChannelLoopSteps пока кнопка зажата")]
        public bool IsChanneling;
        [Tooltip("Индексы степов из Steps, которые повторяются в цикле (или отдельный список — тогда храним копии записей)")]
        public List<int> ChannelLoopStepIndices = new List<int>();
        [Tooltip("Максимальная длительность канала в секундах")]
        public float ChannelMaxDuration = 5f;
        [Tooltip("Длительность одного тика цикла в секундах (или 0 = по сумме DurationPercent степов в цикле)")]
        public float ChannelTickDuration;
    }
}
