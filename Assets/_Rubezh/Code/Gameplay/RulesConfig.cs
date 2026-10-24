using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Все числа правил упражнения. Ассет: Assets/_Rubezh/Data/Rules/RulesConfig.asset.
    [CreateAssetMenu(menuName = "Rubezh/Rules Config", fileName = "RulesConfig")]
    public sealed class RulesConfig : ScriptableObject
    {
        [Header("Упражнение")]
        [Tooltip("Патронов в магазине на шаге 4")]
        [Min(1)] public int seriesRounds = 5;

        [Header("Сектор (VP-05)")]
        [Tooltip("Занос ствола вне сектора, после которого при снаряжённом оружии — грубое нарушение, с")]
        [Min(0f)] public float sectorGraceSeconds = 1.5f;

        public ExerciseRules ToRules() => new ExerciseRules
        {
            SeriesRounds = seriesRounds,
            SectorGraceSeconds = sectorGraceSeconds
        };
    }
}
