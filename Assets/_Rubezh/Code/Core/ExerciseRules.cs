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
    }
}
