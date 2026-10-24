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
        [Tooltip("Выход из сектора короче этого — дрожание руки на границе, не нарушение, с")]
        [Min(0f)] public float sectorJitterSeconds = 0.1f;

        [Header("Палец на спуске (NG-05, VP-06)")]
        [Tooltip("Запасной порог касания, если у контроллера нет ёмкостного датчика: нажатие сильнее — палец на спуске. Ниже порога выстрела")]
        [Range(0f, 1f)] public float touchThreshold = 0.05f;
        [Tooltip("Касание спуска короче этого вне «Огонь» нарушением не считается, с")]
        [Min(0f)] public float triggerTouchMinSeconds = 0.15f;

        public ExerciseRules ToRules() => new ExerciseRules
        {
            SeriesRounds = seriesRounds,
            SectorGraceSeconds = sectorGraceSeconds,
            SectorJitterSeconds = sectorJitterSeconds,
            TriggerTouchMinSeconds = triggerTouchMinSeconds
        };
    }
}
