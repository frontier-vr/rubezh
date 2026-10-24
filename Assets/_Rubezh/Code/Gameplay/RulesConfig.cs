using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Все числа правил упражнения. Ассет: Assets/_Rubezh/Data/Rules/RulesConfig.asset.
    [CreateAssetMenu(menuName = "Rubezh/Rules Config", fileName = "RulesConfig")]
    public sealed class RulesConfig : ScriptableObject
    {
        [System.Serializable]
        public struct ViolationPenalty
        {
            public Violation violation;
            [Min(0)] public int points;
        }

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

        [Header("Взгляд в ствол (VP-07)")]
        [Tooltip("Взгляд в дульный срез дольше этого — грубое нарушение, с")]
        [Min(0f)] public float muzzleLookSeconds = 0.2f;
        [Tooltip("Дальность луча из камеры до дульного среза, м")]
        [Min(0.1f)] public float muzzleLookMaxDistance = 0.7f;
        [Tooltip("Камера в этом конусе вокруг оси ствола — смотрит «в ствол», а не сбоку, градусы")]
        [Range(5f, 60f)] public float muzzleLookConeDegrees = 30f;

        [Header("Осечка (VP-09)")]
        [Tooltip("Осечка случается на выстреле из диапазона, выбирается генератором с зерном")]
        [Min(1)] public int misfireShotMin = 3;
        [Min(1)] public int misfireShotMax = 4;
        [Tooltip("После осечки ждать, не уводя ствол из сектора, до перезарядки; раньше — JamMishandled, с")]
        [Min(0f)] public float misfireWaitSeconds = 5f;

        [Header("Пробоины (VP-23)")]
        [Tooltip("Сколько пробоин держит пул; новая сверх пула занимает место самой старой")]
        [Min(1)] public int bulletHolePoolSize = 10;
        [Tooltip("Диаметр видимой пробоины с ободком, м: крупнее калибра, чтобы читалась с рубежа")]
        [Min(0.005f)] public float bulletHoleDiameter = 0.03f;

        [Header("Балл и вердикт (VP-10, FR-40, FR-41)")]
        [Tooltip("Лимит цикла, с: сверх него — штраф в балле, упражнение не прерывается")]
        [Min(1f)] public float timeLimitSeconds = 360f;
        [Tooltip("Шаг штрафа за время: за каждые начатые столько секунд сверх лимита")]
        [Min(1f)] public float overTimeStepSeconds = 10f;
        [Min(0)] public int overTimePenaltyPerStep = 2;
        [Tooltip("Штраф за время не больше")]
        [Min(0)] public int overTimePenaltyMax = 20;
        [Tooltip("Штраф за каждый промах")]
        [Min(0)] public int missPenalty = 3;
        [Tooltip("Балл от этого — зачёт")]
        [Range(0, 100)] public int passScore = 80;
        [Tooltip("Балл от этого — зачёт с замечаниями; ниже — не зачёт")]
        [Range(0, 100)] public int remarksScore = 60;
        [Tooltip("Штрафы мелких нарушений; грубое нарушение — 0 баллов и «не зачёт»")]
        public ViolationPenalty[] penalties = DefaultPenalties();

        [Header("Экран результата (VP-20)")]
        [Tooltip("После грубого нарушения стоп-экран держится столько, затем на нём же результат с баллом 0, с")]
        [Min(0f)] public float failResultDelaySeconds = 4f;

        [Header("Пистолет в руке (NG-08)")]
        [Tooltip("Нажатие спуска сильнее — TriggerPull. Выше запасного порога касания")]
        [Range(0f, 1f)] public float triggerPullThreshold = 0.75f;
        [Tooltip("Нажатие спуска слабее — TriggerRelease; следующий выстрел только после отпускания")]
        [Range(0f, 1f)] public float triggerReleaseThreshold = 0.4f;
        [Tooltip("Полный ход затвора назад, м")]
        [Min(0.005f)] public float slideTravel = 0.03f;
        [Tooltip("Затвор отведён на эту долю хода — SlideBack")]
        [Range(0.5f, 1f)] public float slideBackFraction = 0.9f;
        [Tooltip("Отведённый затвор, вернувшийся рукой ближе этой доли хода, — SlideForward")]
        [Range(0f, 0.5f)] public float slideForwardFraction = 0.2f;
        [Tooltip("Положение затвора на задержке, доля хода")]
        [Range(0.5f, 1f)] public float slideLockedFraction = 0.8f;

        [Header("Магазин, патроны, места (NG-09)")]
        [Tooltip("Предмет ниже этой высоты и не в руке — упал (все столешницы выше), м")]
        [Min(0f)] public float dropHeight = 0.85f;
        [Tooltip("Упавший предмет через столько секунд возвращается на своё место у текущего якоря, с")]
        [Min(0f)] public float dropReturnSeconds = 1f;
        [Tooltip("После выпуска магазина шахта не принимает его обратно столько секунд, с")]
        [Min(0f)] public float ejectLockSeconds = 0.5f;
        [Tooltip("Скорость, с которой выпущенный магазин выходит из шахты вниз, м/с")]
        [Min(0f)] public float ejectSpeed = 0.6f;

        [Header("Гильзы (VP-19)")]
        [Tooltip("Сколько гильз держит пул; новая сверх пула занимает место самой старой")]
        [Min(1)] public int casingPoolSize = 10;
        [Tooltip("Время полёта гильзы до гильзоуловителя O-22, с")]
        [Min(0.1f)] public float casingFlightSeconds = 0.45f;
        [Tooltip("Дальше этого от гильзоуловителя гильза летит просто вправо, м")]
        [Min(0.1f)] public float casingAimMaxDistance = 2f;
        [Tooltip("Гильза на полу дольше этого появляется на полочке O-21, с")]
        [Min(0f)] public float casingReturnSeconds = 1f;

        [Header("Мишенный механизм (NG-12)")]
        [Tooltip("Скорость каретки вдоль направляющей, м/с")]
        [Min(0.1f)] public float targetCarriageSpeed = 1.5f;
        [Tooltip("Поворот рамки из положения ребром к стрелку, с")]
        [Min(0f)] public float targetTurnSeconds = 0.6f;

        [Header("СИЗ (NG-10)")]
        [Tooltip("Переход между снапшотами «Уши открыты» и «Наушники», с")]
        [Min(0f)] public float earmuffsTransitionSeconds = 0.4f;

        public ExerciseRules ToRules() => new ExerciseRules
        {
            SeriesRounds = seriesRounds,
            SectorGraceSeconds = sectorGraceSeconds,
            SectorJitterSeconds = sectorJitterSeconds,
            TriggerTouchMinSeconds = triggerTouchMinSeconds,
            MuzzleLookSeconds = muzzleLookSeconds,
            MisfireShotMin = misfireShotMin,
            MisfireShotMax = misfireShotMax,
            MisfireWaitSeconds = misfireWaitSeconds,
            TimeLimitSeconds = timeLimitSeconds,
            OverTimeStepSeconds = overTimeStepSeconds,
            OverTimePenaltyPerStep = overTimePenaltyPerStep,
            OverTimePenaltyMax = overTimePenaltyMax,
            MissPenalty = missPenalty,
            PassScore = passScore,
            RemarksScore = remarksScore,
            Penalties = PenaltyMap()
        };

        private System.Collections.Generic.Dictionary<Violation, int> PenaltyMap()
        {
            var map = ExerciseRules.DefaultPenalties();
            if (penalties != null)
                foreach (var p in penalties) map[p.violation] = p.points;
            return map;
        }

        private static ViolationPenalty[] DefaultPenalties()
        {
            var list = new System.Collections.Generic.List<ViolationPenalty>();
            foreach (var kv in ExerciseRules.DefaultPenalties())
                list.Add(new ViolationPenalty { violation = kv.Key, points = kv.Value });
            return list.ToArray();
        }
    }
}
