namespace Rubezh.Core
{
    // Числа правил упражнения. В Core без движка, поэтому обычный класс; в проекте значения
    // приходят из RulesConfig (ScriptableObject в Gameplay), в тестах — значения по умолчанию.
    public sealed class ExerciseRules
    {
        public int SeriesRounds = 5;                // патронов в магазине на шаге 4
        public double SectorGraceSeconds = 1.5;     // занос ствола вне сектора до грубого нарушения
    }
}
