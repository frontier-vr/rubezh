using System.Collections.Generic;

namespace Rubezh.Core
{
    // Числа правил упражнения. В Core без движка, поэтому обычный класс; в проекте значения
    // приходят из RulesConfig (ScriptableObject в Gameplay), в тестах — значения по умолчанию.
    public sealed class ExerciseRules
    {
        public int SeriesRounds = 5;                // патронов в магазине на шаге 4
        public double SectorGraceSeconds = 1.5;     // занос ствола вне сектора до грубого нарушения
        public double SectorJitterSeconds = 0.1;    // выход из сектора короче — дрожание руки, не нарушение
        public double TriggerTouchMinSeconds = 0.15; // касание спуска короче — не нарушение FingerOnTrigger
        public double MuzzleLookSeconds = 0.2;      // взгляд в ствол дольше — грубое LookIntoMuzzle
        public int MisfireShotMin = 3;              // осечка на выстреле из диапазона [Min, Max]
        public int MisfireShotMax = 4;
        public double MisfireWaitSeconds = 5;       // ждать после осечки до перезарядки, иначе JamMishandled
        public double StovePipeChance = 0;          // вероятность прихвата гильзы за упражнение (VP-13); 0 — без него
        public int StovePipeShotMin = 2;            // прихват на выстреле из диапазона [Min, Max], не на осечке
        public int StovePipeShotMax = 5;

        // Балл и вердикт (FR-40, FR-41, VP-10)
        public double TimeLimitSeconds = 360;       // лимит цикла 6 минут
        public double OverTimeStepSeconds = 10;     // за каждые начатые 10 с сверх лимита…
        public int OverTimePenaltyPerStep = 2;      // …минус 2 балла,
        public int OverTimePenaltyMax = 20;         // но не больше 20
        public int MissPenalty = 3;                 // за каждый промах
        public int PassScore = 80;                  // от 80 — зачёт
        public int RemarksScore = 60;               // от 60 — зачёт с замечаниями

        // Штраф мелкого нарушения; грубое останавливает упражнение и даёт 0.
        public Dictionary<Violation, int> Penalties = DefaultPenalties();

        public int PenaltyFor(Violation v) => Penalties != null && Penalties.TryGetValue(v, out int p) ? p : 0;

        public static Dictionary<Violation, int> DefaultPenalties() => new Dictionary<Violation, int>
        {
            { Violation.MuzzleOutOfSector, 10 },
            { Violation.FingerOnTrigger, 10 },
            { Violation.NoEarmuffs, 15 },
            { Violation.NoGlasses, 15 },
            { Violation.StepOrder, 5 },
            { Violation.IncompleteInspection, 10 },
            { Violation.ReturnedLoaded, 15 },
            { Violation.CasingLeft, 5 },
            { Violation.JamMishandled, 10 },
        };
    }
}
