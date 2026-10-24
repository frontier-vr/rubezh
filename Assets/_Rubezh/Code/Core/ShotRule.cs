namespace Rubezh.Core
{
    // Грубые выстрелы (FR-35, FR-36): до команды «Огонь», после «Стой» и вне сектора.
    public static class ShotRule
    {
        // null — выстрел допустим.
        public static Violation? Classify(Phase phase, bool inSector)
        {
            if (phase == Phase.Done || phase == Phase.Failed) return null;
            if (phase < Phase.Fire) return Violation.ShotBeforeFire;
            if (phase > Phase.Fire) return Violation.ShotAfterCease;
            return inSector ? (Violation?)null : Violation.ShotOutOfSector;
        }
    }
}
