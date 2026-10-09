using System.Collections.Generic;

namespace Rubezh.Core
{
    public enum Phase
    {
        Receive, Inspect, Ppe, LoadMagazine, ToLine,
        Load, Fire, Cease, Unload, ShowClear, Return,
        Done, Failed
    }

    public enum Violation
    {
        MuzzleOutOfSector, FingerOnTrigger, LookIntoMuzzle,
        NoEarmuffs, NoGlasses, StepOrder, IncompleteInspection,
        ShotBeforeFire, ShotAfterCease, ShotOutOfSector,
        ReturnedLoaded, CasingLeft, JamMishandled
    }

    public enum Severity { Minor, Critical }
    public enum Verdict { Passed, PassedWithRemarks, NotPassed }

    public readonly struct ViolationRecord
    {
        public readonly Violation Type;
        public readonly Severity Severity;
        public readonly double Time;       // секунды от начала упражнения
        public readonly double Duration;   // для длящихся нарушений

        public ViolationRecord(Violation type, Severity severity, double time, double duration)
        {
            Type = type; Severity = severity; Time = time; Duration = duration;
        }
    }

    public sealed class ExerciseResult
    {
        public double TotalTime;
        public int Shots;
        public int Hits;
        public int Score;
        public Verdict Verdict;
        public bool FailedCritical;
        public Violation? FailReason;       // для стоп-экрана, заполнено при FailedCritical
        public IReadOnlyList<ViolationRecord> Violations;
    }

    public interface IClock { double Now { get; } }   // в тестах подменяется
}
